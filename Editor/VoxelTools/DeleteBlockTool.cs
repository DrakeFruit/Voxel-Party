public class DeleteBlockTool : VoxelTool
{
    public override string Icon => "delete";
    public override string Name => "Delete Block";
    public override KeyCode? Shortcut => KeyCode.E;

    public override void DrawGizmos( Vector3Int blockPosition, Direction faceDirection )
    {
        Gizmo.Draw.Color = Color.Red;
        Gizmo.Draw.LineThickness = 2f;
        Gizmo.Draw.LineBBox( GetBlockBox( blockPosition ) );
    }

    public override void LeftMousePressed( Vector3Int blockPosition, Direction faceDirection )
    {
        var lastBlockData = World.Active.GetBlock( blockPosition );
        SceneEditorSession.Active.AddUndo( "Delete Block", () => World.Active.SetBlock( blockPosition, lastBlockData ),
            () => World.Active.SetBlock( blockPosition, new BlockData( 0 ) ) );
        World.Active.SetBlock( blockPosition, new BlockData( 0 ) );
    }
}