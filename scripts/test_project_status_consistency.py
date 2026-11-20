"""Consistency checks for docs/project-status.json canonical acceptance.

The canonical per-task flags live only in the top-level ``tasks`` array.
Dated package blocks (``*_source_continuation_*``, ``historical_*``) keep
their own ``tasks`` snapshots with ``accepted: false``; those are immutable
history and are not counted here.

Asserts that per-task ``accepted`` flags, the ``sprints`` summary, the
top-level totals and every recorded ``sN_gate_*`` block agree, and that
the declared totals in docs/project-status.md and CLAUDE.md match.
"""

from __future__ import annotations

import collections
import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STATUS_JSON = ROOT / "docs" / "project-status.json"
STATUS_MD = ROOT / "docs" / "project-status.md"
CLAUDE_MD = ROOT / "CLAUDE.md"
GATE_KEY = re.compile(r"^(s\d+)_gate_\d{8}$")


def _sprint_group(sprint: str) -> str:
    return "PILOT" if sprint.startswith("PILOT") else sprint


class ProjectStatusConsistencyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.status = json.loads(STATUS_JSON.read_text(encoding="utf-8"))
        cls.tasks = cls.status["tasks"]
        cls.by_id = {t["id"]: t for t in cls.tasks}
        cls.gates = {
            m.group(1).upper(): value
            for key, value in cls.status.items()
            if (m := GATE_KEY.match(key)) and isinstance(value, dict)
        }

    def test_task_ids_unique(self) -> None:
        counts = collections.Counter(t["id"] for t in self.tasks)
        self.assertEqual([i for i, c in counts.items() if c > 1], [])

    def test_accepted_flag_matches_assessment(self) -> None:
        for task in self.tasks:
            with self.subTest(task=task["id"]):
                self.assertIs(task["accepted"], task.get("assessment") == "accepted")

    def test_sprint_summary_matches_task_flags(self) -> None:
        agg: dict[str, list[int]] = collections.defaultdict(lambda: [0, 0, 0, 0])
        for task in self.tasks:
            row = agg[_sprint_group(task["sprint"])]
            row[0] += 1
            row[1] += task["story_points"]
            if task["accepted"] is True:
                row[2] += 1
                row[3] += task["story_points"]
        summary = {s["sprint"]: s for s in self.status["sprints"]}
        self.assertEqual(set(summary), set(agg))
        for sprint, (n, sp, acc_n, acc_sp) in agg.items():
            with self.subTest(sprint=sprint):
                s = summary[sprint]
                self.assertEqual(
                    (s["tasks"], s["story_points"], s["accepted_tasks"], s["accepted_story_points"]),
                    (n, sp, acc_n, acc_sp),
                )
                self.assertAlmostEqual(float(s["completion_percent"]), round(100 * acc_sp / sp, 2), places=2)

    def test_top_level_totals_match_task_flags(self) -> None:
        accepted = [t for t in self.tasks if t["accepted"] is True]
        self.assertEqual(self.status["total_tasks"], len(self.tasks))
        self.assertEqual(self.status["total_story_points"], sum(t["story_points"] for t in self.tasks))
        self.assertEqual(self.status["accepted_tasks"], len(accepted))
        acc_sp = sum(t["story_points"] for t in accepted)
        self.assertEqual(self.status["accepted_story_points"], acc_sp)
        self.assertAlmostEqual(
            float(self.status["completion_percent"]),
            round(100 * acc_sp / self.status["total_story_points"], 2),
            places=2,
        )

    def test_gate_blocks_match_task_flags(self) -> None:
        self.assertTrue(self.gates, "no sN_gate_* blocks found")
        for sprint, gate in self.gates.items():
            with self.subTest(sprint=sprint):
                accepted_ids = gate["accepted_tasks"]
                not_accepted = [x["id"] if isinstance(x, dict) else x for x in gate.get("not_accepted_tasks", [])]
                sprint_ids = {t["id"] for t in self.tasks if t["sprint"] == sprint}
                self.assertEqual(set(accepted_ids) | set(not_accepted), sprint_ids)
                for task_id in accepted_ids:
                    self.assertIs(self.by_id[task_id]["accepted"], True, task_id)
                for task_id in not_accepted:
                    self.assertIs(self.by_id[task_id]["accepted"], False, task_id)
                self.assertEqual(
                    gate["accepted_story_points_added"],
                    sum(self.by_id[i]["story_points"] for i in accepted_ids),
                )

    def test_no_task_accepted_without_gate_record(self) -> None:
        gated = {i for g in self.gates.values() for i in g["accepted_tasks"]}
        # S0/S1 acceptance predates sN_gate_* blocks and is recorded per task
        # via acceptance_evidence receipts.
        for task in self.tasks:
            if task["accepted"] is True and task["sprint"] not in ("S0", "S1"):
                with self.subTest(task=task["id"]):
                    self.assertIn(task["id"], gated)
            if task["accepted"] is True:
                self.assertTrue(task.get("acceptance_evidence") or task["id"] in gated, task["id"])

    def test_declared_totals_in_markdown(self) -> None:
        n, sp = self.status["accepted_tasks"], self.status["accepted_story_points"]
        md = STATUS_MD.read_text(encoding="utf-8")
        first = re.search(r"Canonical ilerleme: (\d+) iş / (\d+) SP", md)
        self.assertIsNotNone(first)
        self.assertEqual((int(first.group(1)), int(first.group(2))), (n, sp))
        claude = CLAUDE_MD.read_text(encoding="utf-8")
        declared = re.search(r"\((\d+) iş, (\d+) SP", claude)
        self.assertIsNotNone(declared)
        self.assertEqual((int(declared.group(1)), int(declared.group(2))), (n, sp))


if __name__ == "__main__":
    unittest.main()
