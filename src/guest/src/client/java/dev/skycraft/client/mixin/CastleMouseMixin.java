package dev.skycraft.client.mixin;

import com.llamalad7.mixinextras.injector.wrapoperation.Operation;
import com.llamalad7.mixinextras.injector.wrapoperation.WrapOperation;
import dev.skycraft.client.SkyClient;
import net.minecraft.client.Minecraft;
import net.minecraft.client.MouseHandler;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** The foreground host owns input while the mirror window stays hidden. */
@Mixin(MouseHandler.class)
public abstract class CastleMouseMixin {
    @Inject(method="handleAccumulatedMovement",at=@At("RETURN"))
    private void castlecraft$publishLook(CallbackInfo ci){SkyClient.afterMouseMovement();}
    @WrapOperation(method={"onMove","handleAccumulatedMovement","grabMouse"},
        at=@At(value="INVOKE",target="Lnet/minecraft/client/Minecraft;isWindowActive()Z"))
    private boolean castlecraft$forwardedFocus(Minecraft mc,Operation<Boolean> original) {
        return SkyClient.tookOver() || original.call(mc);
    }
}
