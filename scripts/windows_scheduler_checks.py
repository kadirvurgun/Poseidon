import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest


class SchedulerTests(unittest.TestCase):
    def test_preview_constructs_task_with_mocked_scheduler_and_never_registers(self):
        script = Path(__file__).with_name('install-hourly-watch.ps1').resolve()
        with tempfile.TemporaryDirectory(prefix='quota scheduler ') as directory:
            python = Path(directory) / 'pythonw.exe'
            python.touch()
            # Every scheduler cmdlet is a fake. Execute the real installer in
            # Preview mode to validate argument binding and paths without install.
            command = r'''
function New-ScheduledTaskAction { param($Execute,$Argument,$WorkingDirectory) @{execute=$Execute; arguments=$Argument; cwd=$WorkingDirectory} }
function New-ScheduledTaskTrigger { param([switch]$Once,$At,$RepetitionInterval) @{once=$Once.IsPresent; interval=$RepetitionInterval.TotalSeconds} }
function New-ScheduledTaskPrincipal { param($UserId,$LogonType,$RunLevel) @{logon=$LogonType; level=$RunLevel} }
function New-ScheduledTaskSettingsSet { param($MultipleInstances,[switch]$StartWhenAvailable,$ExecutionTimeLimit) @{instances=$MultipleInstances; available=$StartWhenAvailable.IsPresent; timeout=$ExecutionTimeLimit.TotalSeconds} }
function New-ScheduledTask { param($Action,$Trigger,$Principal,$Settings) @{action=$Action; trigger=$Trigger; principal=$Principal; settings=$Settings} }
function Register-ScheduledTask { throw 'Registration forbidden in test' }
& $env:WATCH_INSTALLER -PythonPath $env:WATCH_PYTHON -Preview | ConvertTo-Json -Depth 5 -Compress
'''
            env = dict(os.environ, WATCH_INSTALLER=str(script), WATCH_PYTHON=str(python))
            result = subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-Command', command],
                                    env=env, capture_output=True, text=True, timeout=30, check=True)
            task = json.loads(result.stdout)
            self.assertEqual(task['action']['execute'], str(python))
            self.assertEqual(task['action']['arguments'], '"' + str(script.with_name('codex_watch.py')) + '" once')
            self.assertEqual(task['action']['cwd'], str(script.parent.parent))
            self.assertEqual(task['trigger'], {'once': True, 'interval': 3600})
            self.assertEqual(task['principal'], {'logon': 'Interactive', 'level': 'Limited'})
            self.assertEqual(task['settings'], {'instances': 'IgnoreNew', 'available': True, 'timeout': 300})



if __name__ == '__main__':
    unittest.main()
