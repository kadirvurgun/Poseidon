"""Resolve the source manifest for a validator pinned to a historical baseline.

The active source baseline may move on (docs/sources/active.json) while older
gate packages stay pinned to the baseline they were authored against. Such a
package may only use the active manifest when the active baseline still equals
the pin, or the immutable ``prior_manifest`` recorded by active.json when that
manifest is inside the repository, within the size budget and declares exactly
the pinned baseline_id. Anything else is rejected; callers keep their own error
codes and their own per-file SHA256 checks.
"""
import json
from pathlib import Path

ACTIVE = "docs/sources/active.json"
ACTIVE_LIMIT = 64 * 1024


class PinnedBaselineError(ValueError):
    """The pinned baseline is neither active nor the recorded prior manifest."""


def _contained(root, name, limit):
    if not isinstance(name, str) or not name or len(name) > 512 or "\\" in name:
        raise PinnedBaselineError("manifest_path")
    if name.startswith("/") or ":" in name or any(p in {"", ".", ".."} for p in name.split("/")):
        raise PinnedBaselineError("manifest_path")
    path = (root / name).resolve()
    if not path.is_relative_to(root):
        raise PinnedBaselineError("manifest_path")
    current = root
    for part in name.split("/"):
        current /= part
        if current.is_symlink() or getattr(current, "is_junction", lambda: False)():
            raise PinnedBaselineError("manifest_path")
    if not path.is_file() or path.stat().st_size > limit:
        raise PinnedBaselineError("manifest_budget")
    return path


def pinned_manifest(root, baseline, source_root=None, limit=2 * 1024 * 1024, active=None):
    """Return the manifest path name (relative to root) for the pinned baseline.

    ``source_root``, when given, must equal active.json's source_root. The
    returned manifest is not trusted by itself: callers re-read it and
    re-check its baseline_id and file hashes.
    """
    root = Path(root).resolve()
    if active is None:
        path = root / ACTIVE
        if path.stat().st_size > ACTIVE_LIMIT:
            raise PinnedBaselineError("active_budget")
        active = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(active, dict):
        raise PinnedBaselineError("active_shape")
    if source_root is not None and active.get("source_root") != source_root:
        raise PinnedBaselineError("source_root")
    if active.get("baseline_id") == baseline:
        return active.get("manifest")
    prior = active.get("prior_manifest")
    if prior is None:
        raise PinnedBaselineError("inactive_baseline")
    path = _contained(root, prior, limit)
    manifest = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(manifest, dict) or manifest.get("baseline_id") != baseline:
        raise PinnedBaselineError("prior_baseline")
    return prior
