using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// Provides the certificates that sign app tokens. They are not the certificates that sign Altinn tokens,
    /// so a service that trusts Altinn tokens does not accept an app token in their place, see ADR-0005.
    /// </summary>
    public interface IAppTokenSigningCertificateProvider
    {
        /// <summary>
        /// Returns the valid certificates, newest first.
        /// </summary>
        Task<List<X509Certificate2>> GetCertificates();
    }
}
