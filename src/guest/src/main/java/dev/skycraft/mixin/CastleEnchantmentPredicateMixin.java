package dev.skycraft.mixin;

import dev.skycraft.combat.SkyrimActorEntity;
import net.minecraft.advancements.predicates.entity.EntityTypePredicate;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.tags.EntityTypeTags;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.phys.Vec3;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Bridge only vanilla enchantment sensitivity tags, without impersonating a vanilla entity type. */
@Mixin(EntityTypePredicate.class)
public abstract class CastleEnchantmentPredicateMixin {
 @Inject(method="matches(Lnet/minecraft/world/entity/Entity;Lnet/minecraft/server/level/ServerLevel;Lnet/minecraft/world/phys/Vec3;)Z",at=@At("HEAD"),cancellable=true)
 private void castlecraft$sensitivity(Entity entity,ServerLevel level,Vec3 origin,CallbackInfoReturnable<Boolean> cir){
  if(!(entity instanceof SkyrimActorEntity actor)||!dev.skycraft.combat.SkyCombat.isBridgeLevel(level))return;
  var key=((EntityTypePredicate)(Object)this).types().unwrapKey().orElse(null);
  if(EntityTypeTags.SENSITIVE_TO_SMITE.equals(key))cir.setReturnValue(actor.castleUndead);
  else if(EntityTypeTags.SENSITIVE_TO_BANE_OF_ARTHROPODS.equals(key))cir.setReturnValue(actor.castleArthropod);
 }
}
