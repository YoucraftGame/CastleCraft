namespace PeakCraft.World;
/// Player opt-in plus a real exit/re-entry. AI phase changes do not create another visit.
internal sealed class CastleHunterChoice {
 internal bool Enabled {get;private set;}
 internal int Visits {get;private set;}
 internal int SceneHandle {get;private set;}
 internal bool Inside {get;private set;}
 internal bool Recognized {get;private set;}
 internal bool Active=>Enabled&&Inside&&Visits>=2&&Recognized;
 internal void Toggle()=>Enabled=!Enabled;
 internal void Observe(bool targetScene,int sceneHandle,bool ready){
  if(!targetScene){Inside=false;Recognized=false;return;}
  if(!ready||Inside&&sceneHandle==SceneHandle)return;
  Inside=true;SceneHandle=sceneHandle;Visits++;Recognized=false;
 }
 internal void Recognize(int sceneHandle,bool validHunter){if(Inside&&sceneHandle==SceneHandle&&validHunter)Recognized=true;}
 internal string Hint=>!Enabled?"原版 Boss（默认）":!Inside?"已开启，等待再次进入 Boss 房":Visits<2?"首次进房保留原版，离开再进入后识别":!Recognized?"再次进房，等待识别 Boss":"Minecraft Boss 替换已启用";
}
