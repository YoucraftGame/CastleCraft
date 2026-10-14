// Generated from sheets. Edit sheets first.
package castlecraft;
public final class Rows {
public static final class HooksCastleInput {
public static final String id = "castle_input";
public static final String game = "castle";
public static final String type = "Teran.Hero";
public static final String method = "HandleInput";
public static final String phase = "PREFIX";
public static final String guard = "suppress native controls while visitor active";
}
public static final class HooksMinecraftClient {
public static final String id = "minecraft_client";
public static final String game = "minecraft";
public static final String type = "net.minecraft.client.Minecraft";
public static final String method = "tick";
public static final String phase = "HEAD_TAIL";
public static final String guard = "local integrated server, explicit session";
}
public static final class HooksMinecraftServer {
public static final String id = "minecraft_server";
public static final String game = "minecraft";
public static final String type = "net.minecraft.server.MinecraftServer";
public static final String method = "tickServer";
public static final String phase = "TAIL";
public static final String guard = "local integrated server, one player";
}
public static final class SystemsTransport {
public static final String id = "transport";
public static final String kind = "shared_memory_v11";
public static final int pollMs = 50;
public static final int batch = 1;
public static final int maxBytes = 4194304;
public static final int queueCap = 1;
public static final int protocol = 11;
public static final String description = "Upstream seqlocks, entry rings, byte rings and triple buffered transparent native overlay";
}
public static final class SystemsControl {
public static final String id = "control";
public static final String kind = "F8_handoff";
public static final int pollMs = 50;
public static final int batch = 1;
public static final int maxBytes = 4194304;
public static final int queueCap = 1;
public static final int protocol = 11;
public static final String description = "WASD Space Shift, mouse mining/placement, 1-9 hotbar, F8 return; release stale input";
}
public static final class SystemsAvatar {
public static final String id = "avatar";
public static final String kind = "live_Minecraft_model";
public static final int pollMs = 50;
public static final int batch = 1;
public static final int maxBytes = 4194304;
public static final int queueCap = 1;
public static final int protocol = 11;
public static final String description = "F8 replaces native castle sprite at same feet position with full native Minecraft animated model; fixed castle 2D camera and sprite sorting";
}
public static final class SystemsBlocks {
public static final String id = "blocks";
public static final String kind = "Minecraft_simulation";
public static final int pollMs = 50;
public static final int batch = 1;
public static final int maxBytes = 4194304;
public static final int queueCap = 1;
public static final int protocol = 11;
public static final String description = "Actual blocks, inventory and interactions, export actual baked block geometry";
}
public static final class SystemsPhysics {
public static final String id = "physics";
public static final String kind = "guest_authority";
public static final int pollMs = 50;
public static final int batch = 1;
public static final int maxBytes = 4194304;
public static final int queueCap = 1;
public static final int protocol = 11;
public static final String description = "Export complete measured room floor and known-air regions; preserve Minecraft selection across scene loads; protect against missing floor";
}
public static final class SystemsLifecycle {
public static final String id = "lifecycle";
public static final String kind = "hidden_guest";
public static final int pollMs = 50;
public static final int batch = 1;
public static final int maxBytes = 4194304;
public static final int queueCap = 1;
public static final int protocol = 11;
public static final String description = "Hidden Minecraft window, automatic temporary simulation world, no save selection";
}
public static final class SystemsRendering {
public static final String id = "rendering";
public static final String kind = "Unity_mesh_bridge";
public static final int pollMs = 50;
public static final int batch = 1;
public static final int maxBytes = 4194304;
public static final int queueCap = 1;
public static final int protocol = 11;
public static final String description = "Native host MeshRenderer draws actual guest geometry in native castle scene";
}
public static final class SystemsEquipment {
public static final String id = "equipment";
public static final String kind = "real_inventory";
public static final int pollMs = 50;
public static final int batch = 1;
public static final int maxBytes = 4194304;
public static final int queueCap = 1;
public static final int protocol = 11;
public static final String description = "Actual hotbar, held item and armor, native Minecraft mining and placement rules";
}
public static final class SystemsVerification {
public static final String id = "verification";
public static final String kind = "actual_game_oracle";
public static final int pollMs = 50;
public static final int batch = 1;
public static final int maxBytes = 4194304;
public static final int queueCap = 1;
public static final int protocol = 11;
public static final String description = "Live handoff/return, placement inventory decrement, mining time and drops";
}
public static final class SystemsNativeGui {
public static final String id = "native_gui";
public static final String kind = "original_Minecraft_GUI";
public static final int pollMs = 150;
public static final int batch = 1;
public static final int maxBytes = 4194304;
public static final int queueCap = 1;
public static final int protocol = 11;
public static final String description = "Transparent original Minecraft HUD and inventory only; guest third-person camera removes first-person hands";
}
public static final class SystemsCombat {
public static final String id = "combat";
public static final String kind = "native_monster_proxy";
public static final int pollMs = 50;
public static final int batch = 1;
public static final int maxBytes = 4194304;
public static final int queueCap = 1;
public static final int protocol = 11;
public static final String description = "Minecraft weapon hits drive native castle monster/scarecrow/breakable damage; native enemy attacks route to Minecraft armor, shields, health";
}
public static final class VersionsCastle {
public static final String id = "castle";
public static final String version = "2021.3.25f1";
public static final String runtime = "Mono";
public static final String loader = "BepInEx 5.4.23.5";
public static final String license = "LGPL-2.1";
public static final String requirement = "Lost Castle Steam 434650";
}
public static final class VersionsMinecraft {
public static final String id = "minecraft";
public static final String version = "26.3";
public static final String runtime = "Java 25";
public static final String loader = "Fabric 0.19.5";
public static final String license = "Apache-2.0";
public static final String requirement = "Authenticated Minecraft Java installation";
}
}