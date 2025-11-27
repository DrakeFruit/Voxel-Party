public class SquarePlaceTool : VoxelTool
{
    public override string Icon => "check_box_outline_blank";

    public override string Name => "Square Place Tool";
    public override string Shortcut => "SquareTool";

    private Vector3Int? FirstPosition = null;
    private Direction? BlockAxis = null;
    public bool FlipDraw = false;

    [ToolProperty] public bool ReplaceExistingBlocks { get; set; } = true; // Whether to replace existing blocks or not
    private bool _insetIntoBlock
    = false;
    [ToolProperty]
    public bool InsetIntoBlock
    {
        get
        {
            return _insetIntoBlock ^ EditorShortcuts.IsDown( "CtrlModifier" );
        }
        set
        {
            _insetIntoBlock = value ^ EditorShortcuts.IsDown( "CtrlModifier" );
        }
    }

    public override void DrawGizmos( Vector3Int blockPosition, Direction faceDirection )
    {
        if ( !FirstPosition.HasValue || !BlockAxis.HasValue )
        {
            DrawFirstPointSelector( blockPosition, faceDirection );
            return;
        }
        else
        {
            DrawBoxSelector( blockPosition, faceDirection );
            return;
        }
    }

    private void DrawBoxSelector( Vector3Int blockPosition, Direction faceDirection )
    {
        // Manually trace along the plane of the first point and the axis
        var hitPos = GetSecondPosition();
        var box = GetBoxFromBlocks( FirstPosition.Value, hitPos );
        Gizmo.Draw.IgnoreDepth = true;
        Gizmo.Draw.Color = Color.Green;
        Gizmo.Draw.LineThickness = 2f;
        Gizmo.Draw.LineBBox( box );
    }

    private Vector3Int GetSecondPosition()
    {
        // Calculate the second position based on the first position and the axis
        Vector3Int traceAxis = BlockAxis.Value.Forward();
        if ( !FlipDraw )
            traceAxis = -traceAxis;
        if ( traceAxis.x < 0 ) traceAxis.x = 0;
        if ( traceAxis.y < 0 ) traceAxis.y = 0;
        if ( traceAxis.z < 0 ) traceAxis.z = 0;
        var trace = new Plane( (FirstPosition.Value + traceAxis) * World.BlockScale, BlockAxis.Value.Forward() ).Trace( Gizmo.CurrentRay, true );
        if ( !trace.HasValue ) { return FirstPosition.Value; } // Impossible?
        var hitPos = Helpers.WorldToVoxel( trace.Value ) - traceAxis;
        // Garuntee the hitPos is on the same plane as the first position
        switch ( BlockAxis.Value )
        {
            case Direction.North:
            case Direction.South:
                hitPos.x = FirstPosition.Value.x;
                break;
            case Direction.East:
            case Direction.West:
                hitPos.y = FirstPosition.Value.y;
                break;
            case Direction.Up:
            case Direction.Down:
                hitPos.z = FirstPosition.Value.z;
                break;
        }
        return hitPos;
    }

    private void DrawFirstPointSelector( Vector3Int blockPosition, Direction faceDirection )
    {
        Gizmo.Draw.IgnoreDepth = true;
        Gizmo.Draw.Color = Color.Green;
        Gizmo.Draw.LineThickness = 2f;
        Vector3Int targetPosition = blockPosition + faceDirection.Forward() * 1;
        if ( FlipDraw = InsetIntoBlock )
            targetPosition = blockPosition;
        var targetDir = faceDirection;
        if ( Gizmo.KeyboardModifiers.HasFlag( KeyboardModifiers.Shift ) )
        {
            // if shift is held, use the dir from the camera direction
            targetDir = Directions.FromVector( Gizmo.Camera.Rotation.Forward );
        }

        Gizmo.Draw.LineBBox( GetBlockBox( targetPosition ) );

        // Draw a little green square in the center of the block showing the axis of placement
        var boxCenter = (targetPosition + Vector3Int.One / 2f) * World.BlockScale;
        var boxSize = new Vector3( World.BlockScale, World.BlockScale, World.BlockScale );
        switch ( targetDir )
        {
            case Direction.North:
            case Direction.South:
                boxSize.x *= 0.01f;
                break;
            case Direction.East:
            case Direction.West:
                boxSize.y *= 0.01f;
                break;
            case Direction.Up:
            case Direction.Down:
                boxSize.z *= 0.01f;
                break;
        }
        boxSize *= 0.5f; // Make it smaller for the square
        var squareBox = BBox.FromPositionAndSize( boxCenter, boxSize );
        Gizmo.Draw.LineBBox( squareBox );
    }

    public override void LeftMousePressed( Vector3Int blockPosition, Direction faceDirection )
    {
        base.LeftMousePressed( blockPosition, faceDirection );
        FirstPosition = InsetIntoBlock ? blockPosition : blockPosition + faceDirection.Forward() * 1;
        BlockAxis = faceDirection;
        if ( Gizmo.KeyboardModifiers.HasFlag( KeyboardModifiers.Shift ) )
        {
            BlockAxis = Directions.FromVector( Gizmo.Camera.Rotation.Forward );
        }
    }

    public override void LeftMouseUp( Vector3Int blockPosition, Direction faceDirection )
    {
        base.LeftMouseUp( blockPosition, faceDirection );

        if ( !FirstPosition.HasValue || !BlockAxis.HasValue )
            return;

        var SecondPosition = GetSecondPosition();

        var minX = System.Math.Min( FirstPosition.Value.x, SecondPosition.x );
        var minY = System.Math.Min( FirstPosition.Value.y, SecondPosition.y );
        var minZ = System.Math.Min( FirstPosition.Value.z, SecondPosition.z );
        var maxX = System.Math.Max( FirstPosition.Value.x, SecondPosition.x );
        var maxY = System.Math.Max( FirstPosition.Value.y, SecondPosition.y );
        var maxZ = System.Math.Max( FirstPosition.Value.z, SecondPosition.z );

        var oldBlockData = BlockData.GetAreaInBox( new Vector3Int( minX, minY, minZ ), new Vector3Int( maxX - minX + 1, maxY - minY + 1, maxZ - minZ + 1 ) );
        var blockData = BlockData.WithPlacementBlockData( VoxelBuilder.SelectedItemID, BlockAxis.Value, Gizmo.Camera.Rotation.Forward );

        SceneEditorSession.Active.AddUndo( "Place Rectangle", () =>
        {
            for ( int z = minZ; z <= maxZ; z++ )
            {
                for ( int y = minY; y <= maxY; y++ )
                {
                    for ( int x = minX; x <= maxX; x++ )
                    {
                        var pos = new Vector3Int( x, y, z );
                        World.Active.SetBlock( pos, oldBlockData[x - minX, y - minY, z - minZ] );
                    }
                }
            }
        }, () =>
        {
            for ( int z = minZ; z <= maxZ; z++ )
            {
                for ( int y = minY; y <= maxY; y++ )
                {
                    for ( int x = minX; x <= maxX; x++ )
                    {
                        var pos = new Vector3Int( x, y, z );
                        World.Active.SetBlock( pos, blockData );
                    }
                }
            }
        } );

        for ( int z = minZ; z <= maxZ; z++ )
        {
            for ( int y = minY; y <= maxY; y++ )
            {
                for ( int x = minX; x <= maxX; x++ )
                {
                    var pos = new Vector3Int( x, y, z );
                    if ( ReplaceExistingBlocks || !World.Active.GetBlock( pos ).IsEmpty() )
                    {
                        World.Active.SetBlock( pos, blockData );
                    }
                }
            }
        }

        FirstPosition = null;
        BlockAxis = null;
    }

    public override void MakeOptions( Layout parent )
    {
        var label = new Label( "Hold CTRL to swap Inset mode\nHold SHIFT to toggle place axis\n" );
        parent.Add( label );
        base.MakeOptions( parent );
    }
}