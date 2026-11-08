-- Explicit mapping from a service account in a trusted cluster to the app it runs, for issuers whose
-- app_resolution is ExplicitBinding.
CREATE TABLE IF NOT EXISTS workload_identity.workload_binding (
  issuer      TEXT NOT NULL REFERENCES workload_identity.trusted_issuer (issuer) ON DELETE CASCADE,
  subject     TEXT NOT NULL,
  app         TEXT NOT NULL,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (issuer, subject)
)

TABLESPACE pg_default;
