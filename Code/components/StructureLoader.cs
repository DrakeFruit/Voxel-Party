using System.Threading.Tasks;
using Sandbox.theoretical;

namespace Sandbox;

public partial class StructureLoader : Component, Component.ExecuteInEditor
{
	[Property, Alias( "Structure" )] public Structure LoadedStructure { get; set; }
	public BlockData BlockData { get; set; }

	public Vector3Int StructureSize => LoadedStructure?.StructureDataByID != null
		? World.Active.GetStructureBounds( LoadedStructure.StructureDataByID )
		: Vector3Int.Zero;


	[Button]
	public (BlockData[,,] data, Vector3Int pos) StampStructure()
	{
		if ( LoadedStructure == null )
		{
			Log.Warning( "No structure loaded to stamp." );
			return (null, Vector3Int.Zero);
		}
		if ( !LoadedStructure.IsValid() )
		{
			Log.Warning( "Loaded structure is not valid." );
			return (null, Vector3Int.Zero);
		}

		if ( Game.IsPlaying )
		{
			Log.Error( "Stamping structures is only supported in the editor." );
			return (null, Vector3Int.Zero);
		}
		var worldPosition = Helpers.WorldToVoxel( WorldPosition );
		var oldBlocks = BlockData.GetAreaInBox( worldPosition, World.Active.GetStructureBounds( LoadedStructure.StructureDataByID ) );
		World.Active.LoadStructureByID( worldPosition, LoadedStructure.StructureDataByID );
		GameObject.Destroy();
		return (oldBlocks, worldPosition);
	}

	bool Retry = false;

	[Button]
	public async Task Regenerate()
	{
		if ( !GameObject.Active )
			return;
		if ( !ItemRegistry.FinishedLoading )
		{
			Retry = true;
			return;
		}
		if ( Scene is null )
		{
			Retry = true;
			return;
		}
		var KnownMat = Scene.GetAll<WorldThinker>().FirstOrDefault()?.TextureAtlas;
		var TranslucentMat = Scene.GetAll<WorldThinker>().FirstOrDefault()?.TranslucentTextureAtlas;
		foreach ( var child in GameObject.Children.ToList() )
		{
			child.Destroy();
		}
		if ( Game.IsPlaying )
		{
			if ( !Networking.IsHost )
				return; // Let the host handle this part.
			World.Active.LoadStructureByID( Helpers.WorldToVoxel( WorldPosition ), LoadedStructure.StructureDataByID );
			GameObject.Destroy();
			return;
		}

		var oldWorld = World.Active;
		var tempThinker = AddComponent<WorldThinker>();
		tempThinker.TextureAtlas = KnownMat;
		tempThinker.LoadAroundPlayer = false;
		tempThinker.TranslucentTextureAtlas = TranslucentMat;
		try
		{
			World.Active = tempThinker.World;
			World.Active.LoadStructureByID( Vector3Int.Zero, LoadedStructure.StructureDataByID );
			// Force load all chunks in the radius around the structure
			var bounds = World.Active.GetStructureBounds( LoadedStructure.StructureDataByID );
			var chunkMax = (bounds / 16f).Ceil();
			for ( int z = 0; z <= chunkMax.z; z++ )
			{
				for ( int y = 0; y <= chunkMax.y; y++ )
				{
					for ( int x = 0; x <= chunkMax.x; x++ )
					{
						var chunkPos = new Vector3Int( x, y, z );
						var chunk = World.Active.GetChunk( chunkPos );
						var chunkObj = chunk.Render( Scene, tempThinker );
						if ( chunkObj == null ) continue; // Skip if chunk is empty
						chunkObj.WorldThinkerInstanceOverride = tempThinker;
						await chunkObj.UpdateMesh();
						chunkObj.Destroy();
					}
				}
			}
		}
		finally
		{
			tempThinker.Destroy(); // Clean up the temporary thinker.
			World.Active = oldWorld; // Restore the active world after loading.
		}
	}

	protected override void OnEnabled()
	{
		_ = Regenerate();
	}

	protected override void OnValidate()
	{
		base.OnValidate();
		_ = Regenerate();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		if ( Game.IsPlaying ) return;
		foreach ( var child in GameObject.Children.ToList() )
		{
			child.Destroy();
		}
	}

	protected override void OnUpdate()
	{
		if ( Retry )
		{
			Retry = false;
			_ = Regenerate();
		}
		base.OnUpdate();
		var blockPosition = Helpers.WorldToVoxel( WorldPosition ) * World.BlockScale;
		// Move all chunks to the world position of this component
		foreach ( var child in GameObject.Children )
		{
			// Get the coordinates from the name in the form of "Chunk (x, y, z)"
			// TODO: THIS IS A DIRTY HACK, but it works for now.
			if ( child.Name.StartsWith( "Chunk (" ) )
			{
				var coords = child.Name.Substring( 7, child.Name.Length - 8 ).Split( ", " );
				if ( coords.Length == 3 && int.TryParse( coords[0], out int x ) && int.TryParse( coords[1], out int y ) && int.TryParse( coords[2], out int z ) )
				{
					child.WorldPosition = blockPosition + new Vector3( x * Chunk.SIZE.x, y * Chunk.SIZE.y, z * Chunk.SIZE.z ) * World.BlockScale;
				}
			}
		}
	}

	protected override void DrawGizmos()
	{
		if ( !Game.IsEditor ) return;

		//World.Active.LoadStructure( Helpers.WorldToVoxel(WorldPosition), LoadedStructure.StructureData );
		//Gizmo.Draw.SolidBox( LoadedStructure.StructureData );
	}
}
