package dev.skycraft.client.mixin;
import dev.skycraft.client.SkyClient;
import net.minecraft.server.network.ServerGamePacketListenerImpl;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.network.protocol.game.ServerboundAcceptTeleportationPacket;
import net.minecraft.world.phys.Vec3;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Shadow;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
@Mixin(ServerGamePacketListenerImpl.class)
public abstract class CastleTeleportAckMixin {
 @Shadow public ServerPlayer player;
 @Shadow private Vec3 awaitingPositionFromClient;
 @Inject(method="handleAcceptTeleportPacket",at=@At("TAIL"))
 private void castlecraft$confirmed(ServerboundAcceptTeleportationPacket packet,CallbackInfo ci){
  if(awaitingPositionFromClient==null)SkyClient.confirmVanillaArrival(player,packet.id());
 }
}
