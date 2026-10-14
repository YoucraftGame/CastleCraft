"""Prepare the MIT SkyCraft/PeakCraft bridge modules; keep upstream license and provenance."""
from pathlib import Path
import shutil,json,subprocess
root=Path(__file__).resolve().parents[1]
ref=Path.home()/'.codex/tools/castlecraft/PeakCraft-reference'
src=ref/'peak/src/PeakCraft'; out=root/'src/passthrough/upstream';out.mkdir(parents=True,exist_ok=True)
for folder in ('Link','Render'):
 for p in (src/folder).glob('*.cs'):
  target=out/folder/p.name;target.parent.mkdir(exist_ok=True);shutil.copyfile(p,target)
shutil.copyfile(ref/'LICENSE',out/'UPSTREAM-LICENSE')
sha=subprocess.check_output(['git','-C',str(ref),'rev-parse','HEAD'],text=True).strip()
(out/'PROVENANCE.json').write_text(json.dumps({'source':'https://github.com/aeironnsarmiento/PeakCraft','commit':sha,'license':'MIT','credits':['chasmlol / SkyCraft','aeironnsarmiento / PeakCraft'],'adaptations':['Lost Castle host adapter','2.5D coordinate projection','native built-in sprite materials','no PEAK mirrors']},indent=2))
p=out/'Render/RenderRing.cs';s=p.read_text();s=s.replace('    private readonly System.Collections.Generic.List<Mirror> mirrors = new();\n','')
start=s.index('    private void DrawInMirrors(');end=s.index('    private void Handle(',start)
s=s[:start]+'''    private void DrawInMirrors(Vector3 feet, float mcYaw, bool firstPerson)
    {
        if (firstPerson) {
            CapturedModel model = avatar.Shown ? avatar : body;
            Matrix4x4 matrix = avatar.Shown ? Matrix4x4.Translate(feet) : Matrix4x4.TRS(feet, Quaternion.Euler(0f, mcYaw, 0f), Vector3.one);
            model.Draw(matrix, null, World.Coords.RenderLayer);
        }
    }

'''+s[end:]
p.write_text(s)
p=out/'Render/Atlas.cs';s=p.read_text().replace('"Universal Render Pipeline/Particles/Simple Lit"','"Sprites/Default"').replace('"Universal Render Pipeline/Particles/Lit"','"Unlit/Transparent"').replace('"Universal Render Pipeline/Particles/Unlit"','"Particles/Standard Unlit"').replace('0.6f,','1.0f,');p.write_text(s)
for name in ('CapturedModel.cs','BlockSections.cs'):
 p=out/'Render'/name;s=p.read_text().replace('new Vector3(v->x, v->y, -v->z)','PeakCraft.World.Coords.Relative(v->x, v->y, v->z)')
 if name=='CapturedModel.cs':s=s.replace('int layer = 0)','int layer = -1)').replace('            Graphics.DrawMesh(mesh, matrix, materials[b], layer, camera','            Graphics.DrawMesh(mesh, matrix, materials[b], layer < 0 ? PeakCraft.World.Coords.RenderLayer : layer, camera')
 if name=='BlockSections.cs':s=s.replace('new GameObject(', 'new GameObject(')
 p.write_text(s)
rows=json.loads((root/'sheets/systems.json').read_text())
for row in rows:
 row['protocol']=11
 if row['id']=='transport':row['kind']='shared_memory_v11';row['description']='Upstream seqlocks, entry rings, byte rings and triple buffered transparent native overlay'
 if row['id']=='avatar':row['description']='Original Minecraft renderer captures full animated avatar, skin, armor and held-item batches'
 if row['id']=='native_gui':row['description']='Upstream full transparent native Minecraft HUD/screens, no screenshot cropping'
(root/'sheets/systems.json').write_text(json.dumps(rows,indent=2))
print('Prepared MIT upstream modules, updated sheets for actual passthrough protocol11.')
