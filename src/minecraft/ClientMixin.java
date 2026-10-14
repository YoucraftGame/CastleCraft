package castlecraft.mixin;
import castlecraft.VisitorBridge;
import net.minecraft.client.Minecraft;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
@Mixin(Minecraft.class)
public abstract class ClientMixin {
 @Inject(method="tick",at=@At("HEAD"))private void input(CallbackInfo ci){VisitorBridge.input((Minecraft)(Object)this);}
 @Inject(method="tick",at=@At("TAIL"))private void export(CallbackInfo ci){VisitorBridge.export((Minecraft)(Object)this);}
}
