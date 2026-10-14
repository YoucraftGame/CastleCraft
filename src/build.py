"""Build the active integration; installation and gameplay checks are separate."""
from pathlib import Path
import subprocess, shutil, os, datetime
root = Path(__file__).resolve().parents[1]
out = root / 'build'
out.mkdir(exist_ok=True)
subprocess.run(['python',str(root/'build/compile_cuboid_models.py')],check=True)
subprocess.run(['python',str(root/'src/native-gpu/build.py')],check=True)
subprocess.run(['python',str(root/'src/native-gpu/probe.py')],check=True)
subprocess.run(['python', str(root / 'src/preflight.py')], check=True)
dotnet = Path.home() / '.codex/tools/castlecraft/dotnet/dotnet.exe'
subprocess.run([str(dotnet), 'run', '--project', str(root / 'tests/host/CastleCraft.HostTests.csproj'), '-c', 'Release', '--', str(out / 'host-test-results.json')], check=True)
subprocess.run([str(dotnet), 'build', str(root / 'src/passthrough/CastleCraft.Passthrough.csproj'), '-c', 'Release', '--nologo'], check=True)
shutil.copy2(root / 'src/passthrough/bin/Release/netstandard2.1/CastleCraft.Passthrough.dll', out)
env = os.environ.copy()
env['JAVA_HOME'] = r'C:\Program Files\Java\jdk-25.0.2'
subprocess.run(['cmd', '/c', 'gradlew.bat', 'build', '--no-daemon', '--console', 'plain'], cwd=root / 'src/guest', env=env, check=True)
shutil.copy2(root / 'src/guest/build/libs/skycraft-0.1.2.jar', out)
launcher = out / ('CastleCraftLauncher-' + datetime.datetime.now(datetime.UTC).strftime('%H%M%S') + '.exe')
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe', '/nologo', '/target:winexe', '/optimize+', '/reference:System.Windows.Forms.dll', '/reference:System.Core.dll', f'/out:{launcher}', str(root / 'src/Launcher.cs')], check=True)
(out / 'latest-launcher.txt').write_text(str(launcher), encoding='utf-8')
print('BUILD PASS: active host, guest and launcher. Gameplay verification remains separate.')
