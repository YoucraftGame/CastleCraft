// Generated from sheets. Edit sheets first.
namespace CastleCraft.Rows {
public static class HooksCastleInput {
public const string @id = "castle_input";
public const string @game = "castle";
public const string @type = "Teran.Hero";
public const string @method = "HandleInput";
public const string @phase = "PREFIX";
public const string @guard = "suppress native controls while visitor active";
}
public static class HooksMinecraftClient {
public const string @id = "minecraft_client";
public const string @game = "minecraft";
public const string @type = "net.minecraft.client.Minecraft";
public const string @method = "tick";
public const string @phase = "HEAD_TAIL";
public const string @guard = "local integrated server, explicit session";
}
public static class HooksMinecraftServer {
public const string @id = "minecraft_server";
public const string @game = "minecraft";
public const string @type = "net.minecraft.server.MinecraftServer";
public const string @method = "tickServer";
public const string @phase = "TAIL";
public const string @guard = "local integrated server, one player";
}
public static class SystemsTransport {
public const string @id = "transport";
public const string @kind = "shared_memory_v11";
public const int @pollMs = 50;
public const int @batch = 1;
public const int @maxBytes = 4194304;
public const int @queueCap = 1;
public const int @protocol = 11;
public const string @description = "Upstream seqlocks, entry rings, byte rings and triple buffered transparent native overlay";
}
public static class SystemsControl {
public const string @id = "control";
public const string @kind = "F8_handoff";
public const int @pollMs = 50;
public const int @batch = 1;
public const int @maxBytes = 4194304;
public const int @queueCap = 1;
public const int @protocol = 11;
public const string @description = "WASD Space Shift, mouse mining/placement, 1-9 hotbar, F8 return; release stale input";
}
public static class SystemsAvatar {
public const string @id = "avatar";
public const string @kind = "live_Minecraft_model";
public const int @pollMs = 50;
public const int @batch = 1;
public const int @maxBytes = 4194304;
public const int @queueCap = 1;
public const int @protocol = 11;
public const string @description = "F8 replaces native castle sprite at same feet position with full native Minecraft animated model; fixed castle 2D camera and sprite sorting";
}
public static class SystemsBlocks {
public const string @id = "blocks";
public const string @kind = "Minecraft_simulation";
public const int @pollMs = 50;
public const int @batch = 1;
public const int @maxBytes = 4194304;
public const int @queueCap = 1;
public const int @protocol = 11;
public const string @description = "Actual blocks, inventory and interactions, export actual baked block geometry";
}
public static class SystemsPhysics {
public const string @id = "physics";
public const string @kind = "guest_authority";
public const int @pollMs = 50;
public const int @batch = 1;
public const int @maxBytes = 4194304;
public const int @queueCap = 1;
public const int @protocol = 11;
public const string @description = "Export complete measured room floor and known-air regions; preserve Minecraft selection across scene loads; protect against missing floor";
}
public static class SystemsLifecycle {
public const string @id = "lifecycle";
public const string @kind = "hidden_guest";
public const int @pollMs = 50;
public const int @batch = 1;
public const int @maxBytes = 4194304;
public const int @queueCap = 1;
public const int @protocol = 11;
public const string @description = "Hidden Minecraft window, automatic temporary simulation world, no save selection";
}
public static class SystemsRendering {
public const string @id = "rendering";
public const string @kind = "Unity_mesh_bridge";
public const int @pollMs = 50;
public const int @batch = 1;
public const int @maxBytes = 4194304;
public const int @queueCap = 1;
public const int @protocol = 11;
public const string @description = "Native host MeshRenderer draws actual guest geometry in native castle scene";
}
public static class SystemsEquipment {
public const string @id = "equipment";
public const string @kind = "real_inventory";
public const int @pollMs = 50;
public const int @batch = 1;
public const int @maxBytes = 4194304;
public const int @queueCap = 1;
public const int @protocol = 11;
public const string @description = "Actual hotbar, held item and armor, native Minecraft mining and placement rules";
}
public static class SystemsVerification {
public const string @id = "verification";
public const string @kind = "actual_game_oracle";
public const int @pollMs = 50;
public const int @batch = 1;
public const int @maxBytes = 4194304;
public const int @queueCap = 1;
public const int @protocol = 11;
public const string @description = "Live handoff/return, placement inventory decrement, mining time and drops";
}
public static class SystemsNativeGui {
public const string @id = "native_gui";
public const string @kind = "original_Minecraft_GUI";
public const int @pollMs = 150;
public const int @batch = 1;
public const int @maxBytes = 4194304;
public const int @queueCap = 1;
public const int @protocol = 11;
public const string @description = "Transparent original Minecraft HUD and inventory only; guest third-person camera removes first-person hands";
}
public static class SystemsCombat {
public const string @id = "combat";
public const string @kind = "native_monster_proxy";
public const int @pollMs = 50;
public const int @batch = 1;
public const int @maxBytes = 4194304;
public const int @queueCap = 1;
public const int @protocol = 11;
public const string @description = "Minecraft weapon hits drive native castle monster/scarecrow/breakable damage; native enemy attacks route to Minecraft armor, shields, health";
}
public static class VersionsCastle {
public const string @id = "castle";
public const string @version = "2021.3.25f1";
public const string @runtime = "Mono";
public const string @loader = "BepInEx 5.4.23.5";
public const string @license = "LGPL-2.1";
public const string @requirement = "Lost Castle Steam 434650";
}
public static class VersionsMinecraft {
public const string @id = "minecraft";
public const string @version = "26.3";
public const string @runtime = "Java 25";
public const string @loader = "Fabric 0.19.5";
public const string @license = "Apache-2.0";
public const string @requirement = "Authenticated Minecraft Java installation";
}
}