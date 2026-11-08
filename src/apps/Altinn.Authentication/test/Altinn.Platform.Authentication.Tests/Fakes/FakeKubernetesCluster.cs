using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Platform.Authentication.Tests.Fakes
{
    /// <summary>
    /// Issues service account tokens the way a Kubernetes API server does for a projected token volume, for
    /// tests of the app token exchange.
    /// </summary>
    public sealed class FakeKubernetesCluster
    {
        private readonly RsaSecurityKey _key;

        public FakeKubernetesCluster(string? issuer = null)
        {
            Issuer = issuer ?? $"https://oidc.example.com/{Guid.NewGuid():N}/";
            _key = new RsaSecurityKey(RSA.Create(2048)) { KeyId = Guid.NewGuid().ToString("N") };
        }

        /// <summary>The cluster's service account issuer.</summary>
        public string Issuer { get; }

        /// <summary>The cluster's public keys, as its <c>/openid/v1/jwks</c> endpoint returns them.</summary>
        public string Jwks
        {
            get
            {
                JsonWebKey jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(_key.Rsa.ExportParameters(false)) { KeyId = _key.KeyId });
                return JsonSerializer.Serialize(new
                {
                    keys = new[] { new { kty = jwk.Kty, kid = jwk.Kid, use = "sig", alg = "RS256", n = jwk.N, e = jwk.E } },
                });
            }
        }

        /// <summary>The public signing key.</summary>
        public SecurityKey PublicKey => new RsaSecurityKey(_key.Rsa.ExportParameters(false)) { KeyId = _key.KeyId };

        /// <summary>
        /// Returns a service account token for <paramref name="serviceAccount"/> in <paramref name="serviceAccountNamespace"/>.
        /// </summary>
        public string CreateToken(
            string serviceAccountNamespace,
            string serviceAccount,
            string audience,
            DateTimeOffset issuedAt,
            TimeSpan? lifetime = null,
            string? issuer = null,
            SecurityKey? signingKey = null)
        {
            DateTimeOffset expires = issuedAt.Add(lifetime ?? TimeSpan.FromMinutes(10));
            SecurityTokenDescriptor descriptor = new()
            {
                Issuer = issuer ?? Issuer,
                Audience = audience,
                IssuedAt = issuedAt.UtcDateTime,
                NotBefore = issuedAt.UtcDateTime,
                Expires = expires.UtcDateTime,
                Claims = new Dictionary<string, object>
                {
                    [JwtRegisteredClaimNames.Sub] = $"system:serviceaccount:{serviceAccountNamespace}:{serviceAccount}",
                    [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                    ["kubernetes.io"] = new Dictionary<string, object>
                    {
                        ["namespace"] = serviceAccountNamespace,
                        ["serviceaccount"] = new Dictionary<string, object> { ["name"] = serviceAccount, ["uid"] = Guid.NewGuid().ToString() },
                        ["pod"] = new Dictionary<string, object> { ["name"] = $"{serviceAccount}-abc12", ["uid"] = Guid.NewGuid().ToString() },
                    },
                },
                SigningCredentials = new SigningCredentials(signingKey ?? _key, SecurityAlgorithms.RsaSha256),
            };

            return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
        }
    }
}
