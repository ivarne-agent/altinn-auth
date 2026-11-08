namespace Altinn.Platform.Authentication.Configuration
{
    /// <summary>
    /// Feature flags 
    /// </summary>
    public static class FeatureFlags
    {
        /// <summary>
        /// audit log flag
        /// </summary>
        public const string AuditLog = "AuditLog";

        /// <summary>
        /// Feature flag for SystemUser Controller and functionality
        /// </summary>
        public const string SystemUser = "SystemUser";

        /// <summary>
        /// Feature flag for supporting ID token exchange for self-registered email users.
        /// </summary>
        public const string SupportIDTokenExchangeForSelfRegisteredEmailUsers = "SupportIDTokenExchangeForSelfRegisteredEmailUsers";

        /// <summary>
        /// Feature flag for app tokens issued in exchange for a trusted cluster's service account token,
        /// and the administration of trusted clusters. See ADR-0005.
        /// </summary>
        public const string AppTokens = "AppTokens";
    }
}
