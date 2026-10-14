package castlecraft.mixin;
import net.minecraft.client.gui.screens.worldselection.CreateWorldScreen;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Shadow;
import org.spongepowered.asm.mixin.Unique;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
@Mixin(CreateWorldScreen.class)
public abstract class CreateMixin {
    @Shadow protected abstract void onCreate();
    @Unique private boolean castlecraft$created=false;
    @Inject(method="init",at=@At("TAIL"))
    private void create(CallbackInfo ci){
        if(castlecraft$created||System.getProperty("castlecraft.session")==null)return;
        castlecraft$created=true;((CreateWorldScreen)(Object)this).getUiState().setName("CastleCraft");net.minecraft.client.Minecraft.getInstance().execute(()->onCreate());
    }
}
