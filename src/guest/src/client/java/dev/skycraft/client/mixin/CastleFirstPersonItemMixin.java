package dev.skycraft.client.mixin;

import dev.skycraft.client.CastleActions;
import dev.skycraft.client.SkyClient;
import net.minecraft.client.player.FirstPersonHandsAndItems;
import net.minecraft.client.player.LocalPlayer;
import net.minecraft.client.renderer.state.level.FirstPersonHandsAndItemsRenderState;
import net.minecraft.world.item.ItemStack;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

@Mixin(FirstPersonHandsAndItems.class)
public abstract class CastleFirstPersonItemMixin {
    @Inject(method="extractRenderState",at=@At("TAIL"))
    private void castlecraft$selectedMainWeapon(LocalPlayer player,float tick,FirstPersonHandsAndItemsRenderState state,CallbackInfo ci){
        if(SkyClient.linked()&&CastleActions.nativeWeaponVisible()){
            state.mainHandRenderState.clear();state.mainHandItem=ItemStack.EMPTY;state.hasMainHandMapData=false;
        }
    }
}
