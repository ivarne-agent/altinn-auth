using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.WorkloadIdentity;

namespace Altinn.Platform.Authentication.Services.Interfaces
{
    /// <summary>
    /// Validates a Kubernetes service account token against the cluster that issued it.
    /// </summary>
    public interface IWorkloadAssertionValidator
    {
        /// <summary>
        /// Validates <paramref name="assertion"/>. Returns the trusted issuer and the service account on success,
        /// or the reason it was rejected, one of the <c>WorkloadAssertionValidator.Error*</c> values.
        /// </summary>
        Task<WorkloadAssertionResult> ValidateAsync(string assertion, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The outcome of validating a service account token.
    /// </summary>
    public sealed record WorkloadAssertionResult
    {
        /// <summary>The trusted issuer, when the token is valid.</summary>
        public TrustedWorkloadIssuer? Issuer { get; init; }

        /// <summary>The service account's <c>sub</c>, when the token is valid.</summary>
        public string? Subject { get; init; }

        /// <summary>The service account's namespace, when the token is valid.</summary>
        public string? ServiceAccountNamespace { get; init; }

        /// <summary>The service account's name, when the token is valid.</summary>
        public string? ServiceAccountName { get; init; }

        /// <summary>Why the token was rejected, a low-cardinality value for logs and metrics; null when valid.</summary>
        public string? Error { get; init; }

        /// <summary>Whether the cluster's keys could not be read, which is not the caller's fault.</summary>
        public bool KeysUnavailable { get; init; }

        /// <summary>Whether the token is valid.</summary>
        public bool IsValid => Error is null;
    }
}
