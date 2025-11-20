using System;
using System.Threading.Tasks;

public static class TexArrayTool
{
    public static bool Dirty = true;
    public static async Task UpdateMaterialTexture( Material material )
    {
        if ( !Dirty && last != null && last.IsValid() )
        {
            material.Set( "Abledo", last );
            return;
        }

        //Log.Info( "Updating material texture with texture atlas." );
        material.Set( "Abledo", await BuildTextureArray() );
        Dirty = false;
    }

    private static Texture last;

    public static async Task<Texture> BuildTextureArray()
    {
        if ( last != null )
        {
            last.Dispose();
            last = null; // Clear the last texture to allow for garbage collection.
        }
        List<Texture> textures = new List<Texture>();

        async Task<int?> TryAdd( Texture tex, string sourceID )
        {
            if ( tex == null )
            {
                return null;
            }
            if ( tex.IsError )
            {
                Log.Warning( $"Bad Texture attempted to load for block {sourceID}: {tex.ResourceName}" );
                return null;
            }
            while ( !tex.IsLoaded )
                await GameTask.Delay( 1 );
            if ( tex.IsError )
            {
                Log.Warning( $"Bad Texture attempted to load for block {sourceID}: {tex.ResourceName}" );
                return null;
            }
            if ( textures.Contains( tex ) )
            {
                return textures.IndexOf( tex );
            }
            textures.Add( tex );
            return textures.Count - 1; // Return the index of the newly added texture.
        }
        var items = ResourceLibrary.GetAll<Item>().Where( c => c.Block != null );
        if ( Game.IsEditor ) items = items.OrderBy( c => c.ID ); // Sort by ID in editor for consistency.
        foreach ( var item in items )
        {
            // For each block texture, add it to the list and set the index in the atlas.
            // If it already is in the atlas, skip it, but set the index.
            item.Block.TextureIndex = await TryAdd( item.Block.Texture, item.Name ) ?? 0;
            item.Block.TopTextureIndex = await TryAdd( item.Block.TopTexture, item.Name );
            item.Block.SideTextureIndex = await TryAdd( item.Block.SideTexture, item.Name );
            item.Block.NorthTextureIndex = await TryAdd( item.Block.NorthTexture, item.Name );
            item.Block.SouthTextureIndex = await TryAdd( item.Block.SouthTexture, item.Name );
            item.Block.EastTextureIndex = await TryAdd( item.Block.EastTexture, item.Name );
            item.Block.WestTextureIndex = await TryAdd( item.Block.WestTexture, item.Name );
            item.Block.BottomTextureIndex = await TryAdd( item.Block.BottomTexture, item.Name );
        }

        int tilesWide = 1;
        while ( Math.Pow( Math.Pow( 2, tilesWide ), 2 ) < textures.Count )
        {
            tilesWide++;
        }

        tilesWide = (int)Math.Pow( 2, tilesWide );

        int pixelsWide = 16 * tilesWide;

        last = Texture.Create( pixelsWide, pixelsWide )
            .WithFormat( ImageFormat.RGBA8888 )
            .WithName( "TextureAtlas" )
            .Finish();
        
        for ( int i = 0; i < textures.Count; i++ )
        { 
            if ( textures[i] != null )
            {
                if ( textures[i].GetPixels().Length * 4 != 1024 )
                {
                    Log.Warning( $"Texture at index {i} has invalid size {textures[i].Width}x{textures[i].Height}, expected 16x16." );
                    Log.Warning( $"Texture Resource Name: {textures[i].ResourceId} : ${textures[i].ResourceName} : ${textures[i].ResourcePath}" );
                    continue;
                }
                last.Update( textures[i].GetPixels().SelectMany( p => new[] { p.r, p.g, p.b, p.a } ).ToArray(), (i % tilesWide) * 16, (i / tilesWide) * 16, 16, 16 );
            }
        }
        last.MarkUsed();
        return last;
    }
}
