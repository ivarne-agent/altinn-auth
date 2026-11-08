using Altinn.Platform.Authentication.Core.Models.WorkloadIdentity;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Platform.Authentication.Persistance.RepositoryImplementations.WorkloadIdentity
{
    /// <summary>
    /// Stores trusted workload issuers and their bindings in the <c>workload_identity</c> schema.
    /// </summary>
    public class TrustedWorkloadIssuerRepository(NpgsqlDataSource dataSource, ILogger<TrustedWorkloadIssuerRepository> logger, TimeProvider timeProvider)
        : ITrustedWorkloadIssuerRepository
    {
        private const string IssuerColumns = /*strpsql*/ @"
            issuer, display_name, service_owner, environment, discovery_uri, jwks,
            app_resolution, allowed_namespaces, enabled, created_at, updated_at";

        private readonly NpgsqlDataSource _dataSource = dataSource;
        private readonly ILogger<TrustedWorkloadIssuerRepository> _logger = logger;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <inheritdoc/>
        public async Task<TrustedWorkloadIssuer?> GetIssuerAsync(string issuer, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(issuer);

            string query = $"SELECT {IssuerColumns} FROM workload_identity.trusted_issuer WHERE issuer = @issuer;";
            try
            {
                await using NpgsqlCommand cmd = _dataSource.CreateCommand(query);
                cmd.Parameters.Add(new NpgsqlParameter<string>("issuer", issuer));
                await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);
                return await reader.ReadAsync(cancellationToken) ? await ReadIssuer(reader, cancellationToken) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication // TrustedWorkloadIssuerRepository // GetIssuerAsync // Exception");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<TrustedWorkloadIssuer>> ListIssuersAsync(CancellationToken cancellationToken = default)
        {
            string query = $"SELECT {IssuerColumns} FROM workload_identity.trusted_issuer ORDER BY service_owner, environment, issuer;";
            try
            {
                await using NpgsqlCommand cmd = _dataSource.CreateCommand(query);
                await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);
                List<TrustedWorkloadIssuer> issuers = [];
                while (await reader.ReadAsync(cancellationToken))
                {
                    issuers.Add(await ReadIssuer(reader, cancellationToken));
                }

                return issuers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication // TrustedWorkloadIssuerRepository // ListIssuersAsync // Exception");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<TrustedWorkloadIssuer> UpsertIssuerAsync(TrustedWorkloadIssuer issuer, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(issuer);

            string query = /*strpsql*/ $@"
                INSERT INTO workload_identity.trusted_issuer (
                    issuer, display_name, service_owner, environment, discovery_uri, jwks,
                    app_resolution, allowed_namespaces, enabled, created_at)
                VALUES (
                    @issuer, @display_name, @service_owner, @environment, @discovery_uri, @jwks,
                    @app_resolution, @allowed_namespaces, @enabled, @now)
                ON CONFLICT (issuer) DO UPDATE SET
                    display_name = EXCLUDED.display_name,
                    service_owner = EXCLUDED.service_owner,
                    environment = EXCLUDED.environment,
                    discovery_uri = EXCLUDED.discovery_uri,
                    jwks = EXCLUDED.jwks,
                    app_resolution = EXCLUDED.app_resolution,
                    allowed_namespaces = EXCLUDED.allowed_namespaces,
                    enabled = EXCLUDED.enabled,
                    updated_at = @now
                RETURNING {IssuerColumns};";

            try
            {
                await using NpgsqlCommand cmd = _dataSource.CreateCommand(query);
                cmd.Parameters.AddWithValue("issuer", issuer.Issuer);
                cmd.Parameters.AddWithValue("display_name", issuer.DisplayName);
                cmd.Parameters.AddWithValue("service_owner", issuer.ServiceOwner);
                cmd.Parameters.AddWithValue("environment", issuer.Environment);
                cmd.Parameters.AddWithValue("discovery_uri", (object?)issuer.DiscoveryUri?.ToString() ?? DBNull.Value);
                cmd.Parameters.Add(new NpgsqlParameter("jwks", NpgsqlDbType.Jsonb) { Value = (object?)issuer.JwksJson ?? DBNull.Value });
                cmd.Parameters.AddWithValue("app_resolution", issuer.AppResolution.ToString());
                cmd.Parameters.Add(new NpgsqlParameter<string[]>("allowed_namespaces", [.. issuer.AllowedNamespaces])
                {
                    NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text
                });
                cmd.Parameters.AddWithValue("enabled", issuer.Enabled);
                cmd.Parameters.AddWithValue("now", _timeProvider.GetUtcNow());

                await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);
                await reader.ReadAsync(cancellationToken);
                return await ReadIssuer(reader, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication // TrustedWorkloadIssuerRepository // UpsertIssuerAsync // Exception");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SetIssuerEnabledAsync(string issuer, bool enabled, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(issuer);

            const string QUERY = /*strpsql*/ @"
                UPDATE workload_identity.trusted_issuer
                SET enabled = @enabled, updated_at = @now
                WHERE issuer = @issuer;";
            try
            {
                await using NpgsqlCommand cmd = _dataSource.CreateCommand(QUERY);
                cmd.Parameters.AddWithValue("issuer", issuer);
                cmd.Parameters.AddWithValue("enabled", enabled);
                cmd.Parameters.AddWithValue("now", _timeProvider.GetUtcNow());
                return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication // TrustedWorkloadIssuerRepository // SetIssuerEnabledAsync // Exception");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<WorkloadBinding?> GetBindingAsync(string issuer, string subject, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
            ArgumentException.ThrowIfNullOrWhiteSpace(subject);

            const string QUERY = /*strpsql*/ @"
                SELECT issuer, subject, app
                FROM workload_identity.workload_binding
                WHERE issuer = @issuer AND subject = @subject;";
            try
            {
                await using NpgsqlCommand cmd = _dataSource.CreateCommand(QUERY);
                cmd.Parameters.AddWithValue("issuer", issuer);
                cmd.Parameters.AddWithValue("subject", subject);
                await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return null;
                }

                return new WorkloadBinding
                {
                    Issuer = reader.GetString(0),
                    Subject = reader.GetString(1),
                    App = reader.GetString(2),
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication // TrustedWorkloadIssuerRepository // GetBindingAsync // Exception");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task UpsertBindingAsync(WorkloadBinding binding, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(binding);

            const string QUERY = /*strpsql*/ @"
                INSERT INTO workload_identity.workload_binding (issuer, subject, app)
                VALUES (@issuer, @subject, @app)
                ON CONFLICT (issuer, subject) DO UPDATE SET app = EXCLUDED.app;";
            try
            {
                await using NpgsqlCommand cmd = _dataSource.CreateCommand(QUERY);
                cmd.Parameters.AddWithValue("issuer", binding.Issuer);
                cmd.Parameters.AddWithValue("subject", binding.Subject);
                cmd.Parameters.AddWithValue("app", binding.App);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication // TrustedWorkloadIssuerRepository // UpsertBindingAsync // Exception");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteBindingAsync(string issuer, string subject, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
            ArgumentException.ThrowIfNullOrWhiteSpace(subject);

            const string QUERY = /*strpsql*/ @"
                DELETE FROM workload_identity.workload_binding
                WHERE issuer = @issuer AND subject = @subject;";
            try
            {
                await using NpgsqlCommand cmd = _dataSource.CreateCommand(QUERY);
                cmd.Parameters.AddWithValue("issuer", issuer);
                cmd.Parameters.AddWithValue("subject", subject);
                return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication // TrustedWorkloadIssuerRepository // DeleteBindingAsync // Exception");
                throw;
            }
        }

        private static async Task<TrustedWorkloadIssuer> ReadIssuer(NpgsqlDataReader reader, CancellationToken cancellationToken)
        {
            string? discoveryUri = await reader.IsDBNullAsync(4, cancellationToken) ? null : reader.GetString(4);
            string? jwks = await reader.IsDBNullAsync(5, cancellationToken) ? null : reader.GetString(5);

            return new TrustedWorkloadIssuer
            {
                Issuer = reader.GetString(0),
                DisplayName = reader.GetString(1),
                ServiceOwner = reader.GetString(2),
                Environment = reader.GetString(3),
                DiscoveryUri = discoveryUri is null ? null : new Uri(discoveryUri),
                JwksJson = jwks,
                AppResolution = Enum.Parse<AppResolution>(reader.GetString(6)),
                AllowedNamespaces = await reader.GetFieldValueAsync<string[]>(7, cancellationToken),
                Enabled = reader.GetBoolean(8),
                CreatedAt = await reader.GetFieldValueAsync<DateTimeOffset>(9, cancellationToken),
                UpdatedAt = await reader.IsDBNullAsync(10, cancellationToken) ? null : await reader.GetFieldValueAsync<DateTimeOffset>(10, cancellationToken),
            };
        }
    }
}
