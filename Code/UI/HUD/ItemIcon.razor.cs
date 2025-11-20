using System;
using Sandbox.UI;

namespace VoxelParty.UI;

public partial class ItemIcon : Panel
{
    public Inventory Inventory => VoxelPlayer.LocalPlayer.inventory;
    public int Slot { get; set; } = -1;

    public Texture RenderTexture;
    public ItemStack ItemStack = ItemStack.Empty;

    public override void OnDeleted()
    {
        RenderTexture?.Dispose();
        base.OnDeleted();
    }

    public void Render()
    {
        var inv = Inventory;
        if ( ItemStack.IsNullOrEmpty( ItemStack ) )
            return;
        var item = ItemStack.Item;
        if ( item == null ) return;
        RenderTexture = Texture.CreateRenderTarget( "Item", ImageFormat.RGBA8888, new Vector2( 100, 100 ) );
        Scene scene = new Scene();
        using ( scene.Push() )
        {
            var camera = new GameObject().AddComponent<CameraComponent>();
            camera.Orthographic = true;
            camera.OrthographicHeight = (Screen.Height / 1080f) * 240f;
            camera.WorldPosition = new Vector3( -50, 0, 0 );
            camera.WorldRotation = Rotation.From( 0, 0, 0 );
            camera.BackgroundColor = Color.Transparent;
            camera.RenderTarget = RenderTexture;

            var light = new GameObject().AddComponent<DirectionalLight>();
            light.LightColor = Color.White * 2f;
            light.WorldRotation = Rotation.From( 45, -45, 0 );

            SceneCustomObject _so = null;
            if ( item.Block.BlockObject != null )
            {
                var obj = item.Block.BlockObject.Clone( new Vector3( 0, 0, 0 ), Rotation.FromAxis( Vector3.Right, 35f ) * Rotation.FromAxis( Vector3.Up, 45 ) );
                camera.WorldPosition = new Vector3( -50 * World.BlockScale, 20f * (float)Math.Sqrt( 2f ), 20f * MathF.Sin( 35f / (180f / MathF.PI) ) );
                camera.OrthographicHeight *= 4;
                light.WorldRotation = Rotation.From( 45, 0, 0 );
            }
            else
            {
                _so = new SceneCustomObject( scene.SceneWorld );
                _so.Transform = Transform.Zero;
                _so.Flags.CastShadows = false;
                _so.Flags.IsOpaque = true;
                _so.Flags.IsTranslucent = false;
                _so.RenderOverride = ( obj ) =>
                {
                    item.Render( Transform.Zero.WithPosition( new Vector3( 0f, -7f, -4f ) ).WithRotation( Rotation.FromAxis( Vector3.Right, 35f ) * Rotation.FromAxis( Vector3.Up, 45 ) ) );
                };
            }

            camera.RenderToTexture( RenderTexture );
            _so?.Delete();
        }
        scene.Destroy();
    }

    ItemStack lastKnownStack = ItemStack.Empty;
    public override void Tick()
    {
        if ( VoxelPlayer.LocalPlayer is null ) return;
        if ( Slot != -1 )
            ItemStack = Inventory.GetItem( Slot );
        if ( RenderTexture == null || ItemStack.Count != lastKnownStack?.Count || ItemStack.ItemID != lastKnownStack?.ItemID )
        {
            lastKnownStack = ItemStack;
            Render();
        }
        if ( ItemStack.IsNullOrEmpty( ItemStack ) )
        {
            Style.BackgroundImage = null;
            return;
        }
        Style.BackgroundImage = RenderTexture;
    }

    protected override int BuildHash() => ItemStack.Count;
}
