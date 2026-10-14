package dev.skycraft.client.render;
import java.util.WeakHashMap;
import net.minecraft.client.Minecraft;
import net.minecraft.client.renderer.entity.state.AvatarRenderState;
import net.minecraft.client.model.HumanoidModel.ArmPose;
import net.minecraft.world.InteractionHand;
import net.minecraft.world.entity.HumanoidArm;
import net.minecraft.world.entity.Pose;
import net.minecraft.world.item.ItemStack;
import dev.skycraft.combat.SkyrimActorEntity;
import dev.skycraft.combat.CastleHunterMirror;
import dev.skycraft.combat.CastleHunterActionClock;
/** Fresh actor-owned state. Only resolved skin comes from the player, never poses or mod data. */
public final class CastleHunterRender {
 private record Motion(double x,double z,float phase,long time){}
 private static final WeakHashMap<SkyrimActorEntity,Motion> motion=new WeakHashMap<>();
 private static final WeakHashMap<SkyrimActorEntity,Long> receipts=new WeakHashMap<>();
 static boolean receiptDue(SkyrimActorEntity actor){long now=System.nanoTime();Long last=receipts.get(actor);if(last!=null&&now-last<250_000_000L)return false;receipts.put(actor,now);return true;}
 static AvatarRenderState extract(Minecraft mc,SkyrimActorEntity actor,float partial){
  var state=new AvatarRenderState();fill(mc,actor,state,partial);return state;
 }
 static void fill(Minecraft mc,SkyrimActorEntity actor,AvatarRenderState state,float partial){
  if(mc.player==null||!actor.castleHunter())return;
  state.deathTime=state.swingAnimation=0;state.currentSwing=null;state.hasRedOverlay=false;
  state.skin=mc.player.getSkin();state.entityType=mc.player.getType();
  state.showHat=state.showJacket=state.showLeftPants=state.showRightPants=state.showLeftSleeve=state.showRightSleeve=state.showCape=true;
  long now=System.nanoTime();var prior=motion.get(actor);
  double elapsed=prior==null?0:(now-prior.time)/1e9;
  double distance=prior==null?0:Math.hypot(actor.getX()-prior.x,actor.getZ()-prior.z);
  float speed=elapsed>.0001&&elapsed<.5&&distance<2?(float)Math.min(1,distance/elapsed*.2):0;
  float phase=(prior==null?0:prior.phase)+(float)Math.min(distance,1)*4;
  motion.put(actor,new Motion(actor.getX(),actor.getZ(),phase,now));
  net.minecraft.client.renderer.entity.HumanoidMobRenderer.extractHumanoidRenderState(actor,state,partial,mc.getItemModelResolver());
  state.id=actor.getId();state.x=actor.getX();state.y=actor.getY();state.z=actor.getZ();
  state.ageInTicks=actor.tickCount+partial;state.scale=state.ageScale=1;state.pose=Pose.STANDING;
  state.boundingBoxWidth=actor.getBbWidth();state.boundingBoxHeight=actor.getBbHeight();state.eyeHeight=actor.getEyeHeight();
  state.distanceToCameraSq=actor.distanceToSqr(mc.gameRenderer.mainCamera().position());
  var light=net.minecraft.core.BlockPos.containing(actor.getLightProbePosition(partial));
  state.lightCoords=net.minecraft.util.LightCoordsUtil.pack(actor.isOnFire()?15:actor.level().getBrightness(net.minecraft.world.level.LightLayer.BLOCK,light),actor.level().getBrightness(net.minecraft.world.level.LightLayer.SKY,light));
  state.shadowRadius=.5F;state.displayFireAnimation=actor.displayFireAnimation();
  state.walkAnimationPos=phase;state.walkAnimationSpeed=speed;
  state.bodyRot=(actor.hunterPose()&CastleHunterMirror.RIGHT)!=0?-90:90;state.yRot=0;state.xRot=0;
  int action=CastleHunterActionClock.action(actor.hunterState());float progress=(actor.hunterPose()>>>16)/65535F;
  if(CastleHunterActionClock.attacks(actor.hunterState())){
   state.swingAnimation=progress;
   state.currentSwing=new net.minecraft.world.entity.LivingEntity.SwingDescription(InteractionHand.MAIN_HAND,actor.getMainHandItem().getAttackAnimation(),6);
  }
  if(action==8){state.isCrouching=true;state.walkAnimationSpeed=1;}
  if(action==10||action==11||action==23||action==24){state.hasRedOverlay=true;state.isCrouching=action==11;}
  if(action==18)state.walkAnimationSpeed=Math.min(1,speed*1.5F);
  if(action==19)state.walkAnimationSpeed=-speed;
  if(action==20)state.xRot=(float)Math.sin(progress*Math.PI*2)*6;
  if(action==21)state.isCrouching=true;
  if(action==22)state.xRot=-10;
  if(action==13)state.deathTime=Math.max(.01F,progress*20);
  if(action==3){state.isCrouching=progress<.5F;state.swingAnimation=(float)Math.sin(Math.PI*progress);}
  if(action==17)state.swingAnimation=(float)Math.sin(Math.PI*progress);
  state.leftArmPose=arm(actor,HumanoidArm.LEFT);state.rightArmPose=arm(actor,HumanoidArm.RIGHT);
  state.isInvisible=false;state.isInvisibleToPlayer=false;state.isSpectator=false;state.nameTag=null;
 }
 private static ArmPose arm(SkyrimActorEntity actor,HumanoidArm arm){
  var hand=arm==actor.getMainArm()?InteractionHand.MAIN_HAND:InteractionHand.OFF_HAND;
  ItemStack item=actor.getItemInHand(hand);if(item.isEmpty())return ArmPose.EMPTY;
  if(actor.isUsingItem()&&actor.getUsedItemHand()==hand)return switch(item.getUseAnimation()){
   case BLOCK->ArmPose.BLOCK;case BOW->ArmPose.BOW_AND_ARROW;case CROSSBOW->ArmPose.CROSSBOW_CHARGE;
   case TRIDENT->ArmPose.THROW_TRIDENT;case SPEAR->ArmPose.SPEAR;case SPYGLASS->ArmPose.SPYGLASS;
   case TOOT_HORN->ArmPose.TOOT_HORN;case BRUSH->ArmPose.BRUSH;default->ArmPose.ITEM;
  };
  if(net.minecraft.world.item.CrossbowItem.isCharged(item))return ArmPose.CROSSBOW_HOLD;
  return ArmPose.ITEM;
 }
}
