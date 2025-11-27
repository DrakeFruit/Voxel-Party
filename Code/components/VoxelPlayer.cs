
using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Sandbox.UI;
using Sandbox.Utility;
using VoxelParty.UI;

public partial class VoxelPlayer : Component
{
    World world => World.Active;

    [Sync( SyncFlags.FromHost )]
    public byte[] InventoryData
    {
        get => inventory.Serialize().ToArray();
        set
        {
            if ( value == null || value.Length == 0 )
                return; // Do not deserialize if the value is null or empty
            inventory.Deserialize( value );
        }
    }

    public Inventory inventory = new();
    [Property, ReadOnly, Group( "InventoryDebug" )] public List<int> InventoryItems => inventory.Items.ConvertAll( item => item.Count );

    [Property] public bool CreativeMode { get; set; } = false;
    [Property] public bool GiveBrokenBlocks { get; set; } = false;
    [Property] public bool HasInventory { get; set; } = false;
    [Property] public bool HasCreativeInventory { get; set; } = false;
    [Property] public bool HasHotbar { get; set; } = true;
    public bool ShowInventory { get; set; } = false;

    [Property, Alias( "Reach Distance" ), Description( "In blocks, not inches" )]
    public float ReachDistanceProperty { get; set; } = 3.5f;
    public float ReachDistance => ReachDistanceProperty * World.BlockScale;

    [RequireComponent] PlayerController Controller { get; set; }

    public static VoxelPlayer LocalPlayer { get; set; }

    public int BreakingProgress = 0;
    public TimeSince BreakTime = 0;
    public TimeSince TimeSinceLastBreak = 0;
    public Vector3Int? BreakingBlock;
    public Vector3Int? LastBreakingBlock;
    public Direction BreakingFace = Direction.None;
    public Direction LastBreakingFace = Direction.None;
    public Item GroundBlockL = null;
    public Item GroundBlockR = null;
    public bool IsFlying;
    SceneCustomObject blockBreakEffect;

    public int IslandIndex = 0;

    // Gamemode stuff
    [Sync( SyncFlags.FromHost )] public bool HasBuildVolume { get; set; } = false;
    [Sync( SyncFlags.FromHost )] public Vector3Int BuildAreaMins { get; set; } = Vector3Int.Zero;
    [Sync( SyncFlags.FromHost )] public Vector3Int BuildAreaMaxs { get; set; } = Vector3Int.Zero;
    [Property][Sync( SyncFlags.FromHost )] public bool CanBuild { get; set; } = false;

    [Sync( SyncFlags.FromHost )] public int TotalBlockArea { get; set; } = 0; // Total area of the blocks in the build area, used for gamemode scoring
    [Sync( SyncFlags.FromHost )] public int CorrectBlocksPlaced { get; set; } = 0; // Number of blocks placed correctly by the player, used for gamemode scoring
    [Sync( SyncFlags.FromHost )] public int IncorrectBlocksPlaced { get; set; } = 0; // Number of blocks placed incorrectly by the player, used for gamemode scoring

    [Sync( SyncFlags.FromHost )] public bool TextBoxVisible { get; set; } = false;
    [Sync] public string TextBoxValue { get; set; } = "";

    [Sync( SyncFlags.FromHost )] public string SpecialMessage { get; set; } = null;

    [Sync] public bool IsReady { get; set; } = false; // Whether the player is ready for the gamemode to start

    private bool _specator = false;
    [Sync( SyncFlags.FromHost )]
    [Property]
    public bool Spectator
    {
        get
        {
            return _specator;
        }
        set
        {
            _specator = value;
            var pc = GetComponent<PlayerController>();
            if ( pc == null )
            {
                if ( value ) throw new Exception( "PlayerControler doesn't exist but we tried to set Spectator!" );
                return;
            }
            if ( value )
            {
                GameObject.Tags.Add( "spectator" );
                pc.BodyCollisionTags ??= new TagSet();
                pc.BodyCollisionTags.Add( "spectator" );
            }
            else
            {
                GameObject.Tags.Remove( "spectator" );
                pc.BodyCollisionTags ??= new TagSet();
                pc.BodyCollisionTags.Remove( "spectator" );
            }
        }
    }

    protected override void OnStart()
    {
        LocalPlayer = Scene.GetAllComponents<VoxelPlayer>().FirstOrDefault( x => x.Network.Owner == Connection.Local );

        if ( !IsProxy )
            LoadSkin();

        UpdateSkin();

        SpawnBlockBreakingEffect();
    }

    async void LoadSkin()
    {
        if ( CharacterCreator.Skins == null || CharacterCreator.Skins.Count == 0 )
            await CharacterCreator.Initialize();
        var curSkin = CharacterCreator.Skins[CharacterCreator.Selected];
        PlayerSkin = curSkin.BaseSkinName ?? $"!{curSkin.Username}";
    }

    [Property, Sync, Change] public string PlayerSkin { get; set; } = "";

    public static async Task<Texture> GetTextureFromSkin( string skinName )
    {
        skinName = new Regex( @"\W" ).Replace( skinName.ToLower(), "" );
        if ( string.IsNullOrWhiteSpace( skinName ) )
            return null;
        if ( !FileSystem.Data.DirectoryExists( "skins" ) )
        {
            FileSystem.Data.CreateDirectory( "skins" );
        }
        // Check the Data folder for a PNG
        var filename = $"skins/{skinName}.png";
        if ( FileSystem.Data.FileExists( filename ) )
        {
            return Texture.Load( FileSystem.Data, filename );
        }

        var tex = await Texture.LoadAsync( null, $"https://mineskin.eu/skin/{skinName.Trim()}", false );
        // Apply a CRC to the tex's data.
        var crc = Crc32.FromBytes(
            tex.GetPixels( 0 ).SelectMany( p => new[] { p.r, p.g, p.b, p.a } )
        );
        Log.Info( crc );
        if ( crc == 3371258681 || crc == 1327572573 || crc == 120576859 ) // REFUSE to put on the default skin
        {
            return null;
        }

        if ( tex == null )
        {
            Log.Warning( "No texture loaded!" );
            return null;
        }

        if ( tex.IsError )
        {
            Log.Warning( "Texture errored when loading!" );
            return null;
        }

        if ( tex.Size.y == 32 )
        {
            if ( tex.Size.x != 64 )
            {
                Log.Warning( $"Texture is unexpected size! (Wanted 64x64 or 64x32, got {tex.Size.x}x{tex.Size.y})" );
                return null;
            }
            // Copy the data from the oldTex to the newTex
            int w = 64;
            int h = 32;
            byte[] pixels = new byte[w * h * 4];
            pixels = tex.GetPixels().SelectMany( c => new[] { c.r, c.g, c.b, c.a } ).ToArray();
            byte[] paddedData = new byte[64 * 64 * 4];
            pixels.CopyTo( paddedData, 0 );

            // I hate to do this, but I need to slowly copy regions.
            // There is no fast way to do this
            void CopyRegion( int srcX, int srcY, int width, int height, int destX, int destY )
            {
                for ( int y = 0; y < height; y++ )
                {
                    Buffer.BlockCopy( pixels, ((srcY + y) * 64 + srcX) * 4, paddedData, ((destY + y) * 64 + destX) * 4, width * 4 );
                }
            }
            CopyRegion( 0, 16, 16, 16, 16, 48 ); // Copy leg into left left
            CopyRegion( 40, 16, 16, 16, 32, 48 ); // Copy arm into left arm

            var newTex = Texture.Create( 64, 64, ImageFormat.RGBA8888 )
                .WithData( paddedData )
                .Finish();
            //tex.Dispose();
            tex = newTex;
        }
        if ( tex.Size.x != 64 || tex.Size.y != 64 )
        {
            Log.Warning( $"Texture is unexpected size! (Wanted 64x64 or 64x32, got {tex.Size.x}x{tex.Size.y})" );
            return null;
        }

        // Save the tex as a png.

        var f = FileSystem.Data.OpenWrite( filename );
        f.Write( tex.GetBitmap( 0 ).ToPng() );
        f.Close();

        return tex;
    }

    public void OnPlayerSkinChanged( string oldSkin, string newSkin )
    {
        UpdateSkin();
    }

    [Button]
    public async void UpdateSkin()
    {
        if ( string.IsNullOrWhiteSpace( PlayerSkin ) ) return;
        Texture tex;
        Log.Info( $"Trying to update skin: {PlayerSkin}" );
        if ( PlayerSkin.StartsWith( "!" ) )
        {
            tex = await GetTextureFromSkin( PlayerSkin.Substring( 1 ) );
        }
        else
        {
            tex = await Texture.LoadAsync( FileSystem.Mounted, $"materials/models/skins/{PlayerSkin}" );
            Log.Info( $"Trying to load as base skin. {tex} {tex.IsLoaded} {tex.IsError}" );
        }

        var smr = GetComponentInChildren<SkinnedModelRenderer>();
        smr.MaterialOverride ??= smr.Model.Materials.First().CreateCopy();
        smr.MaterialOverride.Set( "Albedo", tex );
    }

    public void Explode()
    {
        // TODO: Sound, Blood?
        Spectator = true;
        HasBuildVolume = false;
        CanBuild = false;
    }

    public TimeSince TimeSinceLastJump { get; set; } = 0;

    protected override void OnUpdate()
    {



        if ( Input.Pressed( "use" ) ) IsReady = !IsReady;

        if ( Input.Pressed( "jump" ) && TimeSinceLastJump.Relative < .25 && !Controller.IsOnGround ) IsFlying = !IsFlying;
        if ( Input.Pressed( "jump" ) ) TimeSinceLastJump = 0;
        if ( Controller.IsOnGround ) IsFlying = false;
        if ( Spectator )
            IsFlying = true;
    }

    protected override void OnFixedUpdate()
    {
        if ( IsProxy )
            return;

        // Check for stuck
        var pc = GetComponent<PlayerController>();
        if ( pc.TraceBody( WorldPosition + Vector3.Up, WorldPosition + Vector3.Up, 1f, 0.5f ).StartedSolid )
            WorldPosition += Vector3.Up * 20f;


        if ( CanBuild )
        {
            HandleBreak();
            HandlePlace();
        }
        HandleHotbar();
        DoFootsteps();
    }

    protected override void OnPreRender()
    {
        if ( !ShowInventory && !TextBoxVisible )
        {
            ShowHoveredFace();

            if ( !IsProxy && HasBuildVolume )
            {
                Gizmo.Draw.Color = Color.Green.WithAlpha( 0.5f );
                Gizmo.Draw.LineThickness = 8f;
                var bbox = BBox.FromPoints( new[]{
                BuildAreaMins * World.BlockScale,
                (BuildAreaMaxs + Vector3.One) * World.BlockScale
            } );
                Gizmo.Draw.LineBBox( bbox );
            }
        }
    }

    public BlockTraceResult EyeTrace()
    {
        var pc = GetComponent<PlayerController>();
        if ( Scene == null || Scene.Camera == null ) return default;
        return EyeTrace( Scene.Camera.WorldPosition, Scene.Camera.WorldRotation.Forward );
    }

    public BlockTraceResult EyeTrace( Vector3 eyePos, Vector3 eyeForward )
    {
        var trace = Scene.GetAll<WorldThinker>().First().World.Trace(
            eyePos,
            eyePos + eyeForward * ReachDistance
        ).Run();

        if ( HasBuildVolume )
        {
            var ray = new Ray(
                eyePos,
                eyeForward
            );

            var walls = new (float distance, Direction face)[]{
                (BuildAreaMins.x, Direction.South),
                (BuildAreaMaxs.x+1f, Direction.North),
                (BuildAreaMins.y, Direction.East),
                (BuildAreaMaxs.y+1f, Direction.West),
                (BuildAreaMins.z, Direction.Down),
                (BuildAreaMaxs.z+1f, Direction.Up)
                // This big ugly function turns the plane distance for each bound into a casted point. if it ever does actually hit.
            }.Where( w => w.face.Forward().Dot( ray.Forward ) > 0 ) // Only consider faces that are facing the ray direction
                .Select( distanceface => (new Plane( distanceface.face.Forward().Abs(), distanceface.distance * World.BlockScale ).Trace( ray, true, ReachDistance * World.BlockScale ), distanceface.face) )
                .Where( x => x.Item1.HasValue )
                .Select( p => (p.Item1.Value, p.Item2, p.Item1.Value.Distance( ray.Position )) )
                .Where( wall =>
                {
                    // Check that this point is within the bounds of the build area
                    var pos = (wall.Item1 / World.BlockScale).Floor() - wall.Item2.Forward();
                    return pos.x >= BuildAreaMins.x && pos.x <= BuildAreaMaxs.x &&
                           pos.y >= BuildAreaMins.y && pos.y <= BuildAreaMaxs.y &&
                           pos.z >= BuildAreaMins.z && pos.z <= BuildAreaMaxs.z;
                } );
            if ( !walls.Any( c => c.Item3 < (trace.Distance * World.BlockScale) && c.Item3 < ReachDistance * World.BlockScale ) )
                return trace; // If no walls are closer than the trace, return the trace as is.
            var closestWall = walls.OrderBy( c => c.Item3 ).First();
            // Mutate the trace to hit the closest wall instead of the block.
            trace.Hit = true;
            trace.HitBlockPosition = (closestWall.Item1 / World.BlockScale + Vector3.One / 128f).Floor() + closestWall.Item2.Forward();
            if ( closestWall.Item2 is Direction.North or Direction.West or Direction.Up )
            {
                trace.HitBlockPosition -= closestWall.Item2.Forward();
            }
            trace.HitFace = closestWall.Item2.Flip();
            trace.Distance = closestWall.Item3;
            trace.EndPosition = closestWall.Item1;

            return trace;
        }

        return trace;
    }

    [Rpc.Broadcast]
    public void SwapSlots( int slot1, int slot2 )
    {
        var item1 = inventory.GetItem( slot1 );
        var item2 = inventory.GetItem( slot2 );
        inventory.SetItem( slot1, item2 );
        inventory.SetItem( slot2, item1 );
    }

    [Rpc.Broadcast]
    public void SetSlot( int slot, ItemStack item )
    {
        inventory.SetItem( slot, item );
    }


    public static int SelectedSlot = 0;
    public void SpawnBlockBreakingEffect()
    {
        blockBreakEffect = new SceneCustomObject( Scene.SceneWorld );
        blockBreakEffect.Flags.CastShadows = false;
        blockBreakEffect.Flags.IsOpaque = false;
        blockBreakEffect.Flags.IsTranslucent = true;
        blockBreakEffect.RenderOverride = ( obj ) =>
        {
            if ( BreakingBlock == null )
                return;
            if ( world == null ) return;
            var block = world.GetBlock( BreakingBlock.Value ).GetBlock();
            var pos = (BreakingBlock.Value + Vector3.One * 0.5f) * World.BlockScale;
            pos += BreakingFace.Forward() * World.BlockScale * 0.501f; // Offset slightly to avoid z-fighting
            pos -= obj.Position;
            var scale = World.BlockScale; // Scale the effect to half the block size
            var right = BreakingFace.Right();
            var up = BreakingFace.Up();
            var p1 = pos - right * scale / 2f - up * scale / 2f;
            var p2 = pos + right * scale / 2f - up * scale / 2f;
            var p3 = p1 + up * scale;
            var p4 = p2 + up * scale;
            int progress = Math.Min( (int)(BreakTime * 20 / block.Hardness), 9 ); // TODO: based on BreakTime
            Vector2Int textureIndex = new Vector2Int( 6 + progress, 15 );
            var rect = Rect.FromPoints( textureIndex / 16f + Vector2.One / 160f, textureIndex / 16f + Vector2.One / 16f - Vector2.One / 160f ); // Assuming a texture atlas of 16x16, each tile is 0.0625 in UV space
            var v1 = new Vertex( p1, rect.BottomLeft, Color.White );
            var v2 = new Vertex( p2, rect.BottomRight, Color.White );
            var v3 = new Vertex( p3, rect.TopLeft, Color.White );
            var v4 = new Vertex( p4, rect.TopRight, Color.White );
            blockBreakEffect.ColorTint = Color.White.WithAlpha( 1f );
            Graphics.Draw( new List<Vertex> { v1, v2, v3, v4 }, 4, Material.Load( "materials/textureatlastranslucent.vmat" ), new RenderAttributes(), Graphics.PrimitiveType.TriangleStrip );
        };
    }
    public void HandleBreak()
    {
        var trace = EyeTrace();
        if ( CreativeMode && TimeSinceLastBreak.Relative < 0.2f )
        {
            return;
        }
        if ( !trace.Hit || !Input.Down( "Attack1" ) )
        {
            BreakingBlock = null;
            LastBreakingBlock = null;
            BreakingFace = Direction.None;
            LastBreakingFace = Direction.None;
            BreakTime = 0f;

            return;
        }

        // If trace.HitBlockPosition is not in the player's build area, do not break blocks
        var hitPos = trace.HitBlockPosition;
        if ( HasBuildVolume && (hitPos.x < BuildAreaMins.x || hitPos.y < BuildAreaMins.y || hitPos.z < BuildAreaMins.z || hitPos.x > BuildAreaMaxs.x || hitPos.y > BuildAreaMaxs.y || hitPos.z > BuildAreaMaxs.z) )
        {
            BreakingBlock = null;
            LastBreakingBlock = null;
            BreakingFace = Direction.None;
            LastBreakingFace = Direction.None;
            BreakTime = 0f;

            return;
        }

        BreakingBlock = trace.HitBlockPosition;
        //TODO: fix this shit
        // if ( LastBreakingBlock != BreakingBlock || LastBreakingFace != BreakingFace )
        // {
        //  BreakingBlock = null;
        //  BreakingFace = trace.HitFace;
        //  BreakTime = 0f;
        //  
        //  return;
        // }

        if ( !CreativeMode )
        {
            blockBreakEffect.Transform = global::Transform.Zero.WithPosition( WorldPosition );
            var block = world.GetBlock( BreakingBlock.Value ).GetBlock();
            BreakingProgress = Math.Min( (int)(BreakTime * 20 / block.Hardness), 10 );
        }

        if ( (BreakingProgress == 10 || CreativeMode) && BreakingBlock.HasValue )
        {
            if ( !GiveBrokenBlocks )
            {
                world.Thinker.BreakBlock( BreakingBlock.Value, world.GetBlock( BreakingBlock.Value ) );
            }
            else
            {
                BreakAndGive( BreakingBlock.Value, world.GetBlock( BreakingBlock.Value ) );
            }
        }

        LastBreakingBlock = BreakingBlock;
        LastBreakingFace = BreakingFace;
        TimeSinceLastBreak = 0;
    }

    [Rpc.Broadcast]
    public void BreakAndGive( Vector3Int Position, BlockData expectedBlock )
    {
        var i = inventory.PutInFirstAvailableSlot( new ItemStack( ItemRegistry.GetItem( Position ) ) );
        world.Thinker.BreakBlock( Position, expectedBlock, false );
    }

    public void HandlePlace()
    {
        if ( Input.Pressed( "Attack2" ) )
        {
            TryPlace( Scene.Camera.WorldPosition, Scene.Camera.WorldRotation.Forward, SelectedSlot );
        }
    }


    [Rpc.Broadcast]
    public void NetPlace( Vector3Int BlockPos, BlockData data, int selectedSlot )
    {
        // Check that we CAN place here, and if so, decremen the item in the slot
        if ( !world.GetBlock( BlockPos ).GetBlock().Replaceable ) // If we can't put a block here, give up.
            return;
        if ( HasBuildVolume && (BlockPos.x < BuildAreaMins.x || BlockPos.y < BuildAreaMins.y || BlockPos.z < BuildAreaMins.z || BlockPos.x > BuildAreaMaxs.x || BlockPos.y > BuildAreaMaxs.y || BlockPos.z > BuildAreaMaxs.z) )
        {
            return;
        }
        inventory.TakeItem( selectedSlot, 1 ); // Remove one item from the hotbar slot
        if ( Networking.IsHost )
            world.Thinker.PlaceBlock( BlockPos, data );
        else
            world.SetBlock( BlockPos, data );
    }

    public void TryPlace( Vector3 eyePos, Vector3 eyeForward, int selectedSlot )
    {
        var item = inventory.GetItem( selectedSlot );
        if ( ItemStack.IsNullOrEmpty( item ) )
            return;
        if ( item.Item.Block == null )
        {
            return; // TODO: Other item use actions, other placement styles?
        }
        var trace = EyeTrace( eyePos, eyeForward );
        if ( !trace.Hit )
            return;
        var placePos = trace.HitBlockPosition + trace.HitFace.Forward();
        if ( world.GetBlock( trace.HitBlockPosition ).GetBlock().Replaceable && !world.GetBlock( trace.HitBlockPosition ).IsEmpty() ) // Grass and things can have blocks replace them, and should do so if you try and place on top of them.
        {
            placePos = trace.HitBlockPosition;
        }

        if ( !world.GetBlock( placePos ).GetBlock().Replaceable ) // If we can't put a block here, give up.
            return;

        if ( Scene.Trace.Box( new BBox( Vector3.Zero, Vector3.One * World.BlockScale ), new Ray( placePos * World.BlockScale, Vector3.Up ), 0f ).WithTag( "player" ).Run().StartedSolid )
            return; // Fail placing where player is.

        // If placePos is not in the player's build area, do not place blocks
        if ( HasBuildVolume && (placePos.x < BuildAreaMins.x || placePos.y < BuildAreaMins.y || placePos.z < BuildAreaMins.z || placePos.x > BuildAreaMaxs.x || placePos.y > BuildAreaMaxs.y || placePos.z > BuildAreaMaxs.z) )
        {
            return;
        }
        var pc = GetComponent<PlayerController>();
        var newData = BlockData.WithPlacementBlockData( item.Item.Identifier, trace.HitFace, Scene.Camera.WorldRotation.Forward );
        NetPlace( placePos, newData, selectedSlot );

    }

    public void ShowHoveredFace()
    {
        if ( IsProxy ) return;
        var trace = EyeTrace();
        if ( !trace.Hit )
            return;

        var blockPosition = trace.HitBlockPosition;
        var faceDirection = trace.HitFace;

        // Draw a box on the face hit
        Gizmo.Draw.Color = Color.Black;
        var block = world.GetBlock( blockPosition ).GetBlock();
        var box = BBox.FromBoxes( block.GetCollisionAABBWorld( world, blockPosition ) ).Grow( 0.1f );
        switch ( faceDirection )
        {
            case Direction.North: box.Mins.x = box.Maxs.x; break;
            case Direction.South: box.Maxs.x = box.Mins.x; break;
            case Direction.East: box.Maxs.y = box.Mins.y; break;
            case Direction.West: box.Mins.y = box.Maxs.y; break;
            case Direction.Down: box.Maxs.z = box.Mins.z; break;
            case Direction.Up: box.Mins.z = box.Maxs.z; break;
        }
        Gizmo.Draw.Color = Color.Black;
        Gizmo.Draw.LineThickness = 2f;
        Gizmo.Draw.LineBBox( box );
    }

    public void HandleHotbar()
    {
        for ( int i = inventory.InventorySize; i < inventory.TotalSize; i++ )
        {
            if ( Input.Pressed( $"Slot{(i - inventory.InventorySize) + 1}" ) )
                SelectedSlot = i;
        }

        SelectedSlot += -Input.MouseWheel.y.FloorToInt().Clamp( -1, 1 );
        SelectedSlot = SelectedSlot.Clamp( inventory.InventorySize, inventory.InventorySize + inventory.HotbarSize - 1 );
    }

    [Rpc.Owner]
    public void MoveTo( Transform transform )
    {
        WorldPosition = transform.Position;
        WorldRotation = transform.Rotation;
        GameObject.Children.First().WorldRotation = transform.Rotation;
        GetComponent<PlayerController>().EyeAngles = transform.Rotation.Angles();
        Transform.ClearInterpolation();
        IsFlying = false;
    }
    [Rpc.Owner]
    public void MoveCameraTo( Transform transform, bool showViewer )
    {
        Scene.Camera.WorldPosition = transform.Position;
        Scene.Camera.WorldRotation = transform.Rotation;
        if ( showViewer )
            Scene.Camera.RenderExcludeTags.Remove( "viewer" );
    }
    // Request to lock this player, disabling movement and camera.
    [Rpc.Owner]
    public void Lock()
    {
        Enabled = false;

        var pc = GetComponent<PlayerController>();
        pc.UseCameraControls = false;
    }
    [Rpc.Owner]
    public void MakeFlying()
    {
        IsFlying = true;
    }
}
