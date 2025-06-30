using System;
using System.Threading.Tasks;
using Sandbox.theoretical;

/*

    Each round is one of two things. Naming the previous build (or first build), or building the previous name


*/

public class Telephone : Gamemode
{
    private struct ChainLink
    {
        public required string Description;
        public required string PromptBy;
        public string BuiltBy = null;

        public ChainLink()
        {
        }
    }

    [Property] Structure BuildBox { get; set; }

    [Rpc.Broadcast]
    public void SetPlayerTransform( VoxelPlayer player, Vector3 position, Rotation rotation )
    {
        player.WorldPosition = position;
        player.GetComponent<PlayerController>().EyeAngles = rotation;
    }


    public const int MIN_PLAYERS = 1;

    [Property] public int LoopArounds { get; set; } = 1; // How many times to go back over the loop. Ideally should be 1, may be higher for debugging.
    [Property] public int BuildTime { get; set; } = 120; // How long each player gets to build the prompt.
    [Property] public int ExamineTime { get; set; } = 30; // How long each player gets to examine the built structure
    [Property] public int DescribeTime { get; set; } = 30; // How long each player gets to describe a build structure.


    // Chains[i] gets the Chain represented by index i, and then Chain[i][k] gets Link k of Chain i.
    List<List<ChainLink>> Chains = new();

    void BackToSpawn()
    {
        foreach ( var ply in Scene.GetAll<VoxelPlayer>() )
            SetPlayerTransform( ply, Spawn.WorldPosition, Spawn.WorldRotation );
    }

    async Task HandleBrokenChain( bool soft = false )
    {
        if ( Players.Any( c => !c.IsValid ) )
        {
            await Task.DelayRealtimeSeconds( -1f ); // TODO: Broken chain behaviour.

            var badPlayers = Players.Where( c => !c.IsValid ).ToList();
            foreach ( var ply in badPlayers )
                Players.Remove( ply );
        }

        return;
    }

    void SpawnIsland( int chainIndex, int linkIndex )
    {
        World.Active.LoadStructure( new Vector3Int( chainIndex * 19, linkIndex * 19, 0 ), BuildBox.StructureData );
    }

    Transform GetIslandSpawn( int chainIndex, int linkIndex )
    {
        return global::Transform.Zero.WithPosition( new Vector3( chainIndex * 19 + 10, linkIndex * 19 + 1.5f, 1f ) * World.BlockScale ).WithRotation( Rotation.FromYaw( 90f ) );
    }

    Vector3Int GetIslandBuildPoint( int chainIndex, int linkIndex )
    {
        return new Vector3Int( chainIndex * 19 + 2, linkIndex * 19 + 2, 1 );
    }

    public async override Task GameModeLogic()
    {
        foreach ( var ply in Scene.GetAll<VoxelPlayer>() )
        {
            ply.IsReady = false; // Reset player readiness
        }

        TimeUntil readyCheckDone = 120f;
        Hud.Message = $"Waiting for at least {MIN_PLAYERS} players to be ready!";

        while ( readyCheckDone > 0f || Scene.GetAll<VoxelPlayer>().Count( p => p.IsReady ) < MIN_PLAYERS )
        {
            // If NO-ONE is ready, reset the ready check timer
            if ( Scene.GetAll<VoxelPlayer>().Count( p => p.IsReady ) < MIN_PLAYERS )
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
        await Task.DelayRealtimeSeconds( 1 );

        foreach ( var player in Scene.GetAll<VoxelPlayer>().OrderBy( c => Guid.NewGuid() ) )
        {
            Players.Add( player );
            Chains.Add( new List<ChainLink>() );
        }

        Hud.Message = "Write something for someone else to build!";
        foreach ( var ply in Players )
            ply.TextBoxVisible = true;
        Hud.RequestTextBox();

        Hud.HasTimer = true;
        Hud.TotalTime = DescribeTime;
        Hud.TimerEnd = DescribeTime;
        await Task.DelayRealtimeSeconds( DescribeTime );
        Hud.HasTimer = false;
        await HandleBrokenChain();

        for ( int i = 0; i < Players.Count; i++ )
        {
            Players[i].TextBoxVisible = false;
            Chains[i].Add( new ChainLink()
            {
                Description = Players[i].TextBoxValue,
                PromptBy = Players[i].Network.Owner.DisplayName
            } );
        }

        // Main game loop.
        int RoundNumber = 2;
        while ( RoundNumber <= Players.Count * LoopArounds )
        {
            // EVERYBODY, BUILD!
            for ( int i = 0; i < Players.Count; i++ )
            {
                var ply = Players[i];
                int chainIndex = (i + RoundNumber) % Chains.Count;

                int IslandXIndex = chainIndex;
                int IslandYIndex = (RoundNumber / 2) - 1;

                SpawnIsland( IslandXIndex, IslandYIndex );
                var examineTransform = GetIslandSpawn( IslandXIndex, IslandYIndex );


                SetPlayerTransform( ply, examineTransform.Position, examineTransform.Rotation );
                var point = GetIslandBuildPoint( IslandXIndex, IslandYIndex );
                ply.SpecialMessage = $"Build: {Chains[chainIndex].Last().Description}";
                ply.HasBuildVolume = true;
                ply.BuildAreaMins = point;
                ply.BuildAreaMaxs = point + new Vector3Int( 15, 15, 15 );
                ply.CanBuild = true;
            }
            Hud.Message = "Build Phase!";
            Hud.HasTimer = true;
            Hud.TotalTime = BuildTime;
            Hud.TimerEnd = BuildTime;
            await Task.DelayRealtimeSeconds( BuildTime );
            await HandleBrokenChain();
            Hud.HasTimer = false;
            Hud.Message = "";
            BackToSpawn();
            for ( int i = 0; i < Players.Count; i++ )
            {
                var ply = Players[i];
                int chainIndex = (i + RoundNumber) % Chains.Count;
                ply.SpecialMessage = null;
                ply.HasBuildVolume = false;
                ply.CanBuild = false;
                var lastLinkIndex = Chains[chainIndex].Count - 1;
                var lastLink = Chains[chainIndex][lastLinkIndex];
                lastLink.BuiltBy = ply.Network.Owner.DisplayName;
                Chains[chainIndex][lastLinkIndex] = lastLink;
            }
            await Task.DelayRealtimeSeconds( 1f );
            await HandleBrokenChain();


            RoundNumber++;

            if ( RoundNumber > Players.Count * LoopArounds )
                break;

            for ( int i = 0; i < Players.Count; i++ )
            {
                var ply = Players[i];
                int chainIndex = (i + RoundNumber) % Chains.Count;
                int IslandXIndex = chainIndex;
                int IslandYIndex = (RoundNumber / 2) - 1;
                var examineTransform = GetIslandSpawn( IslandXIndex, IslandYIndex );
                SetPlayerTransform( ply, examineTransform.Position, examineTransform.Rotation );
            }

            Hud.Message = "Examine the build";
            Hud.HasTimer = true;
            Hud.TotalTime = ExamineTime;
            Hud.TimerEnd = ExamineTime;
            await Task.DelayRealtimeSeconds( ExamineTime );
            Hud.HasTimer = false;
            await HandleBrokenChain();

            BackToSpawn();
            Hud.RequestTextBox();
            foreach ( var ply in Players )
                ply.TextBoxVisible = true;

            Hud.Message = "Describe the build";

            Hud.HasTimer = true;
            Hud.TotalTime = DescribeTime;
            Hud.TimerEnd = DescribeTime;
            await Task.DelayRealtimeSeconds( DescribeTime );
            Hud.HasTimer = false;
            await HandleBrokenChain();

            for ( int i = 0; i < Players.Count; i++ )
            {
                var ply = Players[i];
                int chainIndex = (i + RoundNumber) % Chains.Count;
                Chains[chainIndex].Add( new ChainLink()
                {
                    Description = ply.TextBoxValue,
                    PromptBy = ply.Network.Owner.DisplayName
                } );
                ply.TextBoxVisible = false;
            }

            await Task.DelayRealtimeSeconds( 1f );
            await HandleBrokenChain();
            RoundNumber++;
        }

        // Let's go through all the chains!
        Hud.Message = "Let's see the builds!";
        await Task.DelayRealtimeSeconds( 2f );
        await HandleBrokenChain( true );

        for ( int chainIndex = 0; chainIndex < Chains.Count; chainIndex++ )
        {
            BackToSpawn();
            Hud.Message = $"Initial Prompt by {Chains[chainIndex][0].PromptBy}";
            await Task.DelayRealtimeSeconds( 4f );
            await HandleBrokenChain( true );

            Hud.Message = $"\"{Chains[chainIndex][0].Description}\"";
            await Task.DelayRealtimeSeconds( 4f );
            await HandleBrokenChain( true );

            foreach ( var ply in Players )
            {
                var examineTransform = GetIslandSpawn( chainIndex, 0 );
                SetPlayerTransform( ply, examineTransform.Position, examineTransform.Rotation );
            }

            Hud.Message = $"As imagined by {Chains[chainIndex][0].BuiltBy}";
            await Task.DelayRealtimeSeconds( 20f );
            await HandleBrokenChain( true );

            for ( int linkIndex = 1; linkIndex < Chains[chainIndex].Count; linkIndex++ )
            {
                Hud.Message = $"Described by {Chains[chainIndex][0].PromptBy} as";
                await Task.DelayRealtimeSeconds( 4f );
                await HandleBrokenChain( true );

                Hud.Message = $"\"{Chains[chainIndex][linkIndex].Description}\"";
                await Task.DelayRealtimeSeconds( 4f );
                await HandleBrokenChain( true );

                if ( Chains[chainIndex][linkIndex].BuiltBy == null ) break;
                foreach ( var ply in Players )
                {
                    var examineTransform = GetIslandSpawn( chainIndex, linkIndex );
                    SetPlayerTransform( ply, examineTransform.Position, examineTransform.Rotation );
                }
                Hud.Message = $"As imagined by {Chains[chainIndex][linkIndex].BuiltBy}";
                await Task.DelayRealtimeSeconds( 20f );
                await HandleBrokenChain( true );
            }
        }
        Hud.Message = "";
        await Task.DelayRealtimeSeconds( 1f );
        await HandleBrokenChain( true );

        // Bring down the walls.

        for ( int chainIndex = 0; chainIndex < Chains.Count; chainIndex++ )
        {
            for ( int linkIndex = 0; linkIndex < Chains[chainIndex].Count; linkIndex++ )
            {
                var anchor = new Vector3Int( chainIndex * 19, linkIndex * 19, 0 );
                for ( int x = 0; x < 20; x++ )
                {
                    for ( int z = 1; z <= 16; z++ )
                    {
                        World.Active.SetBlock( anchor + new Vector3Int( x, 0, z ), BlockData.Empty );
                        World.Active.SetBlock( anchor + new Vector3Int( 0, x, z ), BlockData.Empty );
                        World.Active.SetBlock( anchor + new Vector3Int( x, 19, z ), BlockData.Empty );
                        World.Active.SetBlock( anchor + new Vector3Int( 19, x, z ), BlockData.Empty );
                    }
                }
            }
        }

        Hud.Message = "Ending the round";
        await Task.DelayRealtimeSeconds( 120f );
        Scene.LoadFromFile( "scens/telephone.scene" );
    }
}