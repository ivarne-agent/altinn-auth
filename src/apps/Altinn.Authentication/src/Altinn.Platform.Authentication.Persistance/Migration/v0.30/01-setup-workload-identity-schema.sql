--Schema: workload_identity
-- Trust in Kubernetes clusters whose service account tokens may be exchanged for app tokens.
-- See docs/adr/0005-app-tokens-from-workload-identity.md.
CREATE SCHEMA IF NOT EXISTS workload_identity;
