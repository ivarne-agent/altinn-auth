using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Models.WorkloadIdentity;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Validates a Kubernetes service account token offline, against the keys of the trusted cluster named in
    /// its <c>iss</c>. Issuer, audience, lifetime and signature are all checked exactly; see ADR-0005.
    /// </summary>
    /// <remarks>
    /// Offline validation cannot see that the pod a token is bound to has been deleted, which only the
    /// cluster's TokenReview API knows. The short lifetime the validator requires bounds that window.
    /// </remarks>
    public class WorkloadAssertionValidator(
        ITrustedWorkloadIssuerRepository repository,
        ISigningKeysRetriever signingKeysRetriever,
        IOptions<AppTokenSettings> settings,
        TimeProvider timeProvider,
        ILogger<WorkloadAssertionValidator> logger) : IWorkloadAssertionValidator
    {
        /// <summary>The assertion is not a JWT.</summary>
        public const string ErrorMalformed = "malformed";

        /// <summary>The <c>iss</c> is not a registered, enabled cluster.</summary>
        public const string ErrorUntrustedIssuer = "untrusted_issuer";

        /// <summary>The cluster's keys could not be read.</summary>
        public const string ErrorKeysUnavailable = "signing_keys_unavailable";

        /// <summary>The signature, audience, issuer or lifetime did not validate.</summary>
        public const string ErrorInvalidToken = "invalid_token";

        /// <summary>The token is valid for longer than allowed.</summary>
        public const string ErrorLifetimeTooLong = "lifetime_too_long";

        /// <summary>The <c>sub</c> is not a service account in an allowed namespace.</summary>
        public const string ErrorSubjectNotAllowed = "subject_not_allowed";

        private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(10);

        // The algorithms Kubernetes signs service account tokens with. Never "none", never HMAC.
        private static readonly string[] ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.EcdsaSha256];

        private readonly JsonWebTokenHandler _handler = new();
        private readonly ITrustedWorkloadIssuerRepository _repository = repository;
        private readonly ISigningKeysRetriever _signingKeysRetriever = signingKeysRetriever;
        private readonly AppTokenSettings _settings = settings.Value;
        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly ILogger<WorkloadAssertionValidator> _logger = logger;

        /// <inheritdoc />
        public async Task<WorkloadAssertionResult> ValidateAsync(string assertion, CancellationToken cancellationToken)
        {
            if (!_handler.CanReadToken(assertion))
            {
                return Failed(ErrorMalformed);
            }

            // The issuer is read before the signature is checked only to find the keys to check it with.
            string issuerName;
            try
            {
                issuerName = new JsonWebToken(assertion).Issuer;
            }
            catch (ArgumentException)
            {
                return Failed(ErrorMalformed);
            }

            TrustedWorkloadIssuer? issuer = string.IsNullOrEmpty(issuerName)
                ? null
                : await _repository.GetIssuerAsync(issuerName, cancellationToken);
            if (issuer is not { Enabled: true })
            {
                return Failed(ErrorUntrustedIssuer);
            }

            IEnumerable<SecurityKey> keys;
            try
            {
                keys = await GetSigningKeys(issuer);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Could not read the signing keys of trusted workload issuer {Issuer}", issuer.Issuer);
                return Failed(ErrorKeysUnavailable) with { KeysUnavailable = true };
            }

            TokenValidationResult result = await _handler.ValidateTokenAsync(assertion, new TokenValidationParameters
            {
                ValidIssuer = issuer.Issuer,
                ValidAudience = _settings.AssertionAudience,
                IgnoreTrailingSlashWhenValidatingAudience = false,
                IssuerSigningKeys = keys,
                ValidAlgorithms = ValidAlgorithms,
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                LifetimeValidator = ValidateLifetime,
                ClockSkew = ClockSkew,
            });
            if (!result.IsValid)
            {
                _logger.LogInformation(
                    "Rejected a service account token from {Issuer}: {Reason}",
                    issuer.Issuer,
                    result.Exception?.GetType().Name);
                return Failed(ErrorInvalidToken);
            }

            JsonWebToken token = (JsonWebToken)result.SecurityToken;
            if (token.IssuedAt == DateTime.MinValue
                || token.ValidTo - token.IssuedAt > TimeSpan.FromMinutes(_settings.MaxAssertionLifetimeMinutes))
            {
                return Failed(ErrorLifetimeTooLong);
            }

            if (!WorkloadIdentityValidation.TryParseServiceAccountSubject(token.Subject, out string serviceAccountNamespace, out string name)
                || !issuer.AllowedNamespaces.Contains(serviceAccountNamespace, StringComparer.Ordinal))
            {
                return Failed(ErrorSubjectNotAllowed);
            }

            return new WorkloadAssertionResult
            {
                Issuer = issuer,
                Subject = token.Subject,
                ServiceAccountNamespace = serviceAccountNamespace,
                ServiceAccountName = name,
            };
        }

        private static WorkloadAssertionResult Failed(string error) => new() { Error = error };

        private async Task<IEnumerable<SecurityKey>> GetSigningKeys(TrustedWorkloadIssuer issuer)
        {
            if (issuer.JwksJson is not null)
            {
                return new JsonWebKeySet(issuer.JwksJson).GetSigningKeys();
            }

            // Cached per URL by the OIDC configuration manager, and refreshed when it expires.
            return await _signingKeysRetriever.GetSigningKeys(issuer.DiscoveryUri!.ToString());
        }

        private bool ValidateLifetime(DateTime? notBefore, DateTime? expires, SecurityToken securityToken, TokenValidationParameters parameters)
        {
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            return expires is { } exp
                && now < exp.Add(ClockSkew)
                && (notBefore is not { } nbf || nbf.Subtract(ClockSkew) <= now);
        }
    }
}
