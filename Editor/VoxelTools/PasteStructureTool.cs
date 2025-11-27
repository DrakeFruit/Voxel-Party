using Sandbox.theoretical;

public class PasteStructureTool : VoxelTool
{
    public override string Icon => "paste";
    public override string Name => "Paste Structure";
    VoxelTool LastTool;

    GameObject ghost;

    public PasteStructureTool( string data, VoxelTool lastTool, Vector3Int? StartPosition = null )
    {
        ghost = new GameObject( false, "Pasted Object" );
        var loader = ghost.AddComponent<StructureLoader>( false );
        loader.LoadedStructure = new Structure()
        {
            StructureDataByID = data
        };
        loader.Enabled = true;
        if ( StartPosition.HasValue )
        {
            ghost.WorldPosition = StartPosition.Value * World.BlockScale;
        }
        else
        {
            // Use the current block trace position
            var (pos, _) = VoxelBuilder.BlockTrace();
            ghost.WorldPosition = pos * World.BlockScale;
        }

        ghost.Enabled = true;

        LastTool = lastTool;
    }

    public override void OnDeselected()
    {
        base.OnDeselected();
        if ( ghost != null && ghost.IsValid() )
        {
            ghost.Destroy();
            ghost = null;
        }
    }

    public override void DrawGizmos( Vector3Int blockPosition, Direction faceDirection )
    {
        base.DrawGizmos( blockPosition, faceDirection );
        if ( ghost == null || !ghost.IsValid() || WasKeyPressed( "editor.clear-selection" ) || WasKeyPressed( "editor.delete" ) )
        {
            VoxelBuilder.SelectTool( LastTool ); // Our ghost died and this is sad.
            return;
        }

        using ( Gizmo.Scope( "MoveStructureArrow", ghost.WorldTransform ) )
        {
            if ( Gizmo.Control.Position( "StructurePostion", ghost.WorldPosition, out var newPos ) )
            {
                ghost.WorldPosition = newPos;
            }
        }

        Gizmo.Hitbox.BBox( BBox.FromPoints( new[] { ghost.WorldPosition, ghost.WorldPosition + ghost.GetComponent<StructureLoader>().StructureSize * World.BlockScale } ) );

        if ( (Gizmo.Pressed.IsActive && !Gizmo.Pressed.Any) || WasKeyPressed( "editor.edge-bevel-apply" ) )
        {
            // Stamp the structure
            var loader = ghost.GetComponent<StructureLoader>();
            if ( loader != null && loader.LoadedStructure != null && loader.LoadedStructure.IsValid() )
            {
                var (oldBlocks, pos) = loader.StampStructure();
                var newBlocks = BlockData.GetAreaInBox( pos, loader.StructureSize );
                if ( oldBlocks != null )
                {
                    // Force the selection to be the pasted structure
                    SelectTool.FirstPosition = pos;
                    SelectTool.SecondPosition = pos + loader.StructureSize - Vector3Int.One;
                    SceneEditorSession.Active.AddUndo( "Paste Structure", () =>
                    {
                        // Reset all the old blocks
                        for ( int z = 0; z < oldBlocks.GetLength( 2 ); z++ )
                        {
                            for ( int y = 0; y < oldBlocks.GetLength( 1 ); y++ )
                            {
                                for ( int x = 0; x < oldBlocks.GetLength( 0 ); x++ )
                                {
                                    World.Active.SetBlock( pos + new Vector3Int( x, y, z ), oldBlocks[x, y, z] );
                                }
                            }
                        }
                    }, () =>
                    {
                        // Set the new blocks
                        for ( int z = 0; z < newBlocks.GetLength( 2 ); z++ )
                        {
                            for ( int y = 0; y < newBlocks.GetLength( 1 ); y++ )
                            {
                                for ( int x = 0; x < newBlocks.GetLength( 0 ); x++ )
                                {
                                    World.Active.SetBlock( pos + new Vector3Int( x, y, z ), newBlocks[x, y, z] );
                                }
                            }
                        }
                    } );
                }

            }
        }
    }
}