package dev.skycraft.world;

/** Native normalized animation time -> Minecraft render-state clock, with no gameplay mutation. */
public final class CastleScriptedPose {
    private CastleScriptedPose() {}
    public static float progress(int action,float progress,float rate,float elapsed) {
        float p=progress+Math.max(0,rate)*Math.max(0,Math.min(.1F,elapsed));
        if(!Float.isFinite(p))return 0;
        return action==6||action==7?p-(float)Math.floor(p):Math.max(0,Math.min(1,p));
    }
    public static float walkPhase(float progress) { return progress*(float)(Math.PI*2/.6662); }
    public static float jumpBend(float progress) { return .35F+.65F*(float)Math.sin(Math.PI*Math.max(0,Math.min(1,progress))); }
}
