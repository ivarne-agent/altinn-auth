using Altinn.Platform.Authentication.Core.Models.Oidc;

namespace Altinn.Platform.Authentication.Core.Services.Interfaces
{
    /// <summary>
    /// Exchanges a trusted cluster's service account token for an app token (RFC 7523 jwt-bearer grant), see ADR-0005.
    /// </summary>
    public interface IAppTokenService
    {
        /// <summary>The <c>grant_type</c> of the exchange.</summary>
        public const string JwtBearerGrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer";

        /// <summary>
        /// Validates <paramref name="assertion"/>, resolves the app it identifies and issues an app token for it.
        /// </summary>
        Task<TokenResult> ExchangeAsync(string assertion, CancellationToken cancellationToken);
    }
}
