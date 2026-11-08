using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Altinn.Common.AccessToken.Services;
using Altinn.Platform.Authentication.Core.Models.WorkloadIdentity;
using Altinn.Platform.Authentication.Model.WorkloadIdentity;
using Altinn.Platform.Authentication.Services.Interfaces;
using Altinn.Platform.Authentication.Tests.Fakes;
using Altinn.Platform.Authentication.Tests.RepositoryDataAccess;
using Altinn.Platform.Authentication.Tests.Utils;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Controllers
{
    public class WorkloadIdentityAdminControllerTests(DbFixture dbFixture, WebApplicationFixture webApplicationFixture)
        : WebApplicationTests(dbFixture, webApplicationFixture)
    {
        private const string AdminScope = "altinn:authentication/workloadidentity.admin";
        private const string BasePath = "/authentication/api/v1/workloadidentity";
        private static readonly DateTimeOffset TestTime = new(2026, 11, 08, 12, 00, 00, TimeSpan.Zero);

        protected override void ConfigureHost(IWebHostBuilder builder)
        {
            base.ConfigureHost(builder);
            builder.UseSetting("FeatureManagement:AppTokens", "true");
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);
            services.AddSingleton<IJwtSigningCertificateProvider, JwtSigningCertificateProviderStub>();
            services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            services.AddSingleton<IPublicSigningKeyProvider, SigningKeyResolverStub>();
        }

        protected override ValueTask InitializeAsync()
        {
            TimeProvider.SetUtcNow(TestTime);
            return base.InitializeAsync();
        }

        private HttpClient GetClient(string? scope)
        {
            HttpClient client = CreateClient();
            string token = PrincipalUtil.GetOrgToken("digdir", "991825827", scope, ["altinn"], TestTime);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        private static TrustedWorkloadIssuerDto NewIssuer(AppResolution resolution = AppResolution.ServiceAccountName) => new()
        {
            Issuer = $"https://oidc.example.com/{Guid.NewGuid():N}/",
            DisplayName = "ttd at22",
            ServiceOwner = "ttd",
            Environment = "at22",
            DiscoveryUri = "https://oidc.example.com/.well-known/openid-configuration",
            AppResolution = resolution,
            AllowedNamespaces = ["default"],
        };

        [Fact]
        public async Task PutIssuer_WithoutTheAdminScope_IsForbidden()
        {
            HttpResponseMessage response = await GetClient("altinn:authentication/systemregister.admin")
                .PutAsJsonAsync($"{BasePath}/issuers", NewIssuer());

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task PutIssuer_ThenLookup_ReturnsTheRegistration()
        {
            HttpClient client = GetClient(AdminScope);
            TrustedWorkloadIssuerDto issuer = NewIssuer();

            HttpResponseMessage put = await client.PutAsJsonAsync($"{BasePath}/issuers", issuer);
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);

            TrustedWorkloadIssuerDto? read = await client.GetFromJsonAsync<TrustedWorkloadIssuerDto>(
                $"{BasePath}/issuers/lookup?issuer={Uri.EscapeDataString(issuer.Issuer!)}");
            Assert.NotNull(read);
            Assert.Equal(issuer.Issuer, read.Issuer);
            Assert.Equal(AppResolution.ServiceAccountName, read.AppResolution);
        }

        [Fact]
        public async Task PutIssuer_Invalid_ReturnsValidationProblem()
        {
            HttpResponseMessage response = await GetClient(AdminScope)
                .PutAsJsonAsync($"{BasePath}/issuers", NewIssuer() with { Issuer = "http://insecure.example.com/" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task PutBinding_ForAnIssuerWithExplicitBindings_Succeeds()
        {
            HttpClient client = GetClient(AdminScope);
            TrustedWorkloadIssuerDto issuer = NewIssuer(AppResolution.ExplicitBinding);
            (await client.PutAsJsonAsync($"{BasePath}/issuers", issuer)).EnsureSuccessStatusCode();

            HttpResponseMessage response = await client.PutAsJsonAsync($"{BasePath}/bindings", new WorkloadBindingDto
            {
                Issuer = issuer.Issuer,
                Subject = "system:serviceaccount:default:sa-1",
                App = "my-app",
            });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }
}
