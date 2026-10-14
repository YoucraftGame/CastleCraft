package dev.skycraft.combat;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
class CastleMobReceiptTest {
 @Test void staleRoomAndSessionCannotRedirectAHit(){assertTrue(CastleMobReceipt.matches(10,10,4,(4<<4)|1,7,7,10));assertFalse(CastleMobReceipt.matches(10,11,4,(4<<4)|1,7,7,10));assertFalse(CastleMobReceipt.matches(10,10,4,(4<<4)|1,6,7,10));}
 @Test void droppedHeaderAndExpiredSerialFailClosed(){assertFalse(CastleMobReceipt.matches(10,10,3,(4<<4)|1,7,7,10));assertFalse(CastleMobReceipt.matches(10,10,4,(4<<4)|1,7,7,2_000_000_000L));assertTrue(CastleMobReceipt.matches(10,10,4095,65535,7,7,10));}
}
