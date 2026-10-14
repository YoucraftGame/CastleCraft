package dev.skycraft.mixin;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.item.CrossbowItem;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Invoker;
@Mixin(CrossbowItem.class)
public interface CastleCrossbowInvoker {
 @Invoker(value="tryLoadProjectiles",remap=false)
 static boolean castlecraft$load(LivingEntity actor,ItemStack weapon){throw new AssertionError();}
}
