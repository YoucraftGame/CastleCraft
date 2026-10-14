using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;
using PeakCraft.Link;

namespace PeakCraft.World;

/// <summary>Exports native collision incrementally. Initial handoff is released only after a complete room.</summary>
internal sealed unsafe class CastleCollision {
 private sealed class ObstacleSnapshot {
  public Collider2D Collider=null!;
  public Bounds Bounds;
  public bool Wall;
 }
 private sealed class RegionData {
  public byte[] Triangles=Array.Empty<byte>(),Floor=Array.Empty<byte>();
  public byte[]? Body;
 }
 private sealed class RegionJob {
  public (int x,int z) Key;
  public int Column;
  public readonly List<Proto.ColBlock> Blocks=new(64);
  public readonly List<ObstacleSnapshot> Candidates=new();
 }
 internal const int WallHeightMc=8;
 private const double BudgetMilliseconds=1.75;
 private readonly PeakLink link;
 private Rect bounds;
 private Collider2D[] tracked=Array.Empty<Collider2D>();
 private readonly RaycastHit2D[] hits=new RaycastHit2D[64];
 private readonly Dictionary<int,ObstacleSnapshot> previous=new();
 private readonly Dictionary<(int x,int z),List<ObstacleSnapshot>> spatial=new();
 private readonly Dictionary<(int x,int z),RegionData> regions=new();
 private readonly Queue<(int x,int z)> floors=new(),bodies=new();
 private readonly HashSet<(int x,int z)> queuedBodies=new();
 private readonly HashSet<(int x,int z)> dirty=new();
 private readonly List<ObstacleSnapshot> near=new();
 private RegionJob? job;
 private uint currentEpoch;
 private int stageId,rxMin,rxMax,rzMin,rzMax,floorPart;
 private double roomX;
 private bool initialized,clearing,initializing,boundaryPending;
 private float nextRefresh,nextDiscovery;
 public bool Available=>initialized&&!initializing&&!clearing;
 public bool Ready=>Available&&bodies.Count==0&&floors.Count==0;
 public void RevalidateForArrival(){if(!initialized)return;tracked=UnityEngine.Object.FindObjectsOfType<Collider2D>();CaptureObstacles();nextRefresh=Time.unscaledTime+.5f;}
 public int PendingRegions=>bodies.Count+floors.Count;
 public Rect RoomBounds=>bounds;
 public IEnumerable<Bounds> WallBounds {get {foreach(var entry in previous.Values)if(entry.Wall)yield return entry.Bounds;}}
 public CastleCollision(PeakLink link){this.link=link;}
 private static bool Obstacle(Collider2D c)=>c!=null&&c.gameObject.scene.IsValid()&&c.gameObject.scene.isLoaded&&c.gameObject.activeInHierarchy&&c.enabled&&!c.isTrigger&&(c.CompareTag("wall")||c.CompareTag("move_collider"));
 private bool InRoom(Bounds b)=>bounds.Overlaps(Rect.MinMaxRect(b.min.x,b.min.y,b.max.x,b.max.y));
 private static float Field(Component stage,string name)=>Convert.ToSingle(AccessTools.Field(stage.GetType(),name).GetValue(stage));

 public void Refresh(Vector3 origin,uint epoch){
  var ddType=AccessTools.TypeByName("Teran.DDSystem");var dd=AccessTools.Property(ddType,"Instance").GetValue(null,null);var ctrl=dd==null?null:AccessTools.Field(ddType,"stageCtrl").GetValue(dd);var stage=ctrl==null?null:AccessTools.Field(ctrl.GetType(),"cur_stage").GetValue(ctrl) as Component;
  if(stage==null)throw new InvalidOperationException("Native GameStage bounds unavailable");
  float cameraMinX=Field(stage,"camera_min_x"),cameraMaxX=Field(stage,"camera_max_x"),cameraMinY=Field(stage,"camera_min_y"),groundMaxY=Field(stage,"up_ground_y");
  bool unbounded=CastleCollisionBounds.Unbounded(cameraMinX)||CastleCollisionBounds.Unbounded(cameraMaxX)||CastleCollisionBounds.Unbounded(cameraMinY)||CastleCollisionBounds.Unbounded(groundMaxY);
  bool hasWalls=false;Bounds walls=default;
  if(unbounded)foreach(var c in stage.GetComponentsInChildren<Collider2D>()){
   if(!Obstacle(c)||!c.CompareTag("wall")||c.gameObject.scene!=stage.gameObject.scene)continue;
   if(!hasWalls){walls=c.bounds;hasWalls=true;}else walls.Encapsulate(c.bounds);
  }
  if(!CastleCollisionBounds.TryAxis(cameraMinX,cameraMaxX,Field(stage,"offSet_x"),hasWalls,walls.min.x,walls.max.x,Coords.Scale,out double minX,out double maxX)
   ||!CastleCollisionBounds.TryAxis(cameraMinY,groundMaxY,Field(stage,"offSet_y"),hasWalls,walls.min.y,walls.max.y,Coords.DepthScale,out double minY,out double maxY))
   throw new InvalidOperationException("Native unbounded camera has no finite room wall bounds; collision export refused");
  var nextBounds=Rect.MinMaxRect((float)minX,(float)minY,(float)maxX,(float)maxY);
  bool sameRoom=initialized&&stageId==stage.GetInstanceID()&&roomX==Coords.RoomX&&bounds==nextBounds;
  if(sameRoom&&epoch==currentEpoch)return;
  if(unbounded)Plugin.Log.LogInfo($"Native camera limits replaced for collision only: stage={stage.gameObject.scene.name}, cameraX=[{cameraMinX},{cameraMaxX}], walls={walls}, room={nextBounds}");
  bounds=nextBounds;stageId=stage.GetInstanceID();roomX=Coords.RoomX;currentEpoch=epoch;
  rxMin=(int)Math.Floor((bounds.xMin/Coords.Scale+roomX)/8);rxMax=(int)Math.Floor((bounds.xMax/Coords.Scale+roomX)/8);
  rzMin=(int)Math.Floor(-bounds.yMax/Coords.DepthScale/8);rzMax=(int)Math.Floor(-bounds.yMin/Coords.DepthScale/8);
  floors.Clear();bodies.Clear();queuedBodies.Clear();job=null;floorPart=0;
  if(!sameRoom){regions.Clear();previous.Clear();spatial.Clear();}
  initialized=true;initializing=true;clearing=true;boundaryPending=true;
  // Find scene instances only. Resources.FindObjectsOfTypeAll also walks inactive assets and prefabs.
  tracked=UnityEngine.Object.FindObjectsOfType<Collider2D>();CaptureObstacles();
  nextRefresh=Time.unscaledTime+.5f;nextDiscovery=Time.unscaledTime+2f;
  int anchorX=Mathf.Clamp((int)Math.Floor((origin.x/Coords.Scale+roomX)/8),rxMin,rxMax),anchorZ=Mathf.Clamp((int)Math.Floor(-origin.y/Coords.DepthScale/8),rzMin,rzMax);
  var order=new List<(int x,int z)>();
  for(int x=rxMin;x<=rxMax;x++)for(int z=rzMin;z<=rzMax;z++)order.Add((x,z));
  order.Sort((a,b)=>Distance(a,anchorX,anchorZ).CompareTo(Distance(b,anchorX,anchorZ)));
  foreach(var key in order)floors.Enqueue(key);
  foreach(var key in order)if(key.x!=anchorX||key.z!=anchorZ)QueueBody(key);
  // The guest handoff waits for the body region at its feet. Publish that region last,
  // after every support and wall region, so it cannot walk into unexported geometry.
  QueueBody((anchorX,anchorZ));
  Plugin.Log.LogInfo($"Native collision queued {order.Count} room regions; reuse={sameRoom}, native colliders={previous.Count}. Export uses a {BudgetMilliseconds}ms frame budget.");
  Tick();
 }
 private static long Distance((int x,int z) key,int x,int z){long dx=key.x-x,dz=key.z-z;return dx*dx+dz*dz;}
 private bool RegionInRoom((int x,int z) key)=>key.x>=rxMin&&key.x<=rxMax&&key.z>=rzMin&&key.z<=rzMax;
 private void QueueBody((int x,int z) key){if(RegionInRoom(key)&&queuedBodies.Add(key))bodies.Enqueue(key);}

 /// <summary>Call every host frame while handoff is pending as well as while the guest owns control.</summary>
 public void Tick(){
  if(!initialized)return;
  long started=Stopwatch.GetTimestamp();
  if(!initializing&&Time.unscaledTime>=nextRefresh){
   nextRefresh=Time.unscaledTime+.5f;
   if(Time.unscaledTime>=nextDiscovery){tracked=UnityEngine.Object.FindObjectsOfType<Collider2D>();nextDiscovery=Time.unscaledTime+2f;}
   CaptureObstacles();
  }
  if(clearing){uint epoch=currentEpoch;if(!link.WriteCollision(Proto.ColClear,&epoch,sizeof(uint)))return;clearing=false;}
  if(boundaryPending){
   var room=new Proto.ColRoomBounds{epoch=currentEpoch,minX=bounds.xMin/Coords.Scale+roomX,maxX=bounds.xMax/Coords.Scale+roomX,minZ=-bounds.yMax/Coords.DepthScale,maxZ=-bounds.yMin/Coords.DepthScale};
   if(!link.WriteCollision(Proto.ColRoom,&room,(uint)sizeof(Proto.ColRoomBounds)))return;boundaryPending=false;
  }
  while(Elapsed(started)<BudgetMilliseconds&&link.CollisionBacklog<512*1024){
   if(floors.Count>0){
    var key=floors.Peek();var data=Region(key);
    if(floorPart==0){if(!Send(Proto.ColTris,data.Triangles))return;floorPart=1;}
    if(!Send(Proto.ColRegion,data.Floor))return;
    floorPart=0;floors.Dequeue();continue;
   }
   if(bodies.Count==0){if(initializing){initializing=false;nextRefresh=Time.unscaledTime+.5f;Plugin.Log.LogInfo($"Native collision room export completed: {regions.Count} regions; handoff support and walls ready.");}return;}
   var bodyKey=bodies.Peek();var bodyData=Region(bodyKey);
   if(bodyData.Body==null){
    if(job==null||job.Key!=bodyKey){job=new RegionJob{Key=bodyKey};if(spatial.TryGetValue(bodyKey,out var candidates))job.Candidates.AddRange(candidates);}
    RasterColumn(job);
    if(job.Column<64)continue;
    bodyData.Body=Packet(Header(bodyKey,0,7,job.Blocks.Count),job.Blocks);job=null;
   }
   if(!Send(Proto.ColRegion,bodyData.Body))return;
   bodies.Dequeue();queuedBodies.Remove(bodyKey);
  }
 }
 private static double Elapsed(long started)=>(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;
 private bool Send(uint type,byte[] data){fixed(byte* p=data){((Proto.ColRegionHdr*)p)->epoch=currentEpoch;return link.WriteCollision(type,p,(uint)data.Length);}}
 private Proto.ColRegionHdr Header((int x,int z) key,int minY,int maxY,int count)=>new(){minX=key.x*8,minY=minY,minZ=key.z*8,maxX=key.x*8+7,maxY=maxY,maxZ=key.z*8+7,epoch=currentEpoch,count=(uint)count};
 private RegionData Region((int x,int z) key){
  if(regions.TryGetValue(key,out var cached))return cached;
  float x0=(float)Math.Max(key.x*8,bounds.xMin/Coords.Scale+roomX),x1=(float)Math.Min(key.x*8+8,bounds.xMax/Coords.Scale+roomX),z0=Math.Max(key.z*8,-bounds.yMax/Coords.DepthScale),z1=Math.Min(key.z*8+8,-bounds.yMin/Coords.DepthScale);
  var triangles=new List<Proto.ColTri>(2);var blocks=new List<Proto.ColBlock>(64);
  if(x0<x1&&z0<z1){
   Add(triangles,x0,0,z0,x1,0,z1,x1,0,z0);Add(triangles,x0,0,z0,x0,0,z1,x1,0,z1);
   for(int x=(int)Math.Floor(x0);x<x1;x++)for(int z=(int)Math.Floor(z0);z<z1;z++){
    ulong footprint=0;
    for(int sx=0;sx<8;sx++)for(int sz=0;sz<8;sz++)
     if(x+(sx+1)/8f>x0&&x+sx/8f<x1&&z+(sz+1)/8f>z0&&z+sz/8f<z1)footprint|=1UL<<(sx+sz*8);
    if(footprint==0)continue;
    var block=new Proto.ColBlock{x=x,y=-1,z=z};for(int i=0;i<8;i++)block.bits[i]=footprint;blocks.Add(block);
   }
  }
  cached=new RegionData{Triangles=Packet(Header(key,0,7,triangles.Count),triangles),Floor=Packet(Header(key,-8,-1,blocks.Count),blocks)};regions[key]=cached;return cached;
 }
 private static byte[] Packet(Proto.ColRegionHdr header,List<Proto.ColBlock> blocks){
  byte[] data=new byte[sizeof(Proto.ColRegionHdr)+blocks.Count*sizeof(Proto.ColBlock)];fixed(byte* p=data){*(Proto.ColRegionHdr*)p=header;var output=(Proto.ColBlock*)(p+sizeof(Proto.ColRegionHdr));for(int i=0;i<blocks.Count;i++)output[i]=blocks[i];}return data;
 }
 private static byte[] Packet(Proto.ColRegionHdr header,List<Proto.ColTri> triangles){
  byte[] data=new byte[sizeof(Proto.ColRegionHdr)+triangles.Count*sizeof(Proto.ColTri)];fixed(byte* p=data){*(Proto.ColRegionHdr*)p=header;var output=(Proto.ColTri*)(p+sizeof(Proto.ColRegionHdr));for(int i=0;i<triangles.Count;i++)output[i]=triangles[i];}return data;
 }
 private void RasterColumn(RegionJob work){
  int x=work.Key.x*8+work.Column/8,z=work.Key.z*8+work.Column%8;work.Column++;
  float minX=(float)(x-roomX)*Coords.Scale,maxX=minX+Coords.Scale,minY=-(z+1)*Coords.DepthScale,maxY=-z*Coords.DepthScale;
  near.Clear();
  foreach(var c in work.Candidates)if(c.Collider!=null&&c.Collider.enabled&&c.Bounds.max.x>minX&&c.Bounds.min.x<maxX&&c.Bounds.max.y>minY&&c.Bounds.min.y<maxY)near.Add(c);
  if(near.Count==0)return;
  ulong wall=0,movable=0;
  for(int sx=0;sx<8;sx++)for(int sz=0;sz<8;sz++){
   var point=new Vector2(minX+(sx+.5f)*Coords.Scale/8,maxY-(sz+.5f)*Coords.DepthScale/8);ulong bit=1ul<<(sz*8+sx);
   foreach(var c in near){if((c.Wall?(wall&bit):(movable&bit))!=0)continue;if(point.x<c.Bounds.min.x||point.x>c.Bounds.max.x||point.y<c.Bounds.min.y||point.y>c.Bounds.max.y)continue;if(c.Collider.OverlapPoint(point)){if(c.Wall)wall|=bit;else movable|=bit;}}
  }
  for(int y=0;y<WallHeightMc;y++){ulong mask=wall|(y<2?movable:0);if(mask==0)continue;var block=new Proto.ColBlock{x=x,y=y,z=z};for(int i=0;i<8;i++)block.bits[i]=mask;work.Blocks.Add(block);}
 }
 private void CaptureObstacles(){
  var updated=new Dictionary<int,ObstacleSnapshot>();dirty.Clear();bool changed=false;
  foreach(var collider in tracked){if(!Obstacle(collider))continue;var box=collider.bounds;if(!InRoom(box))continue;int id=collider.GetInstanceID();bool wall=collider.CompareTag("wall");
   if(previous.TryGetValue(id,out var old)&&old.Bounds==box&&old.Wall==wall)updated[id]=old;
   else {updated[id]=new ObstacleSnapshot{Collider=collider,Bounds=box,Wall=wall};Mark(box);if(old!=null)Mark(old.Bounds);changed=true;}
  }
  foreach(var pair in previous)if(!updated.ContainsKey(pair.Key)){Mark(pair.Value.Bounds);changed=true;}
  if(!changed)return;
  previous.Clear();foreach(var pair in updated)previous[pair.Key]=pair.Value;
  spatial.Clear();foreach(var collider in previous.Values){var b=collider.Bounds;int x0=Math.Max(rxMin,(int)Math.Floor((b.min.x/Coords.Scale+roomX)/8)),x1=Math.Min(rxMax,(int)Math.Floor((b.max.x/Coords.Scale+roomX)/8)),z0=Math.Max(rzMin,(int)Math.Floor(-b.max.y/Coords.DepthScale/8)),z1=Math.Min(rzMax,(int)Math.Floor(-b.min.y/Coords.DepthScale/8));
   for(int x=x0;x<=x1;x++)for(int z=z0;z<=z1;z++){var key=(x,z);if(!spatial.TryGetValue(key,out var list)){list=new List<ObstacleSnapshot>();spatial[key]=list;}list.Add(collider);}
  }
  foreach(var key in dirty){if(regions.TryGetValue(key,out var data))data.Body=null;if(job!=null&&job.Key==key)job=null;if(!initializing)QueueBody(key);}
 }
 private void Mark(Bounds b){
  int x0=Math.Max(rxMin,(int)Math.Floor((b.min.x/Coords.Scale+roomX)/8)),x1=Math.Min(rxMax,(int)Math.Floor((b.max.x/Coords.Scale+roomX)/8)),z0=Math.Max(rzMin,(int)Math.Floor(-b.max.y/Coords.DepthScale/8)),z1=Math.Min(rzMax,(int)Math.Floor(-b.min.y/Coords.DepthScale/8));
  for(int x=x0;x<=x1;x++)for(int z=z0;z<=z1;z++)dirty.Add((x,z));
 }
 public bool Contains(Vector3 point)=>bounds.Contains(new Vector2(point.x,point.y));
 public Vector3 Constrain(Vector3 from,Vector3 to,Vector2 size,float height){Vector2 start=new(from.x,from.y),delta=new(to.x-from.x,to.y-from.y),next=start;next+=Sweep(next,new Vector2(delta.x,0),size,height);next+=Sweep(next,new Vector2(0,delta.y),size,height);return new Vector3(next.x,next.y,from.z);}
 private Vector2 Sweep(Vector2 from,Vector2 delta,Vector2 size,float height){float distance=delta.magnitude;if(distance<.00001f)return Vector2.zero;int count=Physics2D.BoxCastNonAlloc(from,size,0,delta/distance,hits,distance);float allowed=distance;for(int i=0;i<count;i++){var c=hits[i].collider;if(c==null||c.isTrigger||!c.enabled||!(c.CompareTag("wall")||(height<1&&c.CompareTag("move_collider"))))continue;allowed=Math.Min(allowed,Math.Max(0,hits[i].distance-.001f));}return delta*(allowed/distance);}
 private static void Add(List<Proto.ColTri> list,float ax,float ay,float az,float bx,float by,float bz,float cx,float cy,float cz){var t=new Proto.ColTri{flags=Proto.TriTerrain};t.v[0]=ax;t.v[1]=ay;t.v[2]=az;t.v[3]=bx;t.v[4]=by;t.v[5]=bz;t.v[6]=cx;t.v[7]=cy;t.v[8]=cz;list.Add(t);}
}
