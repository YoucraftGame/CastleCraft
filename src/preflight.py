"""Validate all sheet cells/references, then generate one class for each row."""
import json, pathlib, re
root=pathlib.Path(__file__).resolve().parents[1]
schemas={
 'rewards':{'id':str,'event':str,'source':str,'target':str,'threshold':int,'amount':int,'cap':int,'label':str},
 'hooks':{'id':str,'game':str,'type':str,'method':str,'phase':str,'guard':str},
 'systems':{'id':str,'kind':str,'pollMs':int,'batch':int,'maxBytes':int,'queueCap':int,'protocol':int,'description':str},
 'versions':{'id':str,'version':str,'runtime':str,'loader':str,'license':str,'requirement':str}}
tables={}; errors=[]; cells=0
for name,schema in schemas.items():
 rows=json.loads((root/'sheets'/f'{name}.json').read_text()); tables[name]=rows
 seen=set()
 for row in rows:
  if row.get('id') in seen: errors.append(f'{name}: duplicate id {row.get("id")}')
  seen.add(row.get('id'))
  if set(row)!=set(schema): errors.append(f'{name}/{row.get("id")}: missing or extra columns')
  for key,typ in schema.items():
   value=row.get(key); cells+=1
   if type(value)!=typ or (typ==str and not value.strip()) or (typ==int and value<=0): errors.append(f'{name}/{row.get("id")}/{key}: unfilled or invalid')
ids={x['id'] for x in tables['hooks']}
for row in tables['rewards']:
 for key in ('source','target'):
  if row[key] not in ids: errors.append(f'rewards/{row["id"]}/{key}: unresolved {row[key]}')
for row in tables['hooks']:
 if row['game'] not in {x['id'] for x in tables['versions']}: errors.append(f'hooks/{row["id"]}: unresolved game')
if errors: raise SystemExit('\n'.join(errors))
out=root/'src'/'generated'; out.mkdir(exist_ok=True)
cs=['// Generated from sheets. Edit sheets first.','namespace CastleCraft.Rows {']
java=['// Generated from sheets. Edit sheets first.','package castlecraft;','public final class Rows {']
for name,rows in tables.items():
 for row in rows:
  cls=''.join(x.title() for x in (name+'_'+row['id']).split('_'))
  cs.append('public static class '+cls+' {');java.append('public static final class '+cls+' {')
  for key,value in row.items():
   typ='int' if type(value)==int else 'string'; jtyp='int' if type(value)==int else 'String'
   literal=str(value) if type(value)==int else json.dumps(value,ensure_ascii=False)
   cs.append(f'public const {typ} @{key} = {literal};');java.append(f'public static final {jtyp} {key} = {literal};')
  cs.append('}');java.append('}')
cs.append('}');java.append('}')
(out/'Rows.cs').write_text('\n'.join(cs),encoding='utf-8');(out/'Rows.java').write_text('\n'.join(java),encoding='utf-8')
print(f'PREFLIGHT PASS: {sum(map(len,tables.values()))} rows, {cells} cells, all references resolve.')
