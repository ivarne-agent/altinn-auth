using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.WorkloadIdentity;

namespace Altinn.Platform.Authentication.Model.WorkloadIdentity
{
    /// <summary>
    /// A Kubernetes cluster trusted to exchange its service account tokens for app tokens.
    /// </summary>
    public sealed record TrustedWorkloadIssuerDto
    {
        /// <summary>The exact <c>iss</c> of the cluster's service account tokens.</summary>
        [JsonPropertyName("issuer")]
        public string? Issuer { get; init; }

        /// <summary>Human-readable name.</summary>
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }

        /// <summary>The service owner the cluster runs apps for, such as <c>ttd</c>.</summary>
        [JsonPropertyName("serviceOwner")]
        public string? ServiceOwner { get; init; }

        /// <summary>The Altinn environment, such as <c>tt02</c>.</summary>
        [JsonPropertyName("environment")]
        public string? Environment { get; init; }

        /// <summary>The cluster's OIDC discovery document. Give this or <see cref="Jwks"/>.</summary>
        [JsonPropertyName("discoveryUri")]
        public string? DiscoveryUri { get; init; }

        /// <summary>The cluster's public keys, pinned. Give this or <see cref="DiscoveryUri"/>.</summary>
        [JsonPropertyName("jwks")]
        public System.Text.Json.JsonElement? Jwks { get; init; }

        /// <summary>How a service account is mapped to an app.</summary>
        [JsonPropertyName("appResolution")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AppResolution? AppResolution { get; init; }

        /// <summary>The namespaces whose service accounts may get app tokens.</summary>
        [JsonPropertyName("allowedNamespaces")]
        public IReadOnlyList<string>? AllowedNamespaces { get; init; }

        /// <summary>Whether the issuer is trusted.</summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; init; } = true;

        /// <summary>When the issuer was registered. Ignored on input.</summary>
        [JsonPropertyName("createdAt")]
        public DateTimeOffset? CreatedAt { get; init; }

        /// <summary>When the issuer was last changed. Ignored on input.</summary>
        [JsonPropertyName("updatedAt")]
        public DateTimeOffset? UpdatedAt { get; init; }

        /// <summary>Maps a stored issuer to its representation.</summary>
        public static TrustedWorkloadIssuerDto From(TrustedWorkloadIssuer issuer) => new()
        {
            Issuer = issuer.Issuer,
            DisplayName = issuer.DisplayName,
            ServiceOwner = issuer.ServiceOwner,
            Environment = issuer.Environment,
            DiscoveryUri = issuer.DiscoveryUri?.ToString(),
            Jwks = issuer.JwksJson is null ? null : System.Text.Json.JsonDocument.Parse(issuer.JwksJson).RootElement.Clone(),
            AppResolution = issuer.AppResolution,
            AllowedNamespaces = issuer.AllowedNamespaces,
            Enabled = issuer.Enabled,
            CreatedAt = issuer.CreatedAt,
            UpdatedAt = issuer.UpdatedAt,
        };
    }

    /// <summary>
    /// Maps a service account in a trusted cluster to the app it runs.
    /// </summary>
    public sealed record WorkloadBindingDto
    {
        /// <summary>The trusted issuer the service account belongs to.</summary>
        [JsonPropertyName("issuer")]
        public string? Issuer { get; init; }

        /// <summary>The service account token's <c>sub</c>, such as <c>system:serviceaccount:default:my-app</c>.</summary>
        [JsonPropertyName("subject")]
        public string? Subject { get; init; }

        /// <summary>The app's name within the issuer's service owner, such as <c>my-app</c>.</summary>
        [JsonPropertyName("app")]
        public string? App { get; init; }
    }
}
