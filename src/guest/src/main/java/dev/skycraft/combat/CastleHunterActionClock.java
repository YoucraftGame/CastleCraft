package dev.skycraft.combat;
/** Native state/phase transitions own the action clock, as vanilla goals own a mob's use intent. */
public final class CastleHunterActionClock {
 private int state=-1;
 private float progress;
 private boolean fired;
 public boolean completed(){return fired;}
 public boolean advance(int next,float time,boolean allowed){
  if(next!=state||time+.2F<progress){state=next;fired=false;}
  progress=time;
  if(!allowed){fired=true;return false;}
  if(!fired&&time>=.35F){fired=true;return true;}
  return false;
 }
 public static int action(int state){return state&255;}
 public static boolean attacks(int state){int action=action(state);return action>=4&&action<=9||action>=25&&action<=27;}
}
