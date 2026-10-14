namespace PeakCraft.World;
// Low byte: animation semantic. High byte: phase (1/2). Actor word at offset 38.
internal static class CastleHunterState {
 internal static readonly string[] Names={"idle","move","born","attack0","attack1","attack2","attack3","dashAttack","skill","beAttacked","beKnockedDown","getUp","die","jump","fall","dash","win","run","move_backward","talking","unactivated","discover","beAttackedA","beAttackedB","skillA","skillB","skillC"};
 internal static ushort Encode(int action,bool phase2)=>(ushort)((action+1)|(phase2?2:1)<<8);
 internal static bool Present(bool alive,bool hunter)=>alive||hunter;
}
