package castlecraft;
import com.google.gson.*;
import java.nio.file.*;
import java.util.*;
import java.lang.reflect.*;
import org.joml.Vector3f;
import org.joml.Vector3fc;
import com.mojang.blaze3d.vertex.PoseStack;
import com.mojang.blaze3d.platform.NativeImage;
import net.minecraft.client.Minecraft;
import net.minecraft.client.model.geom.ModelPart;
import net.minecraft.client.model.player.PlayerModel;
import net.minecraft.client.renderer.entity.player.AvatarRenderer;
import net.minecraft.client.renderer.entity.state.AvatarRenderState;
import net.minecraft.client.renderer.texture.*;
import net.minecraft.core.*;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.level.block.Blocks;
import net.minecraft.world.level.block.state.BlockState;
import net.minecraft.world.InteractionHand;
import net.minecraft.world.phys.*;
import net.minecraft.resources.Identifier;
import net.minecraft.util.RandomSource;
import net.minecraft.client.renderer.block.dispatch.*;
import net.minecraft.client.resources.model.geometry.BakedQuad;

public final class VisitorBridge {
 static final Gson gson=new Gson();
 static boolean ready=false,visiting=false;
 static int setupTicks=0;
 static long nextExport=0,lastInput=0;
 static int lastPlace=0,mined=0,placed=0,lastInventory=0,lastClick=0;
 static long lastGuiCapture=0;
 static volatile boolean capturePending=false;
 static final java.util.concurrent.ExecutorService guiWriter=java.util.concurrent.Executors.newSingleThreadExecutor(r->{Thread t=new Thread(r,"CastleCraft GUI transport");t.setDaemon(true);return t;});
 static JsonObject input=new JsonObject();
 static JsonObject skin=null;
 static final Map<String,JsonObject> textures=new HashMap<>();
 static final Map<String,JsonArray> meshes=new HashMap<>();
 static final Set<BlockPos> seenBlocks=new HashSet<>();
 static BlockPos miningTarget=null;
 public static void log(String s){try{Files.createDirectories(Bridge.session);Files.writeString(Bridge.session.resolve("visitor.log"),java.time.Instant.now()+" "+s+"\n",StandardOpenOption.CREATE,StandardOpenOption.APPEND);}catch(Exception ignored){}}
 static void atomic(String name,String value)throws Exception{Path tmp=Bridge.session.resolve(name+".tmp");Files.writeString(tmp,value);Files.move(tmp,Bridge.session.resolve(name),StandardCopyOption.REPLACE_EXISTING,StandardCopyOption.ATOMIC_MOVE);}
 static boolean flag(String key){return input.has(key)&&input.get(key).getAsBoolean();}
 static double number(String key,double def){return input.has(key)?input.get(key).getAsDouble():def;}
 static void command(MinecraftServer server,String cmd){server.getCommands().performPrefixedCommand(server.createCommandSourceStack(),cmd);}
 public static void serverTick(MinecraftServer server){
  if(!server.isSingleplayer()||server.getPlayerCount()!=1)return;
  try {
   ServerPlayer p=server.getPlayerList().getPlayers().getFirst();
   if(!ready){
    p.setNoGravity(true);p.setDeltaMovement(Vec3.ZERO);
    if(setupTicks++==0){command(server,"forceload add -16 -16 16 16");log("simulation-preparing: force-load room chunks before placing floor");}
    if(setupTicks<40)return;
    // getChunk waits for actual native chunk generation, not a timer-based guess.
    for(int cx=-1;cx<=1;cx++)for(int cz=-1;cz<=1;cz++)p.level().getChunk(cx,cz);
    // Private temporary simulation room. All gameplay is the real integrated Minecraft server.
    command(server,"tp @a 0.5 251 0.5 180 0");
    command(server,"fill -16 250 -16 16 250 16 minecraft:barrier");
    command(server,"fill -16 251 -16 16 260 16 minecraft:air");
    command(server,"give @a minecraft:iron_pickaxe 1");command(server,"give @a minecraft:stone 64");command(server,"give @a minecraft:oak_planks 64");command(server,"give @a minecraft:iron_sword 1");command(server,"give @a minecraft:iron_helmet 1");
    command(server,"setblock 3 251 1 minecraft:oak_log");command(server,"setblock 4 251 1 minecraft:stone");
    if(!p.level().getBlockState(new BlockPos(0,250,0)).is(Blocks.BARRIER))throw new IllegalStateException("Simulation floor not loaded");
    p.setNoGravity(false);ready=true;log("simulation-ready: verified barrier floor before releasing native player physics");
   }
   // Freeze the hidden guest only when the host owns control. Preserve inventory and equipment.
   if(!visiting)p.setDeltaMovement(Vec3.ZERO);
   Path test=Bridge.session.resolve("guest.command");
   if(Boolean.getBoolean("castlecraft.test")&&Files.exists(test)){String cmd=Files.readString(test).trim();Files.delete(test);command(server,cmd);log("TEST server command "+cmd);}
  }catch(Exception e){log("server-error "+e);}
 }
 public static void input(Minecraft mc){
  if(mc.player==null||mc.gameMode==null||!mc.hasSingleplayerServer())return;
  try{
   Path file=Bridge.session.resolve("input.json");
   if(Files.exists(file)){input=JsonParser.parseString(Files.readString(file)).getAsJsonObject();lastInput=Files.getLastModifiedTime(file).toMillis();}
   visiting=flag("visiting")&&System.currentTimeMillis()-lastInput<1500;
   mc.options.pauseOnLostFocus=false;
   mc.options.keyUp.setDown(visiting&&flag("forward"));mc.options.keyDown.setDown(visiting&&flag("backward"));mc.options.keyLeft.setDown(visiting&&flag("left"));mc.options.keyRight.setDown(visiting&&flag("right"));mc.options.keyJump.setDown(visiting&&flag("jump"));mc.options.keyShift.setDown(visiting&&flag("sneak"));mc.options.keySprint.setDown(visiting&&flag("sprint"));
   if(!visiting){mc.gameMode.stopDestroyBlock();miningTarget=null;return;}
   int inv=(int)number("inventorySeq",0);
   if(inv!=lastInventory){lastInventory=inv;mc.setScreenAndShow(mc.gui.screen() instanceof net.minecraft.client.gui.screens.inventory.InventoryScreen?null:new net.minecraft.client.gui.screens.inventory.InventoryScreen(mc.player));}
   if(mc.gui.screen() instanceof net.minecraft.client.gui.screens.inventory.InventoryScreen screen){
    double mx=number("mouseX",0.5)*mc.getWindow().getGuiScaledWidth(),my=number("mouseY",0.5)*mc.getWindow().getGuiScaledHeight();screen.mouseMoved(mx,my);
    int click=(int)number("clickSeq",0);if(click!=lastClick){lastClick=click;var e=new net.minecraft.client.input.MouseButtonEvent(mx,my,new net.minecraft.client.input.MouseButtonInfo((int)number("mouseButton",0),0));screen.mouseClicked(e,false);screen.mouseReleased(e);}return;
   }
   mc.player.setYRot((float)number("yaw",180));mc.player.setXRot(0);
   int slot=(int)number("slot",-1);if(slot>=0&&slot<9)mc.player.getInventory().setSelectedSlot(slot);
   BlockPos pos=new BlockPos((int)Math.floor(number("targetX",3)),(int)Math.floor(number("targetY",251)),(int)Math.floor(number("targetZ",1)));
   if(mc.player.position().distanceTo(Vec3.atCenterOf(pos))<=mc.player.blockInteractionRange()+1){
    if(flag("mine")){
     if(!pos.equals(miningTarget)){mc.gameMode.stopDestroyBlock();mc.gameMode.startDestroyBlock(pos,Direction.UP);miningTarget=pos;}
     else mc.gameMode.continueDestroyBlock(pos,Direction.UP);
     mc.player.swing(InteractionHand.MAIN_HAND,net.minecraft.world.item.component.SwingAnimation.DEFAULT,true);
    }else{mc.gameMode.stopDestroyBlock();miningTarget=null;}
    int click=(int)number("placeSeq",0);
    if(click!=lastPlace){lastPlace=click;BlockPos against=pos.below();mc.gameMode.useItemOn(mc.player,InteractionHand.MAIN_HAND,new BlockHitResult(new Vec3(pos.getX()+0.5,pos.getY(),pos.getZ()+0.5),Direction.UP,against,false));mc.player.swing(InteractionHand.MAIN_HAND,net.minecraft.world.item.component.SwingAnimation.DEFAULT,true);log("place-input "+pos);}
   }
  }catch(Exception e){log("input-error "+e);}
 }
 static JsonObject pixels(NativeImage image){
  JsonObject o=new JsonObject();o.addProperty("width",image.getWidth());o.addProperty("height",image.getHeight());
  int[] p=image.getPixels();byte[] bytes=new byte[p.length*4];
  for(int i=0;i<p.length;i++){bytes[i*4]=(byte)(p[i]>>16);bytes[i*4+1]=(byte)(p[i]>>8);bytes[i*4+2]=(byte)p[i];bytes[i*4+3]=(byte)(p[i]>>24);}
  o.addProperty("rgba",Base64.getEncoder().encodeToString(bytes));return o;
 }
 static JsonObject texture(Minecraft mc,Identifier id)throws Exception{
  String key=id.toString();if(textures.containsKey(key))return textures.get(key);
  AbstractTexture tex=mc.getTextureManager().getTexture(id);JsonObject result;
  if(tex instanceof DynamicTexture dynamic&&dynamic.getPixels()!=null)result=pixels(dynamic.getPixels());
  else if(tex instanceof ReloadableTexture reloadable){try(TextureContents contents=reloadable.loadContents(mc.getResourceManager())){result=pixels(contents.image());}}
  else{try(TextureContents contents=TextureContents.load(mc.getResourceManager(),id)){result=pixels(contents.image());}}
  textures.put(key,result);return result;
 }
 static void vertex(JsonArray out,float x,float y,float z,float u,float v){out.add(x);out.add(y);out.add(z);out.add(u);out.add(v);}
 static JsonArray playerMesh(PlayerModel model){
  JsonArray vertices=new JsonArray();PoseStack stack=new PoseStack();
  model.root().visit(stack,(pose,path,index,cube)->{
   for(var polygon:cube.polygons){var verts=polygon.vertices();for(int i:new int[]{0,1,2,0,2,3}){var v=verts[i];Vector3f position=new Vector3f(v.worldX(),v.worldY(),v.worldZ());pose.pose().transformPosition(position);vertex(vertices,position.x,1.5f-position.y,position.z,v.u(),v.v());}}
  });return vertices;
 }
 static JsonObject block(Minecraft mc,BlockPos pos,BlockState state)throws Exception{
  String key=BuiltInRegistries.BLOCK.getKey(state.getBlock()).toString()+state.toString();
  JsonArray groups=meshes.get(key);
  if(groups==null){
   groups=new JsonArray();List<BlockStateModelPart> parts=new ArrayList<>();mc.getModelManager().getBlockStateModelSet().get(state).collectParts(RandomSource.create(0),parts);
   Map<String,JsonArray> byTexture=new LinkedHashMap<>();
   for(var part:parts)for(Direction dir:new Direction[]{null,Direction.UP,Direction.DOWN,Direction.NORTH,Direction.SOUTH,Direction.EAST,Direction.WEST})for(BakedQuad q:part.getQuads(dir)){
    TextureAtlasSprite sprite=q.materialInfo().sprite();Identifier name=sprite.contents().name();Identifier tex=Identifier.fromNamespaceAndPath(name.getNamespace(),"textures/"+name.getPath()+".png");
    texture(mc,tex);JsonArray vs=byTexture.computeIfAbsent(tex.toString(),k->new JsonArray());
    for(int i:new int[]{0,1,2,0,2,3}){Vector3fc p=q.position(i);long packed=q.packedUV(i);float u=net.minecraft.client.model.geom.builders.UVPair.unpackU(packed),v=net.minecraft.client.model.geom.builders.UVPair.unpackV(packed);u=(u-sprite.getU0())/(sprite.getU1()-sprite.getU0());v=(v-sprite.getV0())/(sprite.getV1()-sprite.getV0());vertex(vs,p.x(),p.y(),p.z(),u,v);}
   }
   for(var entry:byTexture.entrySet()){JsonObject group=new JsonObject();group.addProperty("texture",entry.getKey());group.add("vertices",entry.getValue());groups.add(group);}meshes.put(key,groups);
  }
  JsonObject o=new JsonObject();o.addProperty("x",pos.getX());o.addProperty("y",pos.getY());o.addProperty("z",pos.getZ());o.addProperty("name",BuiltInRegistries.BLOCK.getKey(state.getBlock()).toString());o.add("groups",groups);return o;
 }
 @SuppressWarnings({"rawtypes","unchecked"})public static void export(Minecraft mc){
  if(mc.player==null||mc.level==null||!mc.hasSingleplayerServer()||System.currentTimeMillis()<nextExport)return;
  nextExport=System.currentTimeMillis()+100;
  try{
   AvatarRenderer renderer=(AvatarRenderer)mc.getEntityRenderDispatcher().getRenderer(mc.player);
   AvatarRenderState state=(AvatarRenderState)renderer.createRenderState();renderer.extractRenderState(mc.player,state,1);PlayerModel model=(PlayerModel)renderer.getModel();model.setupAnim(state);
   JsonObject packet=new JsonObject();packet.addProperty("protocol",2);packet.addProperty("ready",ready);packet.addProperty("visiting",visiting);packet.addProperty("x",mc.player.getX());packet.addProperty("y",mc.player.getY());packet.addProperty("z",mc.player.getZ());packet.addProperty("yaw",mc.player.getYRot());packet.addProperty("health",mc.player.getHealth());packet.addProperty("selected",mc.player.getInventory().getSelectedSlot());packet.addProperty("masterVolume",mc.options.getSoundSourceVolume(net.minecraft.sounds.SoundSource.MASTER));
   packet.addProperty("inventory",mc.gui.screen() instanceof net.minecraft.client.gui.screens.inventory.InventoryScreen);packet.addProperty("guiScale",mc.getWindow().getGuiScale());packet.add("vertices",playerMesh(model));packet.add("skin",texture(mc,state.skin.body().texturePath()));
   JsonArray hotbar=new JsonArray();for(int i=0;i<9;i++){var item=mc.player.getInventory().getItem(i);JsonObject o=new JsonObject();o.addProperty("id",BuiltInRegistries.ITEM.getKey(item.getItem()).toString());o.addProperty("count",item.getCount());hotbar.add(o);}packet.add("hotbar",hotbar);
   JsonArray blocks=new JsonArray();Set<BlockPos> current=new HashSet<>();
   for(int x=-7;x<=7;x++)for(int z=-7;z<=7;z++)for(int y=251;y<=255;y++){
    BlockPos pos=new BlockPos(x,y,z);BlockState b=mc.level.getBlockState(pos);if(b.isAir()||b.is(Blocks.BARRIER))continue;current.add(pos);blocks.add(block(mc,pos,b));
   }
   if(visiting){for(BlockPos p:seenBlocks)if(!current.contains(p)){mined++;log("native-block-removed "+p);}for(BlockPos p:current)if(!seenBlocks.contains(p)){placed++;log("native-block-added "+p);}}
   seenBlocks.clear();seenBlocks.addAll(current);packet.add("blocks",blocks);packet.addProperty("mined",mined);packet.addProperty("placed",placed);
   JsonArray textureList=new JsonArray();for(var entry:textures.entrySet()){JsonObject t=entry.getValue().deepCopy();t.addProperty("id",entry.getKey());textureList.add(t);}packet.add("textures",textureList);
   atomic("guest.json",gson.toJson(packet));atomic("minecraft.heartbeat",Long.toString(System.currentTimeMillis()));
   if(visiting&&!capturePending&&System.currentTimeMillis()-lastGuiCapture>150){
    capturePending=true;lastGuiCapture=System.currentTimeMillis();boolean inventory=mc.gui.screen() instanceof net.minecraft.client.gui.screens.inventory.InventoryScreen;double guiScale=mc.getWindow().getGuiScale();
    net.minecraft.client.Screenshot.takeScreenshot(mc.gameRenderer.mainRenderTarget(),image->guiWriter.execute(()->{
     try{
      int fw=image.getWidth(),fh=image.getHeight();int w=(int)((inventory?176:190)*guiScale),h=(int)((inventory?166:64)*guiScale);w=Math.min(w,fw);h=Math.min(h,fh);int x=(fw-w)/2,y=inventory?(fh-h)/2:fh-h;
      try(NativeImage crop=new NativeImage(w,h,false)){for(int py=0;py<h;py++)for(int px=0;px<w;px++)crop.setPixel(px,py,image.getPixel(px+x,py+y));JsonObject data=pixels(crop);data.addProperty("frameWidth",fw);data.addProperty("frameHeight",fh);data.addProperty("x",x);data.addProperty("y",y);atomic("native-gui.json",gson.toJson(data));}
     }catch(Exception e){log("native-gui-error "+e);}finally{image.close();capturePending=false;}
    }));
   }
  }catch(Exception e){log("export-error "+e);}
 }
}
