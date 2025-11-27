using System;

public struct BlockTraceResult
{
	public float Distance;
	public Vector3 EndPosition;
	public Vector3Int HitBlockPosition;
	public Direction HitFace;
	public bool Hit;

}

public class BlockTrace
{
	public Func<Vector3Int, bool> IgnoreFilter { get; set; } = _ => false;
	public Vector3 Start { get; set; }
	public Vector3 Dir { get; set; }
	public float MaxDistance { get; set; } = 1024f;
	public World World { get; set; }
	public bool Debug { get; set; } = false;

	public BlockTrace WithIgnoreFilter( Func<Vector3Int, bool> filter )
	{
		this.IgnoreFilter = filter;
		return this;
	}

	public BlockTrace WithStart( Vector3 start )
	{
		this.Start = start / World.BlockScale; // Convert to grid coordinates
		return this;
	}

	public BlockTrace WithDirection( Vector3 direction )
	{
		this.Dir = direction.Normal;
		return this;
	}

	public BlockTrace WithDistance( float distance )
	{
		this.MaxDistance = distance / World.BlockScale; // Convert to grid coordinates
		return this;
	}

	public BlockTrace WithWorld( World world )
	{
		this.World = world;
		return this;
	}

	public BlockTrace WithDebug( bool debug = true )
	{
		this.Debug = debug;
		return this;
	}

	public BlockTraceResult Run()
	{
		// We use the Error method
		// We step along the way by moving along the direction vector a partial amount such that we hit the next axis border
		// Because blocks can have non-standard AABB, we then run a raycast against the block at that position to see if we hit it (Unless it's IsSolidBlock)
		// Process the face, check if it matches the filter or if we've stepped past the max distance.

		Vector3 pos = Start;
		Vector3Int bPos = pos.Floor(); // Get the block position in grid coordinates
		float distanceTraveled = 0f;
		if ( Debug )
		{
			Gizmo.Draw.Color = Color.Red;
			Gizmo.Draw.SolidSphere( pos * World.BlockScale, 0.1f * World.BlockScale );
		}
		int steps = 0;
		bool hit = false;
		Direction hitFace = Direction.None;
		while ( distanceTraveled < MaxDistance && steps++ < 100 )
		{
			// Get the fractional component of the position, and compare it to the next axis border.
			(float stride, hitFace) = pos.Fractional().Components().Zip( Dir.Components() ).Select( ab =>
			{
				var a = ab.First;
				var b = ab.Second;
				if ( b == 0 ) return float.MaxValue; // Avoid division by zero
				if ( b < 0 ) // if b < 0, we need to stride until the next a = 0. So we take a / b
					return a == 0 ? 1f / -b : a / -b;
				// Otherwise, we need to stride until the next a = 1. So we take (1 - a) / b
				return (1f - a) / b;
			} ).Zip( new[]{
				Dir.x > 0 ? Direction.North : Dir.x < 0 ? Direction.South : Direction.None,
				Dir.y > 0 ? Direction.Left : Dir.y < 0 ? Direction.Right : Direction.None,
				Dir.z > 0 ? Direction.Up : Dir.z < 0 ? Direction.Down : Direction.None
			} ).MinBy( ab => ab.First );
			if ( stride == 0 || stride == float.MaxValue )
			{
				// If stride is 0, we're already at the next axis border, so we can skip this step.
				// If stride is float.MaxValue, we're not moving in any direction, so we can also skip this step.
				break;
			}
			// Move the position along the direction vector by the stride amount.
			pos += Dir * stride;
			distanceTraveled += stride;
			bPos += hitFace.Forward(); // Move the block position in the direction of the hit face.

			if ( Debug )
			{
				Gizmo.Draw.Color = Color.Blue;
				Gizmo.Draw.SolidSphere( pos * World.BlockScale, 0.02f * World.BlockScale );
			}

			// Determine the block position and the face hit depending on our current pos
			// if any dir component is negative, we need to adjust the block position to the next block in that direction.
			if ( Debug )
			{
				Gizmo.Draw.Color = Color.Orange;
				Gizmo.Draw.LineBBox(
					BBox.FromPoints( [bPos * World.BlockScale, (bPos + Vector3.One) * World.BlockScale] )
				);
				Gizmo.Draw.Color = Color.Green;
				Gizmo.Draw.Arrow( pos * World.BlockScale, (pos + hitFace.Forward() * 0.5f) * World.BlockScale, 0.1f * World.BlockScale, 0.01f * World.BlockScale );
				Gizmo.Draw.Color = Color.Blue;
				Gizmo.Draw.Line( pos * World.BlockScale, (bPos + Vector3.One * 0.5f) * World.BlockScale );
			}

			// Determine which of our current axis fractions is closest to zero, and use that to determine the face hit.
			if ( !IgnoreFilter( bPos ) )
			{

				var block = World.GetBlock( bPos ).GetBlock();
				if ( !block.IsFullBlock )
				{
					foreach ( var box in block.GetCollisionAABBBlock( World, bPos ) )
					{
						// We introduce a tiny amount of backtracking incase we trace against the wall of the block.
						if ( box.Trace( new Ray( pos - Dir * 0.01f, Dir ), (MaxDistance - distanceTraveled) + 0.01f, out float dist ) )
						{
							dist -= 0.01f;
							var tracedPos = pos + Dir * dist;
							// We can determine the face by checking which of the bounds closest matches an axis of tracedPos
							var tracedFace = new[] {
								(MathF.Abs(box.Mins.x - tracedPos.x), Direction.South), // -x = South
								(MathF.Abs(box.Maxs.x - tracedPos.x), Direction.North), // +x = North
								(MathF.Abs(box.Mins.y - tracedPos.y), Direction.East), // -y = East
								(MathF.Abs(box.Maxs.y - tracedPos.y), Direction.West), // +y = West
								(MathF.Abs(box.Mins.z - tracedPos.z), Direction.Down), // -z = Down
								(MathF.Abs(box.Maxs.z - tracedPos.z), Direction.Up), // +z = Up
							}.MinBy( x => x.Item1 ).Item2;

							hit = true;
							pos = tracedPos;
							hitFace = tracedFace.Flip();
							distanceTraveled += dist;
							break;
						}
					}
					if ( hit ) break;

				}
				else
				{
					hit = true; // We hit a block that is not ignored.
					break;
				}

			}
		}
		// if any dir component is negative, we need to adjust the block position to the next block in that direction.
		var frac = (pos + 0.5f).Fractional() - 0.5f;
		if ( Debug )
		{
			Gizmo.Draw.Color = Color.Yellow;
			Gizmo.Draw.SolidSphere( bPos * World.BlockScale, 0.1f * World.BlockScale );
			Gizmo.Draw.Color = Color.Orange;
			Gizmo.Draw.Line( pos * World.BlockScale, Start * World.BlockScale );
		}
		return new BlockTraceResult()
		{
			Distance = distanceTraveled,
			EndPosition = pos * World.BlockScale, // Convert back to world coordinates
			HitBlockPosition = bPos,
			HitFace = hitFace.Flip(),
			Hit = hit
		};
	}

}

public class BlockSpace
{
	// SimulatedChunks refer to chunks that have been loaded into memory, and thus can be interacted with.
	public Dictionary<Vector3Int, Chunk> SimulatedChunks { get; private set; } = new();

	public Chunk GetChunk( Vector3Int position )
	{
		if ( SimulatedChunks.TryGetValue( position, out var chunk ) )
			return chunk;
		if ( !LoadOrMakeChunk( position, out var c ) )
			throw new System.Exception( $"Failed to load or create chunk at {position}." );
		return c;
	}

	public Chunk GetChunkFromBlockPos( Vector3Int position )
	{
		var chunkPosition = new Vector3Int(
			(position.x + 15 * (position.x >> 31)) / 16,
			(position.y + 15 * (position.y >> 31)) / 16,
			(position.z + 15 * (position.z >> 31)) / 16
		);
		return GetChunk( chunkPosition );
	}

	public Chunk GetChunkIfExists( Vector3Int position )
	{
		if ( SimulatedChunks.TryGetValue( position, out var chunk ) )
			return chunk;
		return null; // Return null if the chunk does not exist
	}

	public BlockTrace Trace( Vector3 Start, Vector3 End )
	{
		return new BlockTrace()
			.WithStart( Start )
			.WithDirection( (End - Start).Normal )
			.WithDistance( (End - Start).Length )
			.WithWorld( this as World ) // Cast to World to access the World-specific methods
			.WithIgnoreFilter( pos => !GetBlock( pos ).GetBlock().IsSolid ); // Ignore air blocks
	}

	private bool LoadOrMakeChunk( Vector3Int position, out Chunk chunk )
	{
		if ( SimulatedChunks.TryGetValue( position, out chunk ) )
			return true; // Chunk already exists

		// For now, create a blank chunk.
		chunk = new Chunk( this, position );
		return SimulatedChunks.TryAdd( position, chunk );
	}
	// Get the block in World coordinates.
	public BlockData GetBlock( Vector3Int position )
	{
		var chunkPosition = new Vector3Int(
			(position.x + 15 * (position.x >> 31)) / 16,
			(position.y + 15 * (position.y >> 31)) / 16,
			(position.z + 15 * (position.z >> 31)) / 16
		);
		var chunk = GetChunk( chunkPosition );
		var blockPosition = new Vector3Int(
			((position.x % Chunk.SIZE.x) + Chunk.SIZE.x) % Chunk.SIZE.x,
			((position.y % Chunk.SIZE.y) + Chunk.SIZE.y) % Chunk.SIZE.y,
			((position.z % Chunk.SIZE.z) + Chunk.SIZE.z) % Chunk.SIZE.z
		);
		return chunk.GetBlock( blockPosition.x, blockPosition.y, blockPosition.z );
	}


	// Get the BlockObject at a given position
	public GameObject GetBlockObject( Vector3Int position )
	{
		var chunkPosition = new Vector3Int(
			(position.x + 15 * (position.x >> 31)) / 16,
			(position.y + 15 * (position.y >> 31)) / 16,
			(position.z + 15 * (position.z >> 31)) / 16
		);
		var chunk = GetChunkIfExists( chunkPosition );
		if ( chunk == null || chunk.ChunkObject == null || !chunk.ChunkObject.IsValid )
			return null;
		var blockPosition = new Vector3Int(
			((position.x % Chunk.SIZE.x) + Chunk.SIZE.x) % Chunk.SIZE.x,
			((position.y % Chunk.SIZE.y) + Chunk.SIZE.y) % Chunk.SIZE.y,
			((position.z % Chunk.SIZE.z) + Chunk.SIZE.z) % Chunk.SIZE.z
		);
		if ( !chunk.ChunkObject.BlockObjects.ContainsKey( blockPosition ) )
			return null;
		return chunk.ChunkObject.BlockObjects[blockPosition].obj;
	}

	public void SetBlock( Vector3Int position, BlockData blockData )
	{
		var chunkPosition = new Vector3Int(
			(position.x + 15 * (position.x >> 31)) / 16,
			(position.y + 15 * (position.y >> 31)) / 16,
			(position.z + 15 * (position.z >> 31)) / 16
		);
		var chunk = GetChunk( chunkPosition );
		var blockPosition = new Vector3Int(
			((position.x % Chunk.SIZE.x) + Chunk.SIZE.x) % Chunk.SIZE.x,
			((position.y % Chunk.SIZE.y) + Chunk.SIZE.y) % Chunk.SIZE.y,
			((position.z % Chunk.SIZE.z) + Chunk.SIZE.z) % Chunk.SIZE.z
		);
		chunk.SetBlock( blockPosition.x, blockPosition.y, blockPosition.z, blockData );
	}

	public void SpawnBreakParticles( Vector3Int position, BlockData data )
	{
		for ( int i = 0; i < 32; i++ )
		{
			var particlePos = (position + new Vector3(
				Random.Shared.Float(),
				Random.Shared.Float(),
				Random.Shared.Float()
			)) * World.BlockScale;
			var particle = new GameObject().AddComponent<SimpleParticle>();
			particle.GameObject.Flags |= GameObjectFlags.Hidden;
			particle.WorldPosition = particlePos;
			particle.Material = Material.Load( "materials/textureatlas.vmat" );
			particle.TextureRect = new Rect( 0, 0, 1, 1 ); // Assuming a single texture for simplicity.
														   // Velocity is distance to the center of the block, scaled by a random factor.
			particle.Velocity = (particlePos - (position + Vector3.One * 0.5f) * World.BlockScale) * new Vector3( 1f, 1f, 2f ) * Random.Shared.Float( 1f, 3f );
			particle.Acceleration = Vector3.Down * 9.81f * 42f; // Gravity effect.
			particle.Damping = 0.99f; // Damping to slow down the particles.
			particle.Lifetime = Random.Shared.Float( 3f, 5f ); // Random lifetime for the particles.
			Vector2 texCoord = new Vector2( Random.Shared.Float() * 14 / 16f, Random.Shared.Float() * 14 / 16f );
			particle.TextureRect = Rect.FromPoints( texCoord, texCoord + new Vector2( 2 / 16f, 2 / 16f ) );
			particle.ItemID = data.BlockID;
			particle.Scale = new Curve( new List<Curve.Frame>
			{
				new Curve.Frame(0f, 30f, -MathF.PI, MathF.PI),
				new Curve.Frame(1f, 0f, 0f, 0f)
			} );
		}

		/*var particle = item.Block.BreakParticle.IsValid() 
				? item.Block.BreakParticle 
				: ResourceLibrary.Get<PrefabFile>( "prefabs/break particles.prefab" );
			var obj = GameObject.Clone( particle,
				new CloneConfig(Transform.Zero.WithPosition(Helpers.VoxelToWorld( position ) + World.BlockScale / 2)) );

			obj.Components.Get<SampleTextureEffect>().SampleTexture = item.Block.Texture;
			obj.NetworkSpawn();*/
	}

	public string SerializeRegionByID( Vector3Int start, Vector3Int end )
	{
		int xMin = Math.Min( start.x, end.x );
		int xMax = Math.Max( start.x, end.x );
		int yMin = Math.Min( start.y, end.y );
		int yMax = Math.Max( start.y, end.y );
		int zMin = Math.Min( start.z, end.z );
		int zMax = Math.Max( start.z, end.z );
		Dictionary<(string id, byte data), ushort> uniqueBlockMap = new();
		for ( int z = zMin; z <= zMax; z++ )
		{
			for ( int y = yMin; y <= yMax; y++ )
			{
				for ( int x = xMin; x <= xMax; x++ )
				{
					var blockData = GetBlock( new Vector3Int( x, y, z ) );
					string blockID = blockData.IsEmpty() ? "voxelparty:air" : blockData.BlockID;
					var key = (blockID, blockData.BlockDataValue);
					if ( !uniqueBlockMap.ContainsKey( key ) )
					{
						uniqueBlockMap[key] = (ushort)uniqueBlockMap.Count;
					}
				}
			}
		}
		List<byte> data = new();
		// Add the size of the region to the data.
		data.AddRange( BitConverter.GetBytes( xMax - xMin + 1 ) );
		data.AddRange( BitConverter.GetBytes( yMax - yMin + 1 ) );
		data.AddRange( BitConverter.GetBytes( zMax - zMin + 1 ) );
		// Add the size of the dictionary to the data.
		ushort uniqueCount = (ushort)uniqueBlockMap.Count;
		data.AddRange( BitConverter.GetBytes( uniqueCount ) );
		// Add each unique block's identifier and data value.
		foreach ( var (key, index) in uniqueBlockMap )
		{
			data.Add( (byte)key.id.Length );
			data.AddRange( System.Text.Encoding.UTF8.GetBytes( key.id ) );
			data.Add( key.data );
		}
		List<byte> blocks = new();
		for ( int z = zMin; z <= zMax; z++ )
		{
			for ( int y = yMin; y <= yMax; y++ )
			{
				for ( int x = xMin; x <= xMax; x++ )
				{
					var blockData = GetBlock( new Vector3Int( x, y, z ) );
					string blockID = blockData.IsEmpty() ? "voxelparty:air" : blockData.BlockID;
					var key = (blockID, blockData.BlockDataValue);
					ushort index = uniqueBlockMap[key];
					blocks.Add( (byte)index );
				}
			}
		}
		data.AddRange( blocks.RunLengthEncodeBy( 1 ) );
		return Convert.ToBase64String( data.ToArray() );
	}

	public string SerializeBlockData(BlockData[,,] data )
    {
		var bytes = new List<byte>();
		int xSize = data.GetLength( 0 );
		int ySize = data.GetLength( 1 );
		int zSize = data.GetLength( 2 );
		bytes.AddRange( BitConverter.GetBytes( xSize ) );
		bytes.AddRange( BitConverter.GetBytes( ySize ) );
		bytes.AddRange( BitConverter.GetBytes( zSize ) );
		ushort uniqueCount = 0;
		Dictionary<(string id, byte data), ushort> uniqueBlockMap = new();
		for ( int z = 0; z < zSize; z++ )
        {
			for ( int y = 0; y < ySize; y++ )
            {
				for ( int x = 0; x < xSize; x++ )
                {
					var blockData = data[x, y, z];
					string blockID = blockData.IsEmpty() ? "voxelparty:air" : blockData.BlockID;
					var key = (blockID, blockData.BlockDataValue);
					if ( !uniqueBlockMap.ContainsKey( key ) )
					{
						uniqueBlockMap[key] = uniqueCount++;
					}
                }
			}
		}
		// Add the size of the dictionary to the data.
		bytes.AddRange( BitConverter.GetBytes( uniqueCount ) );
		// Add each unique block's identifier and data value.
		foreach ( var (key, index) in uniqueBlockMap )
		{
			bytes.Add( (byte)key.id.Length );
			bytes.AddRange( System.Text.Encoding.UTF8.GetBytes( key.id ) );
			bytes.Add( key.data );
		}
		List<byte> blocks = new();
		for ( int z = 0; z < zSize; z++ )
        {
            for ( int y = 0; y < ySize; y++ )
            {
                for ( int x = 0; x < xSize; x++ )
                {
					var blockData = data[x, y, z];
					string blockID = blockData.IsEmpty() ? "voxelparty:air" : blockData.BlockID;
					var key = (blockID, blockData.BlockDataValue);
					ushort index = uniqueBlockMap[key];
					blocks.Add( (byte)index );
                }
			}
		}
		bytes.AddRange( blocks.RunLengthEncodeBy( 1 ) );
		return Convert.ToBase64String( bytes.ToArray() );
	}

	public Vector3Int GetStructureBounds( string structureData )
	{
		var bytes = Convert.FromBase64String( structureData );
		if ( bytes.Length < 12 )
		{
			Log.Error( "Invalid structure data." );
			return new Vector3Int( 0, 0, 0 );
		}
		int xSize = BitConverter.ToInt32( bytes, 0 );
		int ySize = BitConverter.ToInt32( bytes, 4 );
		int zSize = BitConverter.ToInt32( bytes, 8 );
		return new Vector3Int( xSize, ySize, zSize );
	}

	public BlockData[,,] GetStructureDataByID( string structureData )
    {
        var bytes = Convert.FromBase64String( structureData );
		if ( bytes.Length < 12 )
		{
			return null;
		}
		var binReader = new System.IO.BinaryReader( new System.IO.MemoryStream( bytes ) );
		int xSize = binReader.ReadInt32();
		int ySize = binReader.ReadInt32();
		int zSize = binReader.ReadInt32();
		ushort uniqueCount = binReader.ReadUInt16();
		Dictionary<ushort, (string id, byte data)> uniqueBlockMap = new();
		for ( ushort i = 0; i < uniqueCount; i++ )
        {
			byte idLength = binReader.ReadByte();
			string id = System.Text.Encoding.UTF8.GetString( binReader.ReadBytes( idLength ) );
			byte data = binReader.ReadByte();
			uniqueBlockMap[i] = (id, data);
        }
		var blockData = binReader.ReadBytes( (int)(binReader.BaseStream.Length - binReader.BaseStream.Position) ).RunLengthDecodeBy( 1 ).ToList();
		if ( blockData.Count != xSize * ySize * zSize )
		{
			return null;
		}
		BlockData[,,] structure = new BlockData[xSize, ySize, zSize];
		for ( int z = 0; z < zSize; z++ )
		{
			for ( int y = 0; y < ySize; y++ )
			{
				for ( int x = 0; x < xSize; x++ )
				{
					int index = (z * ySize * xSize + y * xSize + x);
					ushort blockIndex = blockData[index];
					var (blockID, blockDataValue) = uniqueBlockMap[blockIndex];
					structure[x, y, z] = new BlockData( blockID, blockDataValue );
				}
			}
		}
		return structure;
	}

	public BlockData[,,] LoadStructureByID( Vector3Int location, string structureData )
	{
		var bytes = Convert.FromBase64String( structureData );
		if ( bytes.Length < 12 )
		{
			Log.Error( "Invalid structure data." );
			return null;
		}
		var binReader = new System.IO.BinaryReader( new System.IO.MemoryStream( bytes ) );
		int xSize = binReader.ReadInt32();
		int ySize = binReader.ReadInt32();
		int zSize = binReader.ReadInt32();
		ushort uniqueCount = binReader.ReadUInt16();
		Dictionary<ushort, (string id, byte data)> uniqueBlockMap = new();
		for ( ushort i = 0; i < uniqueCount; i++ )
		{
			byte idLength = binReader.ReadByte();
			string id = System.Text.Encoding.UTF8.GetString( binReader.ReadBytes( idLength ) );
			byte data = binReader.ReadByte();
			uniqueBlockMap[i] = (id, data);
		}
		var blockData = binReader.ReadBytes( (int)(binReader.BaseStream.Length - binReader.BaseStream.Position) ).RunLengthDecodeBy( 1 ).ToList();
		if ( blockData.Count != xSize * ySize * zSize )
		{
			Log.Error( $"Invalid structure data length: {blockData.Count}. Expected {xSize * ySize * zSize}." );
			return null;
		}

		for ( int z = 0; z < zSize; z++ )
		{
			for ( int y = 0; y < ySize; y++ )
			{
				for ( int x = 0; x < xSize; x++ )
				{
					int index = (z * ySize * xSize + y * xSize + x);
					ushort blockIndex = blockData[index];
					var (blockID, blockDataValue) = uniqueBlockMap[blockIndex];
					if ( ItemRegistry.GetBlockByIdentifier( blockID )?.StructureSoft ?? true )
					{
						if ( !GetBlock( location + new Vector3Int( x, y, z ) ).GetBlock().Replaceable )
							continue;
					}
					SetBlock( location + new Vector3Int( x, y, z ), new BlockData( blockID, blockDataValue ) );
				}
			}
		}

		return BlockData.GetAreaInBox( location, new Vector3Int( xSize, ySize, zSize ) );
	}

	public IEnumerable<byte> SerializeByID()
    {
		var data = new List<byte>();
		foreach ( var chunk in SimulatedChunks.Values )
        {
			if ( chunk.IsEmpty )
				continue;
			data.AddRange( BitConverter.GetBytes( chunk.Position.x ) );
			data.AddRange( BitConverter.GetBytes( chunk.Position.y ) );
			data.AddRange( BitConverter.GetBytes( chunk.Position.z ) );
			var chunkData = chunk.SerializeByID().ToList();
			data.AddRange( BitConverter.GetBytes( chunkData.Count ) );
			data.AddRange( chunkData );
		}
		return data;
	}
	
	public void DeserializeByID( IEnumerable<byte> data )
	{
		var dataList = data.ToList();
		int index = 0;
		while ( index < dataList.Count )
		{
			int x = BitConverter.ToInt32( dataList.ToArray(), index );
			index += 4;
			int y = BitConverter.ToInt32( dataList.ToArray(), index );
			index += 4;
			int z = BitConverter.ToInt32( dataList.ToArray(), index );
			index += 4;
			var chunkPosition = new Vector3Int( x, y, z );

			int chunkDataLength = BitConverter.ToInt32( dataList.ToArray(), index );
			index += 4;

			var chunkData = dataList.Skip( index ).Take( chunkDataLength ).ToList();
			index += chunkDataLength;

			var chunk = new Chunk( this, chunkPosition );
			chunk.DeserializeByID( chunkData );
			SimulatedChunks[chunkPosition] = chunk;
		}
	}
}
