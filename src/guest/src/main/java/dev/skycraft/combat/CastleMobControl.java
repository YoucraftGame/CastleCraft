package dev.skycraft.combat;
import java.util.WeakHashMap;
import net.minecraft.world.entity.Mob;
import net.minecraft.server.MinecraftServer;
final class CastleMobControl {
 private static class Hold{final boolean previous;final int epoch;int ticks;Hold(Mob m,int e,int t){previous=m.isNoAi();epoch=e;ticks=t;}}
 private static final WeakHashMap<Mob,Hold> held=new WeakHashMap<>();
 static void stun(Mob mob,int epoch,int ticks){var state=held.get(mob);if(state==null)held.put(mob,new Hold(mob,epoch,ticks));else state.ticks=Math.max(state.ticks,ticks);mob.setNoAi(true);}
 static void tick(MinecraftServer server){
  for(var it=held.entrySet().iterator();it.hasNext();){var entry=it.next();var mob=entry.getKey();var hold=entry.getValue();
   if(!(mob.level() instanceof net.minecraft.server.level.ServerLevel level)||level.getServer()!=server)continue;
   boolean linked=SkyCombat.isBridgeLevel(level);
   if(linked&&!dev.skycraft.world.BridgeSimulation.paused(mob)&&!dev.skycraft.world.BridgeSimulation.cinematic(mob))hold.ticks--;
   if(!linked||mob.isRemoved()||!mob.isAlive()||hold.epoch!=dev.skycraft.world.SkyCollision.epoch()||hold.ticks<=0){mob.setNoAi(hold.previous);it.remove();}
  }
 }
}
