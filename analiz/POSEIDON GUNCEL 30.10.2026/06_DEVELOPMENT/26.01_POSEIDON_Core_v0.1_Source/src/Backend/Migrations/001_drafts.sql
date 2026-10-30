CREATE TABLE IF NOT EXISTS companies(tenant_id uuid NOT NULL, id uuid NOT NULL, PRIMARY KEY(tenant_id,id));
CREATE TABLE IF NOT EXISTS drafts(
 tenant_id uuid NOT NULL, id uuid NOT NULL, company_id uuid NOT NULL, kind text NOT NULL CHECK(kind IN ('vessels','certificates')),
 vessel_id uuid, owner_subject text NOT NULL, version bigint NOT NULL CHECK(version>0), data jsonb NOT NULL,
 persistence_state text NOT NULL DEFAULT 'DRAFT' CHECK(persistence_state='DRAFT'),
 PRIMARY KEY(tenant_id,id), FOREIGN KEY(tenant_id,company_id) REFERENCES companies(tenant_id,id),
 FOREIGN KEY(tenant_id,vessel_id) REFERENCES drafts(tenant_id,id), CHECK((kind='vessels' AND vessel_id IS NULL) OR (kind='certificates' AND vessel_id IS NOT NULL)));
CREATE TABLE IF NOT EXISTS idempotency(tenant_id uuid NOT NULL,subject text NOT NULL,key text NOT NULL,fingerprint text NOT NULL,response jsonb NOT NULL, PRIMARY KEY(tenant_id,subject,key));
CREATE TABLE IF NOT EXISTS audit_events(id uuid PRIMARY KEY,tenant_id uuid NOT NULL,company_id uuid NOT NULL,entity_id uuid NOT NULL,vessel_id uuid NOT NULL,actor text NOT NULL,action text NOT NULL,before_data jsonb,after_data jsonb NOT NULL,version bigint NOT NULL,correlation_id text NOT NULL,occurred_at timestamptz NOT NULL DEFAULT now());
CREATE OR REPLACE FUNCTION reject_audit_mutation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'audit_events is append-only'; END $$;
DROP TRIGGER IF EXISTS audit_no_mutation ON audit_events;
CREATE TRIGGER audit_no_mutation BEFORE UPDATE OR DELETE OR TRUNCATE ON audit_events FOR EACH STATEMENT EXECUTE FUNCTION reject_audit_mutation();
