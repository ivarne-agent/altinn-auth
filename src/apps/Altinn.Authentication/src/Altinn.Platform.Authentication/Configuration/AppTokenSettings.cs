namespace Altinn.Platform.Authentication.Configuration
{
    /// <summary>
    /// Settings for app tokens, see ADR-0005.
    /// </summary>
    public class AppTokenSettings
    {
        /// <summary>
        /// The <c>iss</c> of app tokens. It must differ from the issuer of Altinn tokens, such as
        /// <c>https://platform.altinn.no/authentication/api/v1/apptoken</c>.
        /// </summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>The <c>aud</c> of app tokens: the platform APIs that accept them.</summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// The <c>aud</c> a service account token must have to be exchanged, normally the token endpoint's URL.
        /// </summary>
        public string AssertionAudience { get; set; } = string.Empty;

        /// <summary>How long an app token is valid.</summary>
        public int LifetimeMinutes { get; set; } = 10;

        /// <summary>
        /// The longest lifetime a service account token may have to be exchanged. Kubernetes' minimum is
        /// 10 minutes; a long-lived token is a credential that leaks for longer.
        /// </summary>
        public int MaxAssertionLifetimeMinutes { get; set; } = 60;

        /// <summary>The name of the Key Vault certificate that signs app tokens. Not the Altinn token certificate.</summary>
        public string CertificateName { get; set; } = string.Empty;

        /// <summary>A PKCS #12 file to sign with when no Key Vault is configured, for local development.</summary>
        public string CertificatePath { get; set; } = string.Empty;

        /// <summary>The password of <see cref="CertificatePath"/>.</summary>
        public string CertificatePwd { get; set; } = string.Empty;

        /// <summary>How long a new certificate is published in the JWKS before it signs, as for Altinn tokens.</summary>
        public int RolloverDelayHours { get; set; } = 48;
    }
}
