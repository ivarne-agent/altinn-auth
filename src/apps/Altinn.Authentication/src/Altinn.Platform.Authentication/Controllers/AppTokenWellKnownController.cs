using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement.Mvc;

namespace Altinn.Platform.Authentication.Controllers
{
    /// <summary>
    /// Publishes the app token issuer's metadata and keys, separately from the Altinn token issuer's, so that
    /// a service decides explicitly to trust app tokens. See ADR-0005.
    /// </summary>
    [Route("authentication/api/v1/apptoken/.well-known")]
    [AllowAnonymous]
    [ApiController]
    [FeatureGate(FeatureFlags.AppTokens)]
    public class AppTokenWellKnownController(
        IOptions<AppTokenSettings> appTokenSettings,
        IOptions<GeneralSettings> generalSettings,
        IAppTokenSigningCertificateProvider certificateProvider) : ControllerBase
    {
        private readonly AppTokenSettings _appTokenSettings = appTokenSettings.Value;
        private readonly GeneralSettings _generalSettings = generalSettings.Value;
        private readonly IAppTokenSigningCertificateProvider _certificateProvider = certificateProvider;

        /// <summary>
        /// Returns the app token issuer's metadata (RFC 8414).
        /// </summary>
        [HttpGet("oauth-authorization-server")]
        [Produces("application/json")]
        public AppTokenIssuerMetadata GetMetadata()
        {
            string baseUrl = _appTokenSettings.Issuer.TrimEnd('/');
            return new AppTokenIssuerMetadata
            {
                Issuer = _appTokenSettings.Issuer,
                TokenEndpoint = new Uri(_generalSettings.AltinnOidcTokenEndpoint).ToString(),
                JwksUri = $"{baseUrl}/.well-known/jwks",
                GrantTypesSupported = ["urn:ietf:params:oauth:grant-type:jwt-bearer"],
                TokenEndpointAuthMethodsSupported = ["none"],
            };
        }

        /// <summary>
        /// Returns the keys that sign app tokens.
        /// </summary>
        [HttpGet("jwks")]
        [Produces("application/json")]
        public async Task<JwksDocument> GetJsonWebKeySetAsync()
        {
            List<X509Certificate2> certificates = await _certificateProvider.GetCertificates();
            return JwksBuilder.Build(certificates);
        }

        /// <summary>
        /// The app token issuer's metadata.
        /// </summary>
        public sealed record AppTokenIssuerMetadata
        {
            /// <summary>The <c>iss</c> of app tokens.</summary>
            [JsonPropertyName("issuer")]
            public required string Issuer { get; init; }

            /// <summary>Where service account tokens are exchanged for app tokens.</summary>
            [JsonPropertyName("token_endpoint")]
            public required string TokenEndpoint { get; init; }

            /// <summary>The keys that sign app tokens.</summary>
            [JsonPropertyName("jwks_uri")]
            public required string JwksUri { get; init; }

            /// <summary>The grant that issues app tokens.</summary>
            [JsonPropertyName("grant_types_supported")]
            public required IReadOnlyList<string> GrantTypesSupported { get; init; }

            /// <summary>The service account token is the client's authentication, so there is no other.</summary>
            [JsonPropertyName("token_endpoint_auth_methods_supported")]
            public required IReadOnlyList<string> TokenEndpointAuthMethodsSupported { get; init; }
        }
    }
}
