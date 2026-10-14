"""Download redistributable Fabric runtime from Fabric's official metadata."""
import json,urllib.request,pathlib,hashlib
root=pathlib.Path(__file__).resolve().parents[1]
target=root/'build'/'fabric';target.mkdir(exist_ok=True)
metadata=json.load(urllib.request.urlopen('https://meta.fabricmc.net/v2/versions/loader/26.3/0.19.5/profile/json'))
records=[]
for lib in metadata['libraries']:
 group,name,version=lib['name'].split(':')
 path=f'{group.replace(".","/")}/{name}/{version}/{name}-{version}.jar'
 url=lib.get('url','https://maven.fabricmc.net/')+path
 out=target/f'{name}-{version}.jar'
 if not out.exists():urllib.request.urlretrieve(url,out)
 expected=urllib.request.urlopen(url+'.sha1').read().decode().strip().split()[0]
 assert hashlib.sha1(out.read_bytes()).hexdigest()==expected,(url,'checksum mismatch')
 records.append({'name':lib['name'],'url':url,'sha256':hashlib.sha256(out.read_bytes()).hexdigest()})
(target/'provenance.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
print('Verified official Fabric runtime jars:',len(records))
