"""Windows ScheduledTasks construction only. Explicit CI entry; never registers/starts."""
import json
from pathlib import Path
import subprocess
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]


class NightlyInstallerTests(unittest.TestCase):
    def construct(self, publish=False):
        script = ROOT / 'scripts/install-nightly-coordinator.ps1'
        pythonw = Path(sys.executable).with_name('pythonw.exe')
        # All paths are literals; run Preview, never Register-ScheduledTask.
        literal = lambda value: "'" + str(value).replace("'", "''") + "'"
        command = ("$ErrorActionPreference='Stop'; $task = & " + literal(script)
                   + ' -Preview -PythonPath ' + literal(pythonw)
                   + (' -Enable -Publish' if publish else '')
                   + '; [pscustomobject]@{Execute=$task.Actions[0].Execute; Arguments=$task.Actions[0].Arguments; '
                   'Directory=$task.Actions[0].WorkingDirectory; Logon=[string]$task.Principal.LogonType; '
                   'Level=[string]$task.Principal.RunLevel; Multiple=[string]$task.Settings.MultipleInstances; '
                   'Limit=$task.Settings.ExecutionTimeLimit; Interval=$task.Triggers[0].Repetition.Interval} '
                   '| ConvertTo-Json -Compress')
        result = subprocess.run(['pwsh', '-NoProfile', '-NonInteractive', '-Command', command],
                                text=True, encoding='utf-8', capture_output=True, timeout=60, check=True)
        return json.loads(result.stdout)

    def test_disabled_preview_construction(self):
        task = self.construct()
        self.assertEqual(Path(task['Execute']).name.lower(), 'pythonw.exe')
        self.assertTrue(Path(task['Execute']).is_absolute())
        self.assertEqual(task['Arguments'], '"' + str(ROOT / 'scripts/nightly_coordinator.py') + '" --once')
        self.assertEqual(Path(task['Directory']), ROOT)
        self.assertEqual(task['Logon'], 'Interactive')
        self.assertEqual(task['Level'], 'Limited')
        self.assertEqual(task['Multiple'], 'IgnoreNew')
        self.assertEqual(task['Limit'], 'PT45M')
        self.assertEqual(task['Interval'], 'PT10M')

    def test_publish_requires_explicit_installer_flag(self):
        self.assertTrue(self.construct(True)['Arguments'].endswith('--once --enable --publish'))


if __name__ == '__main__':
    unittest.main()
