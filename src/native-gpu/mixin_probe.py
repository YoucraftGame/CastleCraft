"""Use the user's retail classpath to transform Vulkan classes without launching Minecraft."""
from pathlib import Path
import argparse, hashlib, json, re, subprocess
ROOT = Path(__file__).resolve().parents[2]
MC = Path(r'D:\新版客户端\.minecraft\versions\Fabulously Optimized')
JAVA = Path(r'C:\Program Files\Java\jdk-25.0.2\bin')
parser=argparse.ArgumentParser()
parser.add_argument('--jar',type=Path,default=ROOT/'build/skycraft-0.1.2.jar')
parser.add_argument('--label',default='vulkan-mixin-probe')
options=parser.parse_args()
out = ROOT/'build/mixin-probe'
out.mkdir(exist_ok=True)
(out/'game/mods').mkdir(parents=True,exist_ok=True)
script = MC/'mods/错误报告-2026-10-14_6.04.35/启动脚本.bat'
# Extract only classpath; never replay launch arguments or account credentials.
cp = re.search(r'(?:-cp|-classpath)\s+"([^"]+)"', script.read_text(encoding='utf-8-sig'))[1]
source = ROOT/'src/native-gpu/MixinLoadProbe.java'
subprocess.run([str(JAVA/'javac.exe'), '-cp', cp, '-d', str(out), str(source)], check=True)
mods = [p for p in sorted((MC/'mods').glob('*.jar')) if p.name != 'skycraft-0.1.2.jar']
mods.append(options.jar)
args = ['-Dfabric.addMods='+';'.join(map(str,mods)), '-Dfabric.log.disableAnsi=true',
        '-cp', str(out)+';'+cp, 'MixinLoadProbe', str(out/'game')]
argfile=out/'args.txt'
argfile.write_text('\n'.join('"'+a.replace('\\','/').replace('"','\\"')+'"' for a in args),encoding='utf-8')
result=subprocess.run([str(JAVA/'java.exe'),'@'+str(argfile)],cwd=out,capture_output=True,text=True,encoding='utf-8',errors='replace')
(ROOT/('build/'+options.label+'.log')).write_text(result.stdout+'\n'+result.stderr,encoding='utf-8')
passed=result.returncode==0 and 'MIXIN PROBE PASS:' in result.stdout
report={'passed':passed,'retail_mod_count':len(mods),'game_launched':False,
        'jar_sha256':hashlib.sha256(options.jar.read_bytes()).hexdigest(),
        'checks':[line.split('MIXIN LOAD PASS: ',1)[1].strip().split('.')[-1] for line in result.stdout.splitlines() if 'MIXIN LOAD PASS: ' in line] if passed else []}
(ROOT/('build/'+options.label+'.json')).write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report))
if not passed:
 print((result.stdout+'\n'+result.stderr)[-9000:]);raise SystemExit(1)
