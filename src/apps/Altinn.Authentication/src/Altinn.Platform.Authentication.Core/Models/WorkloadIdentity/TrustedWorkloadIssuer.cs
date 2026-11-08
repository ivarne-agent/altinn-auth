namespace Altinn.Platform.Authentication.Core.Models.WorkloadIdentity
{
    /// <summary>
    /// A Kubernetes cluster whose service account tokens may be exchanged for app tokens.
    /// </summary>
    public sealed record TrustedWorkloadIssuer
    {
        /// <summary>The exact <c>iss</c> of the cluster's service account tokens.</summary>
        public required string Issuer { get; init; }

        /// <summary>Human-readable name, for logs and administration.</summary>
        public required string DisplayName { get; init; }

        /// <summary>The service owner the cluster runs apps for, such as <c>ttd</c>.</summary>
        public required string ServiceOwner { get; init; }

        /// <summary>The Altinn environment the cluster belongs to, such as <c>tt02</c>.</summary>
        public required string Environment { get; init; }

        /// <summary>The cluster's OIDC discovery document, or null when its keys are pinned in <see cref="JwksJson"/>.</summary>
        public Uri? DiscoveryUri { get; init; }

        /// <summary>The cluster's public keys as a JWKS, or null when they are fetched from <see cref="DiscoveryUri"/>.</summary>
        public string? JwksJson { get; init; }

        /// <summary>How a service account is mapped to an app.</summary>
        public required AppResolution AppResolution { get; init; }

        /// <summary>The namespaces whose service accounts may get app tokens. Empty allows none.</summary>
        public IReadOnlyList<string> AllowedNamespaces { get; init; } = [];

        /// <summary>Whether the issuer is trusted. A disabled issuer gets no tokens.</summary>
        public bool Enabled { get; init; } = true;

        /// <summary>When the issuer was registered.</summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>When the issuer was last changed.</summary>
        public DateTimeOffset? UpdatedAt { get; init; }
    }
}
