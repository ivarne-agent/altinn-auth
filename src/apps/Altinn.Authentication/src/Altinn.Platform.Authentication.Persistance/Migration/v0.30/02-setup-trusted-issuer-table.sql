-- One row per runtime cluster: the exact "iss" of its service account tokens, the service owner it
-- runs apps for, and where its public keys are.
CREATE TABLE IF NOT EXISTS workload_identity.trusted_issuer (
  issuer              TEXT PRIMARY KEY,
  display_name        TEXT NOT NULL,
  service_owner       TEXT NOT NULL,
  environment         TEXT NOT NULL,
  -- Either the cluster's OIDC discovery document, fetched and cached at runtime, or keys pinned at
  -- registration, so validation does not depend on the cluster's discovery endpoint being reachable.
  discovery_uri       TEXT,
  jwks                JSONB,
  app_resolution      TEXT NOT NULL,
  allowed_namespaces  TEXT[] NOT NULL DEFAULT '{}',
  enabled             BOOLEAN NOT NULL DEFAULT TRUE,
  created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at          TIMESTAMPTZ,
  CONSTRAINT chk_trusted_issuer_keys
    CHECK (discovery_uri IS NOT NULL OR jwks IS NOT NULL),
  CONSTRAINT chk_trusted_issuer_app_resolution
    CHECK (app_resolution IN ('ServiceOwnerOnly', 'ServiceAccountName', 'ExplicitBinding')),
  CONSTRAINT chk_trusted_issuer_service_owner
    CHECK (service_owner ~ '^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$')
)

TABLESPACE pg_default;
