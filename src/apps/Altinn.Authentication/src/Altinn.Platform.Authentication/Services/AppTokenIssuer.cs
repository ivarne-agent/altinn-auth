using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models.WorkloadIdentity;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Signs app tokens with the app token certificate, never the Altinn token certificate, and with the app
    /// token issuer, audience and <c>typ</c>.
    /// </summary>
    public class AppTokenIssuer(
        IAppTokenSigningCertificateProvider certificateProvider,
        IOptions<AppTokenSettings> settings,
        TimeProvider timeProvider) : IAppTokenIssuer
    {
        private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };
        private readonly IAppTokenSigningCertificateProvider _certificateProvider = certificateProvider;
        private readonly AppTokenSettings _settings = settings.Value;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <inheritdoc />
        public async Task<(string Token, int ExpiresIn)> IssueAsync(AppIdentity identity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(identity);

            List<X509Certificate2> certificates = await _certificateProvider.GetCertificates();
            DateTimeOffset now = _timeProvider.GetUtcNow();
            X509Certificate2 certificate = SigningCertificateSelector.Select(certificates, _settings.RolloverDelayHours, now);
            if (!certificate.HasPrivateKey)
            {
                throw new InvalidOperationException("Selected app token signing certificate has no private key.");
            }

            TimeSpan lifetime = TimeSpan.FromMinutes(_settings.LifetimeMinutes);
            Dictionary<string, object> claims = new()
            {
                [JwtRegisteredClaimNames.Sub] = identity.QualifiedApp ?? identity.ServiceOwner,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
                [AppTokenClaimTypes.ServiceOwner] = identity.ServiceOwner,
                [AppTokenClaimTypes.WorkloadIssuer] = identity.WorkloadIssuer,
                [AppTokenClaimTypes.WorkloadSubject] = identity.WorkloadSubject,
            };
            if (identity.QualifiedApp is { } app)
            {
                claims[AppTokenClaimTypes.App] = app;
            }

            SecurityTokenDescriptor descriptor = new()
            {
                Issuer = _settings.Issuer,
                Audience = _settings.Audience,
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = now.Add(lifetime).UtcDateTime,
                Claims = claims,
                TokenType = AppTokenClaimTypes.TokenType,
                SigningCredentials = new X509SigningCredentials(certificate, SecurityAlgorithms.RsaSha256),
            };

            return (_handler.CreateToken(descriptor), (int)lifetime.TotalSeconds);
        }
    }
}
