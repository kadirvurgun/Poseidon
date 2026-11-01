"""DEV-003 contract/deployment separation. No environment reads or secret resolution.

Definition validation freezes required references. Deployment validation requires an
operator-supplied resolved manifest; it never installs resources or grants approval.
Diagnostics are fixed codes and contain no supplied values.
"""
import argparse
import json
import re
from pathlib import Path
from urllib.parse import urlsplit

ENVIRONMENTS = ("DEV", "TEST", "UAT", "PILOT", "PROD")
ROLES = {"DEV": "Engineering", "TEST": "QA/Engineering", "UAT": "PO/QA",
         "PILOT": "Product/Pilot/Engineering", "PROD": "ReleaseAuthority"}
DATA = {"DEV": "Synthetic", "TEST": "SyntheticGolden", "UAT": "MaskedRepresentative",
        "PILOT": "ApprovedReal", "PROD": "Authoritative"}
REFERENCE = re.compile(r"\Aconfig://poseidon/(DEV|TEST|UAT|PILOT|PROD)/[a-z][a-z0-9-]{1,63}\Z")
HASH = re.compile(r"\A[0-9a-f]{64}\Z")
SOURCE = {"path": "analiz/POSEIDON GUNCEL 30.10.2026/07_TEST_PILOT_RELEASE/23.03_POSEIDON_v46.3_Deployment_Environment_Runbook_TR.docx",
          "sha256": "7359acce4371c525d942f78c7dde8383915378bdc8ccff5ddc6137f8ce76ee0a",
          "locators": ["paragraph:19..49", "paragraph:50..111"]}
REQUIRED = {"publicOrigin", "apiOrigin", "databaseSecret", "oidcSecret", "objectStoreSecret",
            "profileKeySecret", "offlineKeySecret", "retentionPolicy", "dataPolicy", "migrationRole",
            "applicationRole", "backupPolicy", "monitoringPolicy", "rollbackPlan"}
SECRET_FIELDS = {"databaseSecret", "oidcSecret", "objectStoreSecret", "profileKeySecret", "offlineKeySecret"}


def load(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("duplicate_key")
            result[key] = value
        return result
    if Path(path).stat().st_size > 128 * 1024:
        raise ValueError("input_budget")
    return json.loads(Path(path).read_text(encoding="utf-8"), object_pairs_hook=unique)


def definition_errors(contract):
    errors = set()
    if not isinstance(contract, dict) or set(contract) != {"schemaVersion", "source", "environments", "promotion"}:
        return ["contract_shape"]
    if contract["schemaVersion"] != "poseidon.environment.v1":
        errors.add("contract_version")
    source = contract["source"]
    if source != SOURCE:
        errors.add("source_identity")
    envs = contract["environments"]
    if not isinstance(envs, dict) or set(envs) != set(ENVIRONMENTS):
        return sorted(errors | {"environment_inventory"})
    for name, row in envs.items():
        if not isinstance(row, dict) or set(row) != {"deployRole", "approvalRole", "dataClass", "productionCopyAllowed", "hosting", "references", "configurationEvidence"}:
            errors.add("environment_shape"); continue
        if row["deployRole"] != "DevOps" or row["approvalRole"] != ROLES[name]:
            errors.add("responsible_role")
        if row["dataClass"] != DATA[name] or row["productionCopyAllowed"] is not False:
            errors.add("data_boundary")
        expected_host = "DevelopmentTestOnly" if name in ("DEV", "TEST") else "OnPremisesOrSelfHosted"
        if row["hosting"] != expected_host:
            errors.add("hosting_boundary")
        refs = row["references"]
        if not isinstance(refs, dict) or set(refs) != REQUIRED:
            errors.add("mandatory_references"); continue
        if any(not isinstance(ref, str) or not REFERENCE.fullmatch(ref) or ref.split("/")[3] != name for ref in refs.values()):
            errors.add("reference_scope")
        if len(set(refs.values())) != len(REQUIRED):
            errors.add("reference_alias")
        evidence = row["configurationEvidence"]
        if not isinstance(evidence, dict) or set(evidence) != {"state", "sourcePaths"} or evidence["state"] not in ("DefaultsOnly", "Unprovisioned") or not isinstance(evidence["sourcePaths"], list):
            errors.add("configuration_evidence")
    if contract["promotion"] != {"order": list(ENVIRONMENTS), "artifact": "SameImmutableDigest",
            "checks": ["RequiredCI", "IndependentQA", "SourceReview", "MigrationDryRun", "BackupRecoveryPoint", "Smoke", "Telemetry", "Rollback"],
            "approval": "IndependentTargetEnvironmentAuthority", "failure": "StopOrRollbackNoAutomaticRetry"}:
        errors.add("promotion_definition")
    return sorted(errors)


def deployment_errors(contract, manifest):
    errors = set(definition_errors(contract))
    if errors:
        return sorted(errors)
    keys = {"environment", "bindings", "artifactSha256", "promotedArtifactSha256", "checks", "approvalReceipt", "dataApprovalReceipt"}
    if not isinstance(manifest, dict) or set(manifest) != keys or manifest.get("environment") not in ENVIRONMENTS:
        return ["deployment_shape"]
    name = manifest["environment"]
    refs = contract["environments"][name]["references"]
    bindings = manifest["bindings"]
    if not isinstance(bindings, dict) or set(bindings) != set(refs.values()):
        return ["unresolved_configuration"]
    for field, ref in refs.items():
        binding = bindings[ref]
        if not isinstance(binding, str) or not binding or len(binding) > 512:
            errors.add("unresolved_configuration"); continue
        if field in ("publicOrigin", "apiOrigin"):
            try:
                url = urlsplit(binding)
                loopback = name in ("DEV", "TEST") and url.hostname in ("127.0.0.1", "localhost", "::1")
                if not url.hostname or url.username or url.password or url.query or url.fragment or url.path not in ("", "/") or not (url.scheme == "https" or loopback and url.scheme == "http") or url.port == 0:
                    errors.add("origin_policy")
            except ValueError:
                errors.add("origin_policy")
        elif field in SECRET_FIELDS:
            # References only; a resolved secret or connection string is rejected.
            if not re.fullmatch(r"secret-ref://[A-Za-z0-9/_-]{3,200}#[A-Za-z0-9_-]{1,80}", binding):
                errors.add("secret_reference_policy")
        elif not re.fullmatch(r"reviewed-ref://[A-Za-z0-9/_-]{3,200}#[a-f0-9]{64}", binding):
            errors.add("reviewed_policy_reference")
    digest = manifest["artifactSha256"]
    if not isinstance(digest, str) or not HASH.fullmatch(digest) or digest != manifest["promotedArtifactSha256"]:
        errors.add("artifact_promotion")
    checks = manifest["checks"]
    if not isinstance(checks, dict) or set(checks) != set(contract["promotion"]["checks"]) or any(not isinstance(v, str) or not HASH.fullmatch(v) for v in checks.values()):
        errors.add("missing_check_receipts")
    if not isinstance(manifest["approvalReceipt"], str) or not HASH.fullmatch(manifest["approvalReceipt"]):
        errors.add("missing_deployment_authority")
    if name in ("UAT", "PILOT", "PROD") and (not isinstance(manifest["dataApprovalReceipt"], str) or not HASH.fullmatch(manifest["dataApprovalReceipt"])):
        errors.add("missing_data_authority")
    # Hashes are required references, never proof that their authority was verified.
    # Coordinator must verify exact receipts before promotion; this function grants nothing.
    return sorted(errors)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("contract")
    parser.add_argument("--deployment-manifest")
    args = parser.parse_args()
    try:
        contract = load(args.contract)
        errors = deployment_errors(contract, load(args.deployment_manifest)) if args.deployment_manifest else definition_errors(contract)
    except (OSError, ValueError, TypeError, KeyError, RecursionError):
        errors = ["invalid_input"]
    print(json.dumps({"mode": "deployment" if args.deployment_manifest else "definition", "errors": errors,
                      "authority": "NOT_VERIFIED", "accepted": False}, sort_keys=True))
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
