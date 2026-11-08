using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models.WorkloadIdentity;
using Altinn.Platform.Authentication.Services;
using Altinn.Platform.Authentication.Tests.Fakes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Services
{
    public class AppTokenIssuerTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 08, 12, 00, 00, TimeSpan.Zero);

        private static readonly AppTokenSettings Settings = new()
        {
            Issuer = "https://platform.example.com/authentication/api/v1/apptoken",
            Audience = "https://platform.example.com/",
            LifetimeMinutes = 10,
        };

        private static readonly AppIdentity App = new()
        {
            ServiceOwner = "ttd",
            App = "my-app",
            WorkloadIssuer = "https://oidc.example.com/cluster/",
            WorkloadSubject = "system:serviceaccount:default:my-app",
        };

        private static AppTokenIssuer CreateIssuer() =>
            new(new AppTokenSigningCertificateProviderStub(), Options.Create(Settings), new FakeTimeProvider(Now));

        private static TokenValidationParameters ValidationParameters(X509Certificate2 certificate) => new()
        {
            ValidIssuer = Settings.Issuer,
            ValidAudience = Settings.Audience,
            ValidTypes = [AppTokenClaimTypes.TokenType],
            IssuerSigningKey = new X509SecurityKey(certificate),
            LifetimeValidator = (notBefore, expires, _, _) => notBefore <= Now.UtcDateTime && Now.UtcDateTime < expires,
        };

        [Fact]
        public async Task IssueAsync_ForAnApp_SignsATokenWithTheAppTokenIssuerAndClaims()
        {
            (string token, int expiresIn) = await CreateIssuer().IssueAsync(App);

            TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(
                token, ValidationParameters(AppTokenSigningCertificateProviderStub.Certificate));

            Assert.True(result.IsValid, result.Exception?.Message);
            JsonWebToken jwt = (JsonWebToken)result.SecurityToken;
            Assert.Equal(600, expiresIn);
            Assert.Equal(AppTokenClaimTypes.TokenType, jwt.Typ);
            Assert.Equal(AppTokenSigningCertificateProviderStub.Certificate.Thumbprint, jwt.Kid);
            Assert.Equal("ttd/my-app", jwt.Subject);
            Assert.Equal("ttd/my-app", jwt.GetClaim(AppTokenClaimTypes.App).Value);
            Assert.Equal("ttd", jwt.GetClaim(AppTokenClaimTypes.ServiceOwner).Value);
            Assert.Equal(App.WorkloadIssuer, jwt.GetClaim(AppTokenClaimTypes.WorkloadIssuer).Value);
            Assert.Equal(App.WorkloadSubject, jwt.GetClaim(AppTokenClaimTypes.WorkloadSubject).Value);
            Assert.Equal(Now.AddMinutes(10).UtcDateTime, jwt.ValidTo);
        }

        [Fact]
        public async Task IssueAsync_ForAServiceOwnerOnly_HasNoAppClaim()
        {
            (string token, _) = await CreateIssuer().IssueAsync(App with { App = null });

            JsonWebToken jwt = new(token);
            Assert.Equal("ttd", jwt.Subject);
            Assert.DoesNotContain(jwt.Claims, c => c.Type == AppTokenClaimTypes.App);
        }

        [Fact]
        public async Task IssueAsync_HasNoClaimsThatAltinnTokenHandlersAuthorizeOn()
        {
            (string token, _) = await CreateIssuer().IssueAsync(App);

            JsonWebToken jwt = new(token);
            Assert.DoesNotContain(jwt.Claims, c => c.Type is "urn:altinn:org" or "urn:altinn:orgNumber" or "urn:altinn:userid" or "scope");
        }

        [Fact]
        public async Task IssueAsync_IsNotValidAgainstTheAltinnTokenCertificate()
        {
            (string token, _) = await CreateIssuer().IssueAsync(App);
            X509Certificate2 altinnTokenCertificate = X509CertificateLoader.LoadCertificateFromFile("selfSignedTestCertificatePublic.cer");

            TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(token, ValidationParameters(altinnTokenCertificate));

            Assert.False(result.IsValid);
            Assert.IsAssignableFrom<SecurityTokenSignatureKeyNotFoundException>(result.Exception);
        }
    }
}
