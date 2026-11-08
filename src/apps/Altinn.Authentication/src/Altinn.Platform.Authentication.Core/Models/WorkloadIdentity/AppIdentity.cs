namespace Altinn.Platform.Authentication.Core.Models.WorkloadIdentity
{
    /// <summary>
    /// Who an app token is issued to: an app, or a service owner's cluster, and the workload that asked.
    /// </summary>
    public sealed record AppIdentity
    {
        /// <summary>The service owner, such as <c>ttd</c>.</summary>
        public required string ServiceOwner { get; init; }

        /// <summary>The app's name, such as <c>my-app</c>, or null when only the service owner is known.</summary>
        public string? App { get; init; }

        /// <summary>The cluster: the <c>iss</c> of the service account token.</summary>
        public required string WorkloadIssuer { get; init; }

        /// <summary>The service account: the <c>sub</c> of the service account token.</summary>
        public required string WorkloadSubject { get; init; }

        /// <summary>The app as <c>{serviceowner}/{app}</c>, or null.</summary>
        public string? QualifiedApp => App is null ? null : $"{ServiceOwner}/{App}";
    }
}
