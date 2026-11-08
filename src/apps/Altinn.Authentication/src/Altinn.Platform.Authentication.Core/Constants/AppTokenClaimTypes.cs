namespace Altinn.Platform.Authentication.Core.Constants
{
    /// <summary>
    /// The claims of an app token, see ADR-0005. They have their own namespace on purpose: no handler that
    /// authorizes Altinn user, organisation or system user tokens reads them, so an app token presented in
    /// the wrong place grants nothing.
    /// </summary>
    public static class AppTokenClaimTypes
    {
        /// <summary>The service owner the app belongs to, such as <c>ttd</c>.</summary>
        public const string ServiceOwner = "urn:altinn:apptoken:serviceowner";

        /// <summary>The app, as <c>{serviceowner}/{app}</c>. Absent when the cluster maps apps to the service owner only.</summary>
        public const string App = "urn:altinn:apptoken:app";

        /// <summary>The <c>iss</c> of the service account token the app token was issued for: the cluster.</summary>
        public const string WorkloadIssuer = "urn:altinn:apptoken:workload_iss";

        /// <summary>The <c>sub</c> of the service account token the app token was issued for: the service account.</summary>
        public const string WorkloadSubject = "urn:altinn:apptoken:workload_sub";

        /// <summary>The JOSE <c>typ</c> header of an app token (RFC 8725 explicit typing).</summary>
        public const string TokenType = "altinn-app+jwt";
    }
}
