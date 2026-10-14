package dev.skycraft.client;
import dev.skycraft.client.mixin.CastleSoundManagerAccessor;
import dev.skycraft.world.BridgeSimulation;
import net.minecraft.client.Minecraft;
import com.mojang.blaze3d.audio.ListenerTransform;
import net.minecraft.world.phys.Vec3;

/** Bridge listener follows live player feet even while the hidden client is not rendering. */
public final class CastleAudio {
 private CastleAudio(){}
 public interface ListenerAccess {void castlecraft$listener(ListenerTransform transform);}
 public static ListenerTransform pose(Minecraft minecraft){
  var player=minecraft.player;
  if(player==null||!SkyClient.linked()||SkyClient.mirrorSuspended()||!BridgeSimulation.isClientMirror(minecraft.level))return null;
  Vec3 position=player.getEyePosition();
  if(!Double.isFinite(position.x)||!Double.isFinite(position.y)||!Double.isFinite(position.z))return null;
  boolean first=minecraft.options.getCameraType().isFirstPerson();
  Vec3 forward=first?player.getLookAngle():new Vec3(0,-1,0);
  Vec3 up=first?Vec3.directionFromRotation(player.getXRot()-90,player.getYRot()):new Vec3(0,0,-1);
  return new ListenerTransform(position,forward,up);
 }
 public static void tick(Minecraft minecraft){
  ListenerTransform pose=pose(minecraft);
  if(pose!=null)((ListenerAccess)(Object)((CastleSoundManagerAccessor)minecraft.getSoundManager()).castlecraft$engine()).castlecraft$listener(pose);
 }
}
