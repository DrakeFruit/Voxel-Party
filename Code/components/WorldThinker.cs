using System;
using System.IO;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Mounting;

public sealed class WorldThinker : Component, Component.ExecuteInEditor
{
	[Property] public int RenderChunkRadius { get; set; } = 10;
	[Property] public int RenderForgetRadius { get; set; } = 20; // How far away chunks are unloaded
	[Property] public Material TextureAtlas { get; set; }
	[Property] public Material TranslucentTextureAtlas { get; set; } // For translucent blocks, like glass or water
	[Property] public int BatchSize { get; set; } = 10; // Number of chunks to load in each batch
	[Property] public int UnloadBatchSize { get; set; } = 10; // Number of chunks to check to unload in each batch

	[Property] public bool LoadAroundPlayer { get; set; } = true;
	[Property]
	public string SerializedWorld { get; set; } // Bringing this back so we can force load the old-style worlds

	[Property, Hide]
	public byte[] SerializedWorldById
    {
        get
        {
            return World.Active?.SerializeByID().ToArray();
        }
        set
        {
            if ( value == null ) return;
            World.Active?.DeserializeByID( value );
        }
    }

	public World World = new();

	[ConVar] public static int vp_debug_showchunkborders { get; set; } = 0;

	protected override void OnStart()
	{
		base.OnStart();
		if ( !ItemRegistry.FinishedLoading )
			OnLoad().Wait(); // Ensure it's loaded as well.
		_ = TexArrayTool.UpdateMaterialTexture( TextureAtlas );
		_ = TexArrayTool.UpdateMaterialTexture( TranslucentTextureAtlas );
		if ( Networking.IsHost )
			foreach ( var child in GameObject.Children.ToList() )
				child.DestroyImmediate();
	}

	protected override Task OnLoad()
	{
		Log.Info( "ENSURING ALL ITEMS ARE LOADED!" );
		// Ensure all `.item` files in the `items` folder are loaded.
		var itemRoot = "items/";
		foreach ( var assetPath in FileSystem.Mounted.FindFile( itemRoot, "*.item_c", true ) )
		{
			if ( !ResourceLibrary.TryGet<Item>( itemRoot + assetPath, out var item ) )
			{
				Log.Error( $"Failed to load {assetPath}!" );
			}
		}
		ItemRegistry.UpdateRegistry();
		ItemRegistry.FinishedLoading = true;
		return GameTask.CompletedTask;
	}

	[Button]
	public void Regenerate()
	{
		var data = this.SerializedWorldById;
		if(data == null || data.Length == 0 )
		{
			Log.Warning( "No serialized world data to regenerate from!" );
			return;
		}
		foreach ( var child in GameObject.Children.ToList() )
			child.DestroyImmediate();
		World.SimulatedChunks.Clear();
		this.SerializedWorldById = data; // Re-apply the serialized world to regenerate it.
		foreach ( var obj in Scene.GetAll<StructureLoader>() )
		{
			_ = obj.Regenerate(); // Re-run the OnEnabled logic to ensure the structure is loaded in the editor.
		}

	}

	[Button]
	public void TryLoadOldWorld()
    {
		if ( string.IsNullOrWhiteSpace( SerializedWorld ) )
        {
			Log.Warning( "No serialized world data to load from!" );
			return;
        }
		var data = Convert.FromBase64String( SerializedWorld );
		// Manually deserialize the world from the old format.
		var reader = new BinaryReader( new MemoryStream( data ) );
		while (reader.BaseStream.Position < reader.BaseStream.Length )
        {
            int x  = reader.ReadInt32();
			int y  = reader.ReadInt32();
			int z  = reader.ReadInt32();
			var chunkPosition = new Vector3Int( x, y, z );
			var chunkDataLength = reader.ReadInt32();
			var chunkData = reader.ReadBytes( chunkDataLength );
			var chunk = World.GetChunk( chunkPosition );
			// Manually deserialize the chunk data
			var dataList = chunkData.RunLengthDecodeBy(2).ToList();
			if (dataList.Count != Chunk.SIZE.x * Chunk.SIZE.y * Chunk.SIZE.z * 2)
            {
				Log.Error( $"Chunk data length mismatch for chunk at {chunkPosition}!" );
				continue;
            }
			int index = 0;
			for ( int cz = 0; cz < Chunk.SIZE.z; cz++ )
			{
				for ( int cy = 0; cy < Chunk.SIZE.y; cy++ )
				{
					for ( int cx = 0; cx < Chunk.SIZE.x; cx++ )
					{
						ushort blockID = (ushort)dataList[index];
						if(blockID == 0)
						{
							// Air block
							chunk.SetBlock( cx, cy, cz, new BlockData( "voxelparty:air" ) );
							index += 2;
							continue;
						}
						var blockByID = ResourceLibrary.GetAll<Item>().FirstOrDefault( i => i is Item it && it.ID == blockID ) as Item;
						var blockIDName = "voxelparty:air";
						if ( blockByID == null )
						{
							Log.Warning( $"Block with ID {blockID} not found in registry!" );
                        }
                        else
                        {
							blockIDName = blockByID.Identifier;
                        }
						chunk.SetBlock( cx, cy, cz, new BlockData( blockIDName,  dataList[index+1]) );
						index += 2;
					}
				}
			}
		}

    }

	protected override void OnFixedUpdate()
	{
		World.Active = World;
		if ( !Networking.IsHost )
			return; // Only the host should handle chunk loading and unloading.
		if ( !ItemRegistry.FinishedLoading )
			return; // Make sure the itemRegistry finished loading first.

		// Iterate (randomly) through every loaded chunk, check if it's outside of the forget radius from any player, and if so, unload it.
		if ( Game.IsPlaying && LoadAroundPlayer )
		{
			List<Vector3Int> forgetList = new();
			foreach ( var kv in Random.Shared.TakeRandom( World.SimulatedChunks.Where( c => c.Value.IsRendered ), UnloadBatchSize ) )
			{
				// If the chunk manhatten distance is greater than the forget radius, append it to the forget list
				var chunk = kv.Value;
				var chunkDistance = Scene.GetAllComponents<PlayerController>()
						.Select( c => ((c.WorldPosition / World.BlockScale / Chunk.SIZE).Floor() - chunk.Position)
							.Components().Select( Math.Abs ).Max() ).Min();
				if ( chunkDistance > RenderForgetRadius )
				{
					forgetList.Add( chunk.Position );
				}
			}
			// Now we have a list of chunks to unload, we can unload them.
			foreach ( var chunkPosition in forgetList )
			{
				var chunk = World.GetChunk( chunkPosition );
				Log.Info( $"Checking chunk at {chunkPosition} for unloading." );
				if ( chunk.IsRendered )
				{
					Log.Info( $"Unloading chunk at {chunkPosition}." );
					chunk.ChunkObject.GameObject?.Destroy();
					chunk.ChunkObject = null;
				}
			}
		}

		// For each player, ensure all the chunks around them are loaded.
		// We do NOT load every chunk at once, we do it in batches, prioritizing chunks closest to the player.

		// To do this, we get every player, and create a hashset of chunks to load.
		// Then, we order the set by distance to the closest player, and skip loaded chunks.
		// Finally, we load the chunks in batches of 10, or until we reach a certain time limit.

		var players = Scene.GetAll<PlayerController>();
		var targetChunks = new HashSet<Vector3Int>();
		if ( !LoadAroundPlayer || !Game.IsPlaying )
		{
			targetChunks = World.SimulatedChunks.Select( kv => kv.Key ).ToHashSet();
		}
		else
		{
			foreach ( var player in players )
			{
				var playerPosition = (player.WorldPosition / World.BlockScale).Floor();
				var playerChunkPosition = new Vector3Int(
					playerPosition.x.FloorDiv( Chunk.SIZE.x ),
					playerPosition.y.FloorDiv( Chunk.SIZE.y ),
					playerPosition.z.FloorDiv( Chunk.SIZE.z )
				);
				for ( int x = -RenderChunkRadius; x <= RenderChunkRadius; x++ )
				{
					for ( int y = -RenderChunkRadius; y <= RenderChunkRadius; y++ )
					{
						for ( int z = -RenderChunkRadius; z <= RenderChunkRadius; z++ )
						{
							var chunkPosition = new Vector3Int(
								playerChunkPosition.x + x,
								playerChunkPosition.y + y,
								playerChunkPosition.z + z
							);
							targetChunks.Add( chunkPosition );
						}
					}
				}
			}
		}

		// Now we have a set of chunks to load, we can order them by distance to the closest player.
		var orderedChunks = Game.IsEditor ? targetChunks.Where( chunkPosition => !World.GetChunk( chunkPosition ).IsEmpty && !World.GetChunk( chunkPosition ).IsRendered ).ToList() : targetChunks
							.Where( chunkPosition => !World.GetChunk( chunkPosition ).IsEmpty && !World.GetChunk( chunkPosition ).IsRendered );
		if ( LoadAroundPlayer )
		{
			orderedChunks = orderedChunks
				.OrderBy( chunkPosition => players.Min( player => (player.WorldPosition / World.BlockScale).DistanceSquared( new Vector3( chunkPosition.x * Chunk.SIZE.x, chunkPosition.y * Chunk.SIZE.y, chunkPosition.z * Chunk.SIZE.z ) ) ) )
				.ToList();

		}

		// Load the chunks in batches of 10, or until we reach a certain time limit.
		float timeLimit = 0.1f; // 100ms per batch
		float startTime = Time.Now;
		foreach ( var pos in orderedChunks.Take( BatchSize ) )
		{
			if ( Time.Now - startTime > timeLimit )
			{
				Log.Warning( "Exceeded time limit for loading chunks, stopping batch processing." );
				break; // Stop loading if we exceed the time limit
			}
			var chunk = World.GetChunk( pos );
			//Log.Info( $"Creating chunk at {pos}. {chunk.Dirty} {chunk.IsRendered}" );
			World.GetChunk( pos ).Render( Scene, this );
		}

	}


	// A wrapper for the World.SetBlock method that broadcasts the change to all clients.
	[Rpc.Broadcast]
	public void PlaceBlock( Vector3Int position, BlockData blockData, bool playSound = true )
	{
		if ( playSound )
		{
			var snd = Sound.Play( blockData.GetBlock().PlaceSound );
			snd.Position = Helpers.VoxelToWorld( position ) + World.BlockScale / 2;
		}
		World.SetBlock( position, blockData );
	}

	[Rpc.Broadcast]
	public void BreakBlock( Vector3Int position, BlockData expectedData, bool dropItems = true, bool spawnParticles = true, bool playSound = true )
	{
		// Fill the block with SimpleParticles
		var data = World.GetBlock( position );
		var block = data.GetBlock();

		if ( playSound )
		{
			var snd = Sound.Play( block.BreakSound );
			snd.Position = Helpers.VoxelToWorld( position ) + World.BlockScale / 2;
		}

		if ( dropItems )
		{
			foreach ( var stack in block.GetDrops( World, position ) )
				stack.Clone().Spawn( (position + new Vector3(
					Random.Shared.Float(),
					Random.Shared.Float(),
					Random.Shared.Float()
				)) * World.BlockScale ); // Spawn the item at the center of the block
		}

		if ( spawnParticles ) SpawnBlockBreakParticles( position, expectedData );
		// Remove the block at the specified position.
		World.SetBlock( position, new BlockData( "voxelparty:air" ) ); // Assuming 0 is the ID for air.
	}

	public void SpawnBlockBreakParticles( Vector3Int position, BlockData data )
	{
		World.SpawnBreakParticles( position, data );
	}

	protected override void OnPreRender()
	{
		if ( vp_debug_showchunkborders == 1 )
		{
			foreach ( var i in World.SimulatedChunks )
			{
				var pos = Helpers.VoxelToWorld( i.Key );
				var size = Chunk.SIZE * World.BlockScale;
				var mins = pos - size / 2;
				var maxs = pos + size / 2;
				var bbox = new BBox( mins, maxs );
				Gizmo.Draw.LineBBox( bbox );
			}
		}
	}
}
