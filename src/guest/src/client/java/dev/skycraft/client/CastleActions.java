package dev.skycraft.client;

import com.mojang.blaze3d.vertex.PoseStack;
import dev.skycraft.link.SkyLink;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import net.minecraft.client.Minecraft;
import net.minecraft.client.model.HumanoidModel;
import net.minecraft.client.model.Model;
import net.minecraft.client.model.geom.ModelPart;
import net.minecraft.client.model.player.PlayerModel;
import net.minecraft.client.renderer.entity.state.AvatarRenderState;
import net.minecraft.client.renderer.entity.state.EntityRenderState;
import net.minecraft.world.InteractionHand;
import net.minecraft.world.entity.HumanoidArm;
import net.minecraft.world.item.ItemStack;
import org.joml.Vector3f;

/** Visual counterpart of the actual castle weapon animation; it never fabricates attacks or items. */
public final class CastleActions {
    private static final int HAND_ANCHORS = 0xCC03;
    private static final ByteBuffer ANCHORS = ByteBuffer.allocate(96).order(ByteOrder.LITTLE_ENDIAN);
    private static final Vector3f RIGHT = new Vector3f(), LEFT = new Vector3f();
    private static final Vector3f[] BASIS = {new Vector3f(),new Vector3f(),new Vector3f(),new Vector3f(),new Vector3f(),new Vector3f()};
    private static int action, category, flags;
    private static float progress, progressRate;
    private static long receivedAt, anchorsAt;
    private CastleActions() {}

    /** Input type 13: action, progress/10000, native weapon category, ownership/mode/equipment flags. */
    public static void accept(Minecraft mc, int code, int a, int b, int c) {
        long now = System.nanoTime();
        float next = Math.clamp(a / 10000.0F, 0.0F, 1.0F);
        float elapsed = (now - receivedAt) / 1_000_000_000.0F;
        boolean restart = (c & 8) != 0 || action != code;
        progressRate = (c & 64) == 0 && !restart && elapsed > .005F && elapsed < .2F
            ? Math.clamp((next - progress) / elapsed, 0.0F, 6.0F) : 0.0F;
        receivedAt = now; progress = next; action = code; category = b; flags = c;
        // The native animation accepted the attack. Trigger the genuine MC hand swing once,
        // without sending an attack click that would also mine/hit a second target.
        if ((c & 1) != 0 && restart && (code == 1 || code == 3) && mc.player != null && !mc.player.isDeadOrDying()) {
            var held = mc.player.getMainHandItem();
            mc.player.swing(InteractionHand.MAIN_HAND, held.getAttackAnimation(), false);
        }
    }

    public static void reset() {
        action = category = flags = 0; progress = progressRate = 0; receivedAt = anchorsAt = 0;
    }

    private static boolean active() {
        return SkyClient.linked() && (flags & 1) != 0 && System.nanoTime() - receivedAt < 600_000_000L;
    }
    public static boolean nativeWeaponVisible() {
        return active() && (flags & 4) != 0 && (flags & (2 | 16)) != 0;
    }
    private static boolean ourPlayer(Object state) {
        var mc = Minecraft.getInstance();
        return active() && mc.player != null && state instanceof AvatarRenderState avatar && avatar.id == mc.player.getId();
    }

    /** Called on the actual exported local-player render state, before the renderer submits its layers. */
    public static void prepareRenderState(EntityRenderState state) {
        if (!ourPlayer(state) || !(state instanceof AvatarRenderState avatar)) return;
        if ((flags & 32) != 0) {
            float p = nativeProgress();
            avatar.isCrouching = false;
            avatar.deathTime = action == 9 ? Math.max(.01F,p*20) : action == 10 ? (1-p)*20 : 0;
            if (action == 6 || action == 7) {
                avatar.walkAnimationPos = dev.skycraft.world.CastleScriptedPose.walkPhase(p);
                avatar.walkAnimationSpeed = action == 7 ? 1 : .75F;
            } else if (action == 8 || action == 9 || action == 10) {
                avatar.walkAnimationSpeed = 0;
            }
        }
        if (!nativeWeaponVisible()) return;
        // Hide only the competing MC item drawing, not the inventory or equipped armour.
        if (Minecraft.getInstance().player.getMainArm() == HumanoidArm.RIGHT) {
            avatar.rightHandItemState.clear(); avatar.rightHandItemStack = ItemStack.EMPTY;
            avatar.rightArmPose = HumanoidModel.ArmPose.ITEM;
        } else {
            avatar.leftHandItemState.clear(); avatar.leftHandItemStack = ItemStack.EMPTY;
            avatar.leftArmPose = HumanoidModel.ArmPose.ITEM;
        }
    }

    /** Called after setupAnim, only in the AVATAR collector. Ragdoll capture must not call this. */
    public static void capture(Model<?> model, Object state, PoseStack pose) {
        if (!ourPlayer(state) || !(state instanceof AvatarRenderState avatar) || !(model instanceof HumanoidModel<?> humanoid)) return;
        if ((flags & 32) != 0 && action == 8) {
            float bend = dev.skycraft.world.CastleScriptedPose.jumpBend(nativeProgress());
            humanoid.rightLeg.xRot = -.65F*bend; humanoid.leftLeg.xRot = .45F*bend;
            humanoid.rightArm.xRot = humanoid.leftArm.xRot = -.45F*bend;
        } else if (nativeWeaponVisible() || action == 4) pose(humanoid, avatar);
        if (!(model instanceof PlayerModel)) return;
        long now = System.nanoTime();
        if (now - anchorsAt < 12_000_000L) return;
        pointAtHand(humanoid, avatar, HumanoidArm.RIGHT, pose, RIGHT,0);
        pointAtHand(humanoid, avatar, HumanoidArm.LEFT, pose, LEFT,3);
        ANCHORS.clear();
        ANCHORS.putFloat(RIGHT.x).putFloat(RIGHT.y).putFloat(RIGHT.z);
        ANCHORS.putFloat(LEFT.x).putFloat(LEFT.y).putFloat(LEFT.z);
        for(var axis:BASIS)ANCHORS.putFloat(axis.x).putFloat(axis.y).putFloat(axis.z);
        ANCHORS.flip();
        if (SkyLink.tryWriteRender(HAND_ANCHORS, ANCHORS, null)) anchorsAt = now;
    }

    private static void pointAtHand(HumanoidModel<?> model, AvatarRenderState state, HumanoidArm arm, PoseStack pose, Vector3f output,int firstAxis) {
        pose.pushPose();
        model.translateToHand(state, arm, pose);
        pose.last().pose().transformPosition(0.0F, .625F, 0.0F, output);
        pose.last().pose().transformDirection(BASIS[firstAxis].set(1,0,0)).normalize();
        pose.last().pose().transformDirection(BASIS[firstAxis+1].set(0,1,0)).normalize();
        pose.last().pose().transformDirection(BASIS[firstAxis+2].set(0,0,1)).normalize();
        pose.popPose();
    }

    private static float nativeProgress() {
        float elapsed = (flags & 64) != 0 ? 0 : Math.clamp((System.nanoTime()-receivedAt)/1_000_000_000.0F,0,.1F);
        return dev.skycraft.world.CastleScriptedPose.progress(action,progress,progressRate,elapsed);
    }

    private static void pose(HumanoidModel<?> model, AvatarRenderState state) {
        float elapsed = Math.clamp((System.nanoTime() - receivedAt) / 1_000_000_000.0F, 0.0F, .1F);
        float p = Math.clamp(progress + progressRate * elapsed, 0.0F, 1.0F);
        float arc = (float)Math.sin(p * Math.PI);
        ModelPart main = state.mainArm == HumanoidArm.LEFT ? model.leftArm : model.rightArm;
        ModelPart off = state.mainArm == HumanoidArm.LEFT ? model.rightArm : model.leftArm;
        float side = state.mainArm == HumanoidArm.LEFT ? -1.0F : 1.0F;
        if (action == 1) {
            if (category == 1 || category == 2 || category == 3) {
                main.xRot = -1.48F + .25F * arc; main.yRot = -.08F * side;
                if (category == 1 || category == 2) { off.xRot = -1.35F; off.yRot = .45F * side; }
            } else {
                main.xRot = -2.1F + 2.35F * p; main.yRot = -.45F * arc * side; main.zRot = -.18F * arc * side;
                if (category == 5 || category == 6) { off.xRot = main.xRot + .2F; off.yRot = .45F * side; }
                if (category == 7) { off.xRot = -.3F - 1.7F * (1 - p); off.yRot = .3F * arc * side; }
            }
        } else if (action == 2) {
            if (category == 1) {
                main.xRot = -1.5F; main.yRot = -.1F * side; off.xRot = -1.5F; off.yRot = .55F * side;
            } else if (category == 4) {
                off.xRot = -1.25F; off.yRot = .4F * side; main.xRot = -.6F;
            } else {
                main.xRot = -2.45F; main.yRot = -.2F * side;
                if (category == 5 || category == 6 || category == 7) { off.xRot = -2.25F; off.yRot = .45F * side; }
            }
        } else if (action == 3) {
            main.xRot = -1.65F - .3F * arc; main.yRot = -.16F * side;
            off.xRot = -1.2F - .4F * arc; off.yRot = .35F * side;
        } else if (action == 4) {
            main.xRot = -1.4F; main.yRot = -.35F * side;
        } else if (nativeWeaponVisible()) {
            main.xRot = category == 3 ? -.6F : -.25F;
            main.yRot = -.08F * side;
            if (category == 4) off.xRot = -.45F;
        }
    }
}
