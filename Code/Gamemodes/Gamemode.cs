using System;
using System.Threading.Tasks;
using VoxelParty.UI;

public abstract class Gamemode : Component
{
    public NetList<VoxelPlayer> Players { get; set; } = new();

    [Property] public GameObject Spawn { get; set; }

    public virtual GamemodeHud Hud => GamemodeHud.Instance;

    public bool IsPlaying = false;

    public virtual int MinPlayersToPlay => 1;

    protected override void OnStart()
    {
        if ( Networking.IsHost )
        {
            if ( IsPlaying ) return;
            IsPlaying = true;
            GameModeLogic();
        }
    }

    public async Task ReadyCheck( int minPlayers = 1, string message = null )
    {
        message ??= minPlayers <= 1 ? "Waiting for players to be ready!" : $"Waiting for {minPlayers} players to be ready!";
        Hud.Message = message;
        TimeUntil readyCheckDone = 120f;
        while ( readyCheckDone > 0f || Scene.GetAll<VoxelPlayer>().Count() < minPlayers || !Scene.GetAll<VoxelPlayer>().Any( c => c.IsReady ) )
        {
            // If NO-ONE is ready, reset the ready check timer
            if ( Scene.GetAll<VoxelPlayer>().Count() < minPlayers || !Scene.GetAll<VoxelPlayer>().Any( c => c.IsReady ) )
            {
                readyCheckDone = 120f;
                Hud.HasTimer = false; // Hide the timer UI whilst waiting for players to be ready
            }
            else
            {
                Hud.HasTimer = true;
            }

            // If half the online players are ready, set the timer to 30 seconds
            if ( Scene.GetAll<VoxelPlayer>().Count( p => p.IsReady ) >= Scene.GetAll<VoxelPlayer>().Count() / 2 )
            {
                readyCheckDone = MathF.Min( readyCheckDone, 30f );
            }

            // If EVERYONE is ready, set the timer to 3 seconds
            if ( Scene.GetAll<VoxelPlayer>().All( p => p.IsReady ) )
            {
                readyCheckDone = MathF.Min( readyCheckDone, 3f );
            }

            if ( Scene.GetAll<VoxelPlayer>().Any( p => p.IsReady ) )
            {
                Hud.TotalTime = 120f;
                Hud.TimerEnd = readyCheckDone;
            }
            else
            {
                Hud.TotalTime = 0f; // No total time if no players are ready
                Hud.TimerEnd = 0f; // No timer if no players are ready
            }
            await Task.DelayRealtime( 100 );
        }

        Hud.HasTimer = false;
        Hud.HasReadyCheck = false; // Hide the ready check UI
        await Task.DelayRealtimeSeconds( 0.5f );
    }

    public abstract Task GameModeLogic();
}