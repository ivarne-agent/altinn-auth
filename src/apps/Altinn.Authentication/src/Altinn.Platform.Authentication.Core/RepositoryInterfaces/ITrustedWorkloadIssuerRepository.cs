using Altinn.Platform.Authentication.Core.Models.WorkloadIdentity;

namespace Altinn.Platform.Authentication.Core.RepositoryInterfaces
{
    /// <summary>
    /// Stores the Kubernetes clusters whose service account tokens may be exchanged for app tokens.
    /// </summary>
    public interface ITrustedWorkloadIssuerRepository
    {
        /// <summary>Returns the issuer with the exact <paramref name="issuer"/>, enabled or not, or null.</summary>
        Task<TrustedWorkloadIssuer?> GetIssuerAsync(string issuer, CancellationToken cancellationToken = default);

        /// <summary>Returns every registered issuer.</summary>
        Task<IReadOnlyList<TrustedWorkloadIssuer>> ListIssuersAsync(CancellationToken cancellationToken = default);

        /// <summary>Registers an issuer, or replaces the registration of one with the same <c>Issuer</c>.</summary>
        Task<TrustedWorkloadIssuer> UpsertIssuerAsync(TrustedWorkloadIssuer issuer, CancellationToken cancellationToken = default);

        /// <summary>Enables or disables an issuer. Returns false if there is none.</summary>
        Task<bool> SetIssuerEnabledAsync(string issuer, bool enabled, CancellationToken cancellationToken = default);

        /// <summary>Returns the binding of <paramref name="subject"/> in <paramref name="issuer"/>, or null.</summary>
        Task<WorkloadBinding?> GetBindingAsync(string issuer, string subject, CancellationToken cancellationToken = default);

        /// <summary>Creates or replaces a binding.</summary>
        Task UpsertBindingAsync(WorkloadBinding binding, CancellationToken cancellationToken = default);

        /// <summary>Removes a binding. Returns false if there is none.</summary>
        Task<bool> DeleteBindingAsync(string issuer, string subject, CancellationToken cancellationToken = default);
    }
}
