package castlecraft.mixin;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.screens.TitleScreen;
import net.minecraft.client.gui.screens.worldselection.CreateWorldScreen;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Unique;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
@Mixin(TitleScreen.class)
public abstract class TitleMixin {
    @Unique private static boolean castlecraft$opened=false;
    @Inject(method="tick",at=@At("TAIL"))
    private void open(CallbackInfo ci){
        if(castlecraft$opened||System.getProperty("castlecraft.session")==null)return;
        castlecraft$opened=true;Minecraft mc=Minecraft.getInstance();mc.options.pauseOnLostFocus=false;
        if(mc.getLevelSource().levelExists("CastleCraft"))mc.createWorldOpenFlows().openWorld("CastleCraft",()->{});
        else CreateWorldScreen.openFresh(mc,()->{});
    }
}
