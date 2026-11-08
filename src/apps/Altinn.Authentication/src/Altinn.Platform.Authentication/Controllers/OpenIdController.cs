using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Authentication.Controllers
{
    /// <summary>
    /// Represents a controller that can expose standard endpoints published by an open id provider.
    /// </summary>
    [Route("authentication/api/v1/openid/.well-known")]
    [AllowAnonymous]
    [ApiController]
    public class OpenIdController : ControllerBase
    {
        private readonly GeneralSettings _generalSettings;

        private readonly IJwtSigningCertificateProvider _certificateProvider;

        /// <summary>
        /// Initialise a new instance of <see cref="OpenIdController"/> with the given input values.
        /// </summary>
        /// <param name="generalSettings">The application general settings.</param>
        /// <param name="certificateProvider">A service able to obtain a list of valid certificates that can be used to sign/validate a JWT.</param>
        public OpenIdController(
            IOptions<GeneralSettings> generalSettings,
            IJwtSigningCertificateProvider certificateProvider)
        {
            _generalSettings = generalSettings.Value;
            _certificateProvider = certificateProvider;
        }

        /// <summary>
        /// Returns a discovery document
        /// </summary>
        /// <returns>The configuration object for Open ID Connect.</returns>
        [HttpGet("openid-configuration")]
        [Produces("application/json")]
        public DiscoveryDocument GetOpenIdConfigurationAsync()
        {
            string baseUrl = _generalSettings.AltinnOidcIssuerUrl;
            string tokenUrl = _generalSettings.AltinnOidcTokenEndpoint;
            string authorizeUrl = _generalSettings.AltinnOidcAuthorizeEndpoint;
            string endSessionUrl = _generalSettings.AltinnOidcEndSessionEndpoint;

            DiscoveryDocument discoveryDocument = new DiscoveryDocument
            {
                // REQUIRED
                Issuer = new Uri(baseUrl).ToString(),

                // REQUIRED
                AuthorizationEndpoint = new Uri(authorizeUrl).ToString(),

                // This is REQUIRED unless only the Implicit Flow is used.
                TokenEndpoint = new Uri(tokenUrl).ToString(),

                // REQUIRED
                JwksUri = new Uri(baseUrl + ".well-known/openid-configuration/jwks").ToString(),

                // REQUIRED
                ResponseTypesSupported = new[] { "token" }, // "code", "id_token", "id_token token", 

                // REQUIRED
                SubjectTypesSupported = new[] { "pairwise" },

                // REQUIRED
                IdTokenSigningAlgValuesSupported = new[] { "RS256" },

                EndSessionEndpoint = new Uri(endSessionUrl).ToString(),
            };

            return discoveryDocument;
        }

        /// <summary>
        /// Returns the JSON Web Key Set to use when validating a token.
        /// </summary>
        /// <returns>The Altinn JSON Web Key Set.</returns>
        [Produces("application/json")]
        [HttpGet("openid-configuration/jwks")]
        public async Task<JwksDocument> GetJsonWebKeySetAsync()
        {
            List<X509Certificate2> certificates = await _certificateProvider.GetCertificates();
            return JwksBuilder.Build(certificates);
        }
    }
}
