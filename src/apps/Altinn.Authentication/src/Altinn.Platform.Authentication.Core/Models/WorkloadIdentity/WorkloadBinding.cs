namespace Altinn.Platform.Authentication.Core.Models.WorkloadIdentity
{
    /// <summary>
    /// Maps a service account in a trusted cluster to the app it runs.
    /// </summary>
    public sealed record WorkloadBinding
    {
        /// <summary>The trusted issuer the service account belongs to.</summary>
        public required string Issuer { get; init; }

        /// <summary>The service account token's <c>sub</c>, such as <c>system:serviceaccount:default:my-app</c>.</summary>
        public required string Subject { get; init; }

        /// <summary>The app's name within the issuer's service owner, such as <c>my-app</c>.</summary>
        public required string App { get; init; }
    }
}
