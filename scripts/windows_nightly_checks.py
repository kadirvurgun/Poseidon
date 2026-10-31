"""Windows ScheduledTasks construction only. Explicit CI entry; never registers/starts."""
import json
from pathlib import Path
import subprocess
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]


class NightlyInstallerTests(unittest.TestCase):
    def construct(self, publish=False, extra=''):
        script = ROOT / 'scripts/install-nightly-coordinator.ps1'
        pythonw = Path(sys.executable).with_name('pythonw.exe')
        # All paths are literals; run Preview, never Register-ScheduledTask.
        literal = lambda value: "'" + str(value).replace("'", "''") + "'"
        command = ("$ErrorActionPreference='Stop'; $task = & " + literal(script)
                   + ' -Preview -PythonPath ' + literal(pythonw)
                   + (' -Enable -Publish' if publish else '')
                   + extra
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
        self.assertEqual(task['Arguments'], '"' + str(ROOT / 'scripts/nightly_coordinator.py')
                         + '" --once --max-jobs 4 --max-model-calls 8 --hours 8 --timeout 900')
        self.assertEqual(Path(task['Directory']), ROOT)
        self.assertEqual(task['Logon'], 'Interactive')
        self.assertEqual(task['Level'], 'Limited')
        self.assertEqual(task['Multiple'], 'IgnoreNew')
        self.assertEqual(task['Limit'], 'PT45M')
        self.assertEqual(task['Interval'], 'PT10M')

    def test_publish_requires_explicit_installer_flag(self):
        self.assertTrue(self.construct(True)['Arguments'].endswith('--timeout 900 --enable --publish'))

    def test_explicit_limits_in_actual_definition(self):
        task = self.construct(extra=' -MaxJobs 20 -MaxModelCalls 40 -Hours 0 -Timeout 1')
        self.assertTrue(task['Arguments'].endswith('--max-jobs 20 --max-model-calls 40 --hours 0 --timeout 1'))


class NightlyInstallerMockTests(unittest.TestCase):
    def invoke(self, extra='', culture='en-US'):
        literal = lambda value: "'" + str(value).replace("'", "''") + "'"
        command = """
$ErrorActionPreference = 'Stop'
function Register-ScheduledTask { throw 'REGISTRATION_FORBIDDEN' }
function New-ScheduledTaskAction {
    param($Execute, $Argument, $WorkingDirectory)
    [pscustomobject]@{Execute=$Execute; Arguments=$Argument; Directory=$WorkingDirectory}
}
function New-ScheduledTaskTrigger { param($Once, $At, $RepetitionInterval) }
function New-ScheduledTaskPrincipal { param($UserId, $LogonType, $RunLevel) }
function New-ScheduledTaskSettingsSet { param($MultipleInstances, [switch]$StartWhenAvailable, $ExecutionTimeLimit) }
function New-ScheduledTask { param($Action, $Trigger, $Principal, $Settings) $Action }
""".replace('param($Once,', 'param([switch]$Once,')
        command += '[Threading.Thread]::CurrentThread.CurrentCulture = ' + literal(culture) + '\n'
        command += ('& ' + literal(ROOT / 'scripts/install-nightly-coordinator.ps1')
                    + ' -Preview -PythonPath ' + literal(Path(sys.executable).with_name('pythonw.exe'))
                    + ' ' + extra + ' | ConvertTo-Json -Compress')
        return subprocess.run(['pwsh', '-NoProfile', '-NonInteractive', '-Command', command],
                              text=True, encoding='utf-8', capture_output=True, timeout=60, check=False)

    def test_defaults_and_explicit_flags_without_registration(self):
        for extra, expected in [('', '--max-jobs 4 --max-model-calls 8 --hours 8 --timeout 900'),
                                ('-MaxJobs 1 -MaxModelCalls 1 -Hours 0 -Timeout 1 -Enable -Publish',
                                 '--max-jobs 1 --max-model-calls 1 --hours 0 --timeout 1 --enable --publish'),
                                ('-MaxJobs 20 -MaxModelCalls 40 -Hours 8 -Timeout 900',
                                 '--max-jobs 20 --max-model-calls 40 --hours 8 --timeout 900')]:
            with self.subTest(extra=extra):
                result = self.invoke(extra)
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertTrue(json.loads(result.stdout)['Arguments'].endswith(expected))

    def test_out_of_range_limits_rejected_before_construction(self):
        for name, values in [('MaxJobs', (0, 21)), ('MaxModelCalls', (0, 41)),
                             ('Hours', (-1, 9)), ('Timeout', (0, 901))]:
            for value in values:
                with self.subTest(name=name, value=value):
                    result = self.invoke(f'-{name} {value}')
                    self.assertNotEqual(result.returncode, 0)
                    self.assertFalse(result.stdout.strip())
                    self.assertNotIn('REGISTRATION_FORBIDDEN', result.stderr)

    def test_fractional_hours_use_invariant_cli_number(self):
        result = self.invoke('-Hours 0.5', culture='tr-TR')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('--hours 0.5 --timeout 900', json.loads(result.stdout)['Arguments'])


if __name__ == '__main__':
    unittest.main()
