using System;
using System.Threading;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.theoretical;
using VoxelParty.UI;

public sealed class SpeedBuild : Gamemode
{
	[Property] List<GameObject> Islands { get; set; } = new(); public List<Structure> TargetStructures => ResourceLibrary.GetAll<Structure>( "structures/speedbuild/", false ).ToList();

	Structure _currentStructure;

	[Rpc.Broadcast]
	public void SetPlayerTransform( VoxelPlayer player, Vector3 position, Rotation rotation )
	{
		player.WorldPosition = position;
		player.GetComponent<PlayerController>().EyeAngles = rotation;
	}

	[Property, Description( "In seconds, in order of rounds. Rounds past the last entry use the same number." )]
	public int[] MemorizeTime { get; set; } = new[]{
		45, // Nice and easy
		30, // A bit more stressful
		15,
		15,
		10,
		10,
		10
	};
	[Property, Description( "In seconds, in order of rounds. Rounds past the last entry use the same number." )]
	public int[] BuildTimeOffset { get; set; } = new[]{
		30, // Nice and easy
		15, // A bit more stressful
		5,
		0,
		-5,
		-5,
		-10,
		-15
	};

	[Property, Description( "In seconds, in order of rounds. Rounds past the last entry use the same number." )]
	public int[] TargetAccuracy { get; set; } = new[]{
		40,
		50,
		60,
		70,
		80,
		90
	};

	[Property]
	public Structure.StructureDifficulty[] Difficulty { get; set; } = new[] {
		Structure.StructureDifficulty.Easy,
		Structure.StructureDifficulty.Easy,
		Structure.StructureDifficulty.Standard,
		Structure.StructureDifficulty.Standard,
		Structure.StructureDifficulty.Standard,
		Structure.StructureDifficulty.Hard,
		Structure.StructureDifficulty.Hard,
	};

	public async override Task GameModeLogic()
	{
		foreach ( var ply in Scene.GetAll<VoxelPlayer>() )
		{
			ply.IsReady = false; // Reset player readiness
		}

		await ReadyCheck();

		List<VoxelPlayer> HistoricalPlayers = new();

		foreach ( var player in Scene.GetAll<VoxelPlayer>() )
		{
			Players.Add( player );
		}

		bool gameRunning = true;
		Structure lastStructure = new();
		int RoundNumber = 0;
		if ( Players.Count < 3 ) // If we're only playing one round, up the difficulty a little.
		{
			RoundNumber = 2;
		}
		while ( gameRunning )
		{
			var structureList = TargetStructures.Where( c => c.ReplicateDifficulty == Difficulty.IndexOrLast( RoundNumber ) ).ToList();
			if ( structureList.Count == 0 )
			{
				Log.Warning( $"We don't have any structures for difficulty {Difficulty.IndexOrLast( RoundNumber )}? Why!?" );
				structureList = TargetStructures;
			}
			else if ( Players.Count < 3 ) // If we're in final death, use any structure.
			{
				structureList = TargetStructures;
			}

			_currentStructure = structureList[Random.Shared.Int( 0, structureList.Count - 1 )];
			while ( lastStructure.IsValid() && _currentStructure == lastStructure )
			{
				_currentStructure = structureList[Random.Shared.Int( 0, structureList.Count - 1 )];
			}

			if ( _currentStructure == null )
			{
				Log.Error( "No target structure set for SpeedBuild." );
				return;
			}

			var index = 0;
			BlockData[,,] anyStructureData = null;
			await Transition.Run( () =>
			{
				foreach ( var player in Players )
				{
					if ( index >= Islands.Count )
					{
						Log.Error( "Not enough islands for all players." );
						return;
					}

					Islands[index].Enabled = true;
					SetPlayerTransform( player, Islands[index].GetComponentInChildren<SpawnPoint>().WorldPosition,
						Rotation.LookAt( Vector3.Zero ).Angles().WithRoll( 0 ) );
					player.MakeFlying();
					player.IslandIndex = index;
					index++;
				}
			}, true );


			await Task.DelayRealtimeSeconds( 1f );
			foreach ( var p in Players.Where( c => !(c?.IsValid() ?? false) ).ToList() )
				Players.Remove( p ); // Cleanup any players we lost.

			List<(Vector3Int mins, Vector3Int maxes)> CleanupAreas = new();
			foreach ( var player in Players )
			{
				// Spawn a copy of the target structure on the player's island
				var obj = new GameObject();
				var structure = obj.AddComponent<StructureLoader>( false );
				structure.LoadedStructure = _currentStructure;

				var targetPosition = Islands[player.IslandIndex].Children.FirstOrDefault( c => c.Name == "StructureAnchor" ).WorldPosition;

				var size = structure.StructureSize;

				structure.WorldPosition = targetPosition - ((size.WithZ( 0 ) - Vector3Int.One) * World.BlockScale * 0.5f);
				structure.Enabled = true;

				player.HasBuildVolume = true;
				player.CanBuild = false;
				player.BuildAreaMins = (structure.WorldPosition / World.BlockScale).Floor();
				player.BuildAreaMaxs = player.BuildAreaMins + size - Vector3Int.One;
				anyStructureData ??= BlockData.GetAreaInBox( player.BuildAreaMins, size );

				CleanupAreas.Add( (player.BuildAreaMins, player.BuildAreaMaxs) );
			}

			Hud.TimerEnd = MemorizeTime.IndexOrLast( RoundNumber ); // Set the timer for 60 seconds
			Hud.TotalTime = MemorizeTime.IndexOrLast( RoundNumber ); // Set the total time for the game mode	
			Hud.HasTimer = true; // Show the timer UI

			Hud.Message = "Memorize the structure!";

			await Task.DelayRealtimeSeconds( MemorizeTime.IndexOrLast( RoundNumber ) ); // Wait for players to memorize the structure

			Hud.Message = "Time's up! Starting the build phase...";
			Hud.HasTimer = false; // Hide the timer UI
			foreach ( var p in Players.Where( c => !(c?.IsValid() ?? false) ).ToList() )
				Players.Remove( p ); // Cleanup any players we lost.
			foreach ( var player in Players )
			{
				// Clear the blocks in the player's build area;
				if ( World.Active != null )
				{
					for ( int z = player.BuildAreaMins.z; z <= player.BuildAreaMaxs.z; z++ )
					{
						for ( int y = player.BuildAreaMins.y; y <= player.BuildAreaMaxs.y; y++ )
						{
							for ( int x = player.BuildAreaMins.x; x <= player.BuildAreaMaxs.x; x++ )
							{
								World.Active.SetBlock( new Vector3Int( x, y, z ), new BlockData( "voxelparty:air" ) );
							}
						}
					}
				}
			}

			//await Task.DelayRealtimeSeconds( 2 ); // Wait for the build phase

			foreach ( var player in Players )
			{
				player.inventory.Clear();
				for ( int z = 0; z < anyStructureData.GetLength( 2 ); z++ )
				{
					for ( int y = 0; y < anyStructureData.GetLength( 1 ); y++ )
					{
						for ( int x = 0; x < anyStructureData.GetLength( 0 ); x++ )
						{
							var block = anyStructureData[x, y, z];
							player.inventory.PutInFirstAvailableSlot( new ItemStack( ItemRegistry.GetItemByIdentifier( block.BlockID ), 1 ) );
						}
					}
				}
				player.CanBuild = true; // Allow players to start building
			}

			Hud.Message = "Build!";
			Hud.TimerEnd = _currentStructure.SecondsToBuild + BuildTimeOffset.IndexOrLast( RoundNumber ); // Set the timer for 5 minutes
			Hud.TotalTime = _currentStructure.SecondsToBuild + BuildTimeOffset.IndexOrLast( RoundNumber ); // Set the total time for the game mode
			Hud.HasTimer = true; // Show the timer UI

			await Task.DelayRealtimeSeconds( _currentStructure.SecondsToBuild + BuildTimeOffset.IndexOrLast( RoundNumber ) ); // Wait for the build phase to end

			Hud.Message = "Time's up! Judging the builds...";
			Hud.HasTimer = false; // Hide the timer UI

			SpeedBuildHud.Instance.KillPercentage = TargetAccuracy.IndexOrLast( RoundNumber );
			foreach ( var p in Players.Where( c => !(c?.IsValid() ?? false) ).ToList() )
				Players.Remove( p ); // Cleanup any players we lost.
			foreach ( var player in Players )
			{
				player.CanBuild = false; // Disable building for all players
			}

			await Task.DelayRealtimeSeconds( 1 );

			int validBlocks = 0;
			for ( int z = 0; z < anyStructureData.GetLength( 2 ); z++ )
			{
				for ( int y = 0; y < anyStructureData.GetLength( 1 ); y++ )
				{
					for ( int x = 0; x < anyStructureData.GetLength( 0 ); x++ )
					{
						var block = anyStructureData[x, y, z];
						if ( !block.IsEmpty() )
							validBlocks++;
					}
				}
			}
			foreach ( var p in Players.Where( c => !(c?.IsValid() ?? false) ).ToList() )
				Players.Remove( p ); // Cleanup any players we lost.
			foreach ( var player in Players )
			{
				player.TotalBlockArea = validBlocks; // Set total block area
				player.CorrectBlocksPlaced = 0; // Reset correct blocks placed
				player.IncorrectBlocksPlaced = 0; // Reset incorrect blocks placed
			}


			Dictionary<VoxelPlayer, int> ScoreByPlayer = new();
			{   // Evaluate each block in each player's build area
				for ( int z = 0; z < anyStructureData.GetLength( 2 ); z++ )
				{
					for ( int y = 0; y < anyStructureData.GetLength( 1 ); y++ )
					{
						for ( int x = 0; x < anyStructureData.GetLength( 0 ); x++ )
						{
							var block = anyStructureData[x, y, z];
							if ( block.IsEmpty() ) continue;
							foreach ( var player in Players )
							{
								var pos = new Vector3Int( x, y, z ) + player.BuildAreaMins;
								var playerBlock = World.Active.GetBlock( pos );
								if ( playerBlock.BlockID == block.BlockID && playerBlock.BlockDataValue == block.BlockDataValue )
								{
									player.CorrectBlocksPlaced++;
								}
								else
								{
									player.IncorrectBlocksPlaced++;
								}
							}

							await Task.DelayRealtime( 10 ); // Wait 10 ms for animation purposes
						}
					}
				}
			}
			await Task.DelayRealtimeSeconds( 0.5f );
			foreach ( var p in Players.Where( c => !(c?.IsValid() ?? false) ).ToList() )
				Players.Remove( p ); // Cleanup any players we lost.
			foreach ( var player in Players )
			{
				ScoreByPlayer[player] = player.CorrectBlocksPlaced * 100 / validBlocks;
			}

			if ( Players.Count > 0 )
			{
				Hud.Message = "Executing the failures!";
				await Task.DelayRealtimeSeconds( 1 );
				foreach ( var p in Players.Where( c => !(c?.IsValid() ?? false) ).ToList() )
					Players.Remove( p ); // Cleanup any players we lost.
				var losers = Players.Where( k => ScoreByPlayer[k] < TargetAccuracy.IndexOrLast( RoundNumber ) ).OrderByDescending( k => ScoreByPlayer[k] ).ToList();
				foreach ( var player in losers )
				{
					if ( !(player?.IsValid() ?? false) )
						continue;
					if ( ScoreByPlayer[player] < TargetAccuracy.IndexOrLast( RoundNumber ) )
					{
						player.TotalBlockArea = 0;
						player.Explode();
						await Task.DelayRealtime( 500 );
					}
				}
				HistoricalPlayers.AddRange( losers.Reverse<VoxelPlayer>() );
				foreach ( var loser in losers )
					Players.Remove( loser );
			}
			foreach ( var p in Players.Where( c => !(c?.IsValid() ?? false) ).ToList() )
				Players.Remove( p ); // Cleanup any players we lost.
			if ( Players.Count <= 0 )
			{
				Hud.Message = "Declaring winners...";
				var podiumObject = GameObject.Children.Find( j => j.Name == "PodiumCamera" );
				await Transition.Run( () =>
				{
					foreach ( var ply in Scene.GetAll<VoxelPlayer>() )
					{
						ply.Spectator = true;
						ply.Lock();
						ply.MoveCameraTo( podiumObject.WorldTransform, true );
					}
				}, true );

				var allPlayers = Players.OrderByDescending( k => ScoreByPlayer[k] ).Concat( HistoricalPlayers.Reverse<VoxelPlayer>() ).Where( c => c?.IsValid() ?? false ).ToArray();
				await Task.DelayRealtimeSeconds( 1 );
				if ( allPlayers.Length > 2 )
				{
					Hud.Message = "Third Place";
					await Task.DelayRealtimeSeconds( 3f );
					if ( allPlayers[2].IsValid() )
					{
						var bronze = GameObject.Children.Find( j => j.Name == "Bronze" );
						Hud.Message = allPlayers[2].Network.Owner.DisplayName;
						allPlayers[2].Spectator = false;
						allPlayers[2].IsFlying = false;
						allPlayers[2].MoveTo( bronze.WorldTransform );
					}
					else
					{
						Hud.Message = "Someone who left!";
					}
					await Task.DelayRealtimeSeconds( 5f );
				}
				if ( allPlayers.Length > 1 )
				{
					Hud.Message = "Second Place";
					await Task.DelayRealtimeSeconds( 3f );
					if ( allPlayers[1].IsValid() )
					{
						var silver = GameObject.Children.Find( j => j.Name == "Silver" );
						Hud.Message = allPlayers[1].Network.Owner.DisplayName;
						allPlayers[1].Spectator = false;
						allPlayers[1].IsFlying = false;
						allPlayers[1].MoveTo( silver.WorldTransform );
					}
					else
					{
						Hud.Message = "Someone who left!";
					}
					await Task.DelayRealtimeSeconds( 5f );
				}
				Hud.Message = "The winner is...";
				await Task.DelayRealtimeSeconds( 3f );
				if ( allPlayers[0].IsValid() )
				{
					var gold = GameObject.Children.Find( j => j.Name == "Gold" );
					Hud.Message = allPlayers[0].Network.Owner.DisplayName;
					if ( allPlayers.Length == 1 )
						Hud.Message = allPlayers[0].Network.Owner.DisplayName + " (By default)";
					allPlayers[0].Spectator = false;
					allPlayers[0].IsFlying = false;
					allPlayers[0].MoveTo( gold.WorldTransform );
				}
				else
				{
					Hud.Message = "Someone who left!";
				}
				await Task.DelayRealtimeSeconds( 10f ); // Take 30 seconds to celebrate!
				Hud.Message = "Restarting!";
				// Restart the lobby
				await Transition.Run( () => Scene.LoadFromFile( "scenes/speed build.scene" ) );
				break;
			}

			foreach ( var p in Players.Where( c => !(c?.IsValid() ?? false) ).ToList() )
				Players.Remove( p ); // Cleanup any players we lost.
			foreach ( var ply in Players )
				ply.TotalBlockArea = 0;

			// Clean up all player structures.
			foreach ( var area in CleanupAreas )
			{
				for ( int z = area.mins.z; z <= area.maxes.z; z++ )
				{
					for ( int y = area.mins.y; y <= area.maxes.y; y++ )
					{
						for ( int x = area.mins.x; x <= area.maxes.x; x++ )
						{
							World.Active.SetBlock( new Vector3Int( x, y, z ), BlockData.Empty );
						}
					}
				}
			}
			Hud.Message = "Get ready for the next round!";
			await Task.DelayRealtimeSeconds( 3 );
			RoundNumber++;
		}

	}
}
