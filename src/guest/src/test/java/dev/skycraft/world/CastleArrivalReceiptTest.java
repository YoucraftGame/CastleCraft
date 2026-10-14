package dev.skycraft.world;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
class CastleArrivalReceiptTest {
 @Test void onlyMatchingVanillaReceiptCanRestore(){var t=new CastleArrivalReceipt.Ticket(4,8,2,19);assertTrue(CastleArrivalReceipt.matches(t,4,8,2,19));assertFalse(CastleArrivalReceipt.matches(t,4,8,2,18));}
 @Test void oldRoomOrSessionCannotRestore(){var t=new CastleArrivalReceipt.Ticket(4,8,2,19);assertFalse(CastleArrivalReceipt.matches(t,5,8,2,19));assertFalse(CastleArrivalReceipt.matches(t,4,8,3,19));assertFalse(CastleArrivalReceipt.matches(t,4,9,2,19));}
}
