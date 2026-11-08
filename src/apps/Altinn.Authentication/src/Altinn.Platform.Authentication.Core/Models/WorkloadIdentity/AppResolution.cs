namespace Altinn.Platform.Authentication.Core.Models.WorkloadIdentity
{
    /// <summary>
    /// How a trusted cluster's service account token is mapped to the app the token is issued for.
    /// </summary>
    public enum AppResolution
    {
        /// <summary>
        /// The app token names the service owner only, as a platform access token without an app does
        /// today. For clusters where apps share a service account.
        /// </summary>
        ServiceOwnerOnly,

        /// <summary>
        /// The service account's name is the app's name, in one of the issuer's allowed namespaces.
        /// </summary>
        ServiceAccountName,

        /// <summary>
        /// Each service account is mapped to an app by a <see cref="WorkloadBinding"/>.
        /// </summary>
        ExplicitBinding,
    }
}
