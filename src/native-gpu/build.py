from pathlib import Path
import subprocess,shutil
root=Path(__file__).resolve().parents[2]
compiler=Path(r'C:\ProgramData\mingw64\mingw64\bin\g++.exe')
output=root/'build/CastleCraft.GpuBridge.dll'
subprocess.run([str(compiler),'-std=c++17','-O2','-shared','-static','-static-libgcc','-static-libstdc++','-Wl,--no-insert-timestamp',str(Path(__file__).with_name('Bridge.cpp')),'-o',str(output),'-ld3d11','-ldxgi','-lopengl32','-luuid','-lwinpthread'],check=True)
resource=root/'src/guest/src/client/resources/native/windows/CastleCraft.GpuBridge.dll'
resource.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(output,resource)
print('NATIVE GPU BUILD PASS:',output)
