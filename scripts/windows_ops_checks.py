"""Explicit Windows installer mock entry. Never discovers or registers real tasks."""
import json
from pathlib import Path
import subprocess
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]


class OpsInstallerMockTests(unittest.TestCase):
    def construct(self, flags=''):
        literal = lambda value: "'" + str(value).replace("'", "''") + "'"
        command = r'''
$ErrorActionPreference = 'Stop'
function Register-ScheduledTask { throw 'REGISTRATION_FORBIDDEN' }
function New-ScheduledTaskAction {
    param($Execute, $Argument, $WorkingDirectory)
    [pscustomobject]@{Execute=$Execute; Arguments=$Argument; Directory=$WorkingDirectory}
}
function New-ScheduledTaskTrigger {
    param([switch]$AtLogOn, [switch]$Once, $At, $RepetitionInterval)
    [pscustomobject]@{Logon=$AtLogOn.IsPresent; Once=$Once.IsPresent; At=$At; Interval=$RepetitionInterval.TotalMinutes}
}
function New-ScheduledTaskPrincipal {
    param($UserId, $LogonType, $RunLevel)
    [pscustomobject]@{Logon=$LogonType; Level=$RunLevel}
}
function New-ScheduledTaskSettingsSet {
    param($MultipleInstances, [switch]$StartWhenAvailable, [switch]$WakeToRun, $ExecutionTimeLimit, $RestartCount, $RestartInterval)
    [pscustomobject]@{Multiple=$MultipleInstances; Available=$StartWhenAvailable.IsPresent; Wake=$WakeToRun.IsPresent; Limit=$ExecutionTimeLimit.TotalSeconds; Restart=$RestartCount}
}
function New-ScheduledTask {
    param($Action, $Trigger, $Principal, $Settings)
    [pscustomobject]@{Action=$Action; Trigger=$Trigger; Principal=$Principal; Settings=$Settings}
}
'''
        command += '\n@(& ' + literal(ROOT / 'scripts/install-ops-runtime.ps1')
        command += ' -Preview -PythonPath ' + literal(Path(sys.executable).with_name('pythonw.exe'))
        command += ' ' + flags + ') | ConvertTo-Json -Depth 8 -Compress'
        result = subprocess.run(['pwsh', '-NoProfile', '-NonInteractive', '-Command', command],
            capture_output=True, encoding='utf-8', timeout=30, check=True)
        value = json.loads(result.stdout)
        return value if isinstance(value, list) else [value]

    def test_default_interactive_limited_pythonw_restart_ignore_new_preview(self):
        task = self.construct()[0]
        self.assertEqual(task['Action']['Arguments'], '"' + str(ROOT / 'scripts/ops_runtime.py') + '" --serve --manage-wake')
        self.assertTrue(Path(task['Action']['Execute']).is_absolute())
        self.assertEqual(Path(task['Action']['Execute']).name.lower(), 'pythonw.exe')
        self.assertEqual(task['Principal'], {'Logon': 'Interactive', 'Level': 'Limited'})
        self.assertEqual(task['Settings']['Multiple'], 'IgnoreNew')
        self.assertTrue(task['Settings']['Available'])
        self.assertEqual(task['Settings']['Limit'], 0)
        self.assertEqual(task['Settings']['Restart'], 3)
        self.assertTrue(task['Trigger'][0]['Logon'])
        self.assertEqual(task['Trigger'][1]['Interval'], 5)

    def test_explicit_development_publish_and_one_shot_reset_wake(self):
        tasks = self.construct('-Develop -Publish -WakeAt 2100000000')
        self.assertTrue(tasks[0]['Action']['Arguments'].endswith('--develop --publish'))
        self.assertTrue(tasks[1]['Action']['Arguments'].endswith('--wake-check'))
        self.assertTrue(tasks[1]['Settings']['Wake'])
        self.assertTrue(tasks[1]['Trigger']['Once'])
        self.assertEqual(len(self.construct('-WakeOnly -WakeAt 2100000000')), 1)

    def test_publish_without_develop_rejected(self):
        with self.assertRaises(subprocess.CalledProcessError):
            self.construct('-Publish')


if __name__ == '__main__':
    unittest.main()
