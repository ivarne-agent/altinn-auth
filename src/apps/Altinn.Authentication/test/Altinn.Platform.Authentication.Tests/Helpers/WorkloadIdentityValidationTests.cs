using System.Collections.Generic;
using System.Text.Json;
using Altinn.Platform.Authentication.Core.Models.WorkloadIdentity;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model.WorkloadIdentity;
using Xunit;

namespace Altinn.Platform.Authentication.Tests.Helpers
{
    public class WorkloadIdentityValidationTests
    {
        private static readonly TrustedWorkloadIssuerDto Valid = new()
        {
            Issuer = "https://oidc.example.com/cluster/",
            DisplayName = "ttd at22",
            ServiceOwner = "ttd",
            Environment = "at22",
            DiscoveryUri = "https://oidc.example.com/cluster/.well-known/openid-configuration",
            AppResolution = AppResolution.ServiceAccountName,
            AllowedNamespaces = ["default"],
        };

        [Fact]
        public void Validate_AValidIssuer_HasNoErrors()
        {
            Dictionary<string, string[]> errors = WorkloadIdentityValidation.Validate(Valid, out TrustedWorkloadIssuer? issuer);

            Assert.Empty(errors);
            Assert.NotNull(issuer);
            Assert.Equal("ttd", issuer.ServiceOwner);
        }

        [Theory]
        [InlineData("http://oidc.example.com/", "issuer")]
        [InlineData("https://oidc.example.com/?a=b", "issuer")]
        [InlineData("not a uri", "issuer")]
        public void Validate_AnIssuerThatIsNotAnHttpsUri_IsRejected(string value, string field)
        {
            Dictionary<string, string[]> errors = WorkloadIdentityValidation.Validate(Valid with { Issuer = value }, out _);

            Assert.Contains(field, errors.Keys);
        }

        [Fact]
        public void Validate_BothDiscoveryAndJwks_IsRejected()
        {
            JsonElement jwks = JsonDocument.Parse("""{"keys":[{"kty":"RSA","kid":"k1","n":"AQAB","e":"AQAB"}]}""").RootElement;

            Dictionary<string, string[]> errors = WorkloadIdentityValidation.Validate(Valid with { Jwks = jwks }, out _);

            Assert.Contains("discoveryUri", errors.Keys);
        }

        [Fact]
        public void Validate_AJwksWithAPrivateKey_IsRejected()
        {
            JsonElement jwks = JsonDocument.Parse("""{"keys":[{"kty":"RSA","kid":"k1","n":"AQAB","e":"AQAB","d":"AQAB"}]}""").RootElement;

            Dictionary<string, string[]> errors = WorkloadIdentityValidation.Validate(Valid with { DiscoveryUri = null, Jwks = jwks }, out _);

            Assert.Equal(["Must have public keys only."], errors["jwks"]);
        }

        [Fact]
        public void Validate_NoNamespaces_IsRejected()
        {
            Dictionary<string, string[]> errors = WorkloadIdentityValidation.Validate(Valid with { AllowedNamespaces = [] }, out _);

            Assert.Contains("allowedNamespaces", errors.Keys);
        }

        [Theory]
        [InlineData("system:serviceaccount:default:my-app", true, "default", "my-app")]
        [InlineData("system:serviceaccount:default:my-app:extra", false, "", "")]
        [InlineData("system:serviceaccount:Default:my-app", false, "", "")]
        [InlineData("system:node:default", false, "", "")]
        [InlineData(null, false, "", "")]
        public void TryParseServiceAccountSubject_ParsesOnlyServiceAccounts(string? subject, bool ok, string expectedNamespace, string expectedName)
        {
            bool parsed = WorkloadIdentityValidation.TryParseServiceAccountSubject(subject, out string ns, out string name);

            Assert.Equal(ok, parsed);
            Assert.Equal(expectedNamespace, ns);
            Assert.Equal(expectedName, name);
        }

        [Fact]
        public void Validate_ABindingForAnIssuerWithoutExplicitBindings_IsRejected()
        {
            WorkloadIdentityValidation.Validate(Valid, out TrustedWorkloadIssuer? issuer);
            WorkloadBindingDto binding = new() { Issuer = Valid.Issuer, Subject = "system:serviceaccount:default:sa", App = "my-app" };

            Dictionary<string, string[]> errors = WorkloadIdentityValidation.Validate(binding, issuer!, out _);

            Assert.Contains("issuer", errors.Keys);
        }

        [Fact]
        public void Validate_ABindingOutsideTheAllowedNamespaces_IsRejected()
        {
            WorkloadIdentityValidation.Validate(Valid with { AppResolution = AppResolution.ExplicitBinding }, out TrustedWorkloadIssuer? issuer);
            WorkloadBindingDto binding = new() { Issuer = Valid.Issuer, Subject = "system:serviceaccount:kube-system:sa", App = "my-app" };

            Dictionary<string, string[]> errors = WorkloadIdentityValidation.Validate(binding, issuer!, out _);

            Assert.Contains("subject", errors.Keys);
        }
    }
}
