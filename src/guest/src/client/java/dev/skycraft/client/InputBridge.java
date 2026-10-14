package dev.skycraft.client;

import dev.skycraft.combat.SkyCombat;
import dev.skycraft.link.Proto;
import dev.skycraft.link.SkyLink;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.screens.PauseScreen;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.client.input.KeyEvent;
import net.minecraft.client.input.MouseButtonInfo;
import org.lwjgl.sdl.SDLKeyboard;

/**
 * Replays Skyrim-captured input into Minecraft's own input handlers, as if the (hidden) MC
 * window had focus. Keeps a virtual keyboard so InputConstants.isKeyDown() still works.
 */
public final class InputBridge {
    private record MobContext(int serial,int epoch,int attacker,java.util.UUID uuid,long session,long received){}
    private static final java.util.Map<Integer,MobContext> mobContexts=new java.util.HashMap<>();
	private static final boolean[] KEYS = new boolean[512];
	private static final boolean[] BUTTONS = new boolean[8];
	private static double cursorX, cursorY;
	private static int modifiers;
	private static int clickLogs;
    private static int pendingLookAck, appliedLookAck;
    public static int lookAck(){return appliedLookAck;}
    public static void movementApplied(){appliedLookAck=pendingLookAck;}

	private InputBridge() {
	}

	public static boolean isKeyDown(int scancode) {
		return scancode >= 0 && scancode < KEYS.length && KEYS[scancode];
	}

	public static void drain(Minecraft minecraft) {
		SkyLink.drainInput((type, code, a, b, c) -> dispatch(minecraft, type, code, a, b, c));
	}

	private static void dispatch(Minecraft minecraft, int type, int code, int a, int b, int c) {
		if (!SkyClient.linked()) return;
        long session = SkyClient.sessionId();
        long handle = minecraft.getWindow().handle();
		switch (type) {
            case 23 -> {
                var server=minecraft.getSingleplayerServer();if(dev.skycraft.SkyCraft.isMirrorServer(server))server.execute(()->{if(session==SkyClient.sessionId()&&SkyClient.linked()&&minecraft.getSingleplayerServer()==server)dev.skycraft.combat.SkyCombat.hunterTarget(a,b,c);});
            }
            case 20 -> {
                var target=minecraft.level==null?null:minecraft.level.getEntity(a);
                if(mobContexts.size()>128)mobContexts.clear();
                if(target instanceof net.minecraft.world.entity.Mob)mobContexts.put(a,new MobContext(code,b,c,target.getUUID(),session,System.nanoTime()));
            }
            case 21,22 -> {
                var context=mobContexts.remove(a);var server=minecraft.getSingleplayerServer();var local=minecraft.player;
                if(context!=null&&dev.skycraft.combat.CastleMobReceipt.matches(context.session,session,context.serial,code,context.epoch,dev.skycraft.world.SkyCollision.epoch(),System.nanoTime()-context.received)&&dev.skycraft.SkyCraft.isMirrorServer(server)&&local!=null){
                    var uuid=local.getUUID();int targetId=a,kind=code&15,value=b;boolean effect=type==22;
                    server.execute(()->{if(session!=SkyClient.sessionId()||!SkyClient.linked()||minecraft.getSingleplayerServer()!=server)return;var owner=server.getPlayerList().getPlayer(uuid);if(owner!=null){if(effect)dev.skycraft.combat.SkyCombat.affectMob(owner,targetId,context.uuid,kind,value,context.epoch);else dev.skycraft.combat.SkyCombat.hurtMob(owner,targetId,context.uuid,kind,value,context.attacker,context.epoch);}});
                }
            }
			case Proto.IN_KEY -> key(minecraft, handle, code, a != 0);
			case Proto.IN_MOUSE_BUTTON -> {
				if (code > 0 && code < BUTTONS.length) {
					BUTTONS[code] = a != 0;
				}
				if (a != 0 && clickLogs++ < 20) {
					var hit = minecraft.hitResult;
					dev.skycraft.SkyCraft.LOG.info("SkyCraft: click {} -> {} {} (grabbed {}, screen {})", code, hit == null ? "null" : hit.getType(),
						hit instanceof net.minecraft.world.phys.EntityHitResult eh ? eh.getEntity().getName().getString() : hit == null ? "" : hit.getLocation(),
						minecraft.mouseHandler.isMouseGrabbed(), minecraft.gui.screen());
				}
				minecraft.mouseHandler.onButton(handle, new MouseButtonInfo(code, modifiers), a != 0 ? 1 : 0);
			}
			case Proto.IN_SCROLL -> minecraft.mouseHandler.onScroll(handle, 0.0, a / 120.0);
			case Proto.IN_CURSOR -> {
				double dx = a - cursorX;
				double dy = b - cursorY;
				cursorX = a;
				cursorY = b;
				// Host owns 2D aim; absolute cursor deltas must not also turn the player.
                if (minecraft.gui.screen() != null)
                    minecraft.mouseHandler.onMove(handle, a, b, dx, dy);
			}
            case 15 -> {
                pendingLookAck=c;
                if (minecraft.gui.screen() == null && minecraft.player != null && minecraft.options.getCameraType().isFirstPerson()) {
                    if (!minecraft.mouseHandler.isMouseGrabbed()) {
                        minecraft.mouseHandler.grabMouse();
                        // Consume vanilla first-move suppression with zero delta, retaining the first real turn.
                        minecraft.mouseHandler.onMove(handle,minecraft.mouseHandler.xpos(),minecraft.mouseHandler.ypos(),0,0);
                    }
                    minecraft.mouseHandler.onMove(handle,minecraft.mouseHandler.xpos()+a,minecraft.mouseHandler.ypos()+b,a,b);
                }
            }
			case Proto.IN_TEXT -> {
				if (minecraft.gui.screen() != null) {
					minecraft.keyboardHandler.textInput(handle, new String(Character.toChars(a)));
				}
			}
			case Proto.IN_RELEASE_ALL -> releaseAll();
			case 9 -> {
				var player=minecraft.player;if(player==null)break;
				double x=a/1000.0,y=b/1000.0,z=c/1000.0;
				player.setPos(x,y,z);player.xo=x;player.yo=y;player.zo=z;
				var velocity=player.getDeltaMovement();player.setDeltaMovement(0,code==1?0:velocity.y,0);if(code==1)player.resetFallDistance();
				var server=minecraft.getSingleplayerServer();if(server!=null){var uuid=player.getUUID();server.execute(()->{if(session!=SkyClient.sessionId()||!SkyClient.linked()||minecraft.getSingleplayerServer()!=server)return;var sp=server.getPlayerList().getPlayer(uuid);if(sp!=null){sp.teleportTo(x,y,z);if(code==1){sp.setDeltaMovement(net.minecraft.world.phys.Vec3.ZERO);sp.resetFallDistance();}}});}
			}
			case Proto.IN_HURT -> hurt(minecraft, code, a / 100.0F, b, c);
			case 10 -> {
				var server=minecraft.getSingleplayerServer();var player=minecraft.player;
				if(server!=null&&player!=null){var uuid=player.getUUID();float amount=a/100.0F;server.execute(()->{if(session!=SkyClient.sessionId()||!SkyClient.linked()||minecraft.getSingleplayerServer()!=server)return;var sp=server.getPlayerList().getPlayer(uuid);if(sp!=null&&amount>0)sp.heal(amount);});}
			}
			case 11 -> CastleLife.accept(minecraft, code, a, b, c);
            case 12 -> CastleMotion.accept(code, a, b);
            case 13 -> CastleActions.accept(minecraft, code, a, b, c);
            case 14 -> {
                var server=minecraft.getSingleplayerServer();var local=minecraft.player;
                if(dev.skycraft.SkyCraft.isMirrorServer(server)&&local!=null){var uuid=local.getUUID();server.execute(()->{if(session!=SkyClient.sessionId()||!SkyClient.linked()||minecraft.getSingleplayerServer()!=server)return;var player=server.getPlayerList().getPlayer(uuid);if(player!=null&&!player.isDeadOrDying())player.setAbsorptionAmount(Math.max(0,player.getAbsorptionAmount()+a/10000f));});}
            }
            case Proto.IN_OPEN_MENU -> {
				if (minecraft.gui.screen() == null && minecraft.player != null) {
					releaseAll();
					minecraft.gui.setScreen(new PauseScreen(true));
				}
			}
			default -> {
			}
		}
	}

	/** Skyrim hit the player: apply it as Minecraft damage on the integrated server (or the host's). */
	private static void hurt(Minecraft minecraft, int kind, float skyrimDamage, int attacker, int flags) {
		var server = minecraft.getSingleplayerServer();
		if (minecraft.player == null) {
			return;
		}
		if (server == null) {
			// A guest in a friend's world: the host's server applies it.
			if (net.fabricmc.fabric.api.client.networking.v1.ClientPlayNetworking.canSend(dev.skycraft.net.SkyNet.Hurt.TYPE)) {
				net.fabricmc.fabric.api.client.networking.v1.ClientPlayNetworking.send(new dev.skycraft.net.SkyNet.Hurt(kind, skyrimDamage, attacker, flags));
			}
			return;
		}
		var uuid = minecraft.player.getUUID();
        long session = SkyClient.sessionId();
		server.execute(() -> {
            if(session!=SkyClient.sessionId()||!SkyClient.linked()||minecraft.getSingleplayerServer()!=server)return;
			ServerPlayer player = server.getPlayerList().getPlayer(uuid);
			if (player != null) {
				SkyCombat.hurtPlayer(player, kind, skyrimDamage, attacker, flags);
			}
		});
	}

	private static void key(Minecraft minecraft, long handle, int scancode, boolean down) {
		if (scancode <= 0 || scancode >= KEYS.length) {
			return;
		}
		boolean wasDown = KEYS[scancode];
		KEYS[scancode] = down;
		updateModifiers();
		int action = down ? (wasDown ? -1 : 1) : 0; // -1 = repeat
		int keycode = SDLKeyboard.SDL_GetKeyFromScancode(scancode, (short) modifiers, true);
		minecraft.keyboardHandler.keyPress(handle, action, new KeyEvent(scancode, keycode, modifiers));
	}

	private static void updateModifiers() {
		int m = 0;
		if (KEYS[225]) m |= 0x0001; // SDL_KMOD_LSHIFT
		if (KEYS[229]) m |= 0x0002; // SDL_KMOD_RSHIFT
		if (KEYS[224]) m |= 0x0040; // SDL_KMOD_LCTRL
		if (KEYS[228]) m |= 0x0080; // SDL_KMOD_RCTRL
		if (KEYS[226]) m |= 0x0100; // SDL_KMOD_LALT
		if (KEYS[230]) m |= 0x0200; // SDL_KMOD_RALT
		modifiers = m;
	}

	/** Lift every key and button we think is held (focus moved to Skyrim, link dropped, ...). */
	public static void releaseAll() {
        CastleMotion.reset();
		Minecraft minecraft = Minecraft.getInstance();
		long handle = minecraft.getWindow().handle();
		for (int sc = 0; sc < KEYS.length; sc++) {
			if (KEYS[sc]) {
				KEYS[sc] = false;
				updateModifiers();
				minecraft.keyboardHandler.keyPress(handle, 0, new KeyEvent(sc, SDLKeyboard.SDL_GetKeyFromScancode(sc, (short) 0, true), modifiers));
			}
		}
		for (int button = 1; button < BUTTONS.length; button++) {
			if (BUTTONS[button]) {
				BUTTONS[button] = false;
				minecraft.mouseHandler.onButton(handle, new MouseButtonInfo(button, 0), 0);
			}
		}
	}
}
