# App tokens from workload identity: design

**TL;DR**

- The detailed design behind ADR-0005, one topic per page.
- Status Proposed: research for a new issue that picks up altinn-authentication#824.
- Read pages 1 to 11 in order. The glossary below defines the terms.
- Every diagram has a text description, so the pages can be rendered to HTML.

> **Drafted with an AI assistant.** Claude Opus 5.5 (`claude-opus-5-5`) in Claude Code wrote this page from the code
> in this repository and the prototype branches, as research to propose a design. Reader: unspecified until a person
> confirms they have read it.

This folder is the detailed design behind [ADR-0005](../0005-app-tokens-from-workload-identity.md). The ADR states the
decision on one page; these pages explain how it works, why, and what is still open. Each page covers one topic, so
it can be read, reviewed and rendered on its own.

> **Status: Proposed.** This is research for a new issue that picks up [altinn-authentication#824](https://github.com/Altinn/altinn-authentication/issues/824)
> (closed, in the archived repository), not an accepted design. Where a question isn't settled, the pages present the alternatives and say which way the
> design currently leans. They don't pretend a decision has been made.

## Reading order

| # | Page | What it answers |
| --- | --- | --- |
| 1 | [Background and goals](01-background.md) | What platform access tokens are, what is wrong with distributing their signing key, and what this design must and must not do |
| 2 | [Solution overview](02-solution-overview.md) | The parts, how a token flows through them, and where the exchange can happen |
| 3 | [Trust registry](03-trust-registry.md) | How clusters are registered, how a service account becomes an app, and the admin API |
| 4 | [Token exchange](04-token-exchange.md) | The RFC 7523 grant on the token endpoint, step by step, with examples |
| 5 | [The app token](05-app-token.md) | Its format and claims, why it has its own key and issuer, and how a consumer validates it |
| 6 | [Security](06-security.md) | Threat model, mitigations and residual risk, compared with today |
| 7 | [Operations](07-operations.md) | Availability, load, configuration, key rotation, observability, runbook |
| 8 | [Rollout](08-rollout.md) | Phases from prototype to removing the shared key, with rollback at each |
| 9 | [Alternatives](09-alternatives.md) | What else was considered and why it isn't the proposal |
| 10 | [Open questions](10-open-questions.md) | What is undecided, the options, and the current leaning |
| 11 | [Prototype](11-prototype.md) | The pushed branches that illustrate the design in code, and what they leave out |

## Glossary

| Term | Meaning here |
| --- | --- |
| **Platform access token** | The JWT in the `PlatformAccessToken` header, today signed by each app with its service owner's private key. |
| **Service account token** | A JWT that a Kubernetes cluster issues to a pod for its service account. Here it is always a *projected* token: short-lived, with an audience, and bound to the pod. |
| **Trusted cluster / trusted issuer** | A Kubernetes cluster registered in Authentication, identified by the exact `iss` of its service account tokens. |
| **App token** | The JWT Authentication issues in exchange for a service account token. It names the service owner, and the app when the cluster can tell. |
| **App resolution** | The rule that maps a service account to an app: service owner only, the service account's name, or an explicit binding. |
| **Altinn token** | The JWTs Authentication already issues to users, organisations and systems, signed with the key published at `openid/.well-known/openid-configuration/jwks`. |

## Conventions for these pages

- Diagrams are Mermaid, as everywhere in [docs/](../../README.md). Each diagram is followed by a **Diagram
  description**: a text version that says what the diagram shows, so the pages work when rendered to HTML, read
  without Mermaid, or read by a screen reader.
- Code links point to the prototype branches on a fork, pinned to commits. See [11-prototype.md](11-prototype.md).
