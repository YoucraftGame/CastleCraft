package dev.skycraft.world;

import static org.junit.jupiter.api.Assertions.*;
import static java.lang.foreign.ValueLayout.*;
import static dev.skycraft.link.Proto.*;
import java.lang.foreign.MemorySegment;
import java.lang.foreign.Arena;
import java.lang.reflect.Method;
import net.minecraft.core.BlockPos;
import net.minecraft.core.Direction;
import net.minecraft.world.phys.AABB;
import net.minecraft.world.phys.shapes.Shapes;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

/** Real protocol decoding plus Minecraft's own voxel movement, without a game window. */
class SkyCollisionTest {
    private static final int EPOCH=712;
    private static void call(String name, Class<?>[] types, Object... args) throws Exception {
        Method method=SkyCollision.class.getDeclaredMethod(name,types);
        method.setAccessible(true);method.invoke(null,args);
    }
    @BeforeEach void reset() throws Exception {call("clear",new Class<?>[]{int.class},EPOCH);}
    private void region(int x,int y,int z,long footprint) throws Exception {
        try (var arena=Arena.ofConfined()) {
        var bytes=arena.allocate(COL_REGION_HEADER_BYTES+COL_BLOCK_BYTES,8);
        bytes.set(JAVA_INT,0,x);bytes.set(JAVA_INT,4,y);bytes.set(JAVA_INT,8,z);
        bytes.set(JAVA_INT,12,x);bytes.set(JAVA_INT,16,y);bytes.set(JAVA_INT,20,z);
        bytes.set(JAVA_INT,24,EPOCH);bytes.set(JAVA_INT,28,1);
        bytes.set(JAVA_INT,32,x);bytes.set(JAVA_INT,36,y);bytes.set(JAVA_INT,40,z);
        for(int layer=0;layer<8;layer++)bytes.set(JAVA_LONG,48+layer*8,footprint);
        call("readRegion",new Class<?>[]{MemorySegment.class,long.class},bytes,0L);
        }
    }
    private void room(int epoch,double minX,double minZ,double maxX,double maxZ) throws Exception {
        try(var arena=Arena.ofConfined()) {
            var bytes=arena.allocate(COL_ROOM_BYTES,8);bytes.set(JAVA_INT,0,epoch);
            bytes.set(JAVA_DOUBLE,8,minX);bytes.set(JAVA_DOUBLE,16,minZ);bytes.set(JAVA_DOUBLE,24,maxX);bytes.set(JAVA_DOUBLE,32,maxZ);
            call("readRoom",new Class<?>[]{MemorySegment.class,long.class},bytes,0L);
        }
    }
    private double collision(AABB body,double dx,Direction.Axis axis) {
        var query=axis==Direction.Axis.X?body.expandTowards(dx,0,0):body.expandTowards(0,0,dx);
        var shapes=SkyCollision.collisions(query).stream().map(SkyCollision.NativeCollision::shape).toList();
        return Shapes.collide(axis,body,shapes,dx);
    }
    @Test void flightCannotCrossEitherRoomSideAtArbitraryHeight() throws Exception {
        room(EPOCH,65536,-10,65556,10);
        for(double y:new double[]{0,32,256,2048}){
            assertEquals(.2,collision(new AABB(65555.2,y,0,65555.8,y+1.8,.6),3,Direction.Axis.X),1e-8);
            assertEquals(-.2,collision(new AABB(65536.2,y,0,65536.8,y+1.8,.6),-3,Direction.Axis.X),1e-8);
        }
    }
    @Test void ridingOrPushingAtDepthBoundaryUsesEntityWidth() throws Exception {
        room(EPOCH,-20,-10,20,10);
        assertEquals(.1,collision(new AABB(-.7,40,8.5,.7,42,9.9),2,Direction.Axis.Z),1e-8);
        assertEquals(-.1,collision(new AABB(-.7,40,-9.9,.7,42,-8.5),-2,Direction.Axis.Z),1e-8);
    }
    @Test void flightInsideRoomAndSavedEntitiesInOtherRoomsRemainUnobstructed() throws Exception {
        room(EPOCH,65536,-10,65556,10);
        assertTrue(SkyCollision.collisions(new AABB(65540,32,-2,65541,34,-1)).isEmpty());
        assertTrue(SkyCollision.collisions(new AABB(131072,32,-2,131073,34,-1)).isEmpty());
    }
    @Test void epochClearRemovesBoundariesAndRejectsStaleRooms() throws Exception {
        room(EPOCH,-20,-10,20,10);
        call("clear",new Class<?>[]{int.class},EPOCH+1);room(EPOCH,-20,-10,20,10);
        assertEquals(2,collision(new AABB(19.2,32,0,19.8,34,1),2,Direction.Axis.X),1e-8);
        room(EPOCH+1,-20,-10,20,10);
        assertEquals(.2,collision(new AABB(19.2,32,0,19.8,34,1),2,Direction.Axis.X),1e-8);
    }
    @Test void malformedRoomMetadataCannotCreateInvisibleWalls() throws Exception {
        room(EPOCH,Double.NaN,-10,20,10);room(EPOCH,20,-10,-20,10);
        assertTrue(SkyCollision.collisions(new AABB(19,32,0,21,34,1)).isEmpty());
    }
    @Test void roomFloorHoldsRepeatedGravityForMobAtRemoteRoomOrigin() throws Exception {
        region(65536,-1,-7,-1L);
        var body=new AABB(65536.2,0,-6.8,65536.8,1.8,-6.2);
        for(int tick=0;tick<80;tick++){
            double gravity=-.0784;
            var nativeShapes=SkyCollision.collisions(body.expandTowards(0,gravity,0));
            var shapes=nativeShapes.stream().map(SkyCollision.NativeCollision::shape).toList();
            double dy=Shapes.collide(Direction.Axis.Y,body,shapes,gravity);
            assertEquals(0,dy,1e-9,"gravity must stop on native floor, tick="+tick);
            body=body.move(0,dy,0);
        }
        var support=SkyCollision.collisions(body.move(0,-.001,0)).getFirst();
        assertEquals(new BlockPos(65536,-1,-7),support.pos());
    }
    @Test void airborneMobLandsUsingVanillaVoxelCollision() throws Exception {
        region(42,-1,8,-1L);
        var body=new AABB(42.2,2,8.2,42.8,3.8,8.8);
        double velocity=0;
        for(int tick=0;tick<40;tick++){
            velocity=(velocity-.08)*.98;
            var shapes=SkyCollision.collisions(body.expandTowards(0,velocity,0)).stream().map(SkyCollision.NativeCollision::shape).toList();
            double dy=Shapes.collide(Direction.Axis.Y,body,shapes,velocity);
            body=body.move(0,dy,0);if(dy!=velocity)velocity=0;
        }
        assertEquals(0,body.minY,1e-9);
    }
    @Test void nativeWallStopsHorizontalMotionAndIgnoresDistantQueries() throws Exception {
        region(-5,0,9,-1L);
        var body=new AABB(-5.8,.1,9.2,-5.2,1.9,9.8);
        var shapes=SkyCollision.collisions(body.expandTowards(.5,0,0)).stream().map(SkyCollision.NativeCollision::shape).toList();
        assertEquals(.2,Shapes.collide(Direction.Axis.X,body,shapes,.5),1e-9);
        assertTrue(SkyCollision.collisions(new AABB(1,0,1,2,2,2)).isEmpty());
    }
    @Test void fractionalFloorEdgeRemainsPartialAndSupportsItsOccupiedSide() throws Exception {
        // x subvoxels 4..7 in each Z row: the lower boundary cell is half covered.
        region(-1,-1,0,0xf0f0f0f0f0f0f0f0L);
        var inside=new AABB(-.4,0,.1,-.1,1.8,.9);
        var shapes=SkyCollision.collisions(inside.expandTowards(0,-.08,0)).stream().map(SkyCollision.NativeCollision::shape).toList();
        assertEquals(0,Shapes.collide(Direction.Axis.Y,inside,shapes,-.08),1e-9);
        assertTrue(SkyCollision.collisions(new AABB(-.95,-.1,.1,-.6,1.8,.9)).isEmpty());
    }
    @Test void snapshotSurvivesEpochClearAndNewQueriesDoNotUseOldRoom() throws Exception {
        region(5,-1,6,-1L);
        var box=new AABB(5.2,-.1,6.2,5.8,1.8,6.8);
        var snapshot=SkyCollision.collisions(box);
        call("clear",new Class<?>[]{int.class},EPOCH+1);
        assertEquals(1,snapshot.size());
        assertEquals(0,snapshot.getFirst().shape().max(Direction.Axis.Y),1e-9);
        assertTrue(SkyCollision.collisions(box).isEmpty());
    }
}
