using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Core.Models.WorkloadIdentity;
using Altinn.Platform.Authentication.Model.WorkloadIdentity;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Platform.Authentication.Helpers
{
    /// <summary>
    /// Validates registrations of trusted workload issuers and bindings, and turns them into the stored models.
    /// </summary>
    public static partial class WorkloadIdentityValidation
    {
        /// <summary>The prefix of a Kubernetes service account token's <c>sub</c>.</summary>
        public const string ServiceAccountSubjectPrefix = "system:serviceaccount:";

        /// <summary>
        /// A lowercase DNS label: a Kubernetes namespace or service account name, a service owner's short
        /// name, or an app's name.
        /// </summary>
        [GeneratedRegex("^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$")]
        public static partial Regex DnsLabel();

        /// <summary>
        /// Splits a service account token's <c>sub</c> into its namespace and name, or returns false.
        /// </summary>
        public static bool TryParseServiceAccountSubject(string? subject, out string serviceAccountNamespace, out string name)
        {
            serviceAccountNamespace = name = string.Empty;
            if (subject is null || !subject.StartsWith(ServiceAccountSubjectPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            string[] parts = subject[ServiceAccountSubjectPrefix.Length..].Split(':');
            if (parts.Length != 2 || !DnsLabel().IsMatch(parts[0]) || !DnsLabel().IsMatch(parts[1]))
            {
                return false;
            }

            (serviceAccountNamespace, name) = (parts[0], parts[1]);
            return true;
        }

        /// <summary>
        /// Validates an issuer registration. Returns the errors by field, and the issuer to store if there are none.
        /// </summary>
        public static Dictionary<string, string[]> Validate(TrustedWorkloadIssuerDto dto, out TrustedWorkloadIssuer? issuer)
        {
            issuer = null;
            Dictionary<string, string[]> errors = [];

            if (!IsHttpsUri(dto.Issuer, out _))
            {
                errors["issuer"] = ["Must be an absolute https URI without query or fragment."];
            }

            if (string.IsNullOrWhiteSpace(dto.DisplayName))
            {
                errors["displayName"] = ["Required."];
            }

            if (dto.ServiceOwner is null || !DnsLabel().IsMatch(dto.ServiceOwner))
            {
                errors["serviceOwner"] = ["Must be the service owner's short name, a lowercase DNS label."];
            }

            if (dto.Environment is null || !DnsLabel().IsMatch(dto.Environment))
            {
                errors["environment"] = ["Must be the environment's name, a lowercase DNS label."];
            }

            Uri? discoveryUri = null;
            string? jwks = null;
            bool hasJwks = dto.Jwks is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined };
            if ((dto.DiscoveryUri is null) == !hasJwks)
            {
                errors["discoveryUri"] = ["Give either discoveryUri or jwks, not both."];
            }
            else if (dto.DiscoveryUri is not null && !IsHttpsUri(dto.DiscoveryUri, out discoveryUri))
            {
                errors["discoveryUri"] = ["Must be an absolute https URI."];
            }
            else if (hasJwks)
            {
                jwks = dto.Jwks!.Value.GetRawText();
                string? jwksError = ValidateJwks(jwks);
                if (jwksError is not null)
                {
                    errors["jwks"] = [jwksError];
                }
            }

            if (dto.AppResolution is null)
            {
                errors["appResolution"] = ["Required."];
            }

            IReadOnlyList<string> namespaces = dto.AllowedNamespaces ?? [];
            if (namespaces.Any(n => n is null || !DnsLabel().IsMatch(n)))
            {
                errors["allowedNamespaces"] = ["Each must be a Kubernetes namespace name."];
            }
            else if (namespaces.Count == 0)
            {
                errors["allowedNamespaces"] = ["At least one namespace is required."];
            }

            if (errors.Count > 0)
            {
                return errors;
            }

            issuer = new TrustedWorkloadIssuer
            {
                Issuer = dto.Issuer!,
                DisplayName = dto.DisplayName!.Trim(),
                ServiceOwner = dto.ServiceOwner!,
                Environment = dto.Environment!,
                DiscoveryUri = discoveryUri,
                JwksJson = jwks,
                AppResolution = dto.AppResolution!.Value,
                AllowedNamespaces = namespaces.Distinct(StringComparer.Ordinal).ToArray(),
                Enabled = dto.Enabled,
            };
            return errors;
        }

        /// <summary>
        /// Validates a binding for <paramref name="issuer"/>. Returns the errors by field, and the binding to store if there are none.
        /// </summary>
        public static Dictionary<string, string[]> Validate(WorkloadBindingDto dto, TrustedWorkloadIssuer issuer, out WorkloadBinding? binding)
        {
            binding = null;
            Dictionary<string, string[]> errors = [];

            if (issuer.AppResolution != AppResolution.ExplicitBinding)
            {
                errors["issuer"] = ["The issuer does not use explicit bindings."];
            }

            if (!TryParseServiceAccountSubject(dto.Subject, out string serviceAccountNamespace, out _))
            {
                errors["subject"] = ["Must be a service account subject, system:serviceaccount:<namespace>:<name>."];
            }
            else if (!issuer.AllowedNamespaces.Contains(serviceAccountNamespace, StringComparer.Ordinal))
            {
                errors["subject"] = ["The service account's namespace is not allowed for the issuer."];
            }

            if (dto.App is null || !DnsLabel().IsMatch(dto.App))
            {
                errors["app"] = ["Must be the app's name, a lowercase DNS label."];
            }

            if (errors.Count == 0)
            {
                binding = new WorkloadBinding { Issuer = issuer.Issuer, Subject = dto.Subject!, App = dto.App! };
            }

            return errors;
        }

        private static bool IsHttpsUri(string? value, out Uri? uri)
        {
            uri = null;
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed)
                || parsed.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(parsed.Query)
                || !string.IsNullOrEmpty(parsed.Fragment)
                || !string.IsNullOrEmpty(parsed.UserInfo))
            {
                return false;
            }

            uri = parsed;
            return true;
        }

        private static string? ValidateJwks(string json)
        {
            JsonWebKeySet keySet;
            try
            {
                keySet = new JsonWebKeySet(json);
            }
            catch (Exception)
            {
                return "Must be a JSON Web Key Set.";
            }

            if (keySet.Keys.Count == 0)
            {
                return "Must have at least one key.";
            }

            // A private key here would be a leaked cluster signing key: refuse to store it.
            // JsonWebKey.HasPrivateKey needs every RSA parameter, so check each private member instead.
            if (keySet.Keys.Any(k => k.D is not null || k.P is not null || k.Q is not null
                || k.DP is not null || k.DQ is not null || k.QI is not null || k.K is not null))
            {
                return "Must have public keys only.";
            }

            return null;
        }
    }
}
