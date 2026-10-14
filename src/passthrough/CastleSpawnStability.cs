using System;
namespace PeakCraft.World;
/// <summary>Wait for native birth/script positioning before publishing a teleport target.</summary>
internal sealed class CastleSpawnStability {
 private string room="";
 private int hero;
 private double x,z,since;
 private bool observed;
 internal bool Stable{get;private set;}
 internal void Observe(string currentRoom,int currentHero,double nextX,double nextZ,double now,bool ready){
  if(!ready||currentHero==0||!Finite(nextX)||!Finite(nextZ)){observed=Stable=false;return;}
  if(!observed||room!=currentRoom||hero!=currentHero||Moved(x,z,nextX,nextZ)){
   room=currentRoom;hero=currentHero;x=nextX;z=nextZ;since=now;observed=true;Stable=false;return;
  }
  Stable=now-since>=.25;
 }
 private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
 internal static bool Moved(double x,double z,double nextX,double nextZ)=>
  !Finite(nextX)||!Finite(nextZ)||(nextX-x)*(nextX-x)+(nextZ-z)*(nextZ-z)>.025*.025;
}
