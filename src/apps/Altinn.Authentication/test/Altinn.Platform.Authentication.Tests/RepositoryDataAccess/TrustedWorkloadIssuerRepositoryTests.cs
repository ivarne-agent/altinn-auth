using System;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.WorkloadIdentity;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Persistence.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.RepositoryDataAccess
{
    public class TrustedWorkloadIssuerRepositoryTests(DbFixture dbFixture) : DbTestBase(dbFixture)
    {
        private ITrustedWorkloadIssuerRepository Repository => Services.GetRequiredService<ITrustedWorkloadIssuerRepository>();

        protected override void ConfigureServices(IServiceCollection services)
        {
            services.TryAddSingleton(TimeProvider.System);
            base.ConfigureServices(services);
        }

        private static TrustedWorkloadIssuer NewIssuer(string? issuer = null) => new()
        {
            Issuer = issuer ?? $"https://oidc.example.com/{Guid.NewGuid():N}/",
            DisplayName = "ttd at22",
            ServiceOwner = "ttd",
            Environment = "at22",
            DiscoveryUri = new Uri("https://oidc.example.com/.well-known/openid-configuration"),
            AppResolution = AppResolution.ServiceAccountName,
            AllowedNamespaces = ["default"],
        };

        [Fact]
        public async Task UpsertIssuer_ThenGet_RoundTrips()
        {
            TrustedWorkloadIssuer issuer = NewIssuer();

            TrustedWorkloadIssuer stored = await Repository.UpsertIssuerAsync(issuer);
            TrustedWorkloadIssuer? read = await Repository.GetIssuerAsync(issuer.Issuer);

            Assert.NotNull(read);
            Assert.Equal(issuer.Issuer, read.Issuer);
            Assert.Equal("ttd", read.ServiceOwner);
            Assert.Equal(AppResolution.ServiceAccountName, read.AppResolution);
            Assert.Equal(["default"], read.AllowedNamespaces);
            Assert.True(read.Enabled);
            Assert.Null(read.UpdatedAt);
            Assert.Equal(stored.CreatedAt, read.CreatedAt);
        }

        [Fact]
        public async Task UpsertIssuer_Twice_ReplacesTheRegistration()
        {
            TrustedWorkloadIssuer issuer = NewIssuer();
            await Repository.UpsertIssuerAsync(issuer);

            TrustedWorkloadIssuer updated = await Repository.UpsertIssuerAsync(issuer with
            {
                DiscoveryUri = null,
                JwksJson = """{"keys":[{"kty":"RSA","kid":"k1","n":"AQAB","e":"AQAB"}]}""",
                AppResolution = AppResolution.ExplicitBinding,
            });

            Assert.Null(updated.DiscoveryUri);
            Assert.Contains("\"kid\": \"k1\"", updated.JwksJson);
            Assert.Equal(AppResolution.ExplicitBinding, updated.AppResolution);
            Assert.NotNull(updated.UpdatedAt);
        }

        [Fact]
        public async Task UpsertIssuer_WithoutKeys_IsRejectedByTheDatabase()
        {
            TrustedWorkloadIssuer issuer = NewIssuer() with { DiscoveryUri = null, JwksJson = null };

            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => Repository.UpsertIssuerAsync(issuer));
            Assert.Equal("chk_trusted_issuer_keys", ex.ConstraintName);
        }

        [Fact]
        public async Task SetIssuerEnabled_DisablesAndReportsUnknownIssuers()
        {
            TrustedWorkloadIssuer issuer = NewIssuer();
            await Repository.UpsertIssuerAsync(issuer);

            Assert.True(await Repository.SetIssuerEnabledAsync(issuer.Issuer, false));
            Assert.False((await Repository.GetIssuerAsync(issuer.Issuer))!.Enabled);
            Assert.False(await Repository.SetIssuerEnabledAsync("https://unknown.example.com/", false));
        }

        [Fact]
        public async Task Bindings_CanBeCreatedReplacedAndDeleted()
        {
            TrustedWorkloadIssuer issuer = await Repository.UpsertIssuerAsync(NewIssuer() with { AppResolution = AppResolution.ExplicitBinding });
            const string Subject = "system:serviceaccount:default:sa-1";

            await Repository.UpsertBindingAsync(new WorkloadBinding { Issuer = issuer.Issuer, Subject = Subject, App = "app-a" });
            await Repository.UpsertBindingAsync(new WorkloadBinding { Issuer = issuer.Issuer, Subject = Subject, App = "app-b" });

            Assert.Equal("app-b", (await Repository.GetBindingAsync(issuer.Issuer, Subject))!.App);
            Assert.True(await Repository.DeleteBindingAsync(issuer.Issuer, Subject));
            Assert.Null(await Repository.GetBindingAsync(issuer.Issuer, Subject));
            Assert.False(await Repository.DeleteBindingAsync(issuer.Issuer, Subject));
        }

        [Fact]
        public async Task ListIssuers_ReturnsRegisteredIssuers()
        {
            TrustedWorkloadIssuer issuer = await Repository.UpsertIssuerAsync(NewIssuer());

            Assert.Contains(await Repository.ListIssuersAsync(), i => i.Issuer == issuer.Issuer);
        }
    }
}
