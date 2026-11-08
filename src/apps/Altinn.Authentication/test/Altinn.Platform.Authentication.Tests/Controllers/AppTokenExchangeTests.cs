using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models.WorkloadIdentity;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers
{
    /// <summary>
    /// The RFC 7523 jwt-bearer grant on the token endpoint: a trusted cluster's service account token in,
    /// an app token out. See ADR-0005.
    /// </summary>
    public class AppTokenExchangeTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        private const string GrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer";
        private const string TokenPath = "/authentication/api/v1/token";
        private const string AssertionAudience = "https://platform.example.com/authentication/api/v1/token";
        private const string AppTokenIssuer = "https://platform.example.com/authentication/api/v1/apptoken";
        private const string AppTokenAudience = "https://platform.example.com/";
        private static readonly DateTimeOffset TestTime = new(2026, 11, 08, 12, 00, 00, TimeSpan.Zero);

        private readonly Mock<ISigningKeysRetriever> _signingKeysRetriever = new();

        protected virtual bool AppTokensEnabled => true;

        private ITrustedWorkloadIssuerRepository Repository => Services.GetRequiredService<ITrustedWorkloadIssuerRepository>();

        protected override void ConfigureHost(IWebHostBuilder builder)
        {
            base.ConfigureHost(builder);
            builder.UseSetting("FeatureManagement:AppTokens", AppTokensEnabled ? "true" : "false");
            builder.UseSetting("AppTokenSettings:Issuer", AppTokenIssuer);
            builder.UseSetting("AppTokenSettings:Audience", AppTokenAudience);
            builder.UseSetting("AppTokenSettings:AssertionAudience", AssertionAudience);
            builder.UseSetting("AppTokenSettings:LifetimeMinutes", "10");
            builder.UseSetting("AppTokenSettings:MaxAssertionLifetimeMinutes", "60");
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);
            services.AddSingleton<IAppTokenSigningCertificateProvider, AppTokenSigningCertificateProviderStub>();
            services.AddSingleton(_signingKeysRetriever.Object);
        }

        protected override ValueTask InitializeAsync()
        {
            TimeProvider.SetUtcNow(TestTime);
            return base.InitializeAsync();
        }

        private async Task<FakeKubernetesCluster> RegisterCluster(
            AppResolution resolution = AppResolution.ServiceAccountName,
            bool enabled = true,
            bool pinnedKeys = true)
        {
            FakeKubernetesCluster cluster = new();
            await Repository.UpsertIssuerAsync(new TrustedWorkloadIssuer
            {
                Issuer = cluster.Issuer,
                DisplayName = "ttd test cluster",
                ServiceOwner = "ttd",
                Environment = "at22",
                JwksJson = pinnedKeys ? cluster.Jwks : null,
                DiscoveryUri = pinnedKeys ? null : new Uri($"{cluster.Issuer}.well-known/openid-configuration"),
                AppResolution = resolution,
                AllowedNamespaces = ["default"],
                Enabled = enabled,
            });
            return cluster;
        }

        private async Task<HttpResponseMessage> Exchange(string assertion, IDictionary<string, string>? extra = null)
        {
            Dictionary<string, string> form = new() { ["grant_type"] = GrantType, ["assertion"] = assertion };
            foreach ((string key, string value) in extra ?? new Dictionary<string, string>())
            {
                form[key] = value;
            }

            return await CreateClient().PostAsync(TokenPath, new FormUrlEncodedContent(form));
        }

        private static async Task<JsonWebToken> ReadValidAppToken(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(600, body.GetProperty("expires_in").GetInt32());

            TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(
                body.GetProperty("access_token").GetString(),
                new TokenValidationParameters
                {
                    ValidIssuer = AppTokenIssuer,
                    ValidAudience = AppTokenAudience,
                    ValidTypes = [AppTokenClaimTypes.TokenType],
                    IssuerSigningKey = new X509SecurityKey(AppTokenSigningCertificateProviderStub.Certificate),
                    ValidateLifetime = false,
                });
            Assert.True(result.IsValid, result.Exception?.Message);
            return (JsonWebToken)result.SecurityToken;
        }

        private static async Task AssertInvalidGrant(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
        }

        [Fact]
        public async Task Exchange_ServiceAccountNamedAfterTheApp_IssuesAnAppToken()
        {
            FakeKubernetesCluster cluster = await RegisterCluster();

            HttpResponseMessage response = await Exchange(cluster.CreateToken("default", "my-app", AssertionAudience, TestTime));

            JsonWebToken token = await ReadValidAppToken(response);
            Assert.Equal("ttd/my-app", token.Subject);
            Assert.Equal("ttd/my-app", token.GetClaim(AppTokenClaimTypes.App).Value);
            Assert.Equal("ttd", token.GetClaim(AppTokenClaimTypes.ServiceOwner).Value);
            Assert.Equal(cluster.Issuer, token.GetClaim(AppTokenClaimTypes.WorkloadIssuer).Value);
            Assert.Equal("system:serviceaccount:default:my-app", token.GetClaim(AppTokenClaimTypes.WorkloadSubject).Value);
        }

        [Fact]
        public async Task Exchange_ClusterMappedToTheServiceOwnerOnly_IssuesATokenWithoutApp()
        {
            FakeKubernetesCluster cluster = await RegisterCluster(AppResolution.ServiceOwnerOnly);

            JsonWebToken token = await ReadValidAppToken(await Exchange(cluster.CreateToken("default", "default", AssertionAudience, TestTime)));

            Assert.Equal("ttd", token.Subject);
            Assert.False(token.TryGetClaim(AppTokenClaimTypes.App, out _));
        }

        [Fact]
        public async Task Exchange_ExplicitBinding_UsesTheBoundApp()
        {
            FakeKubernetesCluster cluster = await RegisterCluster(AppResolution.ExplicitBinding);
            await Repository.UpsertBindingAsync(new WorkloadBinding
            {
                Issuer = cluster.Issuer,
                Subject = "system:serviceaccount:default:sa-123",
                App = "bound-app",
            });

            JsonWebToken token = await ReadValidAppToken(await Exchange(cluster.CreateToken("default", "sa-123", AssertionAudience, TestTime)));

            Assert.Equal("ttd/bound-app", token.Subject);
        }

        [Fact]
        public async Task Exchange_ExplicitBindingMissing_IsInvalidGrant()
        {
            FakeKubernetesCluster cluster = await RegisterCluster(AppResolution.ExplicitBinding);

            await AssertInvalidGrant(await Exchange(cluster.CreateToken("default", "unbound", AssertionAudience, TestTime)));
        }

        [Fact]
        public async Task Exchange_KeysFromDiscovery_IssuesAnAppToken()
        {
            FakeKubernetesCluster cluster = await RegisterCluster(pinnedKeys: false);
            _signingKeysRetriever
                .Setup(r => r.GetSigningKeys($"{cluster.Issuer}.well-known/openid-configuration"))
                .ReturnsAsync([cluster.PublicKey]);

            await ReadValidAppToken(await Exchange(cluster.CreateToken("default", "my-app", AssertionAudience, TestTime)));
        }

        [Fact]
        public async Task Exchange_DiscoveryUnreachable_IsAServerError()
        {
            FakeKubernetesCluster cluster = await RegisterCluster(pinnedKeys: false);
            _signingKeysRetriever
                .Setup(r => r.GetSigningKeys(It.IsAny<string>()))
                .ThrowsAsync(new HttpRequestException("unreachable"));

            HttpResponseMessage response = await Exchange(cluster.CreateToken("default", "my-app", AssertionAudience, TestTime));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task Exchange_UnknownCluster_IsInvalidGrant()
        {
            FakeKubernetesCluster unregistered = new();

            await AssertInvalidGrant(await Exchange(unregistered.CreateToken("default", "my-app", AssertionAudience, TestTime)));
        }

        [Fact]
        public async Task Exchange_DisabledCluster_IsInvalidGrant()
        {
            FakeKubernetesCluster cluster = await RegisterCluster(enabled: false);

            await AssertInvalidGrant(await Exchange(cluster.CreateToken("default", "my-app", AssertionAudience, TestTime)));
        }

        [Fact]
        public async Task Exchange_SignedWithAnotherKey_IsInvalidGrant()
        {
            FakeKubernetesCluster cluster = await RegisterCluster();
            RsaSecurityKey attackerKey = new(RSA.Create(2048));

            await AssertInvalidGrant(await Exchange(cluster.CreateToken("default", "my-app", AssertionAudience, TestTime, signingKey: attackerKey)));
        }

        [Theory]
        [InlineData("https://platform.example.com/authentication/api/v1/token/")]
        [InlineData("https://kubernetes.default.svc")]
        [InlineData("https://platform.example.com/")]
        public async Task Exchange_WrongAudience_IsInvalidGrant(string audience)
        {
            FakeKubernetesCluster cluster = await RegisterCluster();

            await AssertInvalidGrant(await Exchange(cluster.CreateToken("default", "my-app", audience, TestTime)));
        }

        [Fact]
        public async Task Exchange_ExpiredToken_IsInvalidGrant()
        {
            FakeKubernetesCluster cluster = await RegisterCluster();

            await AssertInvalidGrant(await Exchange(cluster.CreateToken("default", "my-app", AssertionAudience, TestTime.AddMinutes(-11))));
        }

        [Fact]
        public async Task Exchange_TokenValidForLongerThanAllowed_IsInvalidGrant()
        {
            FakeKubernetesCluster cluster = await RegisterCluster();

            await AssertInvalidGrant(await Exchange(cluster.CreateToken("default", "my-app", AssertionAudience, TestTime, lifetime: TimeSpan.FromHours(24))));
        }

        [Fact]
        public async Task Exchange_NamespaceNotAllowed_IsInvalidGrant()
        {
            FakeKubernetesCluster cluster = await RegisterCluster();

            await AssertInvalidGrant(await Exchange(cluster.CreateToken("kube-system", "my-app", AssertionAudience, TestTime)));
        }

        [Fact]
        public async Task Exchange_WithClientAuthentication_IsInvalidRequest()
        {
            FakeKubernetesCluster cluster = await RegisterCluster();

            HttpResponseMessage response = await Exchange(
                cluster.CreateToken("default", "my-app", AssertionAudience, TestTime),
                new Dictionary<string, string> { ["client_id"] = "some-client", ["client_secret"] = "secret" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("invalid_request", body.GetProperty("error").GetString());
        }

        [Fact]
        public async Task Exchange_NotAJwt_IsInvalidGrant()
        {
            await AssertInvalidGrant(await Exchange("not-a-jwt"));
        }
    }

    public class AppTokenExchangeDisabledTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        protected override void ConfigureHost(IWebHostBuilder builder)
        {
            base.ConfigureHost(builder);
            builder.UseSetting("FeatureManagement:AppTokens", "false");
        }

        [Fact]
        public async Task Exchange_WhenAppTokensAreOff_IsAnUnsupportedGrant()
        {
            HttpResponseMessage response = await CreateClient().PostAsync("/authentication/api/v1/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = "irrelevant",
            }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("unsupported_grant_type", body.GetProperty("error").GetString());
        }
    }
}
