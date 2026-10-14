using System;
namespace PeakCraft;
/// The native cocoon accepts held Left/Right. Changing routes releases the previous game's input.
internal sealed class CastleStruggleInput {
 private bool active;
 public void Update(bool enabled,bool left,bool right,Action release,Action<string> invoke){
  if(active!=enabled){active=enabled;release();}
  if(!enabled)return;
  if(left)invoke("Left");
  if(right)invoke("Right");
 }
}
