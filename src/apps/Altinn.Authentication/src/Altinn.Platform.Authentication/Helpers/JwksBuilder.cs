using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Altinn.Platform.Authentication.Model;

namespace Altinn.Platform.Authentication.Helpers
{
    /// <summary>
    /// Publishes signing certificates as a JSON Web Key Set.
    /// </summary>
    public static class JwksBuilder
    {
        /// <summary>
        /// Returns the public keys of <paramref name="certificates"/>, each with its certificate chain and its
        /// thumbprint as the key ID.
        /// </summary>
        public static JwksDocument Build(IEnumerable<X509Certificate2> certificates)
        {
            JwksDocument jwksDocument = new JwksDocument
            {
                Keys = new List<JwkDocument>()
            };

            foreach (X509Certificate2 cert in certificates)
            {
                string oidFriendlyName = cert.PublicKey.Oid.FriendlyName ?? "RSA";

                using RSA rsaPublicKey = cert.GetRSAPublicKey()
                    ?? throw new InvalidOperationException($"Signing certificate {cert.Thumbprint} has no RSA public key.");
                RSAParameters exportParameters = rsaPublicKey.ExportParameters(false);
                string exponent = Convert.ToBase64String(exportParameters.Exponent!);
                string modulus = Convert.ToBase64String(exportParameters.Modulus!);

                List<string> chain = ExportChain(cert);

                JwkDocument jwkDocument = new JwkDocument
                {
                    KeyType = oidFriendlyName,
                    PublicKeyUse = "sig",
                    KeyId = cert.Thumbprint,
                    Exponent = exponent,
                    Modulus = modulus,
                    X509Chain = chain
                };

                jwksDocument.Keys.Add(jwkDocument);
            }

            return jwksDocument;
        }

        private static List<string> ExportChain(X509Certificate2 cert)
        {
            List<string> result = new List<string>();

            using (X509Chain chain = new X509Chain { ChainPolicy = { RevocationMode = X509RevocationMode.NoCheck } })
            {
                chain.Build(cert);

                foreach (X509ChainElement chainElement in chain.ChainElements)
                {
                    string export = Convert.ToBase64String(chainElement.Certificate.Export(X509ContentType.Cert));
                    result.Add(export);
                }
            }

            return result;
        }
    }
}
