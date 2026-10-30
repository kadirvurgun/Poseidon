CREATE TABLE evidence_intakes (
 tenant_id uuid NOT NULL, id uuid NOT NULL, company_id uuid NOT NULL, vessel_id uuid NOT NULL,
 target_type text NOT NULL CHECK(target_type IN ('Vessel','Certificate')), target_id uuid NOT NULL,
 previous_id uuid, restricted_reason text,
 actor text NOT NULL, version bigint NOT NULL DEFAULT 1, metadata jsonb NOT NULL,
 expected_bytes bigint NOT NULL CHECK(expected_bytes BETWEEN 1 AND 5242880),
 detected_mime text, type_result text NOT NULL DEFAULT 'NOT_CHECKED' CHECK(type_result IN ('NOT_CHECKED','MATCHED','REJECTED')),
 state text NOT NULL DEFAULT 'RESERVED' CHECK(state IN ('RESERVED','RECEIVING','STORED_QUARANTINED','REJECTED','ABORTED','EXPIRED')), object_id uuid, actual_bytes bigint, sha256 text,
 integrity_state text NOT NULL DEFAULT 'NOT_CHECKED' CHECK(integrity_state IN ('NOT_CHECKED','COMPUTED','MATCHED','MISMATCH','ERROR')), created_at timestamptz NOT NULL DEFAULT now(),
 FOREIGN KEY(tenant_id,previous_id) REFERENCES evidence_intakes(tenant_id,id),
 PRIMARY KEY(tenant_id,id), FOREIGN KEY(tenant_id,target_id) REFERENCES drafts(tenant_id,id)
);
CREATE INDEX evidence_target ON evidence_intakes(tenant_id,company_id,target_id,id);

CREATE FUNCTION protect_evidence_retention() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Evidence deletion is disabled'; END IF;
 IF NEW.tenant_id<>OLD.tenant_id OR NEW.id<>OLD.id OR NEW.company_id<>OLD.company_id OR NEW.vessel_id<>OLD.vessel_id OR NEW.target_type<>OLD.target_type OR NEW.target_id<>OLD.target_id OR NEW.previous_id IS DISTINCT FROM OLD.previous_id OR NEW.restricted_reason IS DISTINCT FROM OLD.restricted_reason OR NEW.actor<>OLD.actor OR NEW.metadata<>OLD.metadata OR NEW.expected_bytes<>OLD.expected_bytes OR NEW.created_at<>OLD.created_at THEN RAISE EXCEPTION 'Evidence reservation is immutable'; END IF;
 IF OLD.state NOT IN ('RESERVED','RECEIVING') THEN RAISE EXCEPTION 'Final evidence intake is immutable'; END IF;
 IF NOT ((OLD.state='RESERVED' AND NEW.state IN ('RECEIVING','EXPIRED')) OR (OLD.state='RECEIVING' AND NEW.state IN ('STORED_QUARANTINED','REJECTED','ABORTED'))) OR NEW.version<>OLD.version+1 THEN RAISE EXCEPTION 'Invalid evidence transition'; END IF;
 IF NEW.state='STORED_QUARANTINED' AND (NEW.object_id IS DISTINCT FROM NEW.id OR NEW.actual_bytes IS DISTINCT FROM NEW.expected_bytes OR NEW.sha256 IS NULL OR NEW.integrity_state NOT IN ('COMPUTED','MATCHED') OR NEW.type_result<>'MATCHED' OR NEW.detected_mime IS NULL) THEN RAISE EXCEPTION 'Incomplete quarantine pointer'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER evidence_retention_guard BEFORE UPDATE OR DELETE ON evidence_intakes FOR EACH ROW EXECUTE FUNCTION protect_evidence_retention();

CREATE UNIQUE INDEX evidence_active_replacement ON evidence_intakes(tenant_id,previous_id) WHERE state IN ('RESERVED','RECEIVING','STORED_QUARANTINED');
