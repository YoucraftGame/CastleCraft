"""Capture/verify immutable repair checkpoints. Never launch games or restore automatically."""
from pathlib import Path
import argparse,datetime,hashlib,json,shutil,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
CHECKPOINTS=ROOT/"checkpoints"
def digest(p):
 with p.open("rb") as f:return hashlib.file_digest(f,"sha256").hexdigest()
def read(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
def timestamp():return datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8)))
def verify(folder):
 expected=read(folder/"checksums.json")
 for name,value in expected.items():
  path=folder/name
  if not path.is_file() or digest(path)!=value:raise RuntimeError("Checkpoint mismatch: "+name)
 actual={p.relative_to(folder).as_posix() for p in folder.rglob("*") if p.is_file() and p.name!="checksums.json"}
 if actual!=set(expected):raise RuntimeError("Checkpoint file inventory changed")
 print("CHECKPOINT VERIFY PASS:",folder.name,len(expected),"files")
 return read(folder/"checkpoint.json")
def create():
 m=read(ROOT/"build/latest-deployment.json")
 source_root=Path(m.get("source_snapshot",str(ROOT)))
 if not source_root.is_dir():raise RuntimeError("Exact deployed source snapshot is unavailable")
 for a in m["artifacts"]:
  if digest(Path(a["installed"]))!=a["after_sha256"] or digest(Path(a["source"]))!=a["after_sha256"]:raise RuntimeError("Installed/staged repair mismatch")
 for name,value in m["source_hashes"].items():
  if digest(ROOT/name)!=value:raise RuntimeError("Source changed since installed build: "+name)
 # Future batches provide their exact log in the deployment plan.
 log_candidates=[p for p in (ROOT/"build").glob("*final-build.log") if digest(p)==m["build_log_sha256"]]
 if len(log_candidates)!=1:raise RuntimeError("Exact final build log not uniquely available")
 log=log_candidates[0]
 CHECKPOINTS.mkdir(exist_ok=True)
 indexpath=CHECKPOINTS/"index.json"
 index=read(indexpath) if indexpath.exists() else {"schema":1,"checkpoints":[]}
 for entry in index["checkpoints"]:
  if entry["build"]==m["build"]:
   verify(CHECKPOINTS/entry["id"]);print("Existing installed-build checkpoint retained; create a new deployed build before next repair checkpoint.");return
 identifier="CP-"+timestamp().strftime("%Y%m%d-%H%M%S")+"-"+m["build"].removeprefix("repair-")
 folder=CHECKPOINTS/identifier
 folder.mkdir()
 def copy(source,relative):
  target=folder/relative;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
 for rel in ["src/passthrough","src/guest/src","src/native-gpu","src/first-person-models","src/generated","src/deployment","tests/host","sheets","docs"]:
  source=source_root/rel if (source_root/rel).exists() else ROOT/rel
  shutil.copytree(source,folder/"project"/rel,ignore=shutil.ignore_patterns("bin","obj","build","__pycache__",".gradle"))
 for rel in ["src/build.py","src/preflight.py","src/Launcher.cs","MODLOG.md"]:
  copy(source_root/rel if (source_root/rel).exists() else ROOT/rel,"project/"+rel)
 for rel in ["build/compile_cuboid_models.py","build/create_first_person_models.py","build/preview_cuboid_models.py"]:
  if (ROOT/rel).exists():copy(ROOT/rel,"project/"+rel)
 for name in ["build.gradle","gradle.properties","settings.gradle","gradlew","gradlew.bat"]:
  f=source_root/"src/guest"/name
  if f.exists():copy(f,"project/src/guest/"+name)
 wrapper=source_root/"src/guest/gradle/wrapper"
 if wrapper.exists():shutil.copytree(wrapper,folder/"project/src/guest/gradle/wrapper")
 for a in m["artifacts"]:copy(Path(a["installed"]),"artifacts/"+a["backup_name"])
 for extra in m.get("runtime_files",[]):
  source=Path(extra["installed"])
  if digest(source)!=extra["sha256"]:raise RuntimeError("Runtime loader hash changed")
  copy(source,"runtime/"+source.name)
 for evidence in ["loader-delivery-verification.json","loader-live-verification.json","gpu-native-probe.json","vulkan-gpu-probe.json","vulkan-loader-probe.json","vulkan-explicit-icd-probe.json","vulkan-nvidia-direct-probe.json","vulkan-instance-probe.json","vulkan-registration-plan.json"]:
  source=ROOT/"build"/evidence
  if source.exists():copy(source,"evidence/"+evidence)
 copy(ROOT/"build/latest-deployment.json","evidence/installed-deployment.json")
 copy(Path(m["deployment_plan"]),"evidence/deployment-plan.json")
 copy(log,"evidence/final-build.log")
 for name in ("native-uv-clean-mixin-probe.json","native-uv-clean-mixin-probe.log","native-uv-fixtures.json","cuboid-model-verification.json", "vulkan-mixin-probe.json","vulkan-mixin-probe.log","vulkan-mixin-before.json","vulkan-mixin-before.log"):
  f=ROOT/"build"/name
  if f.exists():copy(f,"evidence/"+name)
 copy(source_root/"build/host-test-results.json","evidence/host-test-results.json")
 for f in (source_root/"src/guest/build/test-results/test").glob("TEST-*.xml"):copy(f,"evidence/guest-tests/"+f.name)
 for f in (ROOT/"build").glob("deploy-native-uv*.py"):copy(f,"evidence/"+f.name)
 for f in (ROOT/"build").glob("deploy-vulkan-gpu*.py"):copy(f,"evidence/"+f.name)
 for f in (ROOT/"build").glob("deploy-vulkan-mixin*.py"):copy(f,"evidence/"+f.name)
 configs=[]
 for i,(name,value) in enumerate(m["settings_after"].items()):
  f=Path(name)
  if digest(f)!=value:raise RuntimeError("User config changed after deployment; not falsely recording it as unchanged")
  relative="configs/"+str(i)+"-"+f.name
  copy(f,relative);configs.append({"original":name,"snapshot":relative,"sha256":value})
 bugs={
  "CC-28":{"status":"installed_gameplay_pending","sources":["src/guest/src/main/java/dev/skycraft/world/BridgeSimulation.java","src/guest/src/main/java/dev/skycraft/world/CastleMirrorLifecycle.java","src/guest/src/client/java/dev/skycraft/client/SkyClient.java","src/guest/src/client/java/dev/skycraft/client/mixin/CastleSoundMixin.java"],"tests":["CastleMirrorLifecycleTest"]},
  "CC-29":{"status":"partial_installed_gameplay_pending","sources":["src/guest/src/client/java/dev/skycraft/client/render/AvatarExporter.java","src/passthrough/CastleStage3D.cs"],"limitations":["GPU-only and shader-only mod effects not universally exported"]},
  "CC-30":{"status":"partial_installed_again_reported","sources":["src/passthrough/Plugin.cs","src/passthrough/CastleCollision.cs","src/guest/src/client/java/dev/skycraft/client/SkyClient.java"],"limitations":["full room/life/server-arrival safety required; zero loading time not guaranteed"]},
  "CC-31":{"status":"new_reproduction_unresolved","sources":["src/guest/src/client/java/dev/skycraft/client/render/AvatarExporter.java","src/guest/src/client/java/dev/skycraft/client/render/WorldExporter.java","src/passthrough/upstream/Render/RenderRing.cs","src/passthrough/upstream/Render/Atlas.cs"],"findings":["scene cap48, enumeration order","No transparent-mob-specific repair yet"]},
  "CC-32":{"status":"new_reproduction_unresolved_coordinate_audit_OPEN","sources":["src/passthrough/CastleStage3D.cs","src/passthrough/Coords.cs","src/passthrough/Plugin.cs"],"findings":["actor billboard and owner classification to audit","No new overlap/flying-scene-specific repair yet"]}
 }
 extra=ROOT/"docs/ORDERED_REPAIR_STATUS.json"
 if extra.exists():bugs.update(read(extra).get("bug_mapping",{}))
 # Preserve statuses from later separately deployed repairs, including the Guest audio fix.
 latest_entry=read(CHECKPOINTS/"latest.json") if (CHECKPOINTS/"latest.json").exists() else {}
 for key,status in latest_entry.get("bugs",{}).items():
  if key not in bugs:bugs[key]={"status":status,"sources":[],"inherited_from_checkpoint":latest_entry.get("id")}
  else:bugs[key]["status"]=status
 delta=ROOT/"docs/FIRST_PERSON_NATIVE_UV_STATUS.json"
 if delta.exists() and read(delta).get("build")==m["build"]:bugs.update(read(delta).get("bug_mapping",{}))
 delta=ROOT/"docs/HUNTER_STATE_REPAIR_STATUS.json"
 if delta.exists() and read(delta).get("build")==m["build"]:bugs.update(read(delta).get("bug_mapping",{}))
 delta=ROOT/"docs/HUNTER_CHOICE_STATUS.json"
 if delta.exists() and read(delta).get("build")==m["build"]:bugs.update(read(delta).get("bug_mapping",{}))

 cp={"schema":1,"id":identifier,"created_at":timestamp().isoformat(),"build":m["build"],"parent_checkpoint":index["checkpoints"][-1]["id"] if index["checkpoints"] else None,"previous_deployment":m["previous_manifest"],"artifacts":m["artifacts"],"validation":m["validation"],"gameplay_verified":False,"bug_mapping":bugs,"preserved_prior_contracts":"project/docs/BUG_STATUS.md and CONTRACT.md","user_configs":configs,"other_mods":m["original_mods"],"source_hashes":m["source_hashes"],"runtime_files":m.get("runtime_files",[]),"repair_scope":m["repair_scope"],"pending_gameplay":m["pending_gameplay"],"not_completed":m["not_completed"],"restore_policy":"Artifacts only through explicit -Apply; source snapshot for reviewed restoration, no automatic source/config/save rollback."}
 write(folder/"checkpoint.json",cp)
 restore=ROOT/"src/deployment/Restore-Checkpoint-Artifacts.ps1"
 copy(restore,"Restore-Artifacts.ps1")
 (folder/"README.md").write_text("# "+identifier+"\n\nInstalled build: "+m["build"]+"\n\nAutomated checks passed; gameplay not accepted. CC-31/32 remain partial and need retail acceptance. No game saves or account credentials are included.\n\nVerify with project/src/deployment/Checkpoint.py --verify <this directory>. Restore-Artifacts.ps1 previews by default; -Apply explicitly backs up current artifacts and restores only checkpoint-listed DLL/JAR/native GPU artifacts. It never launches/kills games. Source/config snapshots require a separately reviewed restore and must not erase later repaired code.\n",encoding="utf-8")
 write(folder/"checksums.json",{p.relative_to(folder).as_posix():digest(p) for p in sorted(folder.rglob("*")) if p.is_file() and p.name!="checksums.json"})
 verify(folder)
 entry={"id":identifier,"build":m["build"],"created_at":cp["created_at"],"parent":cp["parent_checkpoint"],"bugs":{k:v["status"] for k,v in bugs.items()},"manifest_sha256":digest(folder/"checkpoint.json"),"checksums_sha256":digest(folder/"checksums.json"),"gameplay_verified":False}
 index["checkpoints"].append(entry);write(indexpath,index);write(CHECKPOINTS/"latest.json",entry)
 print("CHECKPOINT SAVED:",folder)
def main():
 parser=argparse.ArgumentParser()
 g=parser.add_mutually_exclusive_group(required=True);g.add_argument("--create",action="store_true");g.add_argument("--verify");g.add_argument("--bug");g.add_argument("--list",action="store_true")
 args=parser.parse_args()
 if args.create:create()
 elif args.verify:verify(Path(args.verify).resolve())
 else:
  index=read(CHECKPOINTS/"index.json")
  for e in index["checkpoints"]:
   if args.list or args.bug in e["bugs"]:print(json.dumps(e,ensure_ascii=False))
if __name__=="__main__":main()
