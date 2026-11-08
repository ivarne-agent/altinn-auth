using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.WorkloadIdentity;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// Issues app tokens, see ADR-0005.
    /// </summary>
    public interface IAppTokenIssuer
    {
        /// <summary>
        /// Issues an app token for <paramref name="identity"/>. Returns the token and its lifetime in seconds.
        /// </summary>
        Task<(string Token, int ExpiresIn)> IssueAsync(AppIdentity identity, CancellationToken cancellationToken = default);
    }
}
