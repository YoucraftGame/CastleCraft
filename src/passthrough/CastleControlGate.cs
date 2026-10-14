namespace PeakCraft;
// A live process is not an in-world player. Cinematics suspend control without cancelling intent.
internal enum CastleControlPhase { Native,ClientWaiting,HeroWaiting,Transition,Cinematic,Ready }
internal static class CastleControlGate {
 public static CastleControlPhase Evaluate(bool wanted,bool linked,bool inWorld,bool liveHero,bool roomReady,bool scripted){
  if(!wanted)return CastleControlPhase.Native;
  if(!linked||!inWorld)return CastleControlPhase.ClientWaiting;
  if(!liveHero)return CastleControlPhase.HeroWaiting;
  if(!roomReady)return CastleControlPhase.Transition;
  return scripted?CastleControlPhase.Cinematic:CastleControlPhase.Ready;
 }
}
