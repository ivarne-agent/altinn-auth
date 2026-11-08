using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Services.Interfaces;

namespace Altinn.Platform.Authentication.Tests.Fakes
{
    /// <summary>
    /// Signs app tokens in tests with a certificate generated once per test run, distinct from the Altinn
    /// token test certificate.
    /// </summary>
    public class AppTokenSigningCertificateProviderStub : IAppTokenSigningCertificateProvider
    {
        /// <summary>The certificate that signs app tokens in tests.</summary>
        public static readonly X509Certificate2 Certificate = CreateCertificate();

        /// <inheritdoc />
        public Task<List<X509Certificate2>> GetCertificates() => Task.FromResult<List<X509Certificate2>>([Certificate]);

        private static X509Certificate2 CreateCertificate()
        {
            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new("CN=app-token-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using X509Certificate2 certificate = request.CreateSelfSigned(
                new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero));

            // Round-trip through PKCS #12 so the private key is usable for signing on every platform.
            return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), null);
        }
    }
}
