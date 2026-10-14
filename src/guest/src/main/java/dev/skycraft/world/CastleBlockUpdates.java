package dev.skycraft.world;

/** The one-block mesh/lighting halo, including diagonals at section corners. */
public final class CastleBlockUpdates {
 private CastleBlockUpdates(){}
 @FunctionalInterface public interface Dirty { void section(int x,int y,int z); }
 public static void changed(int x,int y,int z,Dirty dirty){
  for(int sx=(x-1)>>4;sx<=(x+1)>>4;sx++)
   for(int sy=(y-1)>>4;sy<=(y+1)>>4;sy++)
    for(int sz=(z-1)>>4;sz<=(z+1)>>4;sz++)dirty.section(sx,sy,sz);
 }
}
