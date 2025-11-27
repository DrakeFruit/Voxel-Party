using System;
using System.IO;

public class Chunk
{
	public static readonly Vector3Int SIZE = new Vector3Int( 16, 16, 16 );

	public Vector3Int Position;

	private BlockData[,,] blocks = new BlockData[SIZE.x, SIZE.y, SIZE.z];
	public bool IsEmpty = true; // Flag to indicate if the chunk is empty. This should be set to false when any non-air is added.

	public ChunkObject ChunkObject { get; set; } = null;

	public bool IsRendered => ChunkObject != null && ChunkObject.IsValid();

	public int X => Position.x;
	public int Y => Position.y;
	public int Z => Position.z;
	public bool RenderDirty = true;
	public bool NetworkDirty = true;
	public BlockSpace world;

	public Chunk( BlockSpace world, Vector3Int position )
	{
		Position = position;
		this.world = world;
	}

	public override string ToString()
	{
		return $"Chunk({X}, {Y}, {Z})";
	}

	public BlockData GetBlock( int x, int y, int z )
	{
		// Ensure the coordinates are within the chunk's bounds
		if ( x < 0 || x >= SIZE.x || y < 0 || y >= SIZE.y || z < 0 || z >= SIZE.z )
		{
			throw new System.ArgumentOutOfRangeException( $"Coordinates ({x}, {y}, {z}) are out of bounds for chunk at ({X}, {Y}, {Z})." );
		}
		return blocks[x, y, z];
	}

	public void MarkDirty()
	{
		RenderDirty = true; // Mark the chunk as dirty to indicate it needs to be updated/rendered.
		NetworkDirty = true;
	}

	public void SetBlock( int x, int y, int z, BlockData blockData )
	{
		// Ensure the coordinates are within the chunk's bounds
		if ( x < 0 || x >= SIZE.x || y < 0 || y >= SIZE.y || z < 0 || z >= SIZE.z )
		{
			throw new System.ArgumentOutOfRangeException( $"Coordinates ({x}, {y}, {z}) are out of bounds for chunk at ({X}, {Y}, {Z})." );
		}
		if ( !blockData.IsEmpty() )
		{
			IsEmpty = false; // If we set a non-air block, the chunk is no longer empty.
		}
		if ( blocks[x, y, z] == blockData ) // If we don't actually change, don't update our neighbours at all.
			return;
		blocks[x, y, z] = blockData;
		MarkDirty(); // Mark the chunk as dirty to indicate it needs to be updated/rendered.
					 // If we're on a chunk border (x y or z is 0 or SIZE-1), we might need to update neighboring chunks.
		var pos = Position * Chunk.SIZE + new Vector3Int( x, y, z );
		foreach ( var dir in Directions.All )
			world.GetBlock( pos + dir.Forward() ).GetBlock().OnNeighbourUpdated( world, pos + dir.Forward(), pos );

	}

	public ChunkObject Render( Scene scene, WorldThinker thinker = null )
	{
		// Create a chunk object and then ask it to update.
		if ( IsEmpty ) return null; // Never render empty chunks.
		using ( scene.Push() )
		{
			var obj = new GameObject( true, $"Chunk ({Position.x}, {Position.y}, {Position.z})" );
			obj.Parent = (thinker ?? scene.Get<WorldThinker>()).GameObject;
			var chunkObj = obj.AddComponent<ChunkObject>();
			chunkObj.WorldThinkerInstanceOverride = thinker; // Set the thinker to use for this chunk.
			chunkObj.ChunkPosition = Position; // Set the chunk position in world coordinates.
			obj.WorldPosition = Helpers.VoxelToWorld( Position * SIZE );
			ChunkObject = chunkObj;
			obj.NetworkSpawn();
			obj.Network.AssignOwnership( Connection.Host );
			return chunkObj;
		}
	}

	public IEnumerable<byte> SerializeByID()
    {
		// Iterate through the chunk to determine the unique block IDs present.
		List<(string id, byte data)> uniqueBlocks = new();
		for (int z = 0; z < SIZE.z; z++)
		{
			for (int y = 0; y < SIZE.y; y++)
			{
				for (int x = 0; x < SIZE.x; x++)
				{
					var blockData = GetBlock(x, y, z);
					string blockID = blockData.IsEmpty() ? "voxelparty:air" : blockData.BlockID;
					if (!uniqueBlocks.Any(b => b.id == blockID && b.data == blockData.BlockDataValue))
					{
						uniqueBlocks.Add((blockID, blockData.BlockDataValue));
					}
				}
			}
		}
		var data = new List<byte>();
		// Push the short count of unique blocks.
		ushort uniqueCount = (ushort)uniqueBlocks.Count;
		data.AddRange( BitConverter.GetBytes( uniqueCount ) );
		// Push each unique block's identifier and data value.
		foreach (var (id, blockDataValue) in uniqueBlocks ) {
            data.Add((byte)id.Length);
			data.AddRange(System.Text.Encoding.UTF8.GetBytes(id));
			data.Add(blockDataValue);
        }
		// Now serialize the chunk using the indices of the unique blocks.
		List<byte> chunkData = new();
		for (int z = 0; z < SIZE.z; z++)
        {
			for (int y = 0; y < SIZE.y; y++)
            {
				for (int x = 0; x < SIZE.x; x++)
                {
					var blockData = GetBlock(x, y, z);
					string blockID = blockData.IsEmpty() ? "voxelparty:air" : blockData.BlockID;
					// Find the index of this block in the unique blocks list.
					int index = uniqueBlocks.FindIndex(b => b.id == blockID && b.data == blockData.BlockDataValue);
					chunkData.Add((byte)index);
                }
			}
		}
		data.AddRange(chunkData.RunLengthEncodeBy(1));
		return data;
    }

	public void DeserializeByID( IEnumerable<byte> data )
	{
		var reader = new BinaryReader( new MemoryStream( data.ToArray() ) );
		// Read the unique block count.
		ushort uniqueCount = reader.ReadUInt16();
		var uniqueBlocks = new List<(string id, byte data)>();
		// Read each unique block's identifier and data value.
		for ( int i = 0; i < uniqueCount; i++ )
		{
			byte idLength = reader.ReadByte();
			var idBytes = reader.ReadBytes( idLength );
			string id = System.Text.Encoding.UTF8.GetString( idBytes );
			byte blockDataValue = reader.ReadByte();
			uniqueBlocks.Add( (id, blockDataValue) );
		}
		// Read out the rest of the data into a new list.
		var compressedData = reader.ReadBytes( (int)(reader.BaseStream.Length - reader.BaseStream.Position) );
		var dataList = compressedData.RunLengthDecodeBy( 1 ).ToList();
		if ( dataList.Count != SIZE.x * SIZE.y * SIZE.z )
		{
			Log.Warning( $"Invalid chunk data length: {dataList.Count}. Expected {SIZE.x * SIZE.y * SIZE.z}." );
			return;
		}

		for ( int z = 0; z < SIZE.z; z++ )
		{
			for ( int y = 0; y < SIZE.y; y++ )
			{
				for ( int x = 0; x < SIZE.x; x++ )
				{
					int index = (z * SIZE.y * SIZE.x + y * SIZE.x + x);
					var uniqueIndex = dataList[index];
					if ( uniqueIndex < 0 || uniqueIndex >= uniqueBlocks.Count )
					{
						Log.Warning( $"Invalid unique block index: {uniqueIndex}. Expected between 0 and {uniqueBlocks.Count - 1}." );
						continue;
					}
					var (blockID, blockDataValue) = uniqueBlocks[uniqueIndex];
					blocks[x, y, z] = new BlockData( blockID, blockDataValue );
					if ( !blocks[x,y,z].IsEmpty() )
						IsEmpty = false;
				}
			}
		}
	}
}
