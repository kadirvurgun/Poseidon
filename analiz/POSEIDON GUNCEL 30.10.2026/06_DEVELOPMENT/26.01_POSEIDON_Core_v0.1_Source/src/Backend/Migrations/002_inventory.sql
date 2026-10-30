-- Additive administrative inventory. Existing rows remain DRAFT/schema 1.
ALTER TABLE drafts ADD COLUMN admin_state text NOT NULL DEFAULT 'DRAFT' CHECK(admin_state IN ('DRAFT','RECORDED'));
ALTER TABLE drafts ADD COLUMN inventory_schema_version integer NOT NULL DEFAULT 1 CHECK(inventory_schema_version IN (1,2));
ALTER TABLE drafts ADD CONSTRAINT certificate_inventory_unchanged CHECK(kind='vessels' OR (admin_state='DRAFT' AND inventory_schema_version=1));
ALTER TABLE audit_events ADD COLUMN restricted_metadata jsonb;
CREATE FUNCTION protect_inventory_write() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF (OLD.inventory_schema_version=2 OR OLD.admin_state='RECORDED') AND current_setting('poseidon.inventory_write',true) IS DISTINCT FROM 'on' THEN
  RAISE EXCEPTION 'inventory_resource_required';
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER inventory_write_guard BEFORE UPDATE ON drafts FOR EACH ROW EXECUTE FUNCTION protect_inventory_write();
