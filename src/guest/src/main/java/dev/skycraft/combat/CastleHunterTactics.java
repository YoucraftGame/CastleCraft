package dev.skycraft.combat;
import java.util.List;
import java.util.concurrent.CopyOnWriteArrayList;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.entity.EquipmentSlot;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.item.Items;
import net.minecraft.world.item.ProjectileWeaponItem;
import net.minecraft.core.component.DataComponents;
/** Perception/decision layer; it never reads player keybinds or mutates the player's inventory. */
public final class CastleHunterTactics {
 public interface Skill {
  boolean supports(ItemStack stack);
  /** Implementations own their mod's costs/cooldown/target checks; actor is always the caster. */
  boolean cast(SkyrimActorEntity actor,LivingEntity target,int nativeState);
 }
 private static final List<Skill> skills=new CopyOnWriteArrayList<>();
 public static void register(Skill skill){skills.add(java.util.Objects.requireNonNull(skill));}
 public static LivingEntity target(SkyrimActorEntity actor,ServerPlayer owner){
  var nativeTarget=actor.hunterTarget==0?owner:actor.level().getEntity(actor.hunterTarget);
  if(nativeTarget instanceof LivingEntity living&&living.isAlive()&&actor.hasLineOfSight(living))return living;
  return actor.level().getEntitiesOfClass(LivingEntity.class,actor.getBoundingBox().inflate(32),
   e->e!=actor&&e.isAlive()&&!e.isSpectator()&&(e instanceof net.minecraft.world.entity.player.Player||e instanceof net.minecraft.world.entity.Mob mob&&SkyCombat.isCastleAlly(mob))&&actor.hasLineOfSight(e))
   .stream().min(java.util.Comparator.comparingDouble(actor::distanceToSqr)).orElse(null);
 }
 public static boolean threat(LivingEntity target,SkyrimActorEntity actor){
  if(target==null||!target.isUsingItem())return false;
  var animation=target.getUseItem().getUseAnimation();
  return actor.distanceToSqr(target)<256&&(animation==net.minecraft.world.item.ItemUseAnimation.BOW||animation==net.minecraft.world.item.ItemUseAnimation.CROSSBOW||animation==net.minecraft.world.item.ItemUseAnimation.TRIDENT);
 }
 static boolean healing(ItemStack stack){
  if(!stack.has(DataComponents.CONSUMABLE))return false;
  var potion=stack.get(DataComponents.POTION_CONTENTS);if(potion==null)return false;
  for(var effect:potion.getAllEffects())if(effect.getEffect()==net.minecraft.world.effect.MobEffects.INSTANT_HEALTH||effect.getEffect()==net.minecraft.world.effect.MobEffects.REGENERATION)return true;
  return false;
 }
 static int score(boolean lowHealth,boolean far,boolean healing,boolean ranged,boolean melee,boolean spell){
  if(lowHealth&&healing)return 100;
  if(far&&ranged)return 70;
  if(spell)return 60;
  if(!far&&melee)return 70;
  if(melee)return 30;
  return 0;
 }
 static void choose(SkyrimActorEntity actor,LivingEntity target){
  if(actor.hunterHotbar==null)return;
  // Store depleted ammo, consumables and worn weapons before changing slots.
  actor.hunterHotbar.set(actor.hunterSlot,actor.getMainHandItem().copy());
  boolean low=actor.getHealth()<actor.getMaxHealth()*.5F,far=target!=null&&actor.getBoundingBox().distanceToSqr(target.getBoundingBox())>16;
  int best=-1,value=0;
  for(int i=0;i<actor.hunterHotbar.size();i++){
   var item=actor.hunterHotbar.get(i);if(item.isEmpty())continue;
   boolean ranged=item.getItem() instanceof ProjectileWeaponItem||item.is(Items.TRIDENT);
   boolean spell=skills.stream().anyMatch(s->s.supports(item));
   boolean melee=item.getOrDefault(DataComponents.ATTRIBUTE_MODIFIERS,net.minecraft.world.item.component.ItemAttributeModifiers.EMPTY).modifiers().stream().anyMatch(e->e.attribute().equals(net.minecraft.world.entity.ai.attributes.Attributes.ATTACK_DAMAGE)&&e.slot().test(EquipmentSlot.MAINHAND));
   int candidate=score(low,far,healing(item),ranged,melee&&!ranged,spell);
   if(candidate>value){best=i;value=candidate;}
  }
  if(best>=0&&best!=actor.hunterSlot){actor.hunterSlot=best;actor.setItemSlot(EquipmentSlot.MAINHAND,actor.hunterHotbar.get(best).copy());}
 }
 static boolean cast(SkyrimActorEntity actor,LivingEntity target,int nativeState){
  if(target==null)return false;
  for(var skill:skills)if(skill.supports(actor.getMainHandItem())&&skill.cast(actor,target,nativeState))return true;
  return false;
 }
}
