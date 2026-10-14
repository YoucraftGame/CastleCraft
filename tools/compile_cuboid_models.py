"""Validate original resource models and compile their exact cuboids/palettes/native bone bindings."""
from pathlib import Path
import json,hashlib
r=Path(__file__).resolve().parents[1]
pack=r/'src/first-person-models'
models=pack/'assets/castlecraft/models/parts'
manifest=json.loads((pack/'actor-bindings.json').read_text());bindings=manifest['actors']
data={p.stem:json.loads(p.read_text()) for p in sorted(models.glob('*.json'))}
colors={}
from PIL import Image
for p in (pack/'assets/castlecraft/textures/palette').glob('*.png'):
 im=Image.open(p);assert im.size==(16,16)
 # The procedural pattern's most frequent color is its exact base.
 colors[p.stem]=max(im.getcolors(256),key=lambda x:x[0])[1]
lines=['// Generated from first-person-models. Run build/compile_cuboid_models.py.',
'using System;','using System.Collections.Generic;','namespace PeakCraft.World;',
'internal static class CastleCuboidLibrary {',
'internal static readonly Dictionary<string,CastleCuboid[]> Models=new(StringComparer.Ordinal){']
count=0
for name,m in data.items():
 lines.append('{'+json.dumps(name)+',new CastleCuboid[]{')
 for e in m['elements']:
  a,b=e['from'],e['to'];assert len(a)==len(b)==3 and all(-16<=a[i]<b[i]<=32 for i in range(3))
  dims=[b[i]-a[i] for i in range(3)]
  assert set(e['faces'])=={'north','south','east','west','up','down'}
  palettes=[]
  for face,(w,h) in zip(('north','south','east','west','up','down'),[(dims[0],dims[1])]*2+[(dims[2],dims[1])]*2+[(dims[0],dims[2])]*2):
   f=e['faces'][face];assert f['uv']==[0,0,w,h], 'Face must retain one texel per model unit: '+name
   p=m['textures'][f['texture'][1:]].split('/')[-1];assert p in colors;palettes.append(p)
  assert len(set(palettes))==1, 'Current compiled element supports one palette on all faces'
  values=','.join(str(float(v))+'f' for v in a+b)
  lines.append('new CastleCuboid('+values+','+json.dumps(palettes[0])+'),');count+=1
 lines.append('}},')
lines+=['};','internal static string? Binding(string actor,string part){','int suffix=actor.IndexOf("(Clone)",StringComparison.Ordinal);if(suffix>=0)actor=actor.Substring(0,suffix).TrimEnd();','switch(actor){']
for actor,parts in bindings.items():
 lines.append('case '+json.dumps(actor)+': switch(part){')
 for part,model in parts.items():
  assert model in data
  lines.append('case '+json.dumps(part)+': return '+json.dumps(model)+';')
 lines.append('} break;')
lines+=['}return null;}','internal static bool Suppress(string actor,string part){','int suffix=actor.IndexOf("(Clone)",StringComparison.Ordinal);if(suffix>=0)actor=actor.Substring(0,suffix).TrimEnd();','switch(actor){']
for actor,parts in manifest['replaced_overlays'].items():
 lines.append('case '+json.dumps(actor)+': return '+ '||'.join('part=='+json.dumps(p) for p in parts)+';')
lines+=['}return false;}','internal static uint Color(string palette)=>palette switch {']
for name,(red,green,blue,alpha) in sorted(colors.items()):
 assert alpha==255
 lines.append(json.dumps(name)+'=>0x'+format((red<<16)|(green<<8)|blue,'06X')+'u,')
lines+=['_=>0xFF00FFu,','};','}']
p=r/'src/passthrough/GeneratedCastleCuboids.cs';p.write_text('\n'.join(lines)+'\n',encoding='utf-8')
report={'models':len(data),'elements':count,'native_root_bindings':len(bindings),'bone_bindings':sum(map(len,bindings.values())),'face_uv_pixel_density_verified':True,'gameplay_verified':False,'generated_sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'models_sha256':{str(p.relative_to(pack)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(pack.rglob('*')) if p.is_file()}}
(r/'build/cuboid-model-verification.json').write_text(json.dumps(report,indent=2)+'\n')
print('CUBOID COMPILE PASS:',len(data),'models,',count,'elements,',sum(map(len,bindings.values())),'native bones; actual game pending')
