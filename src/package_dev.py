"""Package only mod code, redistributable dependencies and explicit documentation."""
from pathlib import Path
import shutil, json, hashlib, zipfile, re

root=Path(__file__).resolve().parents[1]
version='0.3.0-dev.20261012'
package=root/'dist'/('CastleCraft-'+version+'-normal-client')
package.mkdir(parents=True,exist_ok=False)
def copy(source,target):
    target=package/target
    target.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(source,target)
copy(root/'build/CastleCraft.Passthrough.dll','LostCastle/BepInEx/plugins/CastleCraft/CastleCraft.Passthrough.dll')
for name in ('skycraft-0.1.2.jar','fabric-api-0.161.0+26.3.jar'):
    copy(root/'build'/name,'Minecraft/mods/'+name)
    copy(root/'build'/name,'Launcher/'+name)
copy(Path((root/'build/latest-launcher.txt').read_text(encoding='utf-8-sig').strip()),'Launcher/CastleCraftLauncher.exe')
copy(Path.home()/'.codex/tools/castlecraft/BepInEx-5.4.23.5.zip','Dependencies/BepInEx-5.4.23.5.zip')
for source in (root/'src/deployment').glob('*.ps1'):copy(source,source.name)
for name in ('安装部署教程.md','正常PCL客户端安装.md','已知问题.md'):copy(root/'docs'/name,name)
copy(root/'LICENSE','LICENSE')
copy(root/'src/passthrough/upstream/UPSTREAM-LICENSE','LICENSE-SkyCraft-MIT')
copy(root/'src/passthrough/upstream/PROVENANCE.json','UPSTREAM-PROVENANCE.json')
(package/'README.md').write_text('''# CastleCraft 开发版\n\n真实 Minecraft Java 26.3 与 Lost Castle 的共享内存联动开发快照。\n\n日常用法：将桥接 jar 安装进正常客户端，普通启动 PCL 和城堡，再按 F8；详见《正常PCL客户端安装.md》。当前仍有《已知问题.md》列出的游戏问题，不能视为完整兼容版。\n\n安装/其他客户端/自定义按键/卸载见《安装部署教程.md》。自有代码 MIT，允许改编，保留上游署名。由 OpenAI Codex 协助开发。基于 chasmlol/SkyCraft 与 aeironnsarmiento/PeakCraft（MIT）。未使用生成美术或音频；游戏资源仍由用户自己的安装提供。\n\n第三方：BepInEx 5.4.23.5（及其附带组件）与 Fabric API 0.161.0+26.3 保留原发行包和内部许可证，不属于我们单独重新授权的代码。官方来源：https://github.com/BepInEx/BepInEx 、https://github.com/FabricMC/fabric 。\n''',encoding='utf-8')
manifest={'name':'CastleCraft','version':version,'stage':'development','host_plugin_version':'0.3.0','guest_mod_id':'skycraft','guest_upstream_version':'0.1.2','games':{'LostCastle':'Unity Mono 2021.3.25f1 inspected build','MinecraftJava':'26.3'},'dependencies':{'BepInEx':'5.4.23.5 x64 Mono','FabricLoader':'0.19.5','FabricAPI':'0.161.0+26.3','Java':'25'},'license':'MIT for original bridge code; retain dependency licenses','validation':{'build':'passed','upstream_collision_raycast_tests':21,'normal_client_mod_combination':'not yet played','all_gameplay_mechanisms':'incomplete; see known issues'},'default_mapping':r'Local\CastleCraft_v3_manual'}
(package/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')

source_files=[]
for folder in ('src/passthrough','src/guest/src','src/guest/gradle','src/generated','src/deployment','sheets'):
    for p in (root/folder).rglob('*'):
        if p.is_file() and not any(part in ('bin','obj','build','.gradle') for part in p.relative_to(root).parts):source_files.append(p)
for name in ('LICENSE','.gitignore','src/build.py','src/preflight.py','src/Launcher.cs','src/package_dev.py','src/guest/build.gradle','src/guest/settings.gradle','src/guest/gradle.properties','src/guest/gradlew','src/guest/gradlew.bat','src/guest/LICENSE','docs/CONTRACT.md','docs/COORDINATES.md','docs/安装部署教程.md','docs/正常PCL客户端安装.md','docs/已知问题.md','docs/INTEGRATION_CHECKLIST.md'):
    p=root/name
    if p.exists():source_files.append(p)
def inspect_text(path):
    if path.suffix.lower() not in ('.dll','.exe','.jar','.zip','.png'):
        data=path.read_bytes()
        if re.search(rb'llc_[A-Za-z0-9_-]{12,}|sk-(?:proj-)?[A-Za-z0-9_-]{24,}|ghp_[A-Za-z0-9]{20,}',data):raise RuntimeError('Potential credential in '+str(path))
for p in source_files:inspect_text(p)
for p in package.rglob('*'):
    if p.is_file():inspect_text(p)
for p in (package/'Minecraft/mods/skycraft-0.1.2.jar',):
    with zipfile.ZipFile(p) as z:
        assert not any(n.startswith(('net/minecraft/','com/mojang/')) and n.endswith('.class') for n in z.namelist()),'Retail classes in mod jar'
        json.loads(z.read('fabric.mod.json').decode('utf-8-sig'))
        assert any(n.startswith('LICENSE') for n in z.namelist()),'Missing guest MIT notice'
hashes=[]
for p in sorted(package.rglob('*')):
    if p.is_file():hashes.append(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.relative_to(package).as_posix())
(package/'SHA256SUMS.txt').write_text('\n'.join(hashes)+'\n',encoding='utf-8')
mod_zip=root/'dist'/('CastleCraft-mod-'+version+'.zip')
with zipfile.ZipFile(mod_zip,'w',zipfile.ZIP_DEFLATED) as z:
    for p in package.rglob('*'):
        if p.is_file():z.write(p,p.relative_to(package).as_posix())
source_zip=root/'dist'/('CastleCraft-source-'+version+'.zip')
with zipfile.ZipFile(source_zip,'w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(set(source_files)):z.write(p,'CastleCraft/'+p.relative_to(root).as_posix())
    z.write(package/'README.md','CastleCraft/README.md')
for p in (mod_zip,source_zip):
    with zipfile.ZipFile(p) as z:assert z.testzip() is None
    print(str(p),p.stat().st_size,'bytes',hashlib.sha256(p.read_bytes()).hexdigest())
print('PACKAGE CHECK PASS: explicit files, credential patterns, no retail classes, ZIP integrity and checksums.')
