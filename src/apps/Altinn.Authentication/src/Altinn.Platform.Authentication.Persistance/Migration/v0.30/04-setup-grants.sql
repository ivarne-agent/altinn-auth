GRANT USAGE ON SCHEMA workload_identity TO auth_authentication;
GRANT SELECT,INSERT,UPDATE,REFERENCES,DELETE ON ALL TABLES IN SCHEMA workload_identity TO auth_authentication;
