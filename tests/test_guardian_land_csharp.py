"""Compile pure C# progression checks with bundled Mono; never launches Unity."""
from pathlib import Path
import os
import subprocess
import tempfile
import unittest


class GuardianCSharpTests(unittest.TestCase):
    def test_real_csharp_save_and_guardian_rules(self):
        root=Path(__file__).resolve().parents[1]
        candidates=sorted(Path('/Applications/Unity/Hub/Editor').glob('*/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge'))
        runtime=next((p for p in candidates if (p/'bin/mono').exists() and (p/'lib/mono/4.5/csc.exe').exists()),None)
        if runtime is None:self.skipTest('Unity bundled Mono is unavailable; no Unity editor run is needed')
        scripts=root/'unity/DawnGarden/Assets/DawnGarden/Scripts'
        libraries=runtime/'lib/mono/unityjit-macos'
        env={**os.environ,'MONO_PATH':str(libraries)}
        with tempfile.TemporaryDirectory(prefix='guardian-rules-') as folder:
            output=Path(folder)/'guardian-checks.exe'
            sources=[scripts/'GardenSave.cs',scripts/'GardenGuardianLand.cs',scripts/'SmallWorldBrowserStorage.cs',root/'tests/csharp/GardenGuardianLandChecks.cs']
            compile=subprocess.run([str(runtime/'bin/mono'),str(runtime/'lib/mono/4.5/csc.exe'),'-nologo','-r:'+str(libraries/'System.Web.Extensions.dll'),'-out:'+str(output),*[str(source) for source in sources]],text=True,capture_output=True,timeout=30,env=env)
            self.assertEqual(0,compile.returncode,compile.stdout+compile.stderr)
            run=subprocess.run([str(runtime/'bin/mono'),str(output),folder],text=True,capture_output=True,timeout=30,env=env)
            self.assertEqual(0,run.returncode,run.stdout+run.stderr)
            self.assertIn('PASS: 34 guardian',run.stdout)


if __name__=='__main__':unittest.main()
