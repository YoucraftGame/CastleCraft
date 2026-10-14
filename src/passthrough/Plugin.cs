using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using PeakCraft.Link;
using PeakCraft.Render;
using PeakCraft.World;
namespace PeakCraft;

[BepInPlugin("castlecraft.bridge", "CastleCraft Passthrough", "0.3.0")]
[DefaultExecutionOrder(-1000)]
public sealed unsafe class Plugin : BaseUnityPlugin {
 internal static ManualLogSource Log {get;private set;}=null!;
 internal static Plugin Instance {get;private set;}=null!;
 internal PeakLink Link {get;private set;}=null!;
 private Overlay overlay=null!;
 private RenderRing render=null!;
 private CastleCollision collision=null!;
 private readonly CastleStage3D stage3D=new();
 private readonly CastleMobTargets mobTargets=new();
 private readonly CastlePortraitHud portraitHud=new();
 private readonly CastleActors actors=new();
 private readonly CastleBindings bindings=new();
 private readonly CastleStruggleInput struggleInput=new();
 private Renderer? sortingRenderer;
 private Component? sortingObject;
 private CastleActions actions=null!;
 private readonly CastleEquipment equipment=new();
 private bool mirroringHealth;
 internal void ReadEquipmentAnchors(byte* data,uint bytes)=>equipment.ReadAnchors(data,bytes);
 private bool nativeDispatch;
 private string bindingSnapshot="";
 private Proto.GuestState guest;
 private CastleStatus guestStatus=new();
 internal Color AvatarTint=>guestStatus.hurt>0?new Color(1,.55f,.55f,1):Color.white;
 internal void ReadStatus(byte* data,uint bytes){if(bytes>65536)return;var copy=new byte[bytes];System.Runtime.InteropServices.Marshal.Copy((IntPtr)data,copy,0,(int)bytes);guestStatus=JsonUtility.FromJson<CastleStatus>(System.Text.Encoding.UTF8.GetString(copy));}
 private Type heroType=null!;
 private Type ddType=null!,monsterType=null!,keyType=null!;
 private System.Reflection.PropertyInfo ddInstance=null!,heroAlive=null!,heroOwner=null!;
 private System.Reflection.FieldInfo ddHeroes=null!;
 private bool requested,owns,handoff;
 private readonly CastleWeaponSlot weaponSlot=new();
 private readonly CastleGearIsolation gearIsolation=new();
 private readonly CastleSpawnStability spawnStability=new();
 internal bool IsolateCastleGear(object hero)=>wanted&&!sharedDead&&ReferenceEquals(hero,Hero());
 internal bool NativeGearSelected=>wanted&&bindings.CastleEquipmentEnabled&&(bindings.CastleMouse||Input.GetKey(KeyCode.LeftAlt)||actions.NativeWeaponVisible);
 private double handoffResidual;
 private bool wanted,resume;
 private CastleControlPhase controlPhase;
 private float handoffStarted,nextHandoffDiagnostic;
 private int originalFrameLimit,originalVsync;
 private bool framePolicyApplied;
 private bool worldVisible;
 private Component? controlledHero;
 private bool sharedDead,newRunPending;
 private int pendingLifeHp,pendingLifeWhite;
 private int confirmedLifeHero; private string confirmedLifeRoom="";
 private ushort pendingLifeCode;
 private int lifeSequence; // Zero means no life command exists; never retry default zero-health payload.
 private float healthSyncDeadline;
 private string roomKey="";
 private Vector3 safeGuestFeet;
 private bool haveSafeGuestFeet;
 private struct HitContext { public ushort Kind; public int Attacker,Flags; }
 private HitContext hitContext=new HitContext{Kind=2};
 private readonly Dictionary<string,double> roomOrigins=new();
 private Type stageWayType=null!;
 private System.Reflection.MethodInfo stageStay=null!;
 private Component[] exits=Array.Empty<Component>();
 private float nextExitScan;
 private void CheckExits(){if(!owns||sharedDead||NativePaused())return;var hero=Hero();if(hero==null)return;if(Time.unscaledTime>=nextExitScan){nextExitScan=Time.unscaledTime+1;exits=Resources.FindObjectsOfTypeAll(stageWayType).Cast<Component>().Where(c=>c.gameObject.scene.IsValid()&&c.gameObject.scene.isLoaded&&c.gameObject.activeInHierarchy).ToArray();}var shadow=Property(hero,"ShadowCollider") as Collider2D;if(shadow==null)return;Physics2D.SyncTransforms();foreach(var exit in exits){if(exit==null||!exit.gameObject.activeInHierarchy)continue;var col=exit.GetComponent<Collider2D>();if(col!=null&&col.enabled&&col.isTrigger&&Rect.MinMaxRect(col.bounds.min.x,col.bounds.min.y,col.bounds.max.x,col.bounds.max.y).Overlaps(Rect.MinMaxRect(shadow.bounds.min.x,shadow.bounds.min.y,shadow.bounds.max.x,shadow.bounds.max.y)))stageStay.Invoke(exit,new object[]{shadow});}}
 private void SelectRoom(string name){if(!roomOrigins.TryGetValue(name,out double origin)){origin=CastleRoomOrigins.Reserve(Path.Combine(Paths.GameRootPath,"CastleCraft","runtime","room-origins.next"));roomOrigins[name]=origin;}Coords.RoomX=origin;}
 private float resumeAfter;
 private uint teleportSeq,epoch=1;
 private string scene="",session="",status="等待 Minecraft 客户端完成加载";
 private Vector3 returnPosition;
 private Renderer[] hidden=Array.Empty<Renderer>();
 private readonly Dictionary<Renderer,bool> originalRenderers=new();
 private int health;
 private float nextStatus;
 private float hostFrameMs;
 private long measurementStart=System.Diagnostics.Stopwatch.GetTimestamp();
 private int measuredFrames;
 private double bridgeMs,actorMs,renderMs,overlayMs,guestRenderMs,stageRenderMs,mobTargetMs;
 private float nextActorSlowLog;
 private Action? equipmentBeforeDraw;
 private static double Millis(long start)=>(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
 private double posX,posZ;
 private double syncedX,syncedY,syncedZ;
 private long guestReadMisses;
 private string collisionRoomKey="";
 private float nextCorrection;
 private int nativeCollisionCorrections;
 private readonly CastleRawMouse rawMouse=new();
 private readonly CastleLook look=new();
 private float yaw=180;
 private float pitch=0;
 private readonly Dictionary<KeyCode,ushort> keys=new();
 private readonly HashSet<KeyCode> forwardedKeys=new();
 private readonly HashSet<int> forwardedMouse=new();
 private readonly Dictionary<Camera,CameraSnapshot> cameraPositions=new();
 private struct CameraSnapshot {public Vector3 Position;public Quaternion Rotation;public bool Ortho;public float Fov,Near,Far,Size;public int Mask;public CameraClearFlags Clear;public Color Background;public Matrix4x4 View,Projection,Culling;public bool AutoView,AutoProjection,AutoCulling;}
 internal bool PreserveAvatar=>wanted;
 internal bool FirstPersonView=>wanted&&Link.GuestAlive&&worldVisible&&render.HasAvatar&&guest.cameraMode==0&&!sharedDead;
 private bool cursorCaptured;private CursorLockMode oldCursorLock;private bool oldCursorVisible;
 private void SyncCursor(){rawMouse.Enabled=FirstPersonView&&Application.isFocused&&(guest.flags&(uint)Proto.GuestFlags.ScreenOpen)==0&&!NativeDialog();if(!rawMouse.Enabled){rawMouse.Drain();look.Reset();}bool capture=FirstPersonView&&(guest.flags&(uint)Proto.GuestFlags.ScreenOpen)==0&&!NativeDialog();if(capture&&!cursorCaptured){oldCursorLock=Cursor.lockState;oldCursorVisible=Cursor.visible;cursorCaptured=true;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}else if(!capture&&cursorCaptured){cursorCaptured=false;Cursor.lockState=oldCursorLock;Cursor.visible=oldCursorVisible;}}

 private float CameraLift=>owns?(float)Math.Max(0,(guest.y-Coords.YOffset)*Coords.Scale-2):0;
 private Component? Hero(){
  if(controlledHero!=null&&controlledHero.gameObject.activeInHierarchy&&wanted&&(bool)heroOwner.GetValue(controlledHero,null))return controlledHero;
  var dd=ddInstance.GetValue(null,null);if(dd==null)return null;
  var heroes=ddHeroes.GetValue(dd) as System.Collections.IEnumerable;
  return heroes?.Cast<Component>().FirstOrDefault(x=>x!=null&&x.gameObject.activeInHierarchy&&(bool)heroOwner.GetValue(x,null));
 }
 private bool NativeAlive(Component hero)=>(bool)heroAlive.GetValue(hero,null);
 private string RoomKey(string name){var dd=ddInstance.GetValue(null,null);var ctrl=dd==null?null:Field(dd,"stageCtrl");var stage=ctrl==null?null:Field(ctrl,"cur_stage") as Component;return name+":"+SceneManager.GetActiveScene().handle+":"+(stage==null?0:stage.GetInstanceID());}
 private bool RoomReady(){var dd=ddInstance.GetValue(null,null);var ctrl=dd==null?null:Field(dd,"stageCtrl");if(!PlayScene()||ctrl==null||(bool)Property(ctrl,"IsStageChanging")||!(Field(ctrl,"cur_stage") is Component stage)||!stage.gameObject.activeInHierarchy)return false;var hero=Hero();return hero==null||!(bool)Field(hero,"update_to_next_stage");}

 private static object Field(object o,string name)=>AccessTools.Field(o.GetType(),name).GetValue(o);
 private static object Property(object o,string name)=>AccessTools.Property(o.GetType(),name).GetValue(o);
 private void Awake(){
  Log=Logger;Instance=this;session=Environment.GetEnvironmentVariable("CASTLECRAFT_SESSION")??"";heroType=AccessTools.TypeByName("Teran.Hero");
  if(session.Length==0){var runtime=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CastleCraft");session=Path.Combine(runtime,"sessions","manual");Directory.CreateDirectory(session);File.WriteAllText(Path.Combine(runtime,"active-session.txt"),session);Log.LogInfo("Normal-client integration: default manual mapping; no temporary Minecraft instance required.");}
  bindings.Initialize(Config);
  if(!Coords.CheckProjection())throw new InvalidOperationException("First-person coordinate axis probes failed");
  Log.LogInfo("First-person coordinate probes passed: MC +X right, +Y up, +Z canonical -Z; no floor shear in view.");
  stageWayType=AccessTools.TypeByName("Teran.StageWay");stageStay=AccessTools.Method(stageWayType,"OnTriggerStay2D");
  ddType=AccessTools.TypeByName("Teran.DDSystem");monsterType=AccessTools.TypeByName("Teran.Monster");keyType=AccessTools.TypeByName("Teran.InputController+KeyType");ddInstance=AccessTools.Property(ddType,"Instance");ddHeroes=AccessTools.Field(ddType,"heros");heroAlive=AccessTools.Property(heroType,"IsAlive");heroOwner=AccessTools.Property(heroType,"IsOwner");
  Link=new PeakLink(Log);if(!Proto.SelfCheck(Log)||!Rings.SelfTest(Log)||!Link.Create("Local\\CastleCraft_v3_"+Path.GetFileName(session))){enabled=false;return;}
  overlay=new Overlay(Link);render=new RenderRing(Link,Config);collision=new CastleCollision(Link);actions=new CastleActions(Link);
  var harmony=new Harmony("castlecraft.bridge");
  harmony.Patch(AccessTools.Method(AccessTools.TypeByName("Teran.BossUICtrl"),"StartBossPainting"),prefix:new HarmonyMethod(typeof(Plugin),nameof(BossPaintingStart)));
  harmony.Patch(AccessTools.Method(AccessTools.TypeByName("Teran.BossStageCtrl"),"PaintingEnd"),postfix:new HarmonyMethod(typeof(Plugin),nameof(BossPaintingEnd)));
CastleMobTargets.Install(harmony);CastleActors.InstallPatches(harmony);CastleGearIsolation.Install(harmony);harmony.Patch(AccessTools.Method(heroType,"HandleInput"),prefix:new HarmonyMethod(typeof(Plugin),nameof(HostInput)));
  var creature=AccessTools.TypeByName("Teran.Creature");var attack=AccessTools.TypeByName("Teran.AttackInfo");
  harmony.Patch(AccessTools.Method(creature,"BeAttacked",new[]{attack,typeof(float),typeof(bool)}),prefix:new HarmonyMethod(typeof(Plugin),nameof(ReceiveAttack)),finalizer:new HarmonyMethod(typeof(Plugin),nameof(ReceiveAttackComplete)));
  harmony.Patch(AccessTools.Method(AccessTools.TypeByName("Teran.InputController"),"Update"),prefix:new HarmonyMethod(typeof(Plugin),nameof(HostController)));
  harmony.Patch(AccessTools.Method(AccessTools.TypeByName("Teran.Entity"),"UpdateMove"),prefix:new HarmonyMethod(typeof(Plugin),nameof(HostMove)));
  harmony.Patch(AccessTools.Method(creature,"DamageCalculate",new[]{typeof(int)}),prefix:new HarmonyMethod(typeof(Plugin),nameof(ReceiveEnvironmentDamage)));
  harmony.Patch(AccessTools.Method(creature,"DamageCalculateByDebuff",new[]{typeof(int),attack}),prefix:new HarmonyMethod(typeof(Plugin),nameof(ReceiveEnvironmentDamage)));
  harmony.Patch(AccessTools.Method(creature,"BeAttackedByDebuff"),prefix:new HarmonyMethod(typeof(Plugin),nameof(ReceiveNativeDebuff)),finalizer:new HarmonyMethod(typeof(Plugin),nameof(ReceiveAttackComplete)));
  harmony.Patch(AccessTools.Method(heroType,"RpcRecoverHp",new[]{typeof(int)}),postfix:new HarmonyMethod(typeof(Plugin),nameof(RecoverGuestHealth)));
  harmony.Patch(AccessTools.PropertySetter(AccessTools.TypeByName("Teran.Creature+PropertiesInspector"),"WhiteHp"),prefix:new HarmonyMethod(typeof(Plugin),nameof(BeforeWhiteHp)),postfix:new HarmonyMethod(typeof(Plugin),nameof(AfterWhiteHp)));
  foreach(var name in new[]{"RpcReborn","RpcRebornPVP","RebornWithProps"}){var rebirth=AccessTools.DeclaredMethod(heroType,name);if(rebirth!=null)harmony.Patch(rebirth,postfix:new HarmonyMethod(typeof(Plugin),nameof(NativeReborn)));}
  var audioReceiver=AccessTools.TypeByName("Teran.AudioEventReceiver");
  if(audioReceiver!=null)harmony.Patch(AccessTools.Method(audioReceiver,"playCharacterBeAttackedSound"),prefix:new HarmonyMethod(typeof(Plugin),nameof(NativeHurtSound)));
  var audioMgr=AccessTools.TypeByName("Teran.AudioMgr");
  if(audioMgr!=null)foreach(var method in AccessTools.GetDeclaredMethods(audioMgr))if(method.Name=="playSoundEffect"&&method.GetParameters().Length>0&&method.GetParameters()[0].ParameterType==typeof(string))harmony.Patch(method,prefix:new HarmonyMethod(typeof(Plugin),nameof(NativeLoopSound)));
  harmony.Patch(AccessTools.Method(heroType,"IsCanChangeStage"),prefix:new HarmonyMethod(typeof(Plugin),nameof(GuestStageEligible)));
  harmony.Patch(AccessTools.Method(heroType,"HardValueHandle",new[]{typeof(int),typeof(float),typeof(Vector3),typeof(float),typeof(int),typeof(float)}),prefix:new HarmonyMethod(typeof(Plugin),nameof(GuestHardValue)));
  for(int i=0;i<26;i++)keys[(KeyCode)((int)KeyCode.A+i)]=(ushort)(4+i);
  for(int i=0;i<9;i++)keys[(KeyCode)((int)KeyCode.Alpha1+i)]=(ushort)(30+i);
  foreach(var pair in new[]{(KeyCode.Space,(ushort)44),(KeyCode.Escape,(ushort)41),(KeyCode.Return,(ushort)40),(KeyCode.Tab,(ushort)43),(KeyCode.LeftShift,(ushort)225),(KeyCode.LeftControl,(ushort)224),(KeyCode.Backspace,(ushort)42),(KeyCode.F5,(ushort)62)})keys[pair.Item1]=pair.Item2;
  foreach(var pair in new[]{(KeyCode.Alpha0,(ushort)39),(KeyCode.LeftAlt,(ushort)226),(KeyCode.RightShift,(ushort)229),(KeyCode.RightControl,(ushort)228),(KeyCode.RightAlt,(ushort)230),(KeyCode.RightArrow,(ushort)79),(KeyCode.LeftArrow,(ushort)80),(KeyCode.DownArrow,(ushort)81),(KeyCode.UpArrow,(ushort)82),(KeyCode.Delete,(ushort)76),(KeyCode.Home,(ushort)74),(KeyCode.End,(ushort)77)})keys[pair.Item1]=pair.Item2;
  // Forward the full ordinary keyboard, including chat punctuation and custom native bindings.
  for(int i=0;i<12;i++){var key=(KeyCode)((int)KeyCode.F1+i);if(key!=KeyCode.F8)keys[key]=(ushort)(58+i);}
  foreach(var pair in new[]{(KeyCode.Minus,(ushort)45),(KeyCode.Equals,(ushort)46),(KeyCode.LeftBracket,(ushort)47),(KeyCode.RightBracket,(ushort)48),(KeyCode.Backslash,(ushort)49),(KeyCode.Semicolon,(ushort)51),(KeyCode.Quote,(ushort)52),(KeyCode.BackQuote,(ushort)53),(KeyCode.Comma,(ushort)54),(KeyCode.Period,(ushort)55),(KeyCode.Slash,(ushort)56),(KeyCode.Insert,(ushort)73),(KeyCode.PageUp,(ushort)75),(KeyCode.PageDown,(ushort)78)})keys[pair.Item1]=pair.Item2;
  for(int i=1;i<=9;i++)keys[(KeyCode)((int)KeyCode.Keypad1+i-1)]=(ushort)(88+i);
  foreach(var pair in new[]{(KeyCode.Keypad0,(ushort)98),(KeyCode.KeypadPeriod,(ushort)99),(KeyCode.KeypadDivide,(ushort)84),(KeyCode.KeypadMultiply,(ushort)85),(KeyCode.KeypadMinus,(ushort)86),(KeyCode.KeypadPlus,(ushort)87),(KeyCode.KeypadEnter,(ushort)88),(KeyCode.KeypadEquals,(ushort)103),(KeyCode.CapsLock,(ushort)57),(KeyCode.Numlock,(ushort)83),(KeyCode.ScrollLock,(ushort)71),(KeyCode.Print,(ushort)70),(KeyCode.Pause,(ushort)72)})keys[pair.Item1]=pair.Item2;
  Camera.onPreCull+=CameraBefore;Camera.onPostRender+=CameraAfter;
  var lightObject=new GameObject("CastleCraft guest-only lighting");lightObject.hideFlags=HideFlags.HideAndDontSave;DontDestroyOnLoad(lightObject);var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=.4f;light.color=new Color(.93f,.91f,.87f);light.cullingMask=(1<<Coords.GeometryLayer)|(1<<Coords.AvatarLayer)|(1<<Coords.PortraitLayer);light.shadows=LightShadows.None;lightObject.transform.rotation=Quaternion.Euler(50,-30,0);
  Application.runInBackground=true;originalFrameLimit=Application.targetFrameRate;originalVsync=QualitySettings.vSyncCount;Log.LogInfo("Actual SkyCraft/PeakCraft shared-memory passthrough modules loaded. No cropped GUI and no resource rewards.");
 }
 private static bool HostInput(object __0)=>Instance==null||!(Instance.owns||Instance.handoff)||Instance.nativeDispatch||Instance.sharedDead||Instance.NativeDialog();
 private static bool NativeHurtSound(Component __instance){var p=Instance;var hero=p?.Hero();return p==null||!p.owns||hero==null||!__instance.transform.IsChildOf(hero.transform);}
 private static bool NativeLoopSound(string __0)=>Instance==null||!Instance.owns||(__0!="character_run"&&__0!="character_heartbeat");
 private static bool GuestHardValue(object __instance)=>Instance==null||!Instance.owns||(__instance as Component)!=Instance.Hero();
 private static bool HostController(){var p=Instance;return p==null||CastleNativeFlow.NativeController(p.owns||p.handoff,p.sharedDead,p.NativeDialog(),p.NativeInputLayer());}
 private static bool HostMove(object __instance){
  var p=Instance;if(p==null||!(p.owns||p.handoff)||(__instance as Component)!=p.Hero())return true;
  if(p.sharedDead||!p.NativeAlive((Component)__instance)||p.NativeScripted())return true;
  if(p.NativePaused()||p.handoff)return false;
  var handler=Field(__instance,"moveHandler");float x=Convert.ToSingle(Field(handler,"offset_x")),y=Convert.ToSingle(Field(handler,"offset_y"));
  bool restrained=p.NativeRestrained();
  bool canMove=(bool)Field(__instance,"m_isCanMove"),locked=restrained||!canMove||p.NativeInputSuppressed()||(bool)Field(__instance,"m_isFellDown")||(p.actions.Action!=0&&(bool)Field(__instance,"isDashFont"));
  if(restrained||!canMove||float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y)||p.sharedDead)x=y=0;
  float dt=Time.deltaTime>Time.smoothDeltaTime*3?Time.smoothDeltaTime:Time.deltaTime;
  p.Link.PushInput((Proto.InputType)12,(ushort)(restrained?3:locked?1:0),(int)Math.Round(Mathf.Clamp(x,-35,35)/Coords.Scale*dt*1000000),(int)Math.Round(-Mathf.Clamp(y,-35,35)/Coords.Scale*dt*1000000));
  AccessTools.Method(handler.GetType(),"reset").Invoke(handler,null);return false;
 }
 private static bool GuestStageEligible(object __instance,ref bool __result){var p=Instance;if(p==null||!(p.owns||p.handoff)||(__instance as Component)!=p.Hero())return true;
  if(p.handoff)return false;__result=(p.guest.flags&(uint)Proto.GuestFlags.Dead)==0&&!p.NativeDialog();return false;}
 private string NativeInputLayer(){var dd=ddInstance.GetValue(null,null);var input=dd==null?null:Field(dd,"inputCtrl");return input==null?"IGNORE":Field(input,"inputLayer").ToString();}
 private Component? nativeBossPainting;
 private static void BossPaintingStart(Component __instance){if(Instance!=null)Instance.nativeBossPainting=__instance;}
 private static void BossPaintingEnd(){if(Instance!=null)Instance.nativeBossPainting=null;}
 private bool NativeScripted(){var hero=Hero();return hero!=null&&CastleNativeFlow.Scripted((bool)Field(hero,"other_input"),(bool)Field(hero,"m_isTalking"),NativeInputLayer(),nativeBossPainting!=null&&nativeBossPainting.gameObject.activeInHierarchy,NativeEnteringRoom(hero));}
 private static bool NativeEnteringRoom(Component hero)=>AccessTools.Property(hero.GetType(),"Update_To_Next_Stage")?.GetValue(hero,null) is bool entering&&entering;
 private bool NativePaused()=>CastleNativeFlow.Paused(Time.timeScale,NativeInputLayer(),NativeScripted());
 private bool nativeWasPaused;
 private bool NativeRestrained()=>Hero() is Component hero&&(bool)Field(hero,"isCocoon");
 private bool NativeInputSuppressed()=>NativePaused()||Hero() is Component hero&&(bool)Field(hero,"isIgnoreInput");
 private bool NativeDialog()=>wanted&&NativeScripted();
 private Animator? presentationAnimator;
 private AnimatorCullingMode presentationCulling;
 private void RestorePresentationAnimator(){if(presentationAnimator!=null)presentationAnimator.cullingMode=presentationCulling;presentationAnimator=null;}
 private float nextHeroRendererScan;
 private void CaptureHero(Component hero){
  if(controlledHero==hero&&hidden.Length>0){
   if(Time.unscaledTime<nextHeroRendererScan)return;
   nextHeroRendererScan=Time.unscaledTime+.2f;
   var scanShadow=Property(hero,"ShadowTransform") as Transform;
   foreach(var renderer in hero.GetComponentsInChildren<Renderer>(true))
    if(!(renderer is ParticleSystemRenderer)&&(scanShadow==null||!renderer.transform.IsChildOf(scanShadow))&&!originalRenderers.ContainsKey(renderer))originalRenderers[renderer]=renderer.enabled;
   hidden=originalRenderers.Keys.Where(renderer=>renderer!=null).ToArray();return;
  }
  foreach(var pair in originalRenderers)if(pair.Key!=null)pair.Key.enabled=pair.Value;
  originalRenderers.Clear();controlledHero=hero;
  RestorePresentationAnimator();presentationAnimator=hero.GetComponent<Animator>();
  if(presentationAnimator!=null){presentationCulling=presentationAnimator.cullingMode;presentationAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate;}
  var shadow=Property(hero,"ShadowTransform") as Transform;
  hidden=hero.GetComponentsInChildren<Renderer>(true).Where(r=>!(r is ParticleSystemRenderer)&&(shadow==null||!r.transform.IsChildOf(shadow))).ToArray();
  foreach(var renderer in hidden)originalRenderers[renderer]=renderer.enabled;
  sortingRenderer=hidden.FirstOrDefault(r=>r.name=="body")??hidden.FirstOrDefault();
  var sortingType=AccessTools.TypeByName("Teran.SortingObj");
  sortingObject=sortingRenderer!=null&&sortingType!=null?sortingRenderer.GetComponentInParent(sortingType):null;
  if(hidden.Length>0){Coords.RenderLayer=hidden[0].gameObject.layer;Coords.SortingLayer=hidden[0].sortingLayerID;Coords.SortingOrder=hidden.Max(renderer=>renderer.sortingOrder)+1;}
 }
 private void UpdateNativeSorting(){
  if(sortingObject!=null)Coords.SortingOrder=Convert.ToInt32(Field(sortingObject,"sortingOrder"));
  else if(sortingRenderer!=null)Coords.SortingOrder=sortingRenderer.sortingOrder;
 }
 private void ReleaseForwardedInput(){bindings.ReleaseAll(NativeKeyUp);Link.PushInput(Proto.InputType.ReleaseAll);forwardedKeys.Clear();forwardedMouse.Clear();}
 private void NativeStruggle(string direction){
  var hero=Hero();if(hero==null||sharedDead||!NativeRestrained()||NativeInputSuppressed())return;
  try{nativeDispatch=true;AccessTools.Method(heroType,"HandleInput").Invoke(hero,new[]{Enum.Parse(keyType,direction)});}finally{nativeDispatch=false;}
 }
 private void StopBridgeInput(){struggleInput.Update(false,false,false,ReleaseForwardedInput,NativeStruggle);equipment.Release();actions.Direction(Hero(),yaw,false,Vector2.zero);actions.Release();Link.PushInput((Proto.InputType)12,2);bindings.ReleaseAll(NativeKeyUp);Link.PushInput(Proto.InputType.ReleaseAll);forwardedKeys.Clear();forwardedMouse.Clear();}
 private void SuspendControl(){
  if(owns||handoff)StopBridgeInput();
  owns=handoff=requested=false;resume=wanted;resumeAfter=Time.unscaledTime+.1f;
  status=controlPhase==CastleControlPhase.Cinematic?"Minecraft 角色保持；等待原生剧情结束":controlPhase==CastleControlPhase.ClientWaiting?"等待 Minecraft 世界重新就绪；接管模式保留":"Minecraft 角色保持；等待房间与碰撞就绪";
 }
 private void FramePolicy(bool alive){
  if(alive&&wanted&&guest.frameLimit>0&&(guest.renderFlags&1)!=0){
   int limit=(int)guest.frameLimit-1;Application.targetFrameRate=limit>=260?-1:System.Math.Max(1,limit);QualitySettings.vSyncCount=(guest.renderFlags&2)!=0?1:0;framePolicyApplied=true;
  }else if(framePolicyApplied){Application.targetFrameRate=originalFrameLimit;QualitySettings.vSyncCount=originalVsync;framePolicyApplied=false;}
 }
 private void NativeAction(string action){if(!bindings.CastleEquipmentEnabled&&(action=="Attack"||action=="SP_ATK"||action=="Skill"))return;var hero=Hero();gearIsolation.Update(hero,wanted,NativeGearSelected);if(hero==null||sharedDead||NativePaused()||(guest.flags&(uint)Proto.GuestFlags.ScreenOpen)!=0)return;try{nativeDispatch=true;actions.Face(hero,yaw);if((guest.flags&(uint)Proto.GuestFlags.OnGround)!=0)AccessTools.Property(heroType,"isFlying").SetValue(hero,false,null);if(action!="Attack"&&action!="SP_ATK")Log.LogInfo($"Native castle action {action}, feet={hero.transform.position}");AccessTools.Method(heroType,"HandleInput").Invoke(hero,new[]{Enum.Parse(keyType,action)});}finally{nativeDispatch=false;}}
 internal void ReadBindings(byte* data,uint bytes){if(bytes>32768)return;var copy=new byte[bytes];System.Runtime.InteropServices.Marshal.Copy((IntPtr)data,copy,0,(int)bytes);var text=System.Text.Encoding.UTF8.GetString(copy);if(text==bindingSnapshot)return;bindingSnapshot=text;bindings.Read(text);if(owns){var dd=ddInstance.GetValue(null,null);if(dd!=null)bindings.Configure(dd);}}
 private static bool ReceiveAttack(object __instance,object __0,bool __2,ref int __result,out HitContext __state){
  var p=Instance;__state=p==null?default:p.hitContext;if(p==null||!p.owns||(__instance as Component)!=p.Hero()||!__2)return true;
  var attacker=Field(__0,"attacker") as Transform;var monster=attacker!=null?attacker.GetComponentInParent(p.monsterType):null;
  if(monster==null&&attacker!=null&&CastleHunterMirror.HunterType!=null)monster=attacker.GetComponentInParent(CastleHunterMirror.HunterType);
  p.hitContext=new HitContext{Kind=(bool)Field(__0,"isFlyingObj")?(ushort)1:(ushort)0,Attacker=monster!=null?monster.GetInstanceID():0,Flags=Convert.ToInt32(Field(__0,"hard_value"))>=5?2:0};
  // Minecraft decides shield direction, armour, invulnerability and health before visual feedback.
  ReceiveEnvironmentDamage(__instance,Convert.ToInt32(Field(__0,"origin_damage")));
  __result=0;return false;
 }
 private static void ReceiveAttackComplete(HitContext __state){if(Instance!=null)Instance.hitContext=__state;}
 private void LogDamage(int damage)=>Log.LogInfo($"Native enemy attack redirected to Minecraft armor/shield/health: raw castle damage={damage}");
 private void NativeKeyUp(string action){var hero=Hero();if(hero==null)return;try{nativeDispatch=true;AccessTools.Method(heroType,"HandleInputKeyUp").Invoke(hero,new[]{Enum.Parse(keyType,action)});}finally{nativeDispatch=false;}}
 private static bool ReceiveEnvironmentDamage(object __instance,int __0){
  var p=Instance;if(p==null||!(p.owns||p.handoff)||(__instance as Component)!=p.Hero())return true;
  if(p.handoff)return false;
  if(__0>0&&!p.sharedDead){var inspector=Field(__instance,"propertiesInspector");float maximum=Convert.ToSingle(Property(inspector,"maxHp"));
   // The guest's legacy input unit is five times one Minecraft health point.
   int units=(int)Math.Round(__0/Math.Max(1,maximum)*Math.Max(1,p.guestStatus.maximum)*500);
   p.Link.PushInput(Proto.InputType.Hurt,p.hitContext.Kind,units,p.hitContext.Attacker,p.hitContext.Flags);
  }return false;
 }
 private static void ReceiveNativeDebuff(object __instance,int __0,object __1,out HitContext __state){var p=Instance;__state=p==null?default:p.hitContext;if(p==null||!p.owns||(__instance as Component)!=p.Hero())return;p.hitContext=new HitContext{Kind=__1.ToString()=="Burned"?(ushort)4:__1.ToString()=="Freezing"||__1.ToString()=="Frozen"?(ushort)6:(ushort)2};}
 private void SendLife(Component hero,bool rebirth){var inspector=Field(hero,"propertiesInspector");float maximum=Convert.ToSingle(Property(inspector,"maxHp"));int hp=Convert.ToInt32(Property(inspector,"CurrentHp")),white=Convert.ToInt32(Property(inspector,"WhiteHp"));lifeSequence=CastleLifeGate.NextSequence(lifeSequence);healthSyncDeadline=Time.unscaledTime+3;pendingLifeCode=(ushort)(rebirth?1:0);pendingLifeHp=(int)Math.Round(hp/Math.Max(1,maximum)*1000000);pendingLifeWhite=(int)Math.Round(white/Math.Max(1,maximum)*1000000);Link.PushInput((Proto.InputType)11,pendingLifeCode,pendingLifeHp,pendingLifeWhite,lifeSequence);}
 private static void NativeReborn(object __instance){var p=Instance;var hero=__instance as Component;if(p==null||hero==null||!p.wanted||hero!=p.Hero()||!p.NativeAlive(hero)||Convert.ToInt32(Property(Field(hero,"propertiesInspector"),"CurrentHp"))<=0)return;p.sharedDead=false;p.resume=true;p.resumeAfter=Time.unscaledTime+.1f;p.SendLife(hero,true);Log.LogInfo("Native castle rebirth forwarded to the same Minecraft life.");}
 private static void RecoverGuestHealth(object __instance,int __0){var p=Instance;if(p==null||!p.owns||(__instance as Component)!=p.Hero()||__0<=0)return;var inspector=Field(__instance,"propertiesInspector");float maximum=Convert.ToSingle(Property(inspector,"maxHp"));p.Link.PushInput((Proto.InputType)10,0,(int)Math.Round(__0/Math.Max(1,maximum)*Math.Max(20,p.guestStatus.maximum)*100),0,0);}
 private static void BeforeWhiteHp(object __instance,out int __state){
  var p=Instance;var hero=p==null?null:p.Hero();__state=int.MinValue;
  if(p!=null&&p.owns&&!p.mirroringHealth&&hero!=null&&ReferenceEquals(__instance,Field(hero,"propertiesInspector")))__state=Convert.ToInt32(Property(__instance,"WhiteHp"));
 }
 private static void AfterWhiteHp(object __instance,int __state){
  var p=Instance;if(p==null||__state==int.MinValue||p.mirroringHealth||p.sharedDead||p.guestStatus.maximum<=0)return;
  int delta=Convert.ToInt32(Property(__instance,"WhiteHp"))-__state;float maximum=Convert.ToSingle(Property(__instance,"maxHp"));
  if(delta!=0)p.Link.PushInput((Proto.InputType)14,0,(int)Math.Round(delta/Math.Max(1,maximum)*p.guestStatus.maximum*10000));
 }
 private void RetryLife(){if(wanted&&Link.GuestAlive&&lifeSequence!=0&&guestStatus.lifeAck!=lifeSequence&&Time.unscaledTime>healthSyncDeadline){healthSyncDeadline=Time.unscaledTime+3;Link.PushInput((Proto.InputType)11,pendingLifeCode,pendingLifeHp,pendingLifeWhite,lifeSequence);}}
 private void EnterSharedDeath(Component hero){
  sharedDead=true;
  // Restore native animation, movement, gear and input before its death/revival pipeline runs.
  Release(true);resume=true;worldVisible=false;
  var inspector=Field(hero,"propertiesInspector");
  mirroringHealth=true;try{AccessTools.Property(inspector.GetType(),"CurrentHp").SetValue(inspector,0,null);AccessTools.Property(inspector.GetType(),"WhiteHp").SetValue(inspector,0,null);}finally{mirroringHealth=false;}
  if(NativeAlive(hero))AccessTools.Method(heroType,"SetIsAlive").Invoke(hero,new object[]{false});
  SendLife(hero,false);
  status="共享生命死亡；城堡处理死亡与复活，F8 返回原生模式";
  Log.LogInfo("Shared death: released Minecraft ownership to native death/revival pipeline.");
 }
 private void SyncHealth(){
  if(!wanted||!Link.GuestAlive)return;var hero=Hero();if(hero==null)return;
  var inspector=Field(hero,"propertiesInspector");float maximum=Convert.ToSingle(Property(inspector,"maxHp"));
  var transition=CastleLifeGate.Evaluate(sharedDead,NativeAlive(hero),Convert.ToInt32(Property(inspector,"CurrentHp")),owns,guestStatus.maximum>0,lifeSequence==0||guestStatus.lifeAck==lifeSequence,guestStatus.health,confirmedLifeHero==hero.GetInstanceID()&&confirmedLifeRoom==roomKey&&RoomReady()&&!NativeScripted()&&!NativeEnteringRoom(hero)&&collision.Ready);
  // Native life transitions supersede a pending guest command, including while suspended.
  if(transition==CastleLifeTransition.NativeRevival){sharedDead=false;SendLife(hero,true);resume=true;resumeAfter=Time.unscaledTime+.1f;return;}
  if(transition==CastleLifeTransition.NativeDeath){EnterSharedDeath(hero);return;}
  if(sharedDead)return;
  if(!owns||guestStatus.maximum<=0)return;
  if(lifeSequence!=0&&guestStatus.lifeAck!=lifeSequence){if(Time.unscaledTime>healthSyncDeadline){healthSyncDeadline=Time.unscaledTime+3;Link.PushInput((Proto.InputType)11,pendingLifeCode,pendingLifeHp,pendingLifeWhite,lifeSequence);Log.LogWarning("Waiting for shared-life acknowledgement; not overwriting health with a stale guest report.");}return;}
  if(transition==CastleLifeTransition.GuestDeath){EnterSharedDeath(hero);return;}
  if(transition!=CastleLifeTransition.Mirror)return;
  mirroringHealth=true;try{
  AccessTools.Property(inspector.GetType(),"CurrentHp").SetValue(inspector,Math.Max(0,(int)Math.Ceiling(maximum*guestStatus.health/guestStatus.maximum)),null);
  AccessTools.Property(inspector.GetType(),"WhiteHp").SetValue(inspector,(int)Math.Ceiling(maximum*guestStatus.absorption/guestStatus.maximum),null);
  }finally{mirroringHealth=false;}
 }
 private bool PlayScene()=>!new[]{"MainMenu","Loading","LoadData","GeniusUI","StatisticUI"}.Contains(SceneManager.GetActiveScene().name);
 private void Begin(){
  var hero=Hero();if(hero==null||!NativeAlive(hero)||Convert.ToInt32(Property(Field(hero,"propertiesInspector"),"CurrentHp"))<=0||!RoomReady()||NativeScripted()||!Link.GuestAlive||(guest.flags&(uint)Proto.GuestFlags.InWorld)==0){status="等待城堡场景、剧情与 Minecraft 世界就绪";return;}
  if(!spawnStability.Stable){if(!wanted)newRunPending=true;wanted=true;resume=true;resumeAfter=Time.unscaledTime+.1f;status="等待原生出生／过场落脚位置稳定";return;}
  bool continuing=resume&&wanted&&!newRunPending;newRunPending=false;CaptureHero(hero);
  if(!continuing)sharedDead=false;
  wanted=true;gearIsolation.Update(hero,true,NativeGearSelected);
  // A live native hero may revive the mirror; retain a pending revival across arrival retries.
  SendLife(hero,(guest.flags&(uint)Proto.GuestFlags.Dead)!=0||(pendingLifeCode==1&&guestStatus.lifeAck!=lifeSequence));
  returnPosition=CastleGround.Position(hero);health=(int)Property(Field(hero,"propertiesInspector"),"CurrentHp");Coords.ZPlane=hero.transform.position.z;Coords.ZBase=hero.transform.position.z-returnPosition.y*.6f;
  var dd=ddInstance.GetValue(null,null);if(dd!=null)bindings.Configure(dd);
  if(!continuing){AccessTools.Method(heroType,"ToIdleState")?.Invoke(hero,null);var handler=Field(hero,"moveHandler");AccessTools.Method(handler.GetType(),"reset")?.Invoke(handler,null);}
  haveSafeGuestFeet=false;Coords.ToMc(returnPosition,out posX,out _,out posZ);syncedX=posX;syncedY=Coords.YOffset;syncedZ=posZ;handoffResidual=double.PositiveInfinity;
  teleportSeq=(uint)(PeakLink.TickCount&0x7fffffff)+1;worldVisible=true;wanted=true;resume=false;requested=true;handoff=true;handoffStarted=Time.unscaledTime;nextHandoffDiagnostic=handoffStarted+3;status="正在同步 Minecraft 安全落脚点与房间碰撞";
  // Same-room cinematic resumption keeps the completed geometry epoch.
  if(collisionRoomKey!=roomKey){collision.Refresh(returnPosition,++epoch);collisionRoomKey=roomKey;}
  collision.RevalidateForArrival();
  Log.LogInfo($"Handoff requested: shadow feet {returnPosition}, MC ({posX},0,{posZ}), layer {Coords.RenderLayer}, seq {teleportSeq}, epoch {epoch}, continuing={continuing}");
 }
 private void Release(bool keepMode=false){
  RestorePresentationAnimator();
  struggleInput.Update(false,false,false,ReleaseForwardedInput,NativeStruggle);sortingRenderer=null;sortingObject=null;
  if(!keepMode){wanted=false;resume=false;}
  portraitHud.Release();equipment.Release();actions.Direction(Hero(),yaw,FirstPersonView,Vector2.zero);actions.Release();Link.PushInput((Proto.InputType)12,2);bindings.ReleaseAll(NativeKeyUp);requested=false;owns=false;handoff=false;Link.PushInput(Proto.InputType.ReleaseAll);
  forwardedKeys.Clear();forwardedMouse.Clear();
  foreach(var pair in originalRenderers)if(pair.Key!=null)pair.Key.enabled=pair.Value;haveSafeGuestFeet=false;originalRenderers.Clear();hidden=Array.Empty<Renderer>();
  gearIsolation.Update(Hero(),false,false);controlledHero=null;SyncCursor();overlay.Clear();status="已恢复失落城堡角色与操作";
 }
 private void Update(){
  long frameStart=System.Diagnostics.Stopwatch.GetTimestamp();
  
  measuredFrames++;double measurementMs=Millis(measurementStart);if(measurementMs>=1000){hostFrameMs=(float)(measurementMs/measuredFrames);measuredFrames=0;measurementStart=System.Diagnostics.Stopwatch.GetTimestamp();}
  Link.Heartbeat();bool alive=Link.GuestAlive;if(alive&&!Link.ReadGuestState(ref guest))guestReadMisses++;FramePolicy(alive);
  string now=SceneManager.GetActiveScene().name;
  string key=RoomKey(now);
  CastleHunterMirror.Choice.Observe(now=="boss_Evil_Hunter",SceneManager.GetActiveScene().handle,RoomReady());
  if(!Input.GetKey(KeyCode.Alpha9))hunterChoiceKeyHeld=false;
  if(HunterChoiceChord&&Input.GetKeyDown(KeyCode.Alpha9)){hunterChoiceKeyHeld=true;CastleHunterMirror.Choice.Toggle();ReleaseForwardedInput();Log.LogInfo("Hunter replacement choice: "+CastleHunterMirror.Choice.Hint);}
  if(sharedDead&&new[]{"MainMenu","GeniusUI","StatisticUI"}.Contains(now))newRunPending=true;
  if(scene!=now||roomKey!=key){bool preserve=wanted;worldVisible=wanted&&alive;render.Reset(wanted&&alive);scene=now;roomKey=key;SelectRoom(key);if(preserve){controlPhase=CastleControlPhase.Transition;SuspendControl();}epoch++;Log.LogInfo($"Room {roomKey}: origin={Coords.RoomX}, preserveControl={preserve}");}
  var hero=Hero();
  var nativeFeet=hero==null?Vector3.zero:CastleGround.Position(hero);
  bool paused=NativePaused();
  if(paused!=nativeWasPaused){if(paused&&(owns||handoff))StopBridgeInput();rawMouse.Drain();nativeWasPaused=paused;Log.LogInfo($"Native pause changed: paused={paused}, layer={NativeInputLayer()}, timescale={Time.timeScale}; ownership and arrival sequence retained");}
  spawnStability.Observe(roomKey,hero==null?0:hero.GetInstanceID(),nativeFeet.x,nativeFeet.y,Time.unscaledTime,RoomReady()&&!owns);
  if(wanted&&alive&&hero!=null&&RoomReady()&&collisionRoomKey!=roomKey){
   collision.Refresh(nativeFeet,++epoch);collisionRoomKey=roomKey;
   Log.LogInfo($"Prewarming room collision during native transition: {roomKey}, epoch={epoch}");
  }
  controlPhase=CastleControlGate.Evaluate(wanted,alive,(guest.flags&(uint)Proto.GuestFlags.InWorld)!=0,hero!=null&&NativeAlive(hero),RoomReady(),NativeScripted());
  if(wanted&&controlPhase!=CastleControlPhase.Ready&&!sharedDead)SuspendControl();
  worldVisible=wanted&&!sharedDead&&alive&&hero!=null&&PlayScene();
  if(worldVisible)CaptureHero(hero!);
  if(handoff&&hero!=null&&CastleSpawnStability.Moved(returnPosition.x,returnPosition.y,nativeFeet.x,nativeFeet.y)){Log.LogInfo($"Native spawn moved during handoff: {returnPosition} -> {nativeFeet}; invalidate pending arrival and wait for stable native feet.");SuspendControl();}
  if(wanted&&!owns&&hero!=null){var feet=CastleGround.Position(hero);Coords.ZBase=hero.transform.position.z-feet.y*.6f;Coords.ToMc(feet,out syncedX,out _,out syncedZ);syncedY=Coords.YOffset;if(!handoff){posX=syncedX;posZ=syncedZ;}}

  if(Input.GetKeyDown(KeyCode.F8)){if(wanted)Release();else Begin();}
  if(resume&&wanted&&Time.unscaledTime>=resumeAfter&&controlPhase==CastleControlPhase.Ready&&spawnStability.Stable)Begin();
  if(handoff)handoffResidual=Math.Sqrt((guest.x-posX)*(guest.x-posX)+(guest.z-posZ)*(guest.z-posZ));
  if(handoff&&controlPhase==CastleControlPhase.Ready&&hero!=null&&CastleHandoff.Accept(collision.Ready,(guest.flags&(uint)Proto.GuestFlags.InWorld)!=0,teleportSeq,epoch,guest.teleportAck,guestStatus.arrivalSeq,guestStatus.arrivalEpoch,lifeSequence,guestStatus.lifeAck,posX,posZ,Coords.YOffset,guest.x,guest.y,guest.z,guestStatus.arrivalX,guestStatus.arrivalY,guestStatus.arrivalZ)){
   confirmedLifeHero=hero!.GetInstanceID();confirmedLifeRoom=roomKey;owns=true;handoff=false;syncedX=guest.x;syncedY=guest.y;syncedZ=guest.z;posX=guest.x;posZ=guest.z;CastleGround.Place(hero!,Coords.Feet(posX,posZ));status="Minecraft 原生玩家接管；F8 返回";
   Log.LogInfo($"Handoff accepted: shadow={CastleGround.Position(hero!)}, guest=({guest.x},{guest.y},{guest.z}), spawn clearance={handoffResidual:F4}, collision epoch={epoch}");
  }
  if(handoff&&!NativePaused()&&Time.unscaledTime>=nextHandoffDiagnostic){nextHandoffDiagnostic=Time.unscaledTime+3;status=!collision.Ready?"等待房间碰撞数据":guestStatus.lifeAck!=lifeSequence?"等待 Minecraft 服务端生命确认":guest.teleportAck!=teleportSeq?"等待 Minecraft 安全落脚点确认":"等待两端落脚坐标一致";Log.LogWarning($"Handoff waiting {Time.unscaledTime-handoffStarted:F1}s: phase={controlPhase}, linked={alive}, inWorld={(guest.flags&1)!=0}, ready={collision.Ready}, life={guestStatus.lifeAck}/{lifeSequence}, arrival={guestStatus.arrivalSeq}@{guestStatus.arrivalEpoch}, teleport={guest.teleportAck}/{teleportSeq}, residual={handoffResidual:F3}, guestY={guest.y:F3}");}
  SyncCursor();
  Follow();
  if(wanted&&alive&&hero!=null&&RoomReady())collision.Tick();
  RetryLife();
  SyncHealth();
  CheckExits();
  long sectionStart=System.Diagnostics.Stopwatch.GetTimestamp();mobTargets.Update(Link,hero,worldVisible&&RoomReady()&&alive&&!NativePaused(),unchecked((int)epoch));mobTargetMs=Millis(sectionStart);actors.Update(Link,worldVisible&&alive);actorMs=Millis(sectionStart);
  if(actorMs>=8&&Time.unscaledTime>=nextActorSlowLog){nextActorSlowLog=Time.unscaledTime+5;Log.LogInfo($"CastleCraft actor slow: total_ms={actorMs:F3} targets_ms={mobTargetMs:F3} scan_ms={actors.ScanMs:F3} state_ms={actors.StateMs:F3} serialize_ms={actors.SerializeMs:F3}");}
  if(owns){
   bool menu=(guest.flags&(uint)Proto.GuestFlags.ScreenOpen)!=0,restrained=NativeRestrained(),suppressed=NativeInputSuppressed();
   struggleInput.Update(restrained&&!sharedDead&&!NativeDialog()&&!menu,bindings.StruggleLeft,bindings.StruggleRight,ReleaseForwardedInput,NativeStruggle);
   bool gameplay=!sharedDead&&!NativeDialog()&&!suppressed&&!restrained&&!menu;
   if(!sharedDead&&!NativeDialog())Aim();
   actions.Direction(hero,yaw,FirstPersonView,gameplay?bindings.Movement:Vector2.zero);
   bindings.AimingAtBlock=Link.ReadSelection(out _,out _);bindings.Pump(NativeAction,NativeKeyUp,gameplay);
   if(bindings.ModeChanged)ReleaseForwardedInput();
   gearIsolation.Update(hero,wanted,NativeGearSelected);
   if(!sharedDead&&!NativeDialog())PumpInput(restrained||suppressed,restrained);else ReleaseForwardedInput();
  }
  var state=new Proto.HostState{flags=(uint)(hero!=null&&controlPhase==CastleControlPhase.Ready?Proto.HostFlags.InGame:Proto.HostFlags.Loading),worldId=unchecked((uint)roomKey.GetHashCode()),collisionEpoch=epoch,posX=posX,posY=Coords.YOffset,posZ=posZ,yaw=yaw,pitch=pitch,teleportSeq=teleportSeq,viewportW=(uint)Screen.width,viewportH=(uint)Screen.height,gameHour=12};
  // Scripted presentation does not wait for collision export. Loading still blocks physics/input.
  bool cinematicWorld=wanted&&alive&&hero!=null&&RoomReady()&&NativeScripted()&&!NativePaused();
  if(cinematicWorld)state.flags=(uint)(Proto.HostFlags.InGame|Proto.HostFlags.Cinematic)|(collision.Available?0u:(uint)Proto.HostFlags.Loading);
  else if(!requested||NativePaused()||handoff&&!collision.Ready)state.flags|=(uint)Proto.HostFlags.Loading;
  CastleHunterMirror.Update(worldVisible&&alive);Link.WriteHostState(state);while(Link.PopEvent(out var e)){if(worldVisible&&alive){if(e.type==11&&e.flags==epoch){actors.AcknowledgeHunter(e.formId);}else if(e.type==10&&RoomReady()&&!NativeScripted()&&!NativePaused()){actors.HealHunter(e.formId,e.a);}else if(e.type==8&&RoomReady()&&owns&&!sharedDead&&!NativePaused()&&!NativeRestrained()&&actors.CanInteract(e.formId,hero)){actors.FocusInteraction(e.formId,hero);NativeAction("PickUp");}else if(RoomReady()&&owns&&!sharedDead&&!NativeScripted()&&!NativePaused()&&!NativeRestrained())actors.Hit(e,hero,collision,epoch);}}
  sectionStart=System.Diagnostics.Stopwatch.GetTimestamp();overlay.Update();overlayMs=Millis(sectionStart);bridgeMs=Millis(frameStart);
  if(Time.unscaledTime>nextStatus){nextStatus=Time.unscaledTime+1;WriteStatus();}
  TestCommands();
 }
 private void Follow(){
  if(!owns||sharedDead)return;var hero=Hero();if(hero==null)return;
  if((guest.flags&(uint)Proto.GuestFlags.OnGround)!=0&&guest.y>=Coords.YOffset-.1f){safeGuestFeet=new Vector3((float)guest.x,(float)guest.y,(float)guest.z);haveSafeGuestFeet=true;}
  if(guest.y<Coords.YOffset-2){if(Time.unscaledTime>=nextCorrection){nextCorrection=Time.unscaledTime+.5f;var safe=haveSafeGuestFeet?safeGuestFeet:new Vector3((float)posX,(float)Coords.YOffset+.25f,(float)posZ);Link.PushInput(Proto.InputType.CorrectPosition,1,(int)Math.Round(safe.x*1000),(int)Math.Round(safe.y*1000),(int)Math.Round(safe.z*1000));Log.LogWarning("Recovered guest below room floor without releasing control or resetting life.");}return;}
  var body=Property(hero,"NoumenonTransform") as Transform;if(body!=null){var local=body.localPosition;local.y=((Vector3)Property(hero,"M_noumenon_originLocalPos")).y+(float)Math.Max(0,guest.y-Coords.YOffset)*Coords.Scale;body.localPosition=local;}
  CastlePose.Sample(guest,PeakLink.Qpc(),Link.QpcFrequency,out syncedX,out syncedY,out syncedZ);
  Vector3 floor=Coords.Feet(syncedX,syncedZ);floor.z=hero.transform.position.z;
  var shadow=Property(hero,"ShadowCollider") as Collider2D;Vector2 size=shadow!=null?(Vector2)shadow.bounds.size:new Vector2(.6f,.3f);
  // Minecraft already resolves the exported room collision. A second,
  // interpolated collision motor followed by server teleports caused snaps
  // into walls and stale correction packets fighting normal movement.
  Vector3 constrained=floor;
  if((constrained-floor).sqrMagnitude>.0001f){Coords.ToMc(constrained,out syncedX,out _,out syncedZ);if(Time.unscaledTime>=nextCorrection){nextCorrection=Time.unscaledTime+.1f;nativeCollisionCorrections++;Link.PushInput(Proto.InputType.CorrectPosition,0,(int)Math.Round(syncedX*1000),(int)Math.Round(guest.y*1000),(int)Math.Round(syncedZ*1000));}}
  CastleGround.Place(hero,constrained);posX=syncedX;posZ=syncedZ;
 }
 private void LateUpdate(){
  if(FirstPersonView){var latest=guest;if(Link.ReadGuestState(ref latest))look.Resolve(latest.yaw,latest.pitch,latest.lookAck,latest.sensitivity,latest.renderFlags,out yaw,out pitch);}
  var hero=worldVisible?Hero():null;
  if(hero!=null){CaptureHero(hero);UpdateNativeSorting();Coords.ZPlane=hero.transform.position.z;Coords.ZBase=hero.transform.position.z-CastleGround.Position(hero).y*.6f;}
  long started=System.Diagnostics.Stopwatch.GetTimestamp();actions.Tick(hero,wanted&&worldVisible&&Link.GuestAlive,bindings.CastleMouse,NativeScripted(),NativePaused());weaponSlot.Update(hero);
  var feet=Coords.ToUnity(syncedX,syncedY,syncedZ);
  equipmentBeforeDraw??=UpdateEquipmentBeforeAvatar;
  render.Update(worldVisible&&PlayScene(),feet,guest.yaw,firstPerson:FirstPersonView,showAvatar:wanted&&Link.GuestAlive&&!FirstPersonView,preserveAvatar:wanted,beforeDraw:equipmentBeforeDraw);
  guestRenderMs=Millis(started);long stageStarted=System.Diagnostics.Stopwatch.GetTimestamp();
  if(FirstPersonView)stage3D.Draw(roomKey,collision,hero,Coords.Canonical(syncedX,syncedY+Math.Max(.1f,guest.eyeHeight),syncedZ),yaw);else stage3D.RestoreAnimations();
  stageRenderMs=Millis(stageStarted);
  bool replaceNative=worldVisible&&render.HasAvatar;
  foreach(var pair in originalRenderers)if(pair.Key!=null)pair.Key.enabled=replaceNative?false:pair.Value;
  portraitHud.Update(wanted&&Link.GuestAlive,render.Portrait,Hero());
  renderMs=Millis(started);
 }
 private void UpdateEquipmentBeforeAvatar(){equipment.LateUpdate(owns?Hero():null,owns,Coords.ToUnity(syncedX,syncedY,syncedZ),actions.NativeWeaponVisible,FirstPersonView);}
 private static bool HunterChoiceChord=>Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt);
 private bool hunterChoiceKeyHeld;
 internal static bool HunterChoiceReserved=>Instance!=null&&Instance.hunterChoiceKeyHeld;
 private void PumpInput(bool blockMovement=false,bool blockCombat=false){
  bool menu=(guest.flags&(uint)Proto.GuestFlags.ScreenOpen)!=0;
  Input.imeCompositionMode=menu?IMECompositionMode.On:IMECompositionMode.Auto;
  foreach(var pair in keys){
   bool allowed=!(pair.Key==KeyCode.Alpha9&&hunterChoiceKeyHeld)&&(menu||(!bindings.Consumes(pair.Key)&&(!blockMovement||!bindings.MovementKey(pair.Key))));
   if(!allowed||Input.GetKeyUp(pair.Key)){if(forwardedKeys.Remove(pair.Key))Link.PushInput(Proto.InputType.Key,pair.Value,0);continue;}
   if(Input.GetKeyDown(pair.Key)||(Input.GetKey(pair.Key)&&!forwardedKeys.Contains(pair.Key))){Link.PushInput(Proto.InputType.Key,pair.Value,1);forwardedKeys.Add(pair.Key);}
  }
  Vector3 mouse=Input.mousePosition;Link.PushInput(Proto.InputType.Cursor,0,(int)mouse.x,Screen.height-(int)mouse.y);
  foreach(var pair in new[]{(0,(ushort)1),(1,(ushort)3),(2,(ushort)2),(3,(ushort)4),(4,(ushort)5),(5,(ushort)6),(6,(ushort)7)}){if((Input.GetMouseButtonDown(pair.Item1)||(Input.GetMouseButton(pair.Item1)&&!forwardedMouse.Contains(pair.Item1)))&&(menu||(!blockCombat&&!bindings.Consumes((KeyCode)((int)KeyCode.Mouse0+pair.Item1))))){Link.PushInput(Proto.InputType.MouseButton,pair.Item2,1);forwardedMouse.Add(pair.Item1);}if(Input.GetMouseButtonUp(pair.Item1)&&forwardedMouse.Remove(pair.Item1))Link.PushInput(Proto.InputType.MouseButton,pair.Item2,0);}
  if(Input.mouseScrollDelta.y!=0)Link.PushInput(Proto.InputType.Scroll,0,(int)(Input.mouseScrollDelta.y*120));
  if(menu){string text=Input.inputString;for(int i=0;i<text.Length;i++){
   int code=text[i];if(char.IsHighSurrogate(text[i])&&i+1<text.Length&&char.IsLowSurrogate(text[i+1]))code=char.ConvertToUtf32(text[i],text[++i]);
   if(code>=32)Link.PushInput(Proto.InputType.Text,0,code);
  }}
 }
 private void Aim(){
  if((guest.flags&(uint)Proto.GuestFlags.ScreenOpen)!=0)return;
  if(!Application.isFocused){rawMouse.Drain();yaw=guest.yaw;pitch=guest.pitch;return;}
  rawMouse.Install();rawMouse.Enabled=FirstPersonView&&Application.isFocused;
  var delta=rawMouse.Drain();
  if(FirstPersonView){if(delta!=Vector2.zero){int x=(int)delta.x,y=(int)delta.y;Link.PushInput((Proto.InputType)15,0,x,y,unchecked((int)look.Add(x,y,guest.lookAck)));}look.Resolve(guest.yaw,guest.pitch,guest.lookAck,guest.sensitivity,guest.renderFlags,out yaw,out pitch);return;}
  var camera=Camera.allCameras.FirstOrDefault(c=>c.name=="Main Camera");if(camera==null)return;
  Ray ray=camera.ScreenPointToRay(Input.mousePosition);if(Math.Abs(ray.direction.z)<.0001f)return;
  ray.origin+=Vector3.up*CameraLift;
  Vector3 point=ray.origin+ray.direction*((Coords.ZPlane-ray.origin.z)/ray.direction.z);
  Vector3 target;if(!actors.Target(new Vector2(point.x,point.y),out target)){Coords.ToMc(point,out double x,out _,out double z);target=new Vector3((float)x,0,(float)z);}
  float dx=target.x-(float)guest.x,dz=target.z-(float)guest.z,dy=target.y-((float)guest.y+1.62f);
  yaw=Mathf.Atan2(-dx,dz)*Mathf.Rad2Deg;pitch=Mathf.Atan2(-dy,Mathf.Sqrt(dx*dx+dz*dz))*Mathf.Rad2Deg;
 }
 private void WriteStatus(){if(session.Length==0)return;try{File.WriteAllText(Path.Combine(session,"passthrough.status"),$"linked={Link.GuestAlive}\nowns={owns}\nhandoff={handoff}\nteleport_seq={teleportSeq}\nteleport_ack={guest.teleportAck}\noverlay={overlay.HasFrame}\nscene={scene}\nguest={guest.x},{guest.y},{guest.z}\nflags={guest.flags}\nlayer={Coords.RenderLayer}\ncamera_count={Camera.allCamerasCount}\nhost_frame_ms={hostFrameMs}\nhost_fps={1000/Math.Max(.001f,hostFrameMs)}\nbridge_ms={bridgeMs}\nactor_ms={actorMs}\nmob_target_ms={mobTargetMs}\nactor_scan_ms={actors.ScanMs}\nactor_state_ms={actors.StateMs}\nactor_serialize_ms={actors.SerializeMs}\nrender_ms={renderMs}\noverlay_ms={overlayMs}\nshared_dead={sharedDead}\nlife_seq={lifeSequence}\nlife_ack={guestStatus.lifeAck}\nhealth={guestStatus.health}/{guestStatus.maximum}\ncollision_ready={collision.Ready}\ncollision_pending={collision.PendingRegions}\nnative_action={actions.Action}\ncastle_mouse={bindings.CastleMouse}\ncastle_equipment_enabled={bindings.CastleEquipmentEnabled}\ncastle_stat_policy=mc_hp_armor_speed_native_weapon_attack_only\nfirst_person={FirstPersonView}\nyaw={yaw}\npitch={pitch}\nfirst_person_native_voxel_models={stage3D.NativeVoxelModels}\nfirst_person_native_voxel_pending={stage3D.NativeVoxelPending}\nfirst_person_native_textures={stage3D.NativeTextureCopies}\nfirst_person_native_texture_pending={stage3D.NativeTexturePending}\nraw_mouse_events={rawMouse.Events}\nguest_render_ms={guestRenderMs}\nstage_render_ms={stageRenderMs}\nnative_weapon_visible={actions.NativeWeaponVisible}\nnative_weapon_parts={equipment.VisibleParts}\nweapon_slot_id={weaponSlot.Id}\nweapon_slot_available={weaponSlot.Available}\nhandoff_residual={handoffResidual}\nwanted={wanted}\ncontrol_phase={controlPhase}\nroom_ready={RoomReady()}\nspawn_stable={spawnStability.Stable}\nentry_walk={Hero()!=null&&NativeEnteringRoom(Hero()!)}\navatar_ready={render.HasAvatar}\nworld_visible={worldVisible}\nnative_renderer_count={originalRenderers.Count}\nscripted={NativeScripted()}\narrival_seq={guestStatus.arrivalSeq}\narrival_epoch={guestStatus.arrivalEpoch}\nguest_read_misses={guestReadMisses}\ninput_suppressed={NativeInputSuppressed()}\nrestrained={NativeRestrained()}\nframe_limit={Application.targetFrameRate}\nvsync={QualitySettings.vSyncCount}");}catch{}}
 private void TestCommands(){
  if(Environment.GetEnvironmentVariable("CASTLECRAFT_TEST")!="1"||session.Length==0)return;string path=Path.Combine(session,"host.command");if(!File.Exists(path))return;string cmd=File.ReadAllText(path).Trim();File.Delete(path);
  if(cmd=="toggle"){if(owns||handoff)Release();else Begin();}
  if(cmd=="inventory"){Link.PushInput(Proto.InputType.Key,8,1);Link.PushInput(Proto.InputType.Key,8,0);}
  if(cmd=="renderers")File.WriteAllLines(Path.Combine(session,"native-renderers.txt"),UnityEngine.Object.FindObjectsOfType<SpriteRenderer>().Select(r=>$"{r.name}|parent={r.transform.parent?.name}|sprite={r.sprite?.name}|position={r.transform.position}|bounds={r.bounds}|layer={r.gameObject.layer}"));
  if(cmd=="start-solo"){var type=AccessTools.TypeByName("Teran.MainMenu");var menu=Resources.FindObjectsOfTypeAll(type).Cast<Component>().FirstOrDefault(x=>x.gameObject.activeInHierarchy);if(menu!=null)AccessTools.Method(type,"SinglePlayer").Invoke(menu,null);}
  if(cmd=="colliders")File.WriteAllLines(Path.Combine(session,"native-colliders.txt"),Resources.FindObjectsOfTypeAll<Collider2D>().Where(c=>c.gameObject.scene.IsValid()&&c.gameObject.scene.isLoaded&&c.gameObject.activeInHierarchy).Select(c=>$"{c.name}|{c.transform.parent?.name}|layer={c.gameObject.layer}|tag={c.tag}|trigger={c.isTrigger}|enabled={c.enabled}|bounds={c.bounds}"));
 }
 private void OnGUI(){if(owns&&!sharedDead){bool menu=(guest.flags&(uint)Proto.GuestFlags.ScreenOpen)!=0;overlay.Draw(menu,new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y));if(!menu){equipment.DrawFirstPerson();weaponSlot.Draw(bindings.CastleMouse,bindings.ToggleLabel);}if(!menu)GUI.Label(new Rect(15,Screen.height-125,Screen.width-30,30),"F8 切换角色 · F5 视角 · "+bindings.Hint);}else if(Link!=null&&Link.GuestAlive)GUI.Label(new Rect(15,Screen.height-65,Screen.width-30,30),status+" · F8 切换");GUI.Label(new Rect(15,Screen.height-165,Screen.width-30,28),"Alt+9 Minecraft Boss 替换："+CastleHunterMirror.Choice.Hint);}
 private void CameraBefore(Camera camera){
  if(worldVisible&&render.HasAvatar)foreach(var pair in originalRenderers)if(pair.Key!=null)pair.Key.enabled=false;
  if(camera.name!="Main Camera")return;
  // A missed post-render must not leave a first-person matrix in the next frame.
  if(cameraPositions.ContainsKey(camera))CameraAfter(camera);
  var view=camera.worldToCameraMatrix;var projection=camera.projectionMatrix;
  var automaticView=Matrix4x4.Scale(new Vector3(1,1,-1))*Matrix4x4.TRS(camera.transform.position,camera.transform.rotation,Vector3.one).inverse;
  var automaticProjection=camera.orthographic?Matrix4x4.Ortho(-camera.orthographicSize*camera.aspect,camera.orthographicSize*camera.aspect,-camera.orthographicSize,camera.orthographicSize,camera.nearClipPlane,camera.farClipPlane):Matrix4x4.Perspective(camera.fieldOfView,camera.aspect,camera.nearClipPlane,camera.farClipPlane);
  cameraPositions[camera]=new CameraSnapshot{Position=camera.transform.position,Rotation=camera.transform.rotation,Ortho=camera.orthographic,Fov=camera.fieldOfView,Near=camera.nearClipPlane,Far=camera.farClipPlane,Size=camera.orthographicSize,Mask=camera.cullingMask,Clear=camera.clearFlags,Background=camera.backgroundColor,View=view,Projection=projection,Culling=camera.cullingMatrix,AutoView=SameMatrix(view,automaticView),AutoProjection=SameMatrix(projection,automaticProjection),AutoCulling=SameMatrix(camera.cullingMatrix,projection*view)};
  camera.cullingMask&=~(1<<Coords.AvatarLayer);
  if(FirstPersonView){
   // A real perspective view of the existing 2.5D stage; native sprites have no hidden 3D surfaces.
   float yr=yaw*Mathf.Deg2Rad,pr=pitch*Mathf.Deg2Rad;
   Vector3 direction=new Vector3(-Mathf.Sin(yr)*Mathf.Cos(pr),-Mathf.Sin(pr),-Mathf.Cos(yr)*Mathf.Cos(pr));
   camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.07f,.06f,1);
   camera.orthographic=false;camera.fieldOfView=Mathf.Clamp(guest.fovDeg>0?guest.fovDeg:70,40,110);camera.nearClipPlane=.025f;camera.farClipPlane=Math.Max(camera.farClipPlane,200);camera.ResetProjectionMatrix();
   camera.transform.position=Coords.ToUnity(syncedX,syncedY+Math.Max(.1f,guest.eyeHeight),syncedZ);
   var rotation=Quaternion.LookRotation(direction,Vector3.up);
   camera.transform.rotation=rotation;
   var eye=Coords.Canonical(syncedX,syncedY+Math.Max(.1f,guest.eyeHeight),syncedZ);
   camera.worldToCameraMatrix=Matrix4x4.Scale(new Vector3(1,1,-1))*Matrix4x4.TRS(eye,rotation,Vector3.one).inverse*Coords.Unproject;
   camera.cullingMask=(1<<CastleStage3D.Layer)|(1<<Coords.GeometryLayer);
   camera.cullingMatrix=camera.projectionMatrix*camera.worldToCameraMatrix;
  }else camera.transform.position+=Vector3.up*CameraLift;
 }
 private static bool SameMatrix(Matrix4x4 a,Matrix4x4 b){for(int i=0;i<16;i++)if(Math.Abs(a[i]-b[i])>.0001f)return false;return true;}
 private void CameraAfter(Camera camera){if(cameraPositions.TryGetValue(camera,out var original)){
  camera.transform.SetPositionAndRotation(original.Position,original.Rotation);camera.orthographic=original.Ortho;camera.fieldOfView=original.Fov;camera.nearClipPlane=original.Near;camera.farClipPlane=original.Far;camera.orthographicSize=original.Size;camera.cullingMask=original.Mask;camera.clearFlags=original.Clear;camera.backgroundColor=original.Background;
  if(original.AutoView)camera.ResetWorldToCameraMatrix();else camera.worldToCameraMatrix=original.View;
  if(original.AutoProjection)camera.ResetProjectionMatrix();else camera.projectionMatrix=original.Projection;
  if(original.AutoCulling)camera.ResetCullingMatrix();else camera.cullingMatrix=original.Culling;
  cameraPositions.Remove(camera);
 }}
 private void OnDestroy(){mobTargets.Dispose();foreach(var camera in cameraPositions.Keys.ToArray())if(camera!=null)CameraAfter(camera);rawMouse.Dispose();Camera.onPreCull-=CameraBefore;Camera.onPostRender-=CameraAfter;stage3D.Dispose();portraitHud.Dispose();render?.DisposePortraits();overlay?.Dispose();if(Link!=null){if(wanted)Release();FramePolicy(false);Link.Close();}}
}





