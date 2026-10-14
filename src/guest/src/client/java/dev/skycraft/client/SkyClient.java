package dev.skycraft.client;

import dev.skycraft.SkyCraft;
import dev.skycraft.client.render.WorldExporter;
import dev.skycraft.link.Proto;
import dev.skycraft.link.SkyLink;
import dev.skycraft.world.SkyCollision;
import net.minecraft.client.Camera;
import net.minecraft.client.Minecraft;
import net.minecraft.client.player.LocalPlayer;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.phys.Vec3;
import org.lwjgl.sdl.SDLVideo;

/**
 * Per-frame glue between the Minecraft client and Skyrim. Everything here runs on the render
 * thread, called from MinecraftMixin.
 */
public final class SkyClient {
	private static final boolean SHOW_WINDOW = Boolean.getBoolean("skycraft.showWindow");
	// Started by Skyrim (SkyCraft's bundled instance passes -Dskycraft.startHidden=true): no window and
	// no title-screen music from the first frame, even while Skyrim is paused (Alt-Tabbed) and the
	// two haven't linked up yet. Otherwise the window only goes once Skyrim is there.
	private static final boolean START_HIDDEN = Boolean.getBoolean("skycraft.startHidden");

	private static final SkyLink.SkyState sky = new SkyLink.SkyState();
	private static final SkyLink.McState mc = new SkyLink.McState();
	// Transport readiness is separate from ownership of an actual mirror world.
	private static volatile boolean linked;
	private static volatile net.minecraft.client.multiplayer.ClientLevel mirrorLevel;
	private static volatile net.minecraft.server.MinecraftServer mirrorServer;
	private static volatile long sessionId;
	private static boolean tookOver;
    private static boolean audioWasSuspended;
    /** Retained while the same mirror stays open, even after transport loss. */
    public static boolean mirrorSuspended(){
     var minecraft=Minecraft.getInstance();
     boolean mirror=dev.skycraft.world.BridgeSimulation.isClientMirror(minecraft.level)
      &&SkyCraft.isMirrorServer(minecraft.getSingleplayerServer());
     return dev.skycraft.world.CastleMirrorLifecycle.suspended(mirror,SkyLink.active(),sky.inGame(),sky.loading());
    }
	private static boolean windowHidden;
	private static int appliedViewportW, appliedViewportH;
    private static int previousWidth, previousHeight;
    private static boolean previousFullscreen;
    private static net.minecraft.client.CameraType previousCamera;

	// Teleport / hold state: Skyrim decides where the player is after loads, doors and respawns.
	private static volatile int lastTeleportSeq = -1;
	private static int teleportAck;
	private static boolean teleportPending;
	private static LocalPlayer lastPlayer;
	private static Vec3 holdPos;
    private static boolean followingCinematic;
    public static final dev.skycraft.world.CastleCinematicMotion cinematicMotion = new dev.skycraft.world.CastleCinematicMotion();
 private static int preparedEpoch;
	private static long holdSince;
 private static boolean arrivalPrepared;
 private static Vec3 arrivalOrigin;
 private static long lastArrivalRequest,lastArrivalDiagnostic;
 private static final int[] arrivalRejections=new int[4];
 private record ServerArrival(long session,int seq,int epoch,Vec3 feet) {}
 private static volatile ServerArrival serverArrival;
 private record PendingArrival(long session,int seq,int epoch,int teleportId,java.util.UUID player,Vec3 feet){}
 private static volatile PendingArrival pendingArrival;
 private static long nextArrivalPoll;
 private static long nextArrivalServerDiagnostic;
 // Recover a missed callback from vanilla's own accepted receipt state, never from a heartbeat.
 private static void pollVanillaArrival(Minecraft minecraft){
  long now=System.currentTimeMillis();if(now<nextArrivalPoll)return;nextArrivalPoll=now+250;
  var pending=pendingArrival;var server=minecraft.getSingleplayerServer();
  if(pending==null||server==null)return;
  if(now>=nextArrivalServerDiagnostic){
   nextArrivalServerDiagnostic=now+3000;
   SkyCraft.LOG.info("CastleCraft arrival pending session={} seq={} epoch={} receipt={} feet={}",pending.session,pending.seq,pending.epoch,pending.teleportId,pending.feet);
  }
  server.execute(()->{
   if(pendingArrival!=pending||pending.session!=sessionId||pending.seq!=lastTeleportSeq||pending.epoch!=sky.collisionEpoch||!linked)return;
   var player=server.getPlayerList().getPlayer(pending.player);if(player==null)return;
   var connection=(dev.skycraft.client.mixin.CastleTeleportStateAccessor)player.connection;
   int receipt=connection.castlecraft$teleportId();var awaiting=connection.castlecraft$awaitingPosition();
   if(awaiting==null&&receipt==pending.teleportId)confirmVanillaArrival(player,receipt);
   else if(awaiting!=null&&awaiting.distanceToSqr(pending.feet)<1e-8&&receipt!=pending.teleportId)
    pendingArrival=new PendingArrival(pending.session,pending.seq,pending.epoch,receipt,pending.player,pending.feet);
  });
 }
 /** Publish bridge arrival only after vanilla accepts the exact client's teleport receipt. */
 public static void confirmVanillaArrival(ServerPlayer player,int teleportId){
  var pending=pendingArrival;
  if(pending==null||!linked||!dev.skycraft.world.CastleArrivalReceipt.matches(
     new dev.skycraft.world.CastleArrivalReceipt.Ticket(pending.session,pending.seq,pending.epoch,pending.teleportId),
     sessionId,lastTeleportSeq,sky.collisionEpoch,teleportId)||!pending.player.equals(player.getUUID())
    ||!dev.skycraft.combat.SkyCombat.isBridgeLevel(player.level())||player.position().distanceToSqr(pending.feet)>1e-8)return;
  serverArrival=new ServerArrival(pending.session,pending.seq,pending.epoch,player.position());pendingArrival=null;
 }

 public static String arrivalStatus(){
  var arrival=serverArrival;
  if(arrival==null||arrival.session!=sessionId||arrival.seq!=teleportAck||arrival.epoch!=sky.collisionEpoch||holdPos!=null)return ",\"arrivalSeq\":0,\"arrivalEpoch\":0";
  return ",\"arrivalSeq\":"+arrival.seq+",\"arrivalEpoch\":"+arrival.epoch+",\"arrivalX\":"+arrival.feet.x+",\"arrivalY\":"+arrival.feet.y+",\"arrivalZ\":"+arrival.feet.z;
 }
 public static int nativeFrameLimit(){return Minecraft.getInstance().options.framerateLimit().get();}

	private static long qpcFreq;
	private static LocalPlayer eyePlayer;
	private static float eyeSmoothed;
	private static long frameCounter;
	private static int exporterErrors;

	private SkyClient() {
	}

	/** Raw transport readiness; startup may use this before the mirror world exists. */
	public static boolean connected() {
		return linked;
	}

	/** Only the local Overworld mirror may receive native input or rendering overrides. */
	public static boolean linked() {
		Minecraft minecraft = Minecraft.getInstance();
		return linked && mirrorLevel != null && minecraft.level == mirrorLevel
			&& minecraft.getSingleplayerServer() == mirrorServer
			&& minecraft.player != null && minecraft.player.level() == mirrorLevel;
	}

	/** Changes when the linked world exits/reopens, so queued work can reject old sessions. */
	public static long sessionId() {
		return sessionId;
	}

	/** Native input replaces physical input only while this mirror is actually active. */
	public static boolean tookOver() {
		return tookOver && linked();
	}

	public static SkyLink.SkyState sky() {
		return sky;
	}

	/** Start of Minecraft.runTick: pull state and input from Skyrim before anything else runs. */
	public static void beginFrame() {
		Minecraft minecraft = Minecraft.getInstance();
		SkyLink.poll();
        if(minecraft.level!=null&&SkyCraft.isMirrorServer(minecraft.getSingleplayerServer()))
            dev.skycraft.world.BridgeSimulation.setClientMirror(minecraft.level);
        else dev.skycraft.world.BridgeSimulation.setClientMirror(null);
		quitWithSkyrim(minecraft);

		boolean nowLinked = SkyLink.active();
		if (nowLinked) {
			SkyLink.readSkyState(sky); // on a torn read we simply keep last frame's state
		}
		if (nowLinked != linked) {
			linked = nowLinked;
			SkyCraft.LOG.info("CastleCraft: native link {}", linked ? "up" : "down");
			if (linked) {
				SkyCollision.startConsumer();
			} else {
				leaveMirror(minecraft);
			}
		}
		boolean audioSuspended=mirrorSuspended();
        if(audioSuspended&&!audioWasSuspended)minecraft.getSoundManager().stop();
        audioWasSuspended=audioSuspended;
        if (!linked) return;

		// Opening the mirror must depend on the transport, not linked(), or startup deadlocks.
		MirrorWorld.openWhenReady(minecraft);
		var server = minecraft.getSingleplayerServer();
		LocalPlayer player = minecraft.player;
		boolean inMirror = player != null && minecraft.level != null && server != null
			&& server.isSingleplayer() && SkyCraft.isMirrorServer(server)
			&& minecraft.level.dimension() == net.minecraft.world.level.Level.OVERWORLD
			&& player.level() == minecraft.level;
		if (!inMirror) {
			if (mirrorLevel != null || tookOver) leaveMirror(minecraft);
			dev.skycraft.world.SkyWater.clear();
			// Commands sent during world/resource loading must never replay into another save.
			SkyLink.drainInput((type, code, a, b, c) -> {});
			publishState(minecraft, false);
			return;
		}
		if (mirrorLevel != minecraft.level || mirrorServer != server) {
			leaveMirror(minecraft);
			mirrorLevel = minecraft.level;
			mirrorServer = server;
			dev.skycraft.combat.SkyCombat.setClientBridgeLevel(mirrorLevel);
			applyLinkedOptions();
		}
		dev.skycraft.world.SkyWater.refresh();
		CastleBindings.frame(minecraft);
		if (sky.menuOpen() || sky.loading() || sky.cinematic()) InputBridge.releaseAll();
		InputBridge.drain(minecraft);
		CastleStatus.frame(minecraft);
		CastleAudio.tick(minecraft);
		if (minecraft.gui.screen() instanceof net.minecraft.client.gui.screens.DeathScreen) {
			minecraft.gui.setScreen(null);
		}
		ProxySync.frame(minecraft);

        // The normal client remains visible through resource loading and world creation.
        if (!tookOver) {
            previousCamera = minecraft.options.getCameraType();
            previousWidth = minecraft.getWindow().getScreenWidth();
            previousHeight = minecraft.getWindow().getScreenHeight();
            previousFullscreen = minecraft.options.fullscreen().get();
            tookOver = true;
            minecraft.options.setCameraType(net.minecraft.client.CameraType.THIRD_PERSON_BACK);
        }
        if (minecraft.gui.overlay() == null && minecraft.gui.screen() == null) {
            applyViewportSize(minecraft);
            hideWindowOnce(minecraft);
        }
        publishState(minecraft, false);

		// A new player object means we just joined or respawned: put it where Skyrim's player is.
		if (player != lastPlayer) {
			lastPlayer = player;
			teleportPending = true;
		}
		if (sky.teleportSeq != lastTeleportSeq) {
			lastTeleportSeq = sky.teleportSeq;
			teleportPending = true;
		}
		if (teleportPending && sky.inGame() && !sky.loading()) {
			pendingArrival=null;serverArrival=null;arrivalPrepared=false;arrivalOrigin=new Vec3(sky.x,sky.y,sky.z);
			teleportPending = false;
			holdPos = new Vec3(sky.x, sky.y, sky.z);
		}

		// Look direction is driven by Skyrim (zero-latency camera); MC uses it for everything else.
		if (minecraft.gui.screen() == null && !minecraft.options.getCameraType().isFirstPerson()) {
			player.setYRot(sky.yaw);
			player.setXRot(sky.pitch);
			player.yRotO = sky.yaw;
			player.xRotO = sky.pitch;
		}
	}

	// Minecraft is started with Skyrim (the SKSE plugin launches it), so it goes when that Skyrim has
	// closed for good: saved and shut down the normal way. -Dskycraft.quitWithSkyrim=false keeps it
	// running instead (development: restarting Skyrim without restarting Minecraft).
	private static final boolean QUIT_WITH_SKYRIM = Boolean.parseBoolean(System.getProperty("skycraft.quitWithSkyrim", "false"));
	private static long skyrimGoneSince;
	private static long nextSkyrimCheck;
	// Started hidden by Skyrim but never connected: nobody can see or use this Minecraft, and it
	// would stop the next Skyrim from starting a fresh one ("already running"). It goes after this.
	private static final long NEVER_CONNECTED_QUIT_MS = 10 * 60 * 1000;
	private static final long STARTED_AT = System.currentTimeMillis();
	private static boolean gaveUpWaiting;

	private static void quitWithSkyrim(Minecraft minecraft) {
		int pid = SkyLink.skyrimPid();
		long now = System.currentTimeMillis();
		if (QUIT_WITH_SKYRIM && START_HIDDEN && pid == 0 && !tookOver && !gaveUpWaiting && now - STARTED_AT > NEVER_CONNECTED_QUIT_MS) {
			gaveUpWaiting = true;
			SkyCraft.LOG.warn("SkyCraft: started hidden but Skyrim never connected in {} minutes; quitting", NEVER_CONNECTED_QUIT_MS / 60000);
			minecraft.stop();
			return;
		}
		if (!QUIT_WITH_SKYRIM || pid == 0 || now < nextSkyrimCheck) {
			return;
		}
		nextSkyrimCheck = now + 1000;
		if (ProcessHandle.of(pid).map(ProcessHandle::isAlive).orElse(false)) {
			skyrimGoneSince = 0;
			return;
		}
		if (skyrimGoneSince == 0) {
			skyrimGoneSince = now;
		} else if (now - skyrimGoneSince > 5000) {
			SkyCraft.LOG.info("SkyCraft: Skyrim (pid {}) has closed; saving and quitting", pid);
			minecraft.stop();
		}
	}

	/** Called at the end of every client tick. */
	public static void clientTick(Minecraft minecraft) {
		MirrorWorld.tick(minecraft);
		DiscordPresence.tick(minecraft);
		if (!linked()) return;
		SkyDigClient.tick(minecraft);
		holdUntilReady(minecraft);
		publishTick(minecraft);
		publishState(minecraft, false);
		CastleStatus.frame(minecraft);
	}

	private static void leaveMirror(Minecraft minecraft) {
		// Invalidate queued server work before releasing input or restoring the normal client.
		sessionId++;
        // Stop queued/looping voices once on actual disconnect, without changing user volumes.
        if(mirrorLevel!=null&&!SkyLink.active())minecraft.getSoundManager().stop();
        mirrorLevel = null;
		mirrorServer = null;
		dev.skycraft.combat.SkyCombat.setClientBridgeLevel(null);
		InputBridge.releaseAll();
		CastleActions.reset();
		CastleStatus.reset();
		restoreWindow(minecraft);
		tookOver = windowHidden = false;
		lastPlayer = eyePlayer = null;
		holdPos = null;
		holdSince = 0;
		teleportPending = false;
		lastTeleportSeq = -1;
		teleportAck = 0;pendingArrival=null;serverArrival=null;arrivalPrepared=false;arrivalOrigin=null;followingCinematic=false;cinematicMotion.sample(false,0,0,0,0);
		eyeSmoothed = 0;
		mc.flags = 0;
		mc.teleportAck = 0;pendingArrival=null;serverArrival=null;arrivalPrepared=false;arrivalOrigin=null;
		dev.skycraft.world.SkyWater.clear();
	}

	/**
	 * Hands Skyrim the raw physics tick (previous + latest feet, smoothed eye height, walk bob) with a
	 * QueryPerformanceCounter timestamp. Skyrim interpolates between them on its own frame clock,
	 * exactly like Minecraft's renderer does with partial ticks.
	 */
	private static void publishTick(Minecraft minecraft) {
		LocalPlayer player = minecraft.player;
		if (!linked() || player == null) {
			return;
		}
		if (qpcFreq == 0) {
			qpcFreq = SkyLink.qpcFrequency();
		}
		float tickMs = minecraft.level != null ? minecraft.level.tickRateManager().millisecondsPerTick() : 50.0F;
		// The tick really "happened" partial ticks ago (DeltaTracker keeps the remainder).
		float remainder = minecraft.getDeltaTracker().getGameTimeDeltaPartialTick(false);
		mc.tickQpc = SkyLink.qpc() - (long) (remainder * tickMs * qpcFreq / 1000.0);
		mc.tickMs = tickMs;
		mc.prevX = player.xo;
		mc.prevY = player.yo;
		mc.prevZ = player.zo;
		mc.curX = player.getX();
		mc.curY = player.getY();
		mc.curZ = player.getZ();
		// Same smoothing as Camera.tick(): eye height eases halfway toward the target each tick.
		if (player != eyePlayer) {
			eyePlayer = player;
			eyeSmoothed = player.getEyeHeight();
		}
		mc.eyeHeightO = eyeSmoothed;
		eyeSmoothed += (player.getEyeHeight() - eyeSmoothed) * 0.5F;
		mc.eyeHeightT = eyeSmoothed;
		boolean bob = minecraft.options.bobView().get();
		var avatar = player.avatarState();
		mc.walkDistO = bob ? avatar.getInterpolatedWalkDistance(0.0F) : 0.0F;
		mc.walkDist = bob ? avatar.getInterpolatedWalkDistance(1.0F) : 0.0F;
		mc.bobO = bob ? avatar.getInterpolatedBob(0.0F) : 0.0F;
		mc.bob = bob ? avatar.getInterpolatedBob(1.0F) : 0.0F;
		SkyLink.writeMcState(mc);
	}

	/** Freeze the player until Skyrim's collision around them has arrived. */
 private static void holdUntilReady(Minecraft minecraft) {
  LocalPlayer player=minecraft.player;if(!linked()||player==null)return;
  boolean cinematic = sky.cinematic()&&sky.inGame();
  cinematicMotion.sample(cinematic,sky.collisionEpoch,sky.x,sky.z,System.nanoTime()/1_000_000_000.0);
  if(cinematic){
   followingCinematic=true;arrivalPrepared=false;pendingArrival=null;serverArrival=null;
   holdPos=new Vec3(sky.x,sky.y,sky.z);arrivalOrigin=holdPos;park(player,holdPos);return;
  }
  if(followingCinematic){
   followingCinematic=false;arrivalOrigin=new Vec3(sky.x,sky.y,sky.z);
   holdPos=arrivalOrigin;arrivalPrepared=false;lastArrivalRequest=0;
  }
  if(!sky.inGame()||sky.loading()){
   if(holdPos==null)holdPos=player.position();
  }
  if(holdPos==null){holdSince=0;return;}
  if(holdSince==0)holdSince=System.currentTimeMillis();
  if(sky.inGame()&&!sky.loading()&&!sky.cinematic()&&SkyCollision.epoch()==sky.collisionEpoch){
   // A same-room menu pause does not create a new arrival or teleport receipt.
   var completed=serverArrival;
   if(completed!=null&&dev.skycraft.world.CastleArrivalReceipt.canResume(
      new dev.skycraft.world.CastleArrivalReceipt.Room(completed.session,completed.seq,completed.epoch),
      sessionId,sky.teleportSeq,sky.collisionEpoch,teleportAck,pendingArrival!=null)){
    holdPos=null;holdSince=0;arrivalPrepared=false;arrivalOrigin=null;return;
   }
   if(arrivalPrepared&&preparedEpoch!=sky.collisionEpoch){arrivalPrepared=false;serverArrival=null;}
   if(arrivalPrepared&&!safeArrival(player,holdPos.x,holdPos.y,holdPos.z)){arrivalPrepared=false;pendingArrival=null;serverArrival=null;lastArrivalRequest=0;}
   if(!arrivalPrepared){
    Vec3 origin=arrivalOrigin==null?new Vec3(sky.x,sky.y,sky.z):arrivalOrigin;
    java.util.Arrays.fill(arrivalRejections,0);
    var safe=dev.skycraft.world.CastleArrivalPlan.choose(origin.x,origin.z,(x,z)->safeArrival(player,x,origin.y,z));
    if(safe==null&&System.currentTimeMillis()-lastArrivalDiagnostic>=3000){lastArrivalDiagnostic=System.currentTimeMillis();SkyCraft.LOG.warn("CastleCraft safe arrival pending seq={} epoch={} nativeFeet={} rejected unknown={} unsupported={} nativeWall={} minecraftCollision={}",sky.teleportSeq,sky.collisionEpoch,origin,arrivalRejections[0],arrivalRejections[1],arrivalRejections[2],arrivalRejections[3]);}
    if(safe!=null){holdPos=new Vec3(safe.x(),origin.y,safe.z());arrivalPrepared=true;preparedEpoch=sky.collisionEpoch;serverArrival=null;lastArrivalRequest=0;}
   }
   if(arrivalPrepared){
    var arrived=serverArrival;
    if(arrived!=null&&arrived.session==sessionId&&arrived.seq==sky.teleportSeq&&arrived.epoch==sky.collisionEpoch&&arrived.feet.distanceToSqr(holdPos)<1e-8){
     park(player,holdPos);teleportAck=sky.teleportSeq;holdPos=null;holdSince=0;arrivalPrepared=false;arrivalOrigin=null;
     SkyCraft.LOG.info("CastleCraft arrival confirmed seq={} epoch={} feet={}",teleportAck,sky.collisionEpoch,arrived.feet);return;
    }
    pollVanillaArrival(minecraft);
    long now=System.currentTimeMillis();if(pendingArrival==null&&now-lastArrivalRequest>=1000||now-lastArrivalRequest>=4000){lastArrivalRequest=now;requestTeleport(minecraft,holdPos.x,holdPos.y,holdPos.z,sky.yaw,sky.pitch);}
   }
  }
  park(player,holdPos);
 }
 private static boolean safeArrival(LocalPlayer player,double x,double y,double z){
  var box=player.getBoundingBox().move(x-player.getX(),y-player.getY(),z-player.getZ());
  int bx=(int)Math.floor(x),by=(int)Math.floor(y),bz=(int)Math.floor(z);
  if(!SkyCollision.isKnown(bx,by-1,bz)||!SkyCollision.isKnown(bx,by,bz)||!SkyCollision.isKnown(bx,by-SkyCollision.REGION_SIZE,bz)){arrivalRejections[0]++;return false;}
  if(SkyCollision.collisions(box.move(0,-.03,0)).isEmpty()){arrivalRejections[1]++;return false;}
  if(!SkyCollision.collisions(box.deflate(1e-5)).isEmpty()){arrivalRejections[2]++;return false;}
  boolean clear=player.level().noCollision(player,box.deflate(1e-5));if(!clear)arrivalRejections[3]++;return clear;
 }
 private static void park(LocalPlayer player,Vec3 feet){
  player.setDeltaMovement(Vec3.ZERO);player.setPos(feet.x,feet.y,feet.z);player.xo=feet.x;player.yo=feet.y;player.zo=feet.z;player.resetFallDistance();
 }

	private static void requestTeleport(Minecraft minecraft, double x, double y, double z, float yaw, float pitch) {
		LocalPlayer player = minecraft.player;
		if (!linked() || player == null) return;
		long session = sessionId;int seq=sky.teleportSeq,epoch=sky.collisionEpoch;
		player.setPos(x, y, z);
		player.setDeltaMovement(Vec3.ZERO);
		player.resetFallDistance();
		var server = minecraft.getSingleplayerServer();
		if (server != null) {
			var uuid = player.getUUID();
			server.execute(() -> {
				if (session != sessionId || !linked || mirrorServer != server || seq!=lastTeleportSeq || epoch!=sky.collisionEpoch) return;
				ServerPlayer sp = server.getPlayerList().getPlayer(uuid);
				if (sp != null && dev.skycraft.combat.SkyCombat.isBridgeLevel(sp.level())) {
                    // Apply the validated feet on the server before issuing the vanilla receipt.
                    // Never bind the handoff ticket to a stale spawn position.
                    sp.setDeltaMovement(Vec3.ZERO);
                    sp.setPos(x, y, z);
                    sp.teleportTo(x, y, z);
					sp.setYRot(yaw);
					sp.setXRot(pitch);
					sp.resetFallDistance();
                    int receipt=((dev.skycraft.client.mixin.CastleTeleportStateAccessor)sp.connection).castlecraft$teleportId();
                    pendingArrival=new PendingArrival(session,seq,epoch,receipt,sp.getUUID(),new Vec3(x,y,z));
                    SkyCraft.LOG.info("CastleCraft server teleport issued seq={} epoch={} receipt={} alive={} feet={}",seq,epoch,receipt,sp.isAlive(),sp.position());
                } else { SkyCraft.LOG.warn("CastleCraft teleport rejected seq={} epoch={} serverPlayer={} bridgeLevel={}",seq,epoch,sp!=null,sp!=null&&dev.skycraft.combat.SkyCombat.isBridgeLevel(sp.level())); }
			});
		}
		SkyCraft.LOG.info("CastleCraft: requesting teleport to {} {} {}", x, y, z);
	}

	/** After GameRenderer.render(): report the player to Skyrim and ship the overlay frame. */
	private static void publishState(Minecraft minecraft, boolean rendered) {
		if (!linked) {
			return;
		}
		LocalPlayer player = minecraft.player;
		int flags = 0;
		if (linked() && player != null) {
			float partial = minecraft.getDeltaTracker().getGameTimeDeltaPartialTick(false);
			Vec3 feet = player.getPosition(partial);
			Camera camera = minecraft.gameRenderer.mainCamera();
			flags |= Proto.MC_IN_WORLD;
			if (player.onGround()) {
				flags |= Proto.MC_ON_GROUND;
			}
			if (player.isShiftKeyDown()) {
				flags |= Proto.MC_SNEAKING;
			}
			if (player.isSprinting()) {
				flags |= Proto.MC_SPRINTING;
			}
			if (player.isDeadOrDying()) {
				flags |= Proto.MC_DEAD;
			}
			if (player.isSwimming()) {
				flags |= Proto.MC_SWIMMING;
			}
			if (player.getAbilities().flying) {
				flags |= Proto.MC_FLYING;
			}
            // Publish the endpoints and their time origin from the same render partial.
            // A tick callback's remainder can describe a different simulation step.
            if (qpcFreq == 0) qpcFreq = SkyLink.qpcFrequency();
            mc.tickMs = minecraft.level != null ? minecraft.level.tickRateManager().millisecondsPerTick() : 50.0F;
            mc.tickQpc = SkyLink.qpc() - (long)(partial * mc.tickMs * qpcFreq / 1000.0);
            mc.prevX=player.xo; mc.prevY=player.yo; mc.prevZ=player.zo;
            mc.curX=player.getX(); mc.curY=player.getY(); mc.curZ=player.getZ();
			mc.x = feet.x;
			mc.y = feet.y;
			mc.z = feet.z;
			mc.yaw = player.getYRot();
			mc.pitch = player.getXRot();
            Vec3 eye = player.getEyePosition(partial);
            mc.eyeHeight = (float)(eye.y - feet.y);
            mc.eyeX = eye.x; mc.eyeY = eye.y; mc.eyeZ = eye.z;
            mc.fov = minecraft.options.fov().get();
            mc.cameraMode = minecraft.options.getCameraType().ordinal();
            mc.cameraDistance = rendered && camera.isDetached() ? (float)camera.position().distanceTo(eye) : 0;
            if (rendered) {
                var entityState = minecraft.gameRenderer.gameRenderState().levelRenderState.cameraRenderState.entityRenderState;
                boolean bob = minecraft.options.bobView().get() && entityState.isPlayer;
                mc.bobPhase = bob ? entityState.backwardsInterpolatedWalkDistance : 0;
                mc.bobAmount = bob ? entityState.bob : 0;
            }
		}
		if (linked() && minecraft.gui.screen() != null) {
			flags |= Proto.MC_SCREEN_OPEN;
		}
		mc.flags = flags;
		mc.sensitivity = minecraft.options.sensitivity().get().floatValue();
        mc.frameLimit=nativeFrameLimit()+1;mc.lookAck=InputBridge.lookAck();
        mc.renderFlags=1|(minecraft.options.enableVsync().get()?2:0)|4
            |(minecraft.options.invertMouseX().get()?8:0)|(minecraft.options.invertMouseY().get()?16:0)
            |(minecraft.options.smoothCamera?32:0)|(minecraft.player!=null&&minecraft.player.isScoping()?64:0);
		mc.teleportAck = holdPos == null ? teleportAck : 0; // not "arrived" until we are released
		mc.guiScale = minecraft.getWindow().getGuiScale();
		mc.frameCounter = rendered ? ++frameCounter : frameCounter;
		SkyLink.writeMcState(mc);

    }

    public static void afterMouseMovement() {
        if(!tookOver())return;
        InputBridge.movementApplied();
        publishState(Minecraft.getInstance(),false);
    }

    public static void afterRender() {
        if (!linked() || !tookOver) return;
        Minecraft minecraft = Minecraft.getInstance();
        publishState(minecraft, true);
        int flags = mc.flags;
		if ((flags & Proto.MC_IN_WORLD) != 0) {
			try {
				WorldExporter.frame(minecraft, minecraft.getDeltaTracker().getGameTimeDeltaPartialTick(false));
			} catch (RuntimeException e) {
				if (exporterErrors++ < 5) {
					SkyCraft.LOG.error("SkyCraft: world export failed", e);
				}
			}
			FrameExporter.capture(minecraft);
		}
	}

    /** Minecraft's frame limiter owns pacing. Waiting for the host here stalls both producers. */
    public static void paceFrame() { } // Native rendering target; simulation tick rates stay independent.

    private static void restoreWindow(Minecraft mc) {
        if (previousCamera != null) mc.options.setCameraType(previousCamera);
        if (tookOver && previousWidth > 0 && previousHeight > 0) {
            mc.getWindow().setWindowed(previousWidth, previousHeight);
            mc.getWindow().setFullscreen(previousFullscreen);
        }
        previousCamera = null;
        appliedViewportW = appliedViewportH = 0;
        if (windowHidden) SDLVideo.SDL_ShowWindow(mc.getWindow().handle());
    }

	private static void applyLinkedOptions() {
        // Linked-only behavior lives in mixins. Never rewrite the user's
        // normal options.txt, render distance, sound volume or tutorial settings.
        SkyCraft.LOG.info("CastleCraft: linked without saving or resetting normal client options");
    }

    private static void hideWindowOnce(Minecraft minecraft) {
		if (windowHidden || SHOW_WINDOW) {
			return;
		}
		windowHidden = true;
		SDLVideo.SDL_HideWindow(minecraft.getWindow().handle());
		SkyCraft.LOG.info("SkyCraft: game window hidden (run with -Dskycraft.showWindow=true to keep it)");
	}

	private static void applyViewportSize(Minecraft minecraft) {
		int w = Math.min(sky.viewportW, Proto.MAX_OVERLAY_W);
		int h = Math.min(sky.viewportH, Proto.MAX_OVERLAY_H);
		if (w <= 0 || h <= 0 || (w == appliedViewportW && h == appliedViewportH)) {
			return;
		}
		appliedViewportW = w;
		appliedViewportH = h;
		minecraft.getWindow().setWindowed(w, h);
		SkyCraft.LOG.info("SkyCraft: sizing overlay to Skyrim viewport {}x{}", w, h);
	}
}

