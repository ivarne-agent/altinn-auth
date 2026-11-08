# ADR-0005: Issue app tokens in exchange for Kubernetes service account tokens

- **Status:** Proposed
- **Date:** 2026-11-08
- **Deciders:** To be decided. This is research and a prototype for a new issue that picks up [altinn-authentication#824](https://github.com/Altinn/altinn-authentication/issues/824), which is closed
  in a repository that is now archived.

**TL;DR**

- Apps sign platform access tokens with a key every app pod shares, so a leak from any app is a leak for every app, with no revocation.
- Proposal: Authentication exchanges a registered cluster's Kubernetes service account token for a short-lived app token, with the RFC 7523 `jwt-bearer` grant.
- App tokens have their own key, issuer, `typ` and claims, so they are never accepted as Altinn tokens.
- No private key in the clusters, per-app identity once apps have their own service accounts, and one switch per cluster.
- Proposed, not decided: the open questions and a tested prototype are linked below.

> **Drafted with an AI assistant.** Claude Opus 5.5 (`claude-opus-5-5`) in Claude Code wrote this page from the code
> in this repository and the prototype branches, as research to propose a design. Reader: unspecified until a person
> confirms they have read it.

## Context

Altinn apps prove to the platform's APIs which service owner, and which app, a request comes from with a
`PlatformAccessToken`: a JWT each app signs itself with its service owner's private key. Every app pod in a runtime
cluster holds that key, in the cluster's `accesstoken` Secret. A proxy for apps' platform calls
([altinn-studio#21767](https://github.com/Altinn/altinn-studio/pull/21767)) would hold it too. A leaked key lets anyone
sign as any app of that service owner until the certificate is rotated. Authentication never sees these tokens being
issued, and cannot revoke them.

Kubernetes already gives every pod a short-lived, audience-bound, signed identity: the projected service account
token. Every cluster can publish the public keys for it through OIDC discovery.
[altinn-authentication#824](https://github.com/Altinn/altinn-authentication/issues/824) asked for "an app token based on an access token"
and was closed in June 2026 because nobody remembered why it was needed. This ADR states that need.

The full background is in [0005-app-tokens/01-background.md](0005-app-tokens/01-background.md).

## Decision

We propose that Authentication issues **app tokens** in exchange for service account tokens from **trusted
Kubernetes clusters**:

1. **A trust registry.** Each runtime cluster is registered once with:
   - the exact `iss` of its service account tokens;
   - its service owner;
   - its public keys, as a discovery URI or a pinned JWKS;
   - how a service account maps to an app.

   See [03-trust-registry.md](0005-app-tokens/03-trust-registry.md).
2. **A new grant on the existing token endpoint.** The token endpoint gets the RFC 7523 `jwt-bearer` grant, behind
   the `AppTokens` feature flag. The service account token is the assertion. It is validated offline and strictly:
   - exact issuer and audience;
   - RS256 or ES256 only;
   - a bounded lifetime;
   - an allowed namespace.

   See [04-token-exchange.md](0005-app-tokens/04-token-exchange.md).
3. **App tokens kept apart from Altinn tokens.** App tokens get their own signing key, issuer, audience, JOSE `typ`
   and claim namespace, and their own JWKS. A service that trusts Altinn tokens therefore never accepts an app
   token in their place. See [05-app-token.md](0005-app-tokens/05-app-token.md).

No private key is distributed to the clusters. Which app a token names depends on the cluster's setup:
- apps that share a service account get a service-owner-only token, the same as today;
- apps with their own service accounts get per-app tokens.

## Consequences

- **Easier:**
  - per-app identity, for the follow-up in altinn-studio#21767;
  - revocation per cluster, by disabling its registration;
  - a central audit point;
  - removing the shared signing key from app pods, eventually.
- **Harder:**
  - Authentication becomes an online dependency of apps' platform calls, softened by app tokens that live
    10 minutes;
  - each cluster must be registered and its keys kept current;
  - consumers must trust a second issuer;
  - a new certificate must be provisioned.
- **Riskier:**
  - a compromised cluster control plane can mint service account tokens for any of its apps. That is the same blast
    radius as the shared key today, and now it can be switched off.
  - The trust registry becomes security-critical configuration.

  See [06-security.md](0005-app-tokens/06-security.md).
- **Undecided:** where the exchange happens, claim compatibility with today's `PlatformAccessToken` consumers,
  registration ownership, and more. See [10-open-questions.md](0005-app-tokens/10-open-questions.md).

## References

- Design, by topic: [0005-app-tokens/README.md](0005-app-tokens/README.md)
- Prototype branches: [0005-app-tokens/11-prototype.md](0005-app-tokens/11-prototype.md)
- [RFC 7523](https://www.rfc-editor.org/rfc/rfc7523) (JWT bearer grant), [RFC 8725](https://www.rfc-editor.org/rfc/rfc8725) (JWT best practices), [RFC 8414](https://www.rfc-editor.org/rfc/rfc8414) (authorization server metadata)
- [Kubernetes: service account token volume projection and issuer discovery](https://kubernetes.io/docs/tasks/configure-pod-container/configure-service-account/)
- Prior issues: [altinn-authentication#824](https://github.com/Altinn/altinn-authentication/issues/824), [altinn-authentication#1762](https://github.com/Altinn/altinn-authentication/issues/1762)
- Related: [altinn-studio#21747](https://github.com/Altinn/altinn-studio/issues/21747), [altinn-studio#21767](https://github.com/Altinn/altinn-studio/pull/21767), [altinn-studio-charts#83](https://github.com/Altinn/altinn-studio-charts/issues/83)
