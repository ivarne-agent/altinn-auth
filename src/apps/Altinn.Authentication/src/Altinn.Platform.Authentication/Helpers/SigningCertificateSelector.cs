using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Altinn.Platform.Authentication.Helpers
{
    /// <summary>
    /// Chooses the certificate to sign with, so a new one is published in the JWKS for a while before it signs.
    /// </summary>
    public static class SigningCertificateSelector
    {
        /// <summary>
        /// Returns the newest certificate that has been valid for <paramref name="rolloverDelayHours"/>, or the
        /// newest valid one if none has been valid that long.
        /// </summary>
        public static X509Certificate2 Select(IEnumerable<X509Certificate2> certificates, int rolloverDelayHours, DateTimeOffset now)
        {
            List<X509Certificate2> all = [.. certificates];

            // Limit to certs older than the rollover delay (UTC)
            DateTime rolloverCutoffUtc = now.AddHours(-rolloverDelayHours).UtcDateTime;
            List<X509Certificate2> potentialCerts = all.Where(c => c.NotBefore.ToUniversalTime() <= rolloverCutoffUtc).ToList();

            // If no certs could be found, then widen the search to any usable certificate.
            if (potentialCerts.Count == 0)
            {
                DateTime nowUtc = now.UtcDateTime;
                potentialCerts = [.. all.Where(c => c.NotBefore.ToUniversalTime() <= nowUtc)];
            }

            // Of the potential certs, return the newest one.
            if (potentialCerts.Count == 0)
            {
                throw new InvalidOperationException("No valid JWT signing certificate available.");
            }

            return potentialCerts.OrderByDescending(c => c.NotBefore).First();
        }
    }
}
