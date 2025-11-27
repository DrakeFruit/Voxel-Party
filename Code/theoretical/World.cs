using System;
using Sandbox;

public class World : BlockSpace
{
	public static readonly int BlockScale = 40; // How many units per block.
	public WorldThinker Thinker => Game.ActiveScene.Get<WorldThinker>();

	public static World Active = null;

	public World()
	{
		Active = this; // Set the active world to this instance.
	}

	public void MakeSpawnPlatform()
	{
		// Create a simple dirt platform at the origin.
		for ( int z = -1; z <= 1; z++ )
		{
			for ( int y = -1; y <= 4; y++ )
			{
				for ( int x = -1; x <= 4; x++ )
				{
					if ( x > 1 && y > 1 ) continue;
					var pos = new Vector3Int( x, y, z );
					SetBlock( pos, new BlockData( z < 1
							? "voxelparty:dirt"
							: "voxelparty:grass" ) );
				}
			}
		}
	}
}
