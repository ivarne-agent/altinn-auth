using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Platform.Authentication.Configuration;
using Altinn.Platform.Authentication.Core.Constants;
using Altinn.Platform.Authentication.Core.Models.WorkloadIdentity;
using Altinn.Platform.Authentication.Core.RepositoryInterfaces;
using Altinn.Platform.Authentication.Helpers;
using Altinn.Platform.Authentication.Model.WorkloadIdentity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace Altinn.Platform.Authentication.Controllers
{
    /// <summary>
    /// Administers the Kubernetes clusters trusted to exchange their service account tokens for app tokens,
    /// see ADR-0005.
    /// </summary>
    [Route("authentication/api/v1/workloadidentity")]
    [ApiController]
    [FeatureGate(FeatureFlags.AppTokens)]
    [Authorize(Policy = AuthzConstants.POLICY_SCOPE_WORKLOADIDENTITY_ADMIN)]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class WorkloadIdentityAdminController(ITrustedWorkloadIssuerRepository repository) : ControllerBase
    {
        private readonly ITrustedWorkloadIssuerRepository _repository = repository;

        /// <summary>
        /// Lists the trusted issuers.
        /// </summary>
        [HttpGet("issuers")]
        public async Task<ActionResult<IEnumerable<TrustedWorkloadIssuerDto>>> ListIssuers(CancellationToken cancellationToken)
        {
            IReadOnlyList<TrustedWorkloadIssuer> issuers = await _repository.ListIssuersAsync(cancellationToken);
            return Ok(issuers.Select(TrustedWorkloadIssuerDto.From));
        }

        /// <summary>
        /// Returns one trusted issuer. The issuer is a URI, so it is passed as a query parameter.
        /// </summary>
        [HttpGet("issuers/lookup")]
        public async Task<ActionResult<TrustedWorkloadIssuerDto>> GetIssuer([FromQuery] string issuer, CancellationToken cancellationToken)
        {
            TrustedWorkloadIssuer? stored = await _repository.GetIssuerAsync(issuer, cancellationToken);
            return stored is null ? NotFound() : Ok(TrustedWorkloadIssuerDto.From(stored));
        }

        /// <summary>
        /// Registers an issuer, or replaces its registration.
        /// </summary>
        [HttpPut("issuers")]
        public async Task<ActionResult<TrustedWorkloadIssuerDto>> PutIssuer([FromBody] TrustedWorkloadIssuerDto dto, CancellationToken cancellationToken)
        {
            Dictionary<string, string[]> errors = WorkloadIdentityValidation.Validate(dto, out TrustedWorkloadIssuer? issuer);
            if (errors.Count > 0)
            {
                return ValidationProblem(new ValidationProblemDetails(errors));
            }

            TrustedWorkloadIssuer stored = await _repository.UpsertIssuerAsync(issuer!, cancellationToken);
            return Ok(TrustedWorkloadIssuerDto.From(stored));
        }

        /// <summary>
        /// Enables or disables an issuer. Disabling stops new app tokens for the whole cluster at once;
        /// tokens already issued stay valid until they expire.
        /// </summary>
        [HttpPut("issuers/enabled")]
        public async Task<ActionResult> SetIssuerEnabled([FromQuery] string issuer, [FromQuery] bool enabled, CancellationToken cancellationToken)
        {
            return await _repository.SetIssuerEnabledAsync(issuer, enabled, cancellationToken) ? NoContent() : NotFound();
        }

        /// <summary>
        /// Maps a service account to an app, for an issuer that uses explicit bindings.
        /// </summary>
        [HttpPut("bindings")]
        public async Task<ActionResult> PutBinding([FromBody] WorkloadBindingDto dto, CancellationToken cancellationToken)
        {
            TrustedWorkloadIssuer? issuer = string.IsNullOrWhiteSpace(dto.Issuer)
                ? null
                : await _repository.GetIssuerAsync(dto.Issuer, cancellationToken);
            if (issuer is null)
            {
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["issuer"] = ["Must be a registered issuer."],
                }));
            }

            Dictionary<string, string[]> errors = WorkloadIdentityValidation.Validate(dto, issuer, out WorkloadBinding? binding);
            if (errors.Count > 0)
            {
                return ValidationProblem(new ValidationProblemDetails(errors));
            }

            await _repository.UpsertBindingAsync(binding!, cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Removes a binding.
        /// </summary>
        [HttpDelete("bindings")]
        public async Task<ActionResult> DeleteBinding([FromQuery] string issuer, [FromQuery] string subject, CancellationToken cancellationToken)
        {
            return await _repository.DeleteBindingAsync(issuer, subject, cancellationToken) ? NoContent() : NotFound();
        }
    }
}
