package dev.skycraft.combat;

import java.util.ArrayList;
import java.util.List;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.entity.EquipmentSlot;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.item.Items;
import net.minecraft.world.item.ArrowItem;
import net.minecraft.world.item.ProjectileWeaponItem;
import net.minecraft.world.item.enchantment.EnchantmentHelper;
import net.minecraft.world.damagesource.DamageSource;
import net.minecraft.world.entity.ai.attributes.Attributes;

/** Copies components, never transfers items or invents a second pathfinding/AI loop. */
public final class CastleHunterMirror {
 public static final int HUNTER=1024, ATTACK=2048, BLOCK=4096, RIGHT=8192;
 private CastleHunterMirror(){}
 private record Encounter(int epoch,List<ItemStack> hotbar,java.util.Map<EquipmentSlot,ItemStack> equipment,ItemStack ammo){}
 private static final java.util.WeakHashMap<ServerLevel,Encounter> encounters=new java.util.WeakHashMap<>();
 public static void snapshot(SkyrimActorEntity actor,ServerPlayer owner){
  int epoch=dev.skycraft.world.SkyCollision.epoch();Encounter cached=encounters.get(owner.level());
  if(cached!=null&&cached.epoch==epoch){
   actor.hunterHotbar=copyHotbar(cached.hotbar::get);for(var slot:EquipmentSlot.values())actor.setItemSlot(slot,cached.equipment.get(slot).copy());actor.hunterAmmo=cached.ammo.copy();for(int i=0;i<9;i++)if(ItemStack.matches(actor.hunterHotbar.get(i),actor.getMainHandItem())){actor.hunterSlot=i;break;}actor.setHunterPose(HUNTER);
   dev.skycraft.SkyCraft.LOG.info("CastleCraft hunter phase mirror reuses entry loadout actor={} epoch={}",actor.formId(),epoch);return;
  }
  actor.hunterHotbar=copyHotbar(owner.getInventory()::getItem);
  for(int i=0;i<9;i++)if(ItemStack.matches(actor.hunterHotbar.get(i),owner.getMainHandItem())){actor.hunterSlot=i;break;}
  for(var slot:EquipmentSlot.values())actor.setItemSlot(slot,owner.getItemBySlot(slot).copy());
  actor.hunterAmmo=owner.getProjectile(actor.getMainHandItem()).copy();
  if(actor.hunterAmmo.isEmpty())for(int i=0;i<owner.getInventory().getContainerSize();i++){
   var item=owner.getInventory().getItem(i);if(item.getItem() instanceof ArrowItem){actor.hunterAmmo=item.copy();break;}
  }
  actor.setHunterPose(HUNTER);
  var equipment=new java.util.EnumMap<EquipmentSlot,ItemStack>(EquipmentSlot.class);for(var slot:EquipmentSlot.values())equipment.put(slot,actor.getItemBySlot(slot).copy());
  encounters.put(owner.level(),new Encounter(epoch,copyHotbar(actor.hunterHotbar::get),equipment,actor.hunterAmmo.copy()));
  dev.skycraft.SkyCraft.LOG.info("CastleCraft hunter mirror snapshot actor={} equipment/components copied without changing player inventory",actor.formId());
 }
 static List<ItemStack> copyHotbar(java.util.function.IntFunction<ItemStack> source){
  var copy=new ArrayList<ItemStack>();for(int i=0;i<9;i++)copy.add(source.apply(i).copy());return copy;
 }
 public static float damage(SkyrimActorEntity actor,ServerPlayer target,DamageSource source){
  double base=actor.getAttributeValue(Attributes.ATTACK_DAMAGE);
  return EnchantmentHelper.modifyDamage(target.level(),actor.getMainHandItem(),target,source,(float)Math.max(0,base));
 }
 public static void postAttack(SkyrimActorEntity actor,net.minecraft.world.entity.LivingEntity target,DamageSource source,float damage){
  ServerLevel level=(ServerLevel)target.level();EnchantmentHelper.doPostAttackEffects(level,target,source);
  float knockback=EnchantmentHelper.modifyKnockback(level,actor.getMainHandItem(),target,source,0);
  if(knockback>0)target.knockback(knockback*.5,actor.getX()-target.getX(),actor.getZ()-target.getZ(),source,damage);
  actor.getMainHandItem().hurtEnemy(target,actor);
  actor.getMainHandItem().postHurtEnemy(target,actor);
 }
 public static boolean ranged(SkyrimActorEntity actor){return actor.getMainHandItem().getItem() instanceof ProjectileWeaponItem||actor.getMainHandItem().is(Items.TRIDENT);}
 public static void pose(SkyrimActorEntity actor,int flags,int nativeState,ServerPlayer owner){
  int previousState=actor.hunterState();
  boolean permitted=(flags&32768)==0&&!dev.skycraft.world.BridgeSimulation.cinematic(actor)&&!dev.skycraft.world.BridgeSimulation.paused(actor);
  var target=CastleHunterTactics.target(actor,owner);
  if(permitted&&CastleHunterActionClock.attacks(nativeState)&&nativeState!=previousState)CastleHunterTactics.choose(actor,target);
  actor.setHunterPose(flags);actor.setHunterState(nativeState);float progress=(flags>>>16)/65535F;
  actor.hunterProgress=progress;
  boolean attacking=CastleHunterActionClock.attacks(nativeState)&&permitted;
  // Reconcile use intent even on idle/death. Only native AI can request an item action.
  if(nativeState!=previousState||!permitted)actor.stopUsingItem();
  boolean blocking=permitted&&((flags&BLOCK)!=0||CastleHunterTactics.threat(target,actor))&&actor.getOffhandItem().has(net.minecraft.core.component.DataComponents.BLOCKS_ATTACKS);
  boolean consuming=attacking&&actor.getMainHandItem().has(net.minecraft.core.component.DataComponents.CONSUMABLE);
  if(blocking){if(!actor.isUsingItem())actor.startUsingItem(net.minecraft.world.InteractionHand.OFF_HAND);}
  else if((consuming||attacking&&ranged(actor))&&!actor.hunterClock.completed()){
   if(!actor.isUsingItem())actor.startUsingItem(net.minecraft.world.InteractionHand.MAIN_HAND);
  }else if(actor.isUsingItem())actor.stopUsingItem();
  if(actor.hunterClock.advance(nativeState,progress,attacking)){
   actor.swing(net.minecraft.world.InteractionHand.MAIN_HAND,actor.getMainHandItem().getAttackAnimation(),true);
   if(CastleHunterTactics.cast(actor,target,nativeState)){actor.stopUsingItem();}
   else if(ranged(actor)){
    if(target!=null&&target.isAlive())shoot(actor,target);
    actor.stopUsingItem();
   }else if(consuming){
    actor.setItemSlot(EquipmentSlot.MAINHAND,actor.getMainHandItem().finishUsingItem(actor.level(),actor));actor.stopUsingItem();
   }
  }
 }
 private static void shoot(SkyrimActorEntity actor,net.minecraft.world.entity.LivingEntity target){
  ServerLevel level=(ServerLevel)actor.level();ItemStack weapon=actor.getMainHandItem();
  if(weapon.getItem() instanceof net.minecraft.world.item.CrossbowItem crossbow){
   if(!net.minecraft.world.item.CrossbowItem.isCharged(weapon))dev.skycraft.mixin.CastleCrossbowInvoker.castlecraft$load(actor,weapon);
   // Let vanilla consume its stored projectile component, including fireworks/multishot.
   if(net.minecraft.world.item.CrossbowItem.isCharged(weapon))crossbow.performShooting(level,actor,net.minecraft.world.InteractionHand.MAIN_HAND,weapon,3.15F,2,target);
   return;
  }
  net.minecraft.world.entity.projectile.arrow.AbstractArrow shot;
  if(weapon.is(Items.TRIDENT))shot=new net.minecraft.world.entity.projectile.arrow.ThrownTrident(level,actor,weapon.copy());
  else {
   ItemStack ammo=actor.hunterAmmo;
   if(ammo.isEmpty()||!(ammo.getItem() instanceof ArrowItem))return; // No invented ammunition.
   shot=((ArrowItem)ammo.getItem()).createArrow(level,ammo.copyWithCount(1),actor,weapon.copy());
   ammo.shrink(EnchantmentHelper.processAmmoUse(level,weapon,ammo,1));
  }
  shot.pickup=net.minecraft.world.entity.projectile.arrow.AbstractArrow.Pickup.DISALLOWED;
  double x=target.getX()-shot.getX(),z=target.getZ()-shot.getZ();
  double y=target.getY()+target.getBbHeight()*.5-shot.getY()+Math.sqrt(x*x+z*z)*.12;
  net.minecraft.world.entity.projectile.Projectile.spawnProjectileUsingShoot(shot,level,weapon,x,y,z,weapon.is(Items.CROSSBOW)?3.15F:1.6F,2);
 }
}
