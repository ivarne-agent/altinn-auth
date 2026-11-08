using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers
{
    public class AppTokenWellKnownControllerTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        protected override void ConfigureHost(IWebHostBuilder builder)
        {
            base.ConfigureHost(builder);
            builder.UseSetting("FeatureManagement:AppTokens", "true");
            builder.UseSetting("AppTokenSettings:Issuer", "https://platform.example.com/authentication/api/v1/apptoken");
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);
            services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProviderStub>();
            services.AddSingleton<IAppTokenSigningCertificateProvider, AppTokenSigningCertificateProviderStub>();
        }

        [Fact]
        public async Task Metadata_NamesTheAppTokenIssuerAndItsJwks()
        {
            JsonElement metadata = await CreateClient().GetFromJsonAsync<JsonElement>(
                "/authentication/api/v1/apptoken/.well-known/oauth-authorization-server");

            Assert.Equal("https://platform.example.com/authentication/api/v1/apptoken", metadata.GetProperty("issuer").GetString());
            Assert.Equal("https://platform.example.com/authentication/api/v1/apptoken/.well-known/jwks", metadata.GetProperty("jwks_uri").GetString());
            Assert.Equal("urn:ietf:params:oauth:grant-type:jwt-bearer", metadata.GetProperty("grant_types_supported")[0].GetString());
        }

        [Fact]
        public async Task Jwks_HasTheAppTokenKeyAndNotTheAltinnTokenKey()
        {
            HttpClient client = CreateClient();

            JsonElement appJwks = await client.GetFromJsonAsync<JsonElement>("/authentication/api/v1/apptoken/.well-known/jwks");
            JsonElement altinnJwks = await client.GetFromJsonAsync<JsonElement>("/authentication/api/v1/openid/.well-known/openid-configuration/jwks");

            string[] appKids = appJwks.GetProperty("keys").EnumerateArray().Select(k => k.GetProperty("kid").GetString()!).ToArray();
            string[] altinnKids = altinnJwks.GetProperty("keys").EnumerateArray().Select(k => k.GetProperty("kid").GetString()!).ToArray();
            Assert.Equal([AppTokenSigningCertificateProviderStub.Certificate.Thumbprint], appKids);
            Assert.Empty(appKids.Intersect(altinnKids));
        }
    }

    public class AppTokenWellKnownControllerDisabledTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);
            services.AddSingleton<IAppTokenSigningCertificateProvider, AppTokenSigningCertificateProviderStub>();
        }

        [Fact]
        public async Task Jwks_WhenAppTokensAreOff_IsNotFound()
        {
            HttpResponseMessage response = await CreateClient().GetAsync("/authentication/api/v1/apptoken/.well-known/jwks");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
