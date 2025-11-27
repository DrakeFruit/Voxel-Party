using System;
using Sandbox;

public struct BlockData
{
	public string BlockID = "voxelparty:air";
	public byte BlockDataValue = 0;

	public static BlockData Empty = new BlockData( "voxelparty:air", 0 );

	public BlockData( string blockID, byte blockDataValue )
	{
		BlockID = blockID;
		BlockDataValue = blockDataValue;
	}

	public static BlockData[,,] GetAreaInBox( Vector3Int position, Vector3Int size )
	{
		if ( size.x <= 0 || size.y <= 0 || size.z <= 0 )
		{
			throw new System.ArgumentException( "Size must be greater than zero in all dimensions." );
		}
		BlockData[,,] area = new BlockData[size.x, size.y, size.z];
		for ( var x = 0; x < size.x; x++ )
		{
			for ( var y = 0; y < size.y; y++ )
			{
				for ( var z = 0; z < size.z; z++ )
				{
					area[x, y, z] = World.Active.GetBlock( position + new Vector3Int( x, y, z ) );
				}
			}
		}

		return area;
	}

	public Direction FacingFromData()
	{
		return (Direction)this.BlockDataValue;
	}

	public BlockData( string blockID ) : this( blockID, 0 ) { }

	public Block GetBlock()
	{
		return ItemRegistry.GetBlockByIdentifier( BlockID ) ?? ItemRegistry.GetBlockByIdentifier("voxelparty:air");
	}

	public static BlockData WithPlacementBlockData( string blockID, Direction placedFace, Vector3 cameraForward )
	{
		var block = ItemRegistry.GetBlockByIdentifier( blockID );
		if ( block == null )
			return BlockData.Empty;
		if ( block.Rotateable )
			return new BlockData( blockID, (byte)block.BestDirectionFrom( placedFace, cameraForward ) );
		return new BlockData( blockID );
	}

	// Equality operator to compare two BlockData instances
	public static bool operator ==( BlockData left, BlockData right )
	{
		return left.BlockID == right.BlockID && left.BlockDataValue == right.BlockDataValue;
	}
	public static bool operator !=( BlockData left, BlockData right )
	{
		return left.BlockID != right.BlockID || left.BlockDataValue != right.BlockDataValue;
	}

	public override bool Equals( object obj )
	{
		if ( obj is BlockData other )
		{
			return this == other;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(BlockID, BlockDataValue);
	}

	public bool IsEmpty()
	{
		return string.IsNullOrWhiteSpace(BlockID) || BlockID == "voxelparty:air";
	}
}
