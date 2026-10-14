namespace PeakCraft;
internal static class CastleNativeFlow {
 // Listener dispatches UI/cinematic observers, including the native ESC-release boss skip.
 // Let it run without giving the gameplay HandleInput/UpdateMove hooks back to the hero.
 internal static bool NativeController(bool intercept,bool dead,bool scripted,string layer)=>!intercept||dead||scripted||layer=="Listener";
 internal static bool Scripted(bool otherInput,bool talking,string layer,bool bossPainting=false,bool enteringRoom=false)=>otherInput||talking||layer=="IGNORE"||bossPainting||enteringRoom;
 internal static bool Paused(float timeScale,string layer,bool scripted)=>timeScale<=0||!scripted&&layer!="GAME";
}
