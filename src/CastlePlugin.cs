using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using CastleCraft.Rows;

namespace CastleCraft {
[Serializable] public class Pixels { public string id,rgba; public int width,height,x,y,frameWidth,frameHeight; }
[Serializable] public class HotbarItem { public string id;public int count; }
[Serializable] public class BlockGroup { public string texture;public float[] vertices; }
[Serializable] public class GuestBlock { public int x,y,z;public string name;public BlockGroup[] groups; }
[Serializable] public class GuestPacket { public int protocol,selected,mined,placed;public bool ready,visiting,inventory;public float x,y,z,yaw,health,guiScale;public float[] vertices;public Pixels skin;public Pixels[] textures;public HotbarItem[] hotbar;public GuestBlock[] blocks; }
[Serializable] public class Controls { public bool visiting,forward,backward,left,right,jump,sneak,sprint,mine;public float targetX,targetY,targetZ,yaw=180,mouseX,mouseY;public int slot=-1,placeSeq,inventorySeq,clickSeq,mouseButton; }
[BepInPlugin("castlecraft.bridge", "CastleCraft", "0.2.0")]
public class CastlePlugin : BaseUnityPlugin {
 static CastlePlugin instance;
 string session,status="等待 Minecraft 后台就绪";
 Type heroType;
 float nextPoll;
 bool visiting,windowSet;
 Vector3 anchor;
 Vector3 guestOrigin;
 float enterGuestX,enterGuestZ;
 int originalHealth,lastProxyHealth,placeSeq,selected,inventorySeq,clickSeq;
 GuestPacket packet;
 GameObject avatarRoot,blocksRoot;
 MeshFilter avatar;
 readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
 readonly Dictionary<string,GameObject> blockObjects=new Dictionary<string,GameObject>();
 Renderer[] nativeRenderers;
 Font font;
 Texture2D nativeGui;
 Pixels guiData;
 Camera guestCamera;
 DateTime guiUpdated;
 Controls controls=new Controls();
 static object Field(object o,string k){return AccessTools.Field(o.GetType(),k).GetValue(o);}
 static object Prop(object o,string k){return AccessTools.Property(o.GetType(),k).GetValue(o,null);}
 static void SetProp(object o,string k,object v){AccessTools.Property(o.GetType(),k).SetValue(o,v,null);}
 Component Hero(){if(heroType==null)return null;var heroes=Resources.FindObjectsOfTypeAll(heroType).Cast<Component>().Where(x=>x.gameObject.activeInHierarchy).ToArray();return heroes.Length==1?heroes[0]:null;}
 void Awake(){
  instance=this;session=Environment.GetEnvironmentVariable("CASTLECRAFT_SESSION");if(string.IsNullOrEmpty(session)){enabled=false;return;}
  Directory.CreateDirectory(session);heroType=AccessTools.TypeByName("Teran.Hero");
  new Harmony("castlecraft.bridge").Patch(AccessTools.Method(heroType,"HandleInput"),prefix:new HarmonyMethod(typeof(CastlePlugin),"NativeInput"));
  Application.runInBackground=true;font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},16);
  avatarRoot=new GameObject("CastleCraft Live Minecraft Player");avatarRoot.layer=30;DontDestroyOnLoad(avatarRoot);avatar=avatarRoot.AddComponent<MeshFilter>();avatarRoot.AddComponent<MeshRenderer>();avatarRoot.SetActive(false);
  blocksRoot=new GameObject("CastleCraft Live Minecraft Blocks");DontDestroyOnLoad(blocksRoot);blocksRoot.SetActive(false);
  Log("host-loaded v2: reward systems removed");
 }
 static bool NativeInput(){return instance==null||!instance.visiting;}
 void Log(string s){File.AppendAllText(Path.Combine(session,"host-visitor.log"),DateTime.UtcNow.ToString("O")+" "+s+Environment.NewLine);}
 void Atomic(string name,string s){string path=Path.Combine(session,name),tmp=path+".tmp";File.WriteAllText(tmp,s);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}
 bool Online(){string p=Path.Combine(session,"guest.json");return File.Exists(p)&&(DateTime.UtcNow-File.GetLastWriteTimeUtc(p)).TotalSeconds<2;}
 void Toggle(){
  var hero=Hero();if(visiting){Leave();return;}
  if(hero==null||packet==null||!packet.ready||!Online()){status="需要城堡角色与 Minecraft 后台都已就绪";return;}
  anchor=hero.transform.position;guestOrigin=new Vector3(packet.x,251,packet.z);enterGuestX=packet.x;enterGuestZ=packet.z;var hp=Field(hero,"propertiesInspector");originalHealth=(int)Prop(hp,"CurrentHp");lastProxyHealth=originalHealth;
  var keyup=AccessTools.Method(heroType,"HandleInputKeyUp");if(keyup!=null){var type=keyup.GetParameters()[0].ParameterType;foreach(var key in Enum.GetValues(type))try{keyup.Invoke(hero,new object[]{key});}catch{}}
  nativeRenderers=hero.GetComponentsInChildren<Renderer>();var sprite=hero.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault();if(sprite!=null){avatarRoot.GetComponent<MeshRenderer>().sortingLayerID=sprite.sortingLayerID;avatarRoot.GetComponent<MeshRenderer>().sortingOrder=sprite.sortingOrder+1;}foreach(var r in nativeRenderers)r.enabled=false;
  visiting=true;avatarRoot.SetActive(true);blocksRoot.SetActive(true);status="Minecraft 角色已进入城堡";Log("enter guest: native Minecraft movement/mining/placement");
 }
 void Leave(){
  visiting=false;controls=new Controls();foreach(var r in nativeRenderers??new Renderer[0])if(r!=null)r.enabled=true;
  var hero=Hero();if(hero!=null){hero.transform.position=anchor;if(originalHealth>0)SetProp(Field(hero,"propertiesInspector"),"CurrentHp",originalHealth);}
  avatarRoot.SetActive(false);blocksRoot.SetActive(false);status="已切回失落城堡角色";Log("return host: native controls restored");
 }
 void Update(){
  if(!windowSet&&Time.realtimeSinceStartup>5){Screen.SetResolution(960,600,false);windowSet=true;}
  if(Input.GetKeyDown(KeyCode.F8))Toggle();
  if(visiting){
   if(!Online()){Leave();status="Minecraft 后台中断，已恢复城堡操作";}
   controls.visiting=visiting;controls.forward=Input.GetKey(KeyCode.W);controls.backward=Input.GetKey(KeyCode.S);controls.left=Input.GetKey(KeyCode.D);controls.right=Input.GetKey(KeyCode.A);controls.jump=Input.GetKey(KeyCode.Space);controls.sneak=Input.GetKey(KeyCode.LeftShift);controls.sprint=Input.GetKey(KeyCode.LeftControl);controls.mine=Input.GetMouseButton(0);
   if(Input.GetKeyDown(KeyCode.E)||Input.GetKeyDown(KeyCode.Escape))inventorySeq++;controls.inventorySeq=inventorySeq;
   if(Input.GetMouseButtonDown(0)||Input.GetMouseButtonDown(1)){clickSeq++;controls.mouseButton=Input.GetMouseButtonDown(1)?1:0;}controls.clickSeq=clickSeq;controls.mouseX=Input.mousePosition.x/Screen.width;controls.mouseY=1-Input.mousePosition.y/Screen.height;
   for(int i=0;i<9;i++)if(Input.GetKeyDown(KeyCode.Alpha1+i))selected=i;
   controls.slot=selected;if(Input.GetMouseButtonDown(1))placeSeq++;controls.placeSeq=placeSeq;
   var hero=Hero();var cam=Camera.main;if(hero!=null&&cam!=null){Vector3 screen=Input.mousePosition;screen.z=cam.WorldToScreenPoint(anchor).z;var p=cam.ScreenToWorldPoint(screen);controls.targetX=p.x-anchor.x+guestOrigin.x;controls.targetZ=-(p.y-anchor.y)+guestOrigin.z;controls.targetY=251;}
  }else controls=new Controls();
  if(Time.realtimeSinceStartup<nextPoll)return;nextPoll=Time.realtimeSinceStartup+SystemsTransport.pollMs/1000f;
  try{
   if(Online()){
    string path=Path.Combine(session,"guest.json");if(new FileInfo(path).Length<=SystemsTransport.maxBytes){using(var fs=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(fs)){packet=JsonUtility.FromJson<GuestPacket>(reader.ReadToEnd());}if(packet.protocol!=2)throw new InvalidOperationException("Guest protocol mismatch");}
    if(packet!=null){UpdateAssets();if(visiting){UpdateBlocks();if(packet.ready&&packet.health<=0){Leave();status="Minecraft 角色倒下，已切回城堡";}}else status=packet.ready&&packet.health>0?"Minecraft 模型已载入；角色切换仍在测试":"Minecraft 后台正在准备场景";}
   }
   if(Environment.GetEnvironmentVariable("CASTLECRAFT_TEST")=="1")TestCommand();
   string gui=Path.Combine(session,"native-gui.json");if(visiting&&File.Exists(gui)&&File.GetLastWriteTimeUtc(gui)>guiUpdated){Pixels data;using(var fs=new FileStream(gui,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(fs)){data=JsonUtility.FromJson<Pixels>(reader.ReadToEnd());}guiData=data;if(nativeGui==null||nativeGui.width!=data.width||nativeGui.height!=data.height)nativeGui=new Texture2D(data.width,data.height,TextureFormat.RGBA32,false);nativeGui.LoadRawTextureData(Convert.FromBase64String(data.rgba));nativeGui.Apply();guiUpdated=File.GetLastWriteTimeUtc(gui);}
   Atomic("input.json",JsonUtility.ToJson(controls));
   Atomic("castle.status","visitor="+visiting+"\nhero="+(Hero()!=null)+"\nmesh_vertices="+(packet==null?0:packet.vertices.Length/5)+"\nblocks="+blockObjects.Count+"\nscene="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
  }catch(Exception e){Logger.LogWarning("CastleCraft v2: "+e.GetType().Name+" "+e.Message);}
 }
 void LateUpdate(){
  if(!visiting||packet==null)return;var hero=Hero();if(hero==null){Leave();return;}
  var main=Camera.main;if(main!=null){if(guestCamera==null){var go=new GameObject("CastleCraft Guest Render Pass");DontDestroyOnLoad(go);guestCamera=go.AddComponent<Camera>();}guestCamera.CopyFrom(main);guestCamera.transform.position=main.transform.position;guestCamera.transform.rotation=main.transform.rotation;guestCamera.cullingMask=1<<30;guestCamera.clearFlags=CameraClearFlags.Depth;guestCamera.depth=main.depth+10;guestCamera.enabled=true;}
  Vector3 delta=new Vector3(packet.x-guestOrigin.x,-(packet.z-guestOrigin.z),0);if(delta.magnitude>3){Leave();status="已到城堡区域边界，恢复原角色";return;}Vector3 floor=anchor+delta;hero.transform.position=floor;
  avatarRoot.transform.position=floor+new Vector3(0,packet.y-251, -0.4f);avatarRoot.transform.rotation=Quaternion.Euler(0,180-packet.yaw,0);
  var hp=Field(hero,"propertiesInspector");int current=(int)Prop(hp,"CurrentHp");
  if(current<lastProxyHealth){int damage=lastProxyHealth-current;Atomic("damage.json","{\"amount\":"+(damage*20f/Math.Max(1,(int)Prop(hp,"maxHp"))).ToString(System.Globalization.CultureInfo.InvariantCulture)+"}");}
  lastProxyHealth=Math.Max(1,(int)Math.Round(packet.health/20f*(int)Prop(hp,"maxHp")));SetProp(hp,"CurrentHp",lastProxyHealth);
 }
 Material MaterialFor(string id,Pixels pixels){
  Material material;if(materials.TryGetValue(id,out material))return material;
  var texture=new Texture2D(pixels.width,pixels.height,TextureFormat.RGBA32,false);texture.LoadRawTextureData(Convert.FromBase64String(pixels.rgba));texture.Apply();texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;
  var shader=Shader.Find("Sprites/Default");if(shader==null)throw new InvalidOperationException("Native sprite shader unavailable");material=new Material(shader);material.mainTexture=texture;material.renderQueue=3000;materials[id]=material;return material;
 }
 Mesh MeshFor(float[] values){
  int count=values.Length/5;var mesh=new Mesh();var vertices=new Vector3[count];var uvs=new Vector2[count];var triangles=new int[count];
  // Raw Minecraft pixels retain their top-first row order. Preserve native model UVs.
  for(int i=0;i<count;i++){vertices[i]=new Vector3(values[i*5],values[i*5+1],values[i*5+2]);uvs[i]=new Vector2(values[i*5+3],values[i*5+4]);triangles[i]=i;}
  mesh.vertices=vertices;mesh.uv=uvs;mesh.colors=Enumerable.Repeat(Color.white,count).ToArray();mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
 }
 void UpdateAssets(){
  if(packet.skin==null||packet.vertices==null)return;
  foreach(var tex in packet.textures??new Pixels[0])MaterialFor(tex.id,tex);
  avatarRoot.GetComponent<MeshRenderer>().material=MaterialFor("player-skin",packet.skin);
  var old=avatar.sharedMesh;avatar.sharedMesh=MeshFor(packet.vertices);if(old!=null)Destroy(old);
 }
 void UpdateBlocks(){
  var wanted=new HashSet<string>();
  foreach(var b in packet.blocks??new GuestBlock[0]){
   string key=b.x+","+b.y+","+b.z;wanted.Add(key);GameObject obj;
   if(!blockObjects.TryGetValue(key,out obj)){
    obj=new GameObject("Minecraft "+b.name);obj.layer=30;obj.transform.parent=blocksRoot.transform;
    foreach(var group in b.groups){Material mat;if(!materials.TryGetValue(group.texture,out mat))continue;var child=new GameObject(group.texture);child.layer=30;child.transform.parent=obj.transform;child.AddComponent<MeshFilter>().mesh=MeshFor(group.vertices);child.AddComponent<MeshRenderer>().material=mat;}
    blockObjects[key]=obj;
   }
   obj.transform.position=anchor+new Vector3(b.x-guestOrigin.x,-(b.z-guestOrigin.z)+(b.y-251),-0.3f);
  }
  foreach(var key in blockObjects.Keys.ToArray())if(!wanted.Contains(key)){Destroy(blockObjects[key]);blockObjects.Remove(key);}
 }
 void TestCommand(){
  string path=Path.Combine(session,"host.command");if(!File.Exists(path))return;string command=File.ReadAllText(path).Trim();File.Delete(path);
  if(command=="toggle")Toggle();
  if(command=="start-solo"){var type=AccessTools.TypeByName("Teran.MainMenu");var menus=Resources.FindObjectsOfTypeAll(type).Cast<Component>().Where(x=>x.gameObject.activeInHierarchy).ToArray();if(menus.Length>0){AccessTools.Method(type,"SinglePlayer").Invoke(menus[0],null);Log("TEST actual SinglePlayer menu action");}}
 }
 void OnGUI(){
  if(string.IsNullOrEmpty(session))return;var style=new GUIStyle(GUI.skin.label);style.font=font;style.fontSize=15;
  if(visiting&&packet!=null&&nativeGui!=null&&guiData!=null&&guiData.frameWidth>0){GUI.DrawTextureWithTexCoords(new Rect((float)guiData.x/guiData.frameWidth*Screen.width,(float)guiData.y/guiData.frameHeight*Screen.height,(float)guiData.width/guiData.frameWidth*Screen.width,(float)guiData.height/guiData.frameHeight*Screen.height),nativeGui,new Rect(0,1,1,-1));}
  GUILayout.BeginArea(new Rect(10,Screen.height-55,380,50),GUI.skin.box);GUILayout.Label("F8 切换角色 · E 原生物品栏",style);GUILayout.Label(status,style);GUILayout.EndArea();
 }
}
}
