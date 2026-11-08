using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Authorization.ServiceDefaults.Telemetry;
using Altinn.Platform.Authentication.Core.Models.Oidc;
using Altinn.Platform.Authentication.Core.Models.WorkloadIdentity;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Core.Services.Interfaces;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Exchanges a trusted cluster's service account token for an app token, see ADR-0005.
    /// </summary>
    public class AppTokenService(
        IWorkloadAssertionValidator validator,
        ITrustedWorkloadIssuerRepository repository,
        IAppTokenIssuer issuer,
        IMetricsProvider metricsProvider,
        ILogger<AppTokenService> logger) : IAppTokenService
    {
        /// <summary>The issuer resolves apps with bindings, and the service account has none.</summary>
        public const string ErrorNoBinding = "no_binding";

        private readonly IWorkloadAssertionValidator _validator = validator;
        private readonly ITrustedWorkloadIssuerRepository _repository = repository;
        private readonly IAppTokenIssuer _issuer = issuer;
        private readonly Metrics _metrics = metricsProvider.Get<Metrics>();
        private readonly ILogger<AppTokenService> _logger = logger;

        /// <inheritdoc />
        public async Task<TokenResult> ExchangeAsync(string assertion, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(assertion))
            {
                return TokenResult.InvalidRequest("Missing assertion.");
            }

            WorkloadAssertionResult validation = await _validator.ValidateAsync(assertion, cancellationToken);
            if (!validation.IsValid)
            {
                _metrics.Exchange(serviceOwner: null, validation.Error);

                // One message for every rejection, so the response does not tell which clusters are trusted.
                return validation.KeysUnavailable
                    ? TokenResult.ServerError("The cluster's signing keys are unavailable.")
                    : TokenResult.InvalidGrant("The assertion is not a valid service account token from a trusted cluster.");
            }

            TrustedWorkloadIssuer trusted = validation.Issuer!;
            string? app;
            switch (trusted.AppResolution)
            {
                case AppResolution.ServiceOwnerOnly:
                    app = null;
                    break;
                case AppResolution.ServiceAccountName:
                    app = validation.ServiceAccountName;
                    break;
                case AppResolution.ExplicitBinding:
                    WorkloadBinding? binding = await _repository.GetBindingAsync(trusted.Issuer, validation.Subject!, cancellationToken);
                    if (binding is null)
                    {
                        _metrics.Exchange(trusted.ServiceOwner, ErrorNoBinding);
                        _logger.LogWarning("No app is bound to {Subject} in {Issuer}", validation.Subject, trusted.Issuer);
                        return TokenResult.InvalidGrant("The service account is not bound to an app.");
                    }

                    app = binding.App;
                    break;
                default:
                    throw new UnreachableException($"Unknown app resolution {trusted.AppResolution}.");
            }

            AppIdentity identity = new()
            {
                ServiceOwner = trusted.ServiceOwner,
                App = app,
                WorkloadIssuer = trusted.Issuer,
                WorkloadSubject = validation.Subject!,
            };
            (string token, int expiresIn) = await _issuer.IssueAsync(identity, cancellationToken);

            _metrics.Exchange(trusted.ServiceOwner, errorType: null);
            _logger.LogInformation(
                "Issued an app token for {App} to {Subject} in {Issuer}",
                identity.QualifiedApp ?? identity.ServiceOwner,
                identity.WorkloadSubject,
                identity.WorkloadIssuer);

            return TokenResult.Success(accessToken: token, idToken: null, expiresIn: expiresIn, scope: null, refreshToken: null, refreshTokenExpiresIn: null);
        }

        private sealed class Metrics(Meter meter)
            : IMetrics<Metrics>
        {
            private readonly Counter<int> _exchanges = meter.CreateCounter<int>(
                name: "altinn.authentication.apptoken.exchange",
                description: "Service account tokens exchanged, or rejected, for app tokens");

            public static Metrics Create(Meter meter) => new(meter);

            /// <summary>
            /// Counts one exchange. The service owner is known only once the issuer is trusted, which bounds
            /// the dimension to the registered service owners.
            /// </summary>
            public void Exchange(string? serviceOwner, string? errorType)
            {
                TagList tags = default;
                if (serviceOwner is not null)
                {
                    tags.Add("altinn.service_owner", serviceOwner);
                }

                if (errorType is not null)
                {
                    tags.Add("error.type", errorType);
                }

                _exchanges.Add(1, tags);
            }
        }
    }
}
