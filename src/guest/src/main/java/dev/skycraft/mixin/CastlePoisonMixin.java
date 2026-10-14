package dev.skycraft.mixin;

import dev.skycraft.combat.SkyrimActorEntity;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.damagesource.DamageSource;
import net.minecraft.world.effect.PoisonMobEffect;
import net.minecraft.world.entity.LivingEntity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Redirect;

/** Preserve vanilla poison timing and mark only its actual damage call for the native bridge. */
@Mixin(PoisonMobEffect.class)
public abstract class CastlePoisonMixin {
 @Redirect(method="applyEffectTick",at=@At(value="INVOKE",target="Lnet/minecraft/world/entity/LivingEntity;hurtServer(Lnet/minecraft/server/level/ServerLevel;Lnet/minecraft/world/damagesource/DamageSource;F)Z"))
 private boolean castlecraft$poison(LivingEntity target,ServerLevel level,DamageSource source,float damage){
  if(target instanceof SkyrimActorEntity actor&&dev.skycraft.combat.SkyCombat.isBridgeLevel(level))return actor.hurtFromCastlePoison(level,source,damage);
  return target.hurtServer(level,source,damage);
 }
}
