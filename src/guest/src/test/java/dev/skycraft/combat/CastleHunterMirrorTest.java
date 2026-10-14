package dev.skycraft.combat;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
import net.minecraft.world.item.Items;
import net.minecraft.world.item.ItemStack;
import net.minecraft.core.component.DataComponents;
import net.minecraft.network.chat.Component;
class CastleHunterMirrorTest {
 @BeforeAll static void bootstrap(){
  net.minecraft.SharedConstants.tryDetectVersion();net.minecraft.server.Bootstrap.bootStrap();
  // 26.3 item defaults bind during data loading, not Bootstrap. Bind only these
  // fixture items here; do not start a game/server/datapack loader for a copy test.
  Items.DIAMOND_SWORD.builtInRegistryHolder().bindComponents(net.minecraft.core.component.DataComponentMap.builder().set(DataComponents.MAX_STACK_SIZE,1).set(DataComponents.MAX_DAMAGE,1000).build());
  Items.TIPPED_ARROW.builtInRegistryHolder().bindComponents(net.minecraft.core.component.DataComponentMap.builder().set(DataComponents.MAX_STACK_SIZE,64).build());
 }
 @Test void snapshotDoesNotConsumeOrMutateRealInventory(){
  var sword=new ItemStack(Items.DIAMOND_SWORD);sword.set(DataComponents.DAMAGE,17);sword.set(DataComponents.CUSTOM_NAME,Component.literal("player weapon"));
  var copy=CastleHunterMirror.copyHotbar(i->i==0?sword:ItemStack.EMPTY);
  assertEquals(9,copy.size());assertNotSame(sword,copy.get(0));
  copy.get(0).set(DataComponents.DAMAGE,90);copy.get(0).set(DataComponents.CUSTOM_NAME,Component.literal("boss"));
  assertEquals(17,sword.get(DataComponents.DAMAGE));assertEquals("player weapon",sword.get(DataComponents.CUSTOM_NAME).getString());
 }
 @Test void changingPlayerEquipmentAfterEntryDoesNotReplaceBossSnapshot(){
  var arrow=new ItemStack(Items.TIPPED_ARROW,32);var copy=CastleHunterMirror.copyHotbar(i->arrow);
  arrow.setCount(1);assertEquals(32,copy.get(0).getCount());
  copy.get(0).setCount(2);assertEquals(32,copy.get(1).getCount());
 }
}
