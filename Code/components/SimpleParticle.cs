using System;
using System.Drawing;

public class SimpleParticle : Component
{
    [Sync] public Material Material { get; set; }

    [Sync] public Rect TextureRect { get; set; } = new Rect( 0, 0, 1, 1 ); // TextureRect defines the portion of the texture to use for this particle.
    [Sync] public string ItemID { get; set; } = "voxelparty:air"; // ItemID is used to identify which block this particle represents, if applicable.
    public int TextureID => ItemRegistry.GetBlockByIdentifier( ItemID )?.TextureIndex??0;

    [Sync] public Vector3 Velocity { get; set; } = Vector3.Zero; // Velocity is the speed and direction of the particle's movement.
    [Sync] public Vector3 Acceleration { get; set; } = Vector3.Zero; // Acceleration is the change in velocity over time, affecting the particle's movement.
    [Sync] public float Damping { get; set; } = 0.90f; // Damping is a factor that reduces the particle's velocity over time, simulating friction or air resistance.
    [Sync] public float Lifetime { get; set; } = 1f; // Lifetime is the duration for which the particle exists before it is removed in seconds
    [Sync] public float MaxLifetime { get; set; } = 1f; // MaxLifetime is the maximum duration for which the particle can exist, used to reset or recycle particles.

    // If Animated:
    // Frames is the number of frames in the animation, and it plays along the course of the particle's lifetime.
    // TilesWide is the number of tiles in the texture atlas that the animation spans horizontally.
    // TileHeight is the height of each tile in the texture atlas.
    public bool Animated { get; set; } = false;
    public int? Frames { get; set; } = null;
    public int TilesWide { get; set; } = 1;
    public int TileHeight { get; set; } = 1;

    [Sync( SyncFlags.Query )]
    private byte[] _scaleData
    {
        get
        {
            if ( !Scale.Frames.Any() )
                return Array.Empty<byte>();

            var data = new List<byte>();
            data.AddRange( BitConverter.GetBytes( Scale.Frames.Count() ) ); // Store the number of frames.
            foreach ( var frame in Scale.Frames )
            {
                data.AddRange( BitConverter.GetBytes( frame.Time ) ); // Store the time for each frame.
                data.AddRange( BitConverter.GetBytes( frame.Value ) ); // Store the value for each frame.
            }
            return data.ToArray();
        }
        set
        {
            if ( value == null || value.Length == 0 )
            {
                Scale = new Curve( new Curve.Frame( 0, 10 ), new Curve.Frame( 1, 10 ) ); // Reset to default if no data is provided.
                return;
            }

            int frameCount = BitConverter.ToInt32( value, 0 ); // Read the number of frames from the data.
            var frames = new List<Curve.Frame>();
            for ( int i = 0; i < frameCount; i++ )
            {
                int offset = 4 + i * 8; // Each frame consists of two floats (time and value), so we read 8 bytes per frame.
                float time = BitConverter.ToSingle( value, offset );
                float frameValue = BitConverter.ToSingle( value, offset + 4 );
                frames.Add( new Curve.Frame( time, frameValue ) ); // Add the frame to the list.
            }

            Scale = Scale.WithFrames( frames );
        }
    }

    public Curve Scale { get; set; } = new Curve( new Curve.Frame( 0, 10 ), new Curve.Frame( 1, 10 ) ); // Scale defines how the particle's size changes over its lifetime.

    private SceneCustomObject sceneObject;
    protected override void OnStart()
    {
        if ( !IsProxy )
            MaxLifetime = Lifetime; // Initialize MaxLifetime to the initial Lifetime value.
        sceneObject = new SceneCustomObject( Scene.SceneWorld );
        sceneObject.Position = WorldPosition; // Set the initial position of the scene object to the particle's world position.
        sceneObject.Flags.CastShadows = false; // Disable shadow casting for the particle.
        sceneObject.Flags.IsOpaque = true;
        sceneObject.Flags.IsTranslucent = false;

        sceneObject.RenderOverride = ( obj ) =>
        {
            int texID = TextureID;
            // Draw a tiny cube
            var size = Scale.Evaluate( 1f - (Lifetime / MaxLifetime) ) * 0.1f; // Use the initial scale to determine the size of the particle.
            var transform = new Transform( WorldPosition, Rotation.Identity, new Vector3( size, size, size ) );

            List<Vector3> verts = new();
            List<Vector3> uvs = new();
            List<Vector3> normals = new();

            Vertex[] vertex = new Vertex[6 * Directions.All.Count]; // 6 vertices per face, multiplied by the number of faces.
            int index = 0;

            var ttop = TextureRect.Top;
            var tbottom = TextureRect.Bottom;
            var tleft = TextureRect.Left;
            var tright = TextureRect.Right;

            foreach ( var dir in Directions.All ) // Each face
            {
                var forward = dir.Forward();
                var up = dir.Up();
                var right = dir.Right();

                vertex[index++] = new Vertex( forward + up + right, forward, Vector3.Zero, new Vector4( tright, ttop, texID, 0f ) );
                vertex[index++] = new Vertex( forward + up - right, forward, Vector3.Zero, new Vector4( tleft, ttop, texID, 0f ) );
                vertex[index++] = new Vertex( forward - up - right, forward, Vector3.Zero, new Vector4( tleft, tbottom, texID, 0f ) );
                vertex[index++] = new Vertex( forward + up + right, forward, Vector3.Zero, new Vector4( tright, ttop, texID, 0f ) );
                vertex[index++] = new Vertex( forward - up - right, forward, Vector3.Zero, new Vector4( tleft, tbottom, texID, 0f ) );
                vertex[index++] = new Vertex( forward - up + right, forward, Vector3.Zero, new Vector4( tright, tbottom, texID, 0f ) );
            }

            Graphics.Draw( vertex, vertex.Length, Material );
        };
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        sceneObject.Delete();
    }
    private const float MaxPushRadius = 30f;
    public const float MinPushRadius = 5f;
    public const float PushForce = 100f; // The force applied to push the particle away from the player.
    protected override void OnUpdate()
    {
        if ( !IsProxy )
        {

            Lifetime -= Time.Delta; // Decrease the lifetime by the time since the last update.
            if ( Lifetime <= 0 )
            {
                // If the lifetime is less than or equal to zero, we remove the particle.
                GameObject.Destroy();
                return;
            }

            Velocity += Acceleration * Time.Delta; // Update the velocity based on acceleration.

            Velocity *= Damping; // Apply damping to the velocity.

            // Get each player, and if the distance to the player is < 50 points, push the particle away from the player.
            foreach ( var player in Scene.GetAllComponents<VoxelPlayer>() )
            {
                var delta = WorldPosition - player.GameObject.GetBounds().Center;
                // Adjust the delta's z position to account for the player's height. This makes the player functionally a tall stick.
                var oldZ = delta.z;
                var diff = Math.Sign( delta.z ) * Math.Min( player.GameObject.GetBounds().Size.z * 0.5f, Math.Abs( delta.z ) );
                delta.z -= diff;
                var distance = MathF.Max( delta.Length, MinPushRadius );
                if ( distance < MaxPushRadius )
                {
                    var direction = delta.Normal;
                    // Push the particle away from the player.
                    var pushAmount = (MaxPushRadius - distance) / Time.Delta; // Calculate how much to push the particle away based on the distance.
                                                                              // Apply the push to the particle's velocity.
                    Velocity += direction * pushAmount; // Push the particle away with a force proportional to the distance.
                }
            }

            // Try to move the particle based on its velocity.
            // We do a basic cast
            var trace = Scene.Trace
                .Box( new Vector3( Scale.Evaluate( 1f - (Lifetime / MaxLifetime) ), Scale.Evaluate( 1f - (Lifetime / MaxLifetime) ), Scale.Evaluate( 1f - (Lifetime / MaxLifetime) ) ) * 0.2f, WorldPosition, WorldPosition + Velocity * Time.Delta )
                .WithoutTags( "player" )
                .Run();
            WorldPosition = trace.EndPosition;
            if ( trace.Hit )
            {
                // Cancel out our velocity along the normal of the hit surface.
                var normal = trace.Normal;
                Velocity -= normal * Vector3.Dot( Velocity, normal ) * 1.3f;
                Velocity *= 0.2f;
                WorldPosition += trace.Normal * 0.1f; // Move the particle slightly away from the surface to avoid sticking.
            }
        }

        sceneObject.Transform = WorldTransform; // Update the scene object's transform to match the particle's world position and rotation.
    }
}