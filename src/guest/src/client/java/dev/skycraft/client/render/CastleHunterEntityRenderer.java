package dev.skycraft.client.render;
import net.minecraft.client.Minecraft;
import net.minecraft.client.renderer.entity.EntityRenderer;
import net.minecraft.client.renderer.entity.EntityRendererProvider;
import net.minecraft.client.renderer.entity.state.AvatarRenderState;
import net.minecraft.client.renderer.entity.state.EntityRenderState;
import net.minecraft.client.renderer.SubmitNodeCollector;
import net.minecraft.client.renderer.state.level.CameraRenderState;
import com.mojang.blaze3d.vertex.PoseStack;
import dev.skycraft.combat.SkyrimActorEntity;
/** Same player-model state in Minecraft and host export; other proxies remain invisible. */
public final class CastleHunterEntityRenderer extends EntityRenderer<SkyrimActorEntity,CastleHunterEntityRenderer.State> {
 public CastleHunterEntityRenderer(EntityRendererProvider.Context context){super(context);}
 public static final class State extends EntityRenderState { AvatarRenderState avatar; }
 @Override public State createRenderState(){return new State();}
 @Override public void extractRenderState(SkyrimActorEntity actor,State state,float partial){
  super.extractRenderState(actor,state,partial);
  state.avatar=null;
  if(actor.castleHunter()&&Minecraft.getInstance().player!=null){
   var avatar=CastleHunterRender.extract(Minecraft.getInstance(),actor,partial);
   if(avatar.skin!=null)state.avatar=avatar;
  }
 }
 @Override public void submit(State state,PoseStack pose,SubmitNodeCollector collector,CameraRenderState camera){
  if(state.avatar!=null)Minecraft.getInstance().getEntityRenderDispatcher().getRenderer(state.avatar).submit(state.avatar,pose,collector,camera);
 }
}
