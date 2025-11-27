using System.IO;
using System.Runtime.Serialization;
using Sandbox.theoretical;
using FileSystem = Editor.FileSystem;

[EditorTool]
[Title( "Structure Writer" )]
[Icon( "save" )]
[Alias( "structurewriter" )]
[Group( "8" )]
public class StructureWriter : EditorTool
{
    public static Vector3Int FirstPosition { get; set; } = Vector3Int.Zero;
    static Vector3 FirstPositionFractional { get; set; } = Vector3.Zero;
    public static Vector3Int SecondPosition { get; set; } = Vector3Int.Zero;
    static Vector3 SecondPositionFractional { get; set; } = Vector3.Zero;
    static public bool FirstPositionSet { get; set; } = false;
    static public bool SecondPositionSet { get; set; } = false;

    public override void OnEnabled()
    {
        AllowGameObjectSelection = false;

        var window = new WidgetWindow( SceneOverlay );
        window.WindowTitle = "Structure Writer";
        window.Layout = Layout.Column();
        window.Layout.Margin = 16;


        var reset = new Button( "Reset Positions" );
        reset.Clicked = () =>
        {
            FirstPositionSet = false;
            SecondPositionSet = false;
            FirstPosition = Vector3Int.Zero;
            SecondPosition = Vector3Int.Zero;
            FirstPositionFractional = Vector3.Zero;
            SecondPositionFractional = Vector3.Zero;
        };
        window.Layout.Add( reset );

        var save = new Button( "Save Structure" );

        save.Clicked = () =>
        {
            if ( !FirstPositionSet || !SecondPositionSet )
            {
                Log.Error( "You must set both positions before saving." );
                return;
            }
            
            var structure = new Structure { StructureDataByID = World.Active.SerializeRegionByID( FirstPosition, SecondPosition ) };
            var path = EditorUtility.SaveFileDialog( "Save Your Structure", ".struct", FileSystem.Content.GetFullPath( "/structures" ) );
	        File.WriteAllText(path, structure.Serialize().ToString());
        };

        window.Layout.Add( save );

        AddOverlay( window, TextFlag.RightTop, 10 );
    }

    const float MAX_DISTANCE = 8096;
    public override void OnUpdate()
    {
        base.OnUpdate();

        if ( FirstPositionSet )
        {
            Gizmo.Draw.Color = Color.Blue;
            Gizmo.Draw.IgnoreDepth = true;
            Gizmo.Draw.LineThickness = 2f;
            Gizmo.Draw.LineBBox( BBox.FromPositionAndSize( (FirstPosition + Vector3.One / 2f) * World.BlockScale, Vector3.One * World.BlockScale ) );
        }
        if ( SecondPositionSet )
        {
            Gizmo.Draw.Color = Color.Red;
            Gizmo.Draw.IgnoreDepth = true;
            Gizmo.Draw.LineThickness = 2f;
            Gizmo.Draw.LineBBox( BBox.FromPositionAndSize( (SecondPosition + Vector3.One / 2f) * World.BlockScale, Vector3.One * World.BlockScale ) );
        }

        if ( FirstPositionSet && SecondPositionSet )
        {
            Gizmo.Draw.Color = Color.Green;
            Gizmo.Draw.IgnoreDepth = true;
            Gizmo.Draw.LineThickness = 2f;
            var center = (FirstPosition + SecondPosition) / 2f;
            Gizmo.Draw.LineBBox( BBox.FromPoints(
                new[]{
                    FirstPosition * World.BlockScale, (FirstPosition + Vector3.One) * World.BlockScale,
                    SecondPosition * World.BlockScale, (SecondPosition + Vector3.One) * World.BlockScale,
                }
            ) );

            using ( Gizmo.Scope( "", global::Transform.Zero.WithPosition( FirstPositionFractional ) ) )
                if ( Gizmo.Control.Position( "wow", FirstPositionFractional, out Vector3 newPos ) )
                {
                    FirstPositionFractional = newPos;
                    FirstPosition = (FirstPositionFractional / World.BlockScale).Floor();
                }
            using ( Gizmo.Scope( "", global::Transform.Zero.WithPosition( SecondPositionFractional ) ) )
                if ( Gizmo.Control.Position( "wow2", SecondPositionFractional, out Vector3 newPos2 ) )
                {
                    SecondPositionFractional = newPos2;
                    SecondPosition = (SecondPositionFractional / World.BlockScale).Floor();
                }
        }

        Gizmo.Draw.IgnoreDepth = false;

        var trace = World.Active
            .Trace( Gizmo.CurrentRay.Position, Gizmo.CurrentRay.Position + Gizmo.CurrentRay.Forward * MAX_DISTANCE )
            .Run();
        var hitPos = trace.HitBlockPosition;
        var hitDirection = trace.HitFace;
        if ( !trace.Hit )
            return;
        var faceCenter = (hitPos + 0.5f) * World.BlockScale + hitDirection.Forward() * World.BlockScale * 0.5f;
        Vector3 boxSize = new Vector3( World.BlockScale, World.BlockScale, World.BlockScale );
        switch ( hitDirection )
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

        Gizmo.Draw.Color = Color.Black;
        Gizmo.Draw.LineThickness = 2f;
        Gizmo.Draw.LineBBox( BBox.FromPositionAndSize( faceCenter, boxSize ) );

        if ( Gizmo.WasLeftMousePressed )
        {
            if ( !FirstPositionSet )
            {
                FirstPosition = hitPos;
                FirstPositionFractional = (hitPos + Vector3.One / 2f) * World.BlockScale;
                FirstPositionSet = true;
            }
            else if ( !SecondPositionSet )
            {
                SecondPosition = hitPos;
                SecondPositionFractional = (hitPos + Vector3.One / 2f) * World.BlockScale;
                SecondPositionSet = true;
            }
        }
    }

}
