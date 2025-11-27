using System;

public class WorldItem : Component, Component.ITriggerListener {
    public SceneCustomObject _so;

    public ItemStack stack = ItemStack.Empty;
    private bool Spent = false;

    [Property]
    public string ItemID {
        get => stack.ItemIdentifier;
        set {
            if ( stack.ItemIdentifier == value ) return;
            stack = new ItemStack { ItemIdentifier = value, Count = 1 };
        }
    }
    [Property]
    public int Count {
        get => stack.Count;
        set {
            if ( stack.Count == value ) return;
            stack = new ItemStack { ItemIdentifier = stack.ItemIdentifier, Count = value };
        }
    }

    public const float ShadowScale = 10f;

    private const float cosine = 0.86602540378f; // cos(30 degrees)

    // Offsets for the 6 unique locations of the places to render the items of a stack
    // These represent the log2 of the number of items in the stack
    private readonly static List<Vector3> RenderOffsets = [
        Vector3.Zero, // First item is always in the center
        new Vector3(5f, 5f, -5f),
        new Vector3(-5f, 5f, -6f),
        new Vector3(5f, -5f, -4f),
        new Vector3(-5f, -5f, -5f),
        new Vector3(5f, 5f, 5f),
        new Vector3(-5f, 5f, 4f)
    ];

    protected override void OnStart() {
        base.OnStart();
        _so = new SceneCustomObject( Scene.SceneWorld );
        _so.Flags.CastShadows = true;
        _so.Flags.IsOpaque = true;
        _so.Flags.IsTranslucent = true;

        _so.RenderOverride = ( obj ) => {
            var item = ItemRegistry.GetItem( stack.ItemIdentifier );
            for ( int i = 0; i < Math.Min( RenderOffsets.Count, Math.Log2( stack.Count ) + 1 ); i++ ) {
                var offset = RenderOffsets[i];
                item.Render( global::Transform.Zero.WithPosition( offset ) );
            }

            // BlockTrace directly down for a drop shadow
            var trace = Scene.GetAll<WorldThinker>().First().World.Trace(
                WorldPosition,
                WorldPosition - Vector3.Up * World.BlockScale
            ).Run();
            if ( trace.Hit ) {
                float shadowScale = Math.Clamp( ShadowScale - (trace.Distance * World.BlockScale / 10f), 0f, ShadowScale );
                List<Vertex> verts = [
                    new Vertex(){
                        Position = _so.Transform.PointToLocal(trace.EndPosition + Vector3.Up * 0.5f + Vector3.Forward * shadowScale * 2f * 0.8f),
                        Normal = Vector3.Up,
                        Color = Color.White.WithAlpha( 1f ),
                        TexCoord0 = new Vector2( 0.5f, 1.5f )
                    },
                    new Vertex(){
                        Position = _so.Transform.PointToLocal(trace.EndPosition + Vector3.Up * 0.5f + Vector3.Backward * shadowScale * 2f * 0.7f - Vector3.Right * shadowScale * 2f * cosine),
                        Normal = Vector3.Up,
                        Color = Color.White.WithAlpha( 1f ),
                        TexCoord0 = new Vector2( 0.5f-cosine, 0f )
                    },
                    new Vertex(){
                        Position = _so.Transform.PointToLocal(trace.EndPosition + Vector3.Up * 0.5f + Vector3.Backward * shadowScale * 2f * 0.7f + Vector3.Right * shadowScale * 2f * cosine),
                        Normal = Vector3.Up,
                        Color = Color.White.WithAlpha( 1f ),
                        TexCoord0 = new Vector2( 0.5f+cosine, -0.5f )
                    },
                ];
                Graphics.Draw( verts, verts.Count, Material.Load( "materials/dropshadow.vmat" ) );
            }
        };

        var boxCollider = GetOrAddComponent<BoxCollider>();
        boxCollider.Scale = new Vector3( 10f, 10f, 10f );

        var Rigidbody = GetOrAddComponent<Rigidbody>();
        Rigidbody.LinearDamping = 2f; // Damping to slow down the item
        GameObject.Tags.Add( "item" );

    }

    protected override void OnUpdate() {
        var t = global::Transform.Zero
            .WithPosition( new Vector3( -5f, -5f, -5f ) );
        _so.Transform = WorldTransform.ToWorld( t );

    }

    protected override void OnFixedUpdate() {
        base.OnFixedUpdate();
        if ( Spent ) return;

        // Ground trace up to 0.5f blocks
        var trace = Scene.GetAll<WorldThinker>().First().World.Trace(
            WorldPosition,
            WorldPosition + Vector3.Down * 0.5f * World.BlockScale
        ).Run();

        if ( trace.Hit && trace.Distance < 0.5f ) {
            // Apply a soft upwards force to make us hover
            var Rigidbody = GetComponent<Rigidbody>();
            if ( Rigidbody != null ) {
                Rigidbody.Velocity += (Vector3.Up * 8f) / trace.Distance; // Soft upwards force
            }
        }

        // If we're within 3 blocks of the center of a player, hover towards them
        var player = VoxelPlayer.LocalPlayer;
        if ( player.IsValid() ) {
            var playerPos = player.GameObject.GetBounds().Center;
            var direction = (playerPos - WorldPosition).Normal;
            var distance = (playerPos - WorldPosition).Length;

            if ( distance < 1f * World.BlockScale ) {
                // Pick us up if we're close enough
                var inv = player.inventory;
                if ( ItemStack.IsNullOrEmpty( inv.PutInFirstAvailableSlot( stack ) ) ) {
                    Spent = true;
                    GameObject.Destroy(); // Destroy the item if it was picked up
                }
                return;
            } else if ( distance < 3f * World.BlockScale ) {
                var Rigidbody = GetComponent<Rigidbody>();
                if ( Rigidbody != null ) {
                    Rigidbody.Velocity += direction * 50f; // Move towards the player at a constant speed
                }
                return;
            }
        }

        // Otherwise, float to the center of all items in a 1.5 block radius
        var items = Scene.GetAll<WorldItem>()
            .Where( i => (i.WorldPosition - WorldPosition).Length < 3f * World.BlockScale )
            .ToList();
        if ( items.Count > 1 ) {
            var center = items.Aggregate( Vector3.Zero, ( acc, item ) => acc + item.WorldPosition ) / items.Count;
            var direction = (center - WorldPosition).Normal;
            var distance = (center - WorldPosition).Length;


            if ( distance < 0.3 * World.BlockScale ) {
                // Destroy all other items in the same radius
                foreach ( var item in items ) {
                    if ( item != this ) {
                        if ( ItemStack.IsNullOrEmpty( stack.Merge( item.stack ) ) ) {
                            item.Spent = true; // Mark as spent to prevent further processing
                            item.GameObject.Destroy();
                        }
                    }
                }

            } else if ( distance < 1.5f * World.BlockScale ) {
                var Rigidbody = GetComponent<Rigidbody>();
                if ( Rigidbody != null ) {
                    Rigidbody.Velocity += direction * 5f; // Move towards the center at a constant speed
                }
            }
        }


    }

    protected override void OnDestroy() {
        base.OnDestroy();
        if ( _so != null ) {
            _so.Delete();
            _so = null;
        }
    }


}
