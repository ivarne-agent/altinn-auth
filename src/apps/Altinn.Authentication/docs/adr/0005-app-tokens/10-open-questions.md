# 10. Open questions

**TL;DR**

- Ten questions are deliberately open, and none of them needs a redesign.
- The biggest: who registers clusters, where the exchange happens, and claim compatibility for consumers.
- Each lists the options, the current leaning and what would settle it.
- The last question is ownership, which is what this document is meant to start a discussion about.

> **Drafted with an AI assistant.** Claude Opus 5.5 (`claude-opus-5-5`) in Claude Code wrote this page from the code
> in this repository and the prototype branches, as research to propose a design. Reader: unspecified until a person
> confirms they have read it.

These are deliberately not decided by this proposal. Each lists the options, the current leaning and what would
settle it. The prototype is built so that none of them needs a redesign: each is a setting, a mode or a small
addition.

## Q1. Who owns registrations?

- **Options:** Altinn operations by hand; the runtime cluster provisioning pipeline; Altinn Studio for bindings.
  See [03](03-trust-registry.md#who-registers-clusters).
- **Leaning:** the provisioning pipeline for clusters, since it knows when a cluster's issuer exists and when it goes
  away. Nobody writes bindings unless service account names can't identify apps.
- **Settled by:** who provisions runtime clusters, and whether that pipeline can hold the admin scope.

## Q2. Where does the exchange happen?

- **Options:** in each app, through the app library; in the platform proxy; both during a transition. See
  [02](02-solution-overview.md#where-the-exchange-happens).
- **Leaning:** the platform proxy first, because it is one component and gives parity with today
  (`ServiceOwnerOnly`). Then per app, or a delegation extension, when per-app identity is needed.
- **Settled by:** the direction of [altinn-studio#21747](https://github.com/Altinn/altinn-studio/issues/21747), and
  when per-app service accounts ([altinn-studio-charts#83](https://github.com/Altinn/altinn-studio-charts/issues/83))
  arrive.

## Q3. Claim compatibility with today's consumers

Consumers of today's platform access tokens identify the caller by `iss` (the service owner) and an app claim. App
tokens deliberately use their own issuer and `urn:altinn:apptoken:*` claims
([05](05-app-token.md#why-it-has-its-own-key-issuer-and-claims)).

- **Options:**
  - Keep the namespaced claims, and let consumers map them.
  - Also add the claim names consumers read today, accepting that a consumer validating loosely might act on them.
  - Have the shared validation library expose both kinds of token as one "caller identity" to consumers.
- **Leaning:** the namespaced claims, plus the third option, so each consumer changes once, in one place.
- **Settled by:** the owners of the shared access token validation and of the main consumers.

## Q4. RFC 7523 or RFC 8693?

- **Options:** the RFC 7523 `jwt-bearer` grant (as prototyped); RFC 8693 token exchange; both.
- **Leaning:** RFC 7523 now. RFC 8693 only if the platform proxy must act *for* an app, sending the app's identity as
  an `actor_token` or `subject_token` next to its own.
- **Settled by:** Q2 and whether delegation is wanted.

## Q5. Discovery or pinned keys by default?

- **Options:** `discovery_uri`, which follows rotation automatically but depends at runtime on the cluster's
  discovery host (Azure-hosted on AKS); or a pinned `jwks`, which works offline but must be updated before each
  rotation. See [03](03-trust-registry.md#where-the-keys-come-from).
- **Leaning:** pinned keys, given the wish to limit runtime dependencies on Azure, *if* registration is automated
  (Q1) so rotations can't be forgotten. Discovery otherwise.
- **Settled by:** Q1 and how often cluster keys rotate.

## Q6. How a service account name identifies an app

`ServiceAccountName` assumes the service account is named after the app.

- **Options:** service account name = app name (a DNS label of at most 63 characters, unique per service owner);
  a prefix or suffix convention; explicit bindings.
- **Leaning:** name = app name, with explicit bindings for exceptions.
- **Settled by:** whether every app name is a valid, unique DNS label, which Studio can check, and how
  [altinn-studio-charts#83](https://github.com/Altinn/altinn-studio-charts/issues/83) names service accounts.

## Q7. One audience or one per API?

- **Options:** one `aud` for every platform API (as prototyped); per-API audiences requested with RFC 8707
  `resource`, so a token stolen from one API can't be used at another.
- **Leaning:** one audience now. Per-API audiences are worth it once tokens are per app and consumers check
  audiences.
- **Settled by:** consumer readiness (phase 3 in [08](08-rollout.md)).

## Q8. Lifetimes

- **Options:** app token 10 min (prototype) versus 30 min; maximum assertion lifetime 60 min (prototype) versus
  10 min.
- **Leaning:** 10 and 10, once clusters set `expirationSeconds: 600`.
- **Settled by:** Authentication's availability record ([07](07-operations.md#availability)), and the runtime
  team's projected volume settings.

## Q9. Rate limiting

- **Options:** ASP.NET Core rate limiting per trusted issuer on the grant; rely on ingress and APIM limits.
- **Leaning:** per issuer, in Authentication, so one misbehaving cluster can't starve the others.
- **Settled by:** measured load ([07](07-operations.md#load)).

## Q10. Who builds and owns it?

The registry, grant and issuer live in this repository, and so with the authentication team. The motivation comes
from Altinn Studio's runtime.

- **Options:** the authentication team builds it; Studio contributes it and the authentication team reviews and owns
  it; a joint effort.
- **Settled by:** the discussion this ADR is meant to start, in a new issue that picks up
  [altinn-authentication#824](https://github.com/Altinn/altinn-authentication/issues/824).
