using System.ComponentModel;
using HalfEdgeMesh;
using Sandbox;
using Sandbox.UI;

/** An instance of Block describes the behaviour and properties of a block in the world.
 * All eg. Stone, refer to the same Block instance which is mapped via BlockData.BlockID.
 */
public class Block
{
	public enum PlacementOption
	{
		StableAnywhere,
		OnSolid,
		AgaintsSolid
	}

	[Description( "Is this block solid, meaning it blocks movement?" )]
	public bool IsSolid { get; set; } = true; // Whether the block is solid (blocks movement).
	public int Hardness { get; set; } = 1; // Hardness of the block, used for mining speed calculations.

	[Description( "If true, don't replace other blocks with this when placing a structure" )]
	public bool StructureSoft { get; set; } = false;

	[DisplayName( "Is Full Block" )]
	[Description( "Is this block considered a full solid block, meaning it occupies the entire space of a block?" )]
	public bool IsFullBlock { get; set; } = true; // Is this block considered a full solid block
	[HideIf( nameof( IsFullBlock ), true )]
	[Description( "Collision bound mins, as a number of pixels" )]
	public Vector3Int BlockBoundsMins { get; set; } = new Vector3Int( 0, 0, 0 ); // The minimum bounds of the block in the X and Y directions. (When not IsSolidBlock)
	[HideIf( nameof( IsFullBlock ), true )]
	[Description( "Collision bounds maxs, as a number of pixels" )]
	public Vector3Int BlockBoundsMaxs { get; set; } = new Vector3Int( 16, 16, 16 ); // The maximum bounds of the block in the X and Y directions. (When not IsSolidBlock)

	[Group( "Placement" )]
	[Description( "Can another block destructively fill this block's space?" )]
	public bool Replaceable { get; set; } = false;

	[Group( "Placement" )]
	[Description( "Can this block be rotated when being placed?" )]
	public bool Rotateable { get; set; } = false;

	[HideIf( nameof( Rotateable ), false )]
	[Group( "Placement" )]
	[Description( "If true, the re-arranges its textures such that North is Forward." )]
	public bool RotateTextures { get; set; } = true;
	[HideIf( nameof( Rotateable ), false )]
	[Group( "Placement" )]
	[Description( "Valid directions this block can face." )]
	[Property]
	public DirectionFlags ValidDirections = new DirectionFlags( Directions.All.ToArray() );
	[Hide]
	public Direction[] _validDirections
	{
		get
		{
			return ValidDirections.All.ToArray();
		}
		set
		{
			foreach ( Direction dir in Directions.All )
			{
				ValidDirections[dir] = value.Contains( dir );
			}
		}
	}
	[HideIf( nameof( Rotateable ), false )]
	[Group( "Placement" )]
	[Description( "The default direction for this block to place if it was placed without direction information, or placed at an invalid direction. This may be an otherwise Invalid direction" )]
	[Property]
	public Direction DefaultDirection { get; set; } = Direction.None;
	[HideIf( nameof( Rotateable ), false )]
	[Group( "Placement" )]
	[Description( "Place depending on the camera direction, rather than the hit blocks facing" )]
	public bool CameraDirectionPlaced { get; set; } = false;
	[HideIf( nameof( Rotateable ), false )]
	[Group( "Placement" )]
	[Description( "Is our facing the inverse of the normal placed direction?" )]
	public bool FlipPlacedDirection { get; set; } = false;

	[Group( "Placement" )]
	[Description( "Where can this be placed / stay stable on. AgainstSolid requires a rotateable block to have it's Opposite block have a solid likewise face, OnSolid requires the block underneath have a solid top face" )]
	public PlacementOption ValidPlacementOption { get; set; } = PlacementOption.StableAnywhere;


	[Group( "Rendering" )]
	public PrefabFile BreakParticle { get; set; } = ResourceLibrary.Get<PrefabFile>( "prefabs/break particles.prefab" );

	[Group( "Rendering" )]
	public Material Material { get; set; } = null;

	[Group( "Step sounds" )] public SoundEvent WalkStepSound { get; set; } = ResourceLibrary.Get<SoundEvent>( "sounds/footsteps/stone/stone_walk.sound" );
	[Group( "Step sounds" )] public SoundEvent RunStepSound { get; set; } = ResourceLibrary.Get<SoundEvent>( "sounds/footsteps/stone/stone_run.sound" );
	[Group( "Step sounds" )] public SoundEvent CrouchStepSound { get; set; } = ResourceLibrary.Get<SoundEvent>( "sounds/footsteps/stone/stone_wander.sound" );
	[Group( "Step sounds" )] public SoundEvent LandingSound { get; set; } = ResourceLibrary.Get<SoundEvent>( "sounds/footsteps/stone/stone_walk.sound" );

	[Group( "Block Sounds" )] public SoundEvent PlaceSound { get; set; } = ResourceLibrary.Get<SoundEvent>( "sounds/blocks/stone/stone_place.sound" );
	[Group( "Block Sounds" )] public SoundEvent BreakingSound { get; set; } = ResourceLibrary.Get<SoundEvent>( "sounds/blocks/stone/stone_breaking.sound" );
	[Group( "Block Sounds" )] public SoundEvent BreakSound { get; set; } = ResourceLibrary.Get<SoundEvent>( "sounds/blocks/stone/stone_break.sound" );

	[Group( "Rendering" )]
	[Description( "Is this block opaque? Or partially transparent?" )]
	public bool Opaque { get; set; } = true; // Whether the block is opaque (blocks light).
	[Group( "Textures" )] public Texture Texture { get; set; } = null; // The texture used for rendering the block, can be null if not set.
	[Group( "Textures" )] public Texture TopTexture { get; set; } = null; // The texture used for the top face of the block, can be null if not set.
	[Group( "Textures" )] public Texture SideTexture { get; set; } = null; // The texture used for the side faces of the block, can be null if not set.
	[Group( "Textures" )] public Texture NorthTexture { get; set; } = null; // The texture used for the north face of the block, can be null if not set.
	[Group( "Textures" )] public Texture SouthTexture { get; set; } = null; // The texture used for the south face of the block, can be null if not set.
	[Group( "Textures" )] public Texture EastTexture { get; set; } = null; // The texture used for the east face of the block, can be null if not set.
	[Group( "Textures" )] public Texture WestTexture { get; set; } = null; // The texture used for the west face of the block, can be null if not set.
	[Group( "Textures" )] public Texture BottomTexture { get; set; } = null; // The texture used for the bottom face of the block, can be null if not set.

	[Hide] public int TextureIndex { get; set; } = 0; // Default texture index for the block, used for rendering.
	[Hide] public int? TopTextureIndex { get; set; } = null; // Default texture index for the top face of the block.
	[Hide] public int? SideTextureIndex { get; set; } = null; // Default texture index for the side faces of the block.
	[Hide] public int? NorthTextureIndex { get; set; } = null; // Default texture index for the north face of the block.
	[Hide] public int? SouthTextureIndex { get; set; } = null; // Default texture index for the south face of the block.
	[Hide] public int? EastTextureIndex { get; set; } = null; // Default texture index for the east face of the block.
	[Hide] public int? WestTextureIndex { get; set; } = null; // Default texture index for the west face of the block.
	[Hide] public int? BottomTextureIndex { get; set; } = null; // Default texture index for the bottom face of the block.
	[InlineEditor]
	public ItemStack[] Drops { get; set; } = []; // Items that drop when the block is broken, default is empty stack.

	public GameObject BlockObject { get; set; } = null; // A GameObject that represents the block in the world, can be null if not set.

	public virtual int GetTextureIndex( BlockSpace world, Vector3Int blockPos, Direction face )
	{
		if ( this.Rotateable && this.RotateTextures )
		{
			var facing = world.GetBlock( blockPos ).FacingFromData();
			face = face.RotateBy( facing );
		}
		return face switch
		{
			Direction.North => NorthTextureIndex ?? SideTextureIndex ?? TextureIndex,
			Direction.South => SouthTextureIndex ?? SideTextureIndex ?? TextureIndex,
			Direction.East => EastTextureIndex ?? SideTextureIndex ?? TextureIndex,
			Direction.West => WestTextureIndex ?? SideTextureIndex ?? TextureIndex,
			Direction.Up => TopTextureIndex ?? TextureIndex,
			Direction.Down => BottomTextureIndex ?? SideTextureIndex ?? TextureIndex,
			_ => TextureIndex
		}; // Returns the texture index for the block face at the given position.
	}

	public virtual void AddBlockMesh( BlockSpace world, Vector3Int blockPos, List<Vertex> verts )
	{
		if ( !IsFullBlock )
			return; // If we are not a solid block, this behaviour should be overridden, otherwise, we don't render anything.
					// For each face, call AddFaceMesh with the appropriate parameters.
		foreach ( var dir in Directions.All )
		{
			if ( ShouldFaceBeVisible( world, blockPos, dir ) )
			{
				AddFaceMesh( world, blockPos, dir, verts );
			}
		}
	}

	public bool IsValidPlacement( BlockSpace world, Vector3Int blockPos, Direction facing )
	{
		switch ( this.ValidPlacementOption )
		{
			case PlacementOption.OnSolid:
				var underBlock = blockPos + Vector3Int.Down;
				return world.GetBlock( underBlock ).GetBlock().IsFaceSolid( world, underBlock, Direction.Up );
			case PlacementOption.AgaintsSolid:
				var backBlock = blockPos + facing.Flip().Forward();
				return world.GetBlock( backBlock ).GetBlock().IsFaceSolid( world, backBlock, facing );
			case PlacementOption.StableAnywhere:
			default:
				return true;
		}
	}

	public void OnNeighbourUpdated( BlockSpace world, Vector3Int blockPos, Vector3Int neighbourPos )
	{
		world.GetChunkFromBlockPos( blockPos )?.MarkDirty();
		if ( !IsValidPlacement( world, blockPos, this.Rotateable ? world.GetBlock( blockPos ).FacingFromData() : Direction.None ) )
		{
			this.Pop( world, blockPos );
		}
	}

	public Direction BestDirectionFrom( Direction face, Vector3 cameraDirection )
	{
		if ( !this.Rotateable )
			return Direction.None;
		if ( this.CameraDirectionPlaced )
			face = Directions.FromVector( cameraDirection );
		if ( this.FlipPlacedDirection )
			face = face.Flip();
		if ( !this.ValidDirections[face] )
			face = this.DefaultDirection;
		return face;
	}

	// Pop off an invalid position
	public void Pop( BlockSpace world, Vector3Int pos )
	{
		// Check all VoxelPlayers if we're in their build volume, and give us to them if we are.
		foreach ( var ply in Game.ActiveScene.GetAll<VoxelPlayer>() )
		{
			if ( ply.GiveBrokenBlocks && ply.HasBuildVolume )
			{
				if ( pos.Within( ply.BuildAreaMins, ply.BuildAreaMaxs ) )
				{
					ply.inventory.PutInFirstAvailableSlot( new ItemStack( ItemRegistry.GetItem( pos ) ) );
					world.SetBlock( pos, new BlockData( "voxelparty:air" ) );
					return;
				}
			}
		}

		if ( world is World w )
		{
			// Pop off as an item.
			w.Thinker.BreakBlock( pos, world.GetBlock( pos ), true );
			return;
		}

		// Just clear us.
		world.SetBlock( pos, new BlockData( "voxelparty:air", 0 ) );

	}

	public virtual IEnumerable<BBox> GetHitboxes( BlockSpace world, Vector3Int blockPos )
	{
		if ( this.IsFullBlock )
		{
			return [new BBox( Vector3.Zero, Vector3.One )];
		}
		// If we have a blockObject that has an IHitboxProvider
		if ( world.GetBlockObject( blockPos ) is GameObject blockObject && blockObject.GetComponent<IHitboxProvider>() is IHitboxProvider hitbox )
		{
			return hitbox.ProvideHitboxes( world, blockPos );
		}

		return [new BBox( this.BlockBoundsMins / 16f, this.BlockBoundsMaxs / 16f )];
	}

	public virtual IEnumerable<BBox> GetCollisionAABBChunk( BlockSpace world, Vector3Int blockPos )
	{
		foreach ( var bbox in GetHitboxes( world, blockPos ) )
		{
			yield return bbox.Translate( blockPos.Modulo( Chunk.SIZE ) ).Scale( World.BlockScale );
		}
	}

	public virtual IEnumerable<BBox> GetCollisionAABBWorld( BlockSpace world, Vector3Int blockPos )
	{
		foreach ( var bbox in GetHitboxes( world, blockPos ) )
		{
			yield return bbox.Translate( blockPos ).Scale( World.BlockScale );
		}
	}

	public virtual IEnumerable<BBox> GetCollisionAABBBlock( BlockSpace world, Vector3Int blockPos )
	{
		foreach ( var bbox in GetHitboxes( world, blockPos ) )
		{
			yield return bbox.Translate( blockPos );
		}
	}

	public virtual ItemStack[] GetDrops( BlockSpace world, Vector3Int blockPos )
	{
		// Returns the items that drop when the block is broken.
		// Default implementation returns the Drops property.
		return Drops;
	}

	public virtual void AddFaceMesh( BlockSpace world, Vector3Int blockPos, Direction face, List<Vertex> verts )
	{
		// This method should be overridden to add the mesh for a specific face of the block.
		// For example, it could add vertices, normals, and UVs for the specified face.
		var textureIndex = GetTextureIndex( world, blockPos, face );
		var forward = face.Forward();
		var up = face.Up();
		var right = face.Right();
		var tangent = new Vector4( up, -1f );
		// Add vertices, normals, and UVs based on the face direction.
		var chunkPos = ((Vector3)blockPos).Modulo( Chunk.SIZE );
		verts.Add( new Vertex( ((forward + up + right + Vector3.One) / 2f + chunkPos) * World.BlockScale, forward, tangent, new Vector4( 1f, 0f, textureIndex, 0f ) ){
			TexCoord0 = new Vector4( 1f, 0f, 0f, 0f),
			TexCoord1 = new Vector4(textureIndex, 0f, 0f, 0f)
		} );
		verts.Add( new Vertex( ((forward + up - right + Vector3.One) / 2f + chunkPos) * World.BlockScale, forward, tangent, new Vector4( 0f, 0f, textureIndex, 0f ) ){
			TexCoord0 = new Vector4( 0f, 0f, 0f, 0f),
			TexCoord1 = new Vector4(textureIndex, 0f, 0f, 0f)
		} );
		verts.Add( new Vertex( ((forward - up - right + Vector3.One) / 2f + chunkPos) * World.BlockScale, forward, tangent, new Vector4( 0f, 1f, textureIndex, 0f ) ){
			TexCoord0 = new Vector4( 0f, 1f, 0f, 0f),
			TexCoord1 = new Vector4(textureIndex, 0f, 0f, 0f)
		} );
		verts.Add( new Vertex( ((forward + up + right + Vector3.One) / 2f + chunkPos) * World.BlockScale, forward, tangent, new Vector4( 1f, 0f, textureIndex, 0f ) ){
			TexCoord0 = new Vector4( 1f, 0f, 0f, 0f),
			TexCoord1 = new Vector4(textureIndex, 0f, 0f, 0f)
		} );
		verts.Add( new Vertex( ((forward - up - right + Vector3.One) / 2f + chunkPos) * World.BlockScale, forward, tangent, new Vector4( 0f, 1f, textureIndex, 0f ) ){
			TexCoord0 = new Vector4( 0f, 1f, 0f, 0f),
			TexCoord1 = new Vector4(textureIndex, 0f, 0f, 0f)
		} );
		verts.Add( new Vertex( ((forward - up + right + Vector3.One) / 2f + chunkPos) * World.BlockScale, forward, tangent, new Vector4( 1f, 1f, textureIndex, 0f ) ){
			TexCoord0 = new Vector4( 1f, 1f, 0f, 0f),
			TexCoord1 = new Vector4(textureIndex, 0f, 0f, 0f)
		} );
	}

	public virtual bool IsFaceOpaque( BlockSpace world, Vector3Int blockPos, Direction face )
	{
		// Check if the face is solid by checking the block's properties.
		return IsFullBlock && Opaque;
	}

	public virtual bool IsFaceSolid( BlockSpace world, Vector3Int blockPos, Direction face )
	{
		// Check if the face is solid by checking the block's properties.
		return IsFullBlock && IsSolid;
	}
	public virtual bool ShouldFaceBeVisible( BlockSpace world, Vector3Int blockPos, Direction face )
	{
		// If we are not opaque, we always show the face.
		if ( !Opaque )
		{
			var adjacentPos = blockPos + face.Forward();
			var adjacentBlockData = world.GetBlock( adjacentPos );
			var adjacentBlock = ItemRegistry.GetBlockByIdentifier( adjacentBlockData.BlockID );
			if ( adjacentBlock == null )
			{
				return true; // If no adjacent block, show the face.
			}
			if ( adjacentBlockData == world.GetBlock( blockPos ) )
			{
				return false; // If the adjacent block is the same as this block, don't show the face.
			}
			return true;
		}
		else
		{
			// Check if the adjacent block in the direction of the face is solid.
			var adjacentPos = blockPos + face.Forward();
			var adjacentBlockData = world.GetBlock( adjacentPos );
			var adjacentBlock = ItemRegistry.GetBlockByIdentifier( adjacentBlockData.BlockID );
			if ( adjacentBlock == null )
			{
				return true; // If no adjacent block, show the face.
			}
			if ( adjacentBlock.IsFaceOpaque( world, adjacentPos, face.Flip() ) )
			{
				return false; // If the adjacent block's opposite face is solid, don't show this face.
			}
			return true; // Otherwise, show the face.
		}
	}
}
