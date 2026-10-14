package castlecraft;
import net.fabricmc.api.ModInitializer;
import net.minecraft.server.MinecraftServer;
import java.nio.file.*;
public final class Bridge implements ModInitializer {
 public static final Path session=Path.of(System.getProperty("castlecraft.session","."));
 public void onInitialize(){VisitorBridge.log("guest-loaded v2: no resource or kill rewards");}
 public static void tick(MinecraftServer server){VisitorBridge.serverTick(server);}
}
