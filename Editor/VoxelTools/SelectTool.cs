
using System;
using Sandbox.theoretical;
using Sandbox.Utility;

public class SelectTool : VoxelTool
{
    public override string Icon => "crop_free";

    public override string Name => "Select";
    public override string Shortcut => "SelectTool";

    public static Vector3Int? FirstPosition = null;
    public static Vector3Int? SecondPosition = null;
    public TimeSince startHold = 0f;

    static BBox selectBox;

    public override void DrawGizmos( Vector3Int blockPosition, Direction faceDirection )
    {
        SceneEditorSession.Active.Selection.Clear();

        if ( WasKeyPressed( "editor.paste" ) )
        {
            PasteRegion();
        }
        Gizmo.Draw.IgnoreDepth = true;
        if ( FirstPosition.HasValue && SecondPosition.HasValue )
        {
            var first = FirstPosition.Value;
            var second = SecondPosition.Value;
            if ( WasKeyPressed( "editor.cut" ) )
                CutRegion( first, second );
            else if ( WasKeyPressed( "editor.copy" ) )
                CopyRegion( first, second );
            else if ( WasKeyPressed( "editor.delete" ) )
                ClearRegion( first, second );
            else if ( WasKeyPressed( "editor.duplicate" ) )
                DuplicateRegion();
            else if ( WasKeyPressed( "mesh.vertex-weld-uvs" ) )
                MoveRegion();

            if ( Gizmo.Control.BoundingBox( "SelectBox", selectBox, out BBox oBox ) )
            {
                selectBox = oBox;

                // Adjust FirstPosition to be the mins (Rounded) and SecondPosition to be the Maxs (Rounded)
                var mins = selectBox.Mins / World.BlockScale;
                var maxs = selectBox.Maxs / World.BlockScale;
                FirstPosition = new Vector3Int( (int)System.MathF.Floor( mins.x + 0.5f ), (int)System.MathF.Floor( mins.y + 0.5f ), (int)System.MathF.Floor( mins.z + 0.5f ) );
                SecondPosition = new Vector3Int( (int)System.MathF.Ceiling( maxs.x - 1.5f ), (int)System.MathF.Ceiling( maxs.y - 1.5f ), (int)System.MathF.Ceiling( maxs.z - 1.5f ) );

                return;
            }
            if ( !Gizmo.Pressed.Any )
            {
                selectBox = BBox.FromPoints( [FirstPosition.Value * World.BlockScale, SecondPosition.Value * World.BlockScale, (FirstPosition.Value + Vector3Int.One) * World.BlockScale, (SecondPosition.Value + Vector3Int.One) * World.BlockScale] );
            }
        }


        if ( !Gizmo.Pressed.Any && Gizmo.WasLeftMousePressed )
        {
            FirstPosition = blockPosition;
            startHold = 0f;
            SecondPosition = null; // Reset second position when starting a new selection
        }
        if ( !FirstPosition.HasValue )
        {
            Gizmo.Draw.Color = Color.Black;
            Gizmo.Draw.LineThickness = 2f;
            Gizmo.Draw.LineBBox( GetBlockBox( blockPosition ) );
        }
        else
        {
            if ( !Gizmo.Pressed.Any && Gizmo.IsLeftMouseDown )
            {
                SecondPosition = blockPosition;
                selectBox = BBox.FromPoints( [FirstPosition.Value * World.BlockScale, SecondPosition.Value * World.BlockScale, (FirstPosition.Value + Vector3.One) * World.BlockScale, (SecondPosition.Value + Vector3.One) * World.BlockScale] );
            }

            Gizmo.Draw.IgnoreDepth = true;
            Gizmo.Draw.Color = Color.Blue;
            Gizmo.Draw.LineThickness = 2f;
            Gizmo.Draw.LineBBox( selectBox );

            Gizmo.Draw.Color = Color.Blue.WithAlpha( 0.2f );
            Gizmo.Draw.LineThickness = 1f;
            Gizmo.Draw.SolidBox( GetBoxFromBlocks( FirstPosition.Value, SecondPosition.HasValue ? SecondPosition.Value : blockPosition ) );
        }

        if ( !Gizmo.Pressed.Any && (Gizmo.WasLeftMouseReleased && startHold < 0.1f) )
        {
            FirstPosition = null; // Reset selection on quick release
            SecondPosition = null;
        }
    }

    private void NormalizePositions()
    {
        var minX = System.Math.Min( FirstPosition.Value.x, SecondPosition.Value.x );
        var minY = System.Math.Min( FirstPosition.Value.y, SecondPosition.Value.y );
        var minZ = System.Math.Min( FirstPosition.Value.z, SecondPosition.Value.z );
        var maxX = System.Math.Max( FirstPosition.Value.x, SecondPosition.Value.x );
        var maxY = System.Math.Max( FirstPosition.Value.y, SecondPosition.Value.y );
        var maxZ = System.Math.Max( FirstPosition.Value.z, SecondPosition.Value.z );
        FirstPosition = new Vector3Int( minX, minY, minZ );
        SecondPosition = new Vector3Int( maxX, maxY, maxZ );
    }

    public void CutRegion( Vector3Int first, Vector3Int second )
    {
        NormalizePositions();
        var area = BlockData.GetAreaInBox( first, second - first + Vector3Int.One );
        EditorUtility.Clipboard.Copy( World.Active.SerializeRegionByID( first, second ) );
        SceneEditorSession.Active.QuickAddUndo( "Cut Region", () =>
        {
            for ( int x = 0; x < area.GetLength( 0 ); x++ )
            {
                for ( int y = 0; y < area.GetLength( 1 ); y++ )
                {
                    for ( int z = 0; z < area.GetLength( 2 ); z++ )
                    {
                        var pos = new Vector3Int( first.x + x, first.y + y, first.z + z );
                        World.Active.SetBlock( pos, area[x, y, z] );
                    }
                }
            }
        }, () =>
        {
            for ( int x = 0; x < area.GetLength( 0 ); x++ )
            {
                for ( int y = 0; y < area.GetLength( 1 ); y++ )
                {
                    for ( int z = 0; z < area.GetLength( 2 ); z++ )
                    {
                        var pos = new Vector3Int( first.x + x, first.y + y, first.z + z );
                        World.Active.SetBlock( pos, new BlockData( "voxelparty:air" ) );
                    }
                }
            }
        } );
    }

    public void CopyRegion( Vector3Int first, Vector3Int second )
    {
        NormalizePositions();
        EditorUtility.Clipboard.Copy( World.Active.SerializeRegionByID( first, second ) );
        Log.Info( World.Active.GetStructureBounds( EditorUtility.Clipboard.Paste() ) );
    }

    public void PasteRegion()
    {
        // Prefer pasting as a Structure?

        var str = EditorUtility.Clipboard.Paste();
        var blockData = World.Active.GetStructureDataByID( str );

        if ( blockData == null )
        {
            return;
        }

        VoxelBuilder.SelectTool( new PasteStructureTool( str, this ) );
    }

    public void DuplicateRegion()
    {
        NormalizePositions();
        var first = FirstPosition.Value;
        var second = SecondPosition.Value;
        var str = World.Active.SerializeRegionByID( first, second );

        VoxelBuilder.SelectTool( new PasteStructureTool( str, this, first ) );
    }

    public void MoveRegion()
    {
        NormalizePositions();
        var first = FirstPosition.Value;
        var second = SecondPosition.Value;
        var str = World.Active.SerializeRegionByID( first, second );
        ClearRegion( first, second );
        VoxelBuilder.SelectTool( new PasteStructureTool( str, this, first ) );
    }

    public void ClearRegion( Vector3Int first, Vector3Int second )
    {
        NormalizePositions();
        var area = BlockData.GetAreaInBox( first, second - first + Vector3Int.One );
        SceneEditorSession.Active.QuickAddUndo( "Clear Region", () =>
        {
            for ( int x = 0; x < area.GetLength( 0 ); x++ )
            {
                for ( int y = 0; y < area.GetLength( 1 ); y++ )
                {
                    for ( int z = 0; z < area.GetLength( 2 ); z++ )
                    {
                        var pos = new Vector3Int( first.x + x, first.y + y, first.z + z );
                        World.Active.SetBlock( pos, area[x, y, z] );
                    }
                }
            }
        }, () =>
        {
            for ( int x = 0; x < area.GetLength( 0 ); x++ )
            {
                for ( int y = 0; y < area.GetLength( 1 ); y++ )
                {
                    for ( int z = 0; z < area.GetLength( 2 ); z++ )
                    {
                        var pos = new Vector3Int( first.x + x, first.y + y, first.z + z );
                        World.Active.SetBlock( pos, new BlockData( "voxelparty:air" ) );
                    }
                }
            }
        } );
    }

    public override void MakeOptions( Layout parent )
    {
        var instructions = new Label( $"Select a region by clicking and dragging the mouse.\n\nUse the following keys for actions:\n{EditorShortcuts.GetDisplayKeys( "editor.cut" )}:\tCut\n{EditorShortcuts.GetDisplayKeys( "editor.copy" )}:\tCopy\n{EditorShortcuts.GetDisplayKeys( "editor.paste" )}:\tPaste\n{EditorShortcuts.GetDisplayKeys( "editor.delete" )}:\tClear\n{EditorShortcuts.GetDisplayKeys( "editor.duplicate" )}:\tDuplicate\n{EditorShortcuts.GetDisplayKeys( "mesh.vertex-weld-uvs" )}:\tMove Region\n" );
        parent.Add( instructions );
        base.MakeOptions( parent );
    }

    private void TransformRegion( Func<Vector3Int, Vector3Int, Vector3Int> transformFunc, bool needsBox = false )
    {
        if ( !FirstPosition.HasValue || !SecondPosition.HasValue )
        {
            Log.Warning( "No selection made to transform." );
            return;
        }

        NormalizePositions();

        var first = FirstPosition.Value;
        var second = SecondPosition.Value;

        if ( needsBox )
        {
            var maxAxis = (second - first).Components().Max();
            SecondPosition = second = first + new Vector3Int( maxAxis, maxAxis, maxAxis );
        }

        var size = second - first + Vector3Int.One;
        var blocks = BlockData.GetAreaInBox( first, size );
        SceneEditorSession.Active.QuickAddUndo( "Transform Region", () =>
        {
            for ( int x = 0; x < size.x; x++ )
            {
                for ( int y = 0; y < size.y; y++ )
                {
                    for ( int z = 0; z < size.z; z++ )
                    {
                        var pos = new Vector3Int( first.x + x, first.y + y, first.z + z );
                        World.Active.SetBlock( pos, blocks[size.x - 1 - x, y, z] );
                    }
                }
            }
        }, () =>
        {
            for ( int x = 0; x < size.x; x++ )
            {
                for ( int y = 0; y < size.y; y++ )
                {
                    for ( int z = 0; z < size.z; z++ )
                    {
                        var pos = new Vector3Int( first.x + x, first.y + y, first.z + z );
                        var newPos = transformFunc( new Vector3Int( x, y, z ), size );
                        var newX = newPos.x;
                        var newY = newPos.y;
                        var newZ = newPos.z;
                        World.Active.SetBlock( pos, blocks[newX, newY, newZ] );
                    }
                }
            }
        } );
    }

    [VoxelToolButton]
    public void FlipX()
    {
        // Flip the structure along the X axis
        TransformRegion( ( pos, size ) => new Vector3Int( size.x - 1 - pos.x, pos.y, pos.z ) );
    }
    [VoxelToolButton]
    public void FlipY()
    {
        // Flip the structure along the Y axis
        TransformRegion( ( pos, size ) => new Vector3Int( pos.x, size.y - 1 - pos.y, pos.z ) );
    }

    [VoxelToolButton]
    public void FlipZ()
    {
        // Flip the structure along the Z axis
        TransformRegion( ( pos, size ) => new Vector3Int( pos.x, pos.y, size.z - 1 - pos.z ) );
    }

    [VoxelToolButton]
    public void RotateX()
    {
        // Rotate the structure 90 degrees around the X axis
        TransformRegion( ( pos, size ) => new Vector3Int( pos.x, size.z - 1 - pos.z, pos.y ), true );
    }

    [VoxelToolButton]
    public void RotateY()
    {
        // Rotate the structure 90 degrees around the Y axis
        TransformRegion( ( pos, size ) => new Vector3Int( size.z - 1 - pos.z, pos.y, pos.x ), true );
    }

    [VoxelToolButton]
    public void RotateZ()
    {
        // Rotate the structure 90 degrees around the Z axis
        TransformRegion( ( pos, size ) => new Vector3Int( pos.y, size.x - 1 - pos.x, pos.z ), true );
    }

    [VoxelToolButton]
    public void SaveArea()
    {
        if ( !FirstPosition.HasValue || !SecondPosition.HasValue )
        {
            Log.Warning( "No selection made to save." );
            return;
        }

        NormalizePositions();

        var first = FirstPosition.Value;
        var second = SecondPosition.Value;

        var size = second - first + Vector3Int.One;
        var blocks = BlockData.GetAreaInBox( first, size );
        var structureData = World.Active.SerializeRegionByID( first, second );
        var structure = new Structure { StructureDataByID = structureData };
        var path = EditorUtility.SaveFileDialog( "Save Your Structure", ".struct", Editor.FileSystem.Content.GetFullPath( "/structures" ) );
        System.IO.File.WriteAllText( path, structure.Serialize().ToString() );
    }
}