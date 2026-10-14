package dev.skycraft.client;

import dev.skycraft.link.SkyLink;
import dev.skycraft.client.mixin.CastleKeyAccessor;
import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import net.minecraft.client.Minecraft;

/** Includes actual modded key mappings; refreshes after a reconnect even if options did not change. */
final class CastleBindings {
 private static long next, lastSent;
 private static String last="";
 static void frame(Minecraft mc){
  long now=System.nanoTime();if(now<next)return;next=now+1_000_000_000L;
  StringBuilder b=new StringBuilder();
  for(var key:mc.options.keyMappings)b.append(key.getName()).append('=').append(((CastleKeyAccessor)key).castlecraft$key().getName()).append('\n');
  String text=b.toString();
  if((!text.equals(last)||now-lastSent>5_000_000_000L)&&SkyLink.tryWriteRender(0xCC01,ByteBuffer.wrap(text.getBytes(StandardCharsets.UTF_8)),null)){last=text;lastSent=now;}
 }
}
