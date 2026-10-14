namespace PeakCraft.World;
/// Spread a discovery sweep over frames, preserving the one-second refresh interval.
internal sealed class CastleActorScanSchedule {
 private int cursor=-1,lastFrame=-1;
 private float next;
 internal int Take(float now,int frame,int count){
  if(count<=0||frame==lastFrame)return -1;
  if(cursor<0){if(now<next)return -1;cursor=0;next=now+1;}
  lastFrame=frame;int result=cursor++;
  if(cursor==count)cursor=-1;
  return result;
 }
 internal void Reset(){cursor=-1;lastFrame=-1;next=0;}
}
