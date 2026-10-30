-- Existing certificates remain DRAFT/schema 1; IDs, parents, audit and replay rows are untouched.
ALTER TABLE drafts DROP CONSTRAINT certificate_inventory_unchanged;
ALTER TABLE drafts ADD COLUMN certificate_schema_version integer NOT NULL DEFAULT 1 CHECK(certificate_schema_version IN (1,2));
ALTER TABLE drafts ADD CONSTRAINT aggregate_schema_markers CHECK((kind='vessels' AND certificate_schema_version=1) OR (kind='certificates' AND inventory_schema_version=1));
CREATE OR REPLACE FUNCTION protect_inventory_write() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF OLD.kind='vessels' AND (OLD.inventory_schema_version=2 OR OLD.admin_state='RECORDED') AND current_setting('poseidon.inventory_write',true) IS DISTINCT FROM 'on' THEN
  RAISE EXCEPTION 'inventory_resource_required';
 END IF;
 IF OLD.kind='certificates' AND (OLD.certificate_schema_version=2 OR OLD.admin_state='RECORDED') AND current_setting('poseidon.certificate_write',true) IS DISTINCT FROM 'on' THEN
  RAISE EXCEPTION 'certificate_resource_required';
 END IF;
 RETURN NEW;
END $$;
