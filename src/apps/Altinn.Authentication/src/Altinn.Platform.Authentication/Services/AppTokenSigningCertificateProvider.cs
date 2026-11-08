using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Model;
using Altinn.Platform.Authentication.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Altinn.Platform.Authentication.Services
{
    /// <summary>
    /// Reads the app token signing certificate's versions from Key Vault, or a file when no Key Vault is
    /// configured, and keeps them for an hour. Intended as a singleton.
    /// </summary>
    public class AppTokenSigningCertificateProvider(
        IOptions<KeyVaultSettings> keyVaultSettings,
        IOptions<AppTokenSettings> appTokenSettings,
        TimeProvider timeProvider) : IAppTokenSigningCertificateProvider
    {
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(1);

        private readonly KeyVaultSettings _keyVaultSettings = keyVaultSettings.Value;
        private readonly AppTokenSettings _settings = appTokenSettings.Value;
        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        private List<X509Certificate2>? _certificates;
        private DateTimeOffset _refreshAt;

        /// <inheritdoc />
        public async Task<List<X509Certificate2>> GetCertificates()
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_certificates is not null && _timeProvider.GetUtcNow() < _refreshAt)
                {
                    return _certificates;
                }

                List<X509Certificate2> certificates;
                if (string.IsNullOrEmpty(_keyVaultSettings.ClientId) || string.IsNullOrEmpty(_keyVaultSettings.ClientSecret))
                {
                    if (string.IsNullOrEmpty(_settings.CertificatePath))
                    {
                        throw new InvalidOperationException("AppTokenSettings:CertificatePath is required when no Key Vault is configured.");
                    }

                    certificates = [X509CertificateLoader.LoadPkcs12FromFile(_settings.CertificatePath, _settings.CertificatePwd)];
                }
                else
                {
                    if (string.IsNullOrEmpty(_settings.CertificateName))
                    {
                        throw new InvalidOperationException("AppTokenSettings:CertificateName is required.");
                    }

                    certificates = await KeyVaultCertificateVersions.GetAll(_keyVaultSettings, _settings.CertificateName);
                }

                _certificates = [.. certificates.OrderByDescending(c => c.NotBefore)];
                _refreshAt = _timeProvider.GetUtcNow().Add(CacheLifetime);
                return _certificates;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
