package dev.skycraft.world;
/** Pure policy shared by render/audio/entity pause decisions. Ordinary worlds never inherit it. */
public final class CastleMirrorLifecycle {
 private CastleMirrorLifecycle(){}
 public static boolean worldAudio(boolean suspended,String source){
  return suspended&&(source.equals("HOSTILE")||source.equals("NEUTRAL")||source.equals("PLAYERS")||source.equals("BLOCKS")||source.equals("WEATHER")||source.equals("AMBIENT"));
 }
 public static boolean suspended(boolean mirror,boolean linked,boolean inGame,boolean loading){
  return mirror&&(!linked||!inGame||loading);
 }
}
