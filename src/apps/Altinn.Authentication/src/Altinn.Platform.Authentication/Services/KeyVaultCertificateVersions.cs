using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Model;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Rest.Azure;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Reads the versions of a Key Vault certificate, for the providers of signing certificates.
    /// </summary>
    internal static class KeyVaultCertificateVersions
    {
        /// <summary>
        /// Returns every enabled, unexpired version of the certificate <paramref name="certificateName"/>, with its private key.
        /// </summary>
        public static async Task<List<X509Certificate2>> GetAll(KeyVaultSettings keyVaultSettings, string certificateName)
        {
            List<X509Certificate2> certificates = new List<X509Certificate2>();

            KeyVaultClient client = KeyVaultSettings.GetClient(keyVaultSettings.ClientId, keyVaultSettings.ClientSecret);

            // Get the first page of certificates
            IPage<CertificateItem> certificateItemsPage = await client.GetCertificateVersionsAsync(keyVaultSettings.SecretUri, certificateName);
            while (true)
            {
                foreach (var certificateItem in certificateItemsPage)
                {
                    // Ignore disabled or expired certificates
                    if (certificateItem.Attributes.Enabled == true &&
                        (certificateItem.Attributes.Expires == null ||
                         certificateItem.Attributes.Expires > DateTime.UtcNow))
                    {
                        CertificateBundle certificateVersionBundle =
                            await client.GetCertificateAsync(certificateItem.Identifier.Identifier);
                        SecretBundle certificatePrivateKeySecretBundle =
                            await client.GetSecretAsync(certificateVersionBundle.SecretIdentifier.Identifier);
                        byte[] privateKeyBytes = Convert.FromBase64String(certificatePrivateKeySecretBundle.Value);
                        X509Certificate2 certificateWithPrivateKey = new X509Certificate2(privateKeyBytes);

                        certificates.Add(certificateWithPrivateKey);
                    }
                }

                if (certificateItemsPage.NextPageLink == null)
                {
                    break;
                }

                certificateItemsPage = await client.GetCertificateVersionsNextAsync(certificateItemsPage.NextPageLink);
            }

            return certificates;
        }
    }
}
