package dev.skycraft.client.mixin;
import net.minecraft.server.network.ServerGamePacketListenerImpl;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;
@Mixin(ServerGamePacketListenerImpl.class)
public interface CastleTeleportStateAccessor {
 @Accessor("awaitingTeleport") int castlecraft$teleportId();
 @Accessor("awaitingPositionFromClient") net.minecraft.world.phys.Vec3 castlecraft$awaitingPosition();
}
