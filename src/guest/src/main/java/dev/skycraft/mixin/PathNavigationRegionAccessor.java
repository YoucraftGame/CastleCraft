package dev.skycraft.mixin;

import net.minecraft.world.level.Level;
import net.minecraft.world.level.PathNavigationRegion;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;

/** Resolve pathfinding wrappers to their world before applying native room collision. */
@Mixin(PathNavigationRegion.class)
public interface PathNavigationRegionAccessor {
    @Accessor("level")
    Level skycraft$level();
}
