# 9. Alternatives

**TL;DR**

- Eight alternatives compared: per-app keys, a proxy client, Maskinporten, Entra, SPIFFE/SPIRE, TokenReview, and hardening the key.
- Entra Workload Identity with Key Vault signing is the smallest change, but deepens the dependency on Azure.
- Hardening the shared key is a reasonable interim step, and stays compatible with the proposal.
- SPIRE could later plug into the same grant as one more kind of trusted issuer.

> **Drafted with an AI assistant.** Claude Opus 5.5 (`claude-opus-5-5`) in Claude Code wrote this page from the code
> in this repository and the prototype branches, as research to propose a design. Reader: unspecified until a person
> confirms they have read it.

The problem from [01-background.md](01-background.md) can be attacked in several ways. This page records the ones
considered, so the choice can be revisited with the same facts.

| | Alternative | No key in clusters | Per-app identity | Vendor-neutral | Change in Authentication | Change for consumers |
| --- | --- | --- | --- | --- | --- | --- |
| **A** | **Service account token → app token (this proposal)** | ✅ | ✅ with per-app service accounts | ✅ | Medium | Trust a second issuer |
| B | Per-app client with `private_key_jwt` | ❌ one key per app | ✅ | ✅ | Small (finish a TODO) | Trust a second issuer |
| C | The platform proxy as one trusted client | ❌ or ✅ (via A) | ❌ unless it delegates | ✅ | Small | Trust a second issuer |
| D | Maskinporten | ❌ the client signs its grant | ❌ | ✅ (Norwegian) | Small (exchange exists) | Different token |
| E | Entra Workload Identity, Key Vault signing | ✅ | ✅ | ❌ Azure | None or small | None (same token) |
| F | SPIFFE/SPIRE | ✅ | ✅ | ✅ | Medium | Trust SPIRE bundles, or A on top |
| G | A, but online validation with TokenReview | ✅ | ✅ | ✅ | Medium, plus credentials per cluster | As A |
| H | Keep the shared key, harden its handling | ❌ | ❌ | ✅ | None | None |

## B. A per-app client with `private_key_jwt`

Register each app (or cluster) as an OAuth client with a public key, and let it get tokens with client credentials,
authenticating with a signed JWT (RFC 7523 §2.2). Authentication already has most of this:
- `OidcClient` has `JwksUri`, `JwksJson` and `TokenEndpointAuthMethod.PrivateKeyJwt`;
- `TokenService` has a `case TokenClientAuthType.PrivateKeyJwt` that is a TODO and rejects every request.

**Not proposed because** it multiplies the problem: one private key per app to generate, distribute, store in the
cluster and rotate, instead of one per service owner. The client model is also built for browser sign-in: it
requires a redirect URI.

## C. The platform proxy as one trusted client

Register the platform proxy of each cluster as one client and give it service-owner tokens. With a secret or a key,
this is B with one client per cluster. With its service account token, it is A in `ServiceOwnerOnly` mode, which A
already supports ([02](02-solution-overview.md#where-the-exchange-happens)). Letting the proxy state *which app* is
calling, from Linkerd identities, would be a delegation extension on top of A ([Q4](10-open-questions.md#q4-rfc-7523-or-rfc-8693)).

## D. Maskinporten

Maskinporten authenticates **organisations** to APIs, and Authentication already exchanges Maskinporten tokens for
Altinn tokens. But:
- its grant must be signed by the client with a registered key or business certificate, so a key is still needed in
  the cluster;
- it has no claim for "which app";
- it doesn't accept custom claims (an unknown claim in the grant is rejected with `400 invalid_assertion`).

Its fine-grained types (`urn:altinn:systemuser`, `urn:altinn:consent`) are defined by Digdir and Altinn for other
purposes. It is the right tool for organisations outside Altinn, not for Altinn's own runtime.

## E. Entra Workload Identity and Key Vault signing

On AKS, pods can get Entra tokens through federated credentials, and could ask Key Vault to sign today's access
tokens with a non-exportable key. That removes the key from the cluster *without any change* for consumers.

**Not proposed because** it deepens the dependency on Azure for a core runtime function, against the stated wish to
depend on Azure no more than necessary. It also doesn't give Authentication a view of, or a switch for, app tokens.
It remains the smallest change if that wish is relaxed.

## F. SPIFFE/SPIRE

SPIRE gives every workload an attested identity (`spiffe://…`) as an X.509 or JWT document, and can federate trust
across clusters. It is the most complete workload identity system, and vendor-neutral.

**Not proposed now because** it is a new, critical system to run in every cluster, while Kubernetes' built-in
service account tokens already give what this design needs. If SPIRE is adopted later, a SPIRE JWT-SVID can be the
assertion in the same grant: it's one more kind of trusted issuer, with no redesign.

## G. Online validation with TokenReview

Instead of verifying signatures offline, Authentication could send each service account token to its cluster's
TokenReview API. That is the only check that knows whether the bound pod still exists.

**Not proposed because** Authentication would need network access and credentials for every runtime cluster's API
server, and every exchange would depend on that cluster's control plane. The benefit, faster revocation of a stolen
service account token, is small when those tokens live 10 minutes and are good only for this exchange.

## H. Keep the shared key and harden it

Keep platform access tokens as they are, and narrow who can read the `accesstoken` Secret: separate Secrets per
namespace, a signing sidecar, or a signer in the platform proxy so app containers never see the key.

**Not proposed as the end state because** it doesn't give per-app identity, revocation or audit, and the key still
sits in every cluster. It is a reasonable **interim** measure. [altinn-studio#21767](https://github.com/Altinn/altinn-studio/pull/21767)
is a step in that direction, and it stays compatible with A.
