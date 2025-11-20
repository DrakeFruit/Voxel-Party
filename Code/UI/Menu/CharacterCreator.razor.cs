using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sandbox.UI;

namespace VoxelParty.UI;

public partial class CharacterCreator : Panel
{
    public static List<Skin> Skins { get; set; } = new();
    private static int _selected { get; set; }
    public static int Selected
    {
        get
        {
            return _selected;
        }
        set
        {
            _selected = value;
            FileSystem.Data.WriteAllText( "selectedskin.txt", _selected.ToString() );
        }
    }
    public TextEntry Name { get; set; }

    public class Skin
    {
        public string Username { get; set; }
        public string BaseSkinName { get; set; }
        public Texture Texture;
    }

    public static async Task Initialize()
    {
        var path = "materials/models/skins/";
        foreach ( var i in FileSystem.Mounted.FindFile( path, "*.png", true ) )
        {
            if ( Skins.Any( c => c.BaseSkinName == i ) )
                continue;
            Skins.Add( new Skin { BaseSkinName = i, Texture = await Texture.LoadAsync( FileSystem.Mounted, $"{path}{i}" ) } );
        }

        if ( FileSystem.Data.FileExists( "selectedskin.txt" ) )
        {
            if ( int.TryParse( FileSystem.Data.ReadAllText( "selectedskin.txt" ), out int v ) )
                Selected = v;
        }

        // Load data-folder skins
        if ( FileSystem.Data.FileExists( "customskins.txt" ) )
        {
            var skins = FileSystem.Data.ReadAllText( "customskins.txt" );
            foreach ( var s in skins.Split( "\n" ) )
            {
                Skins.Add( new Skin { Username = s, Texture = await VoxelPlayer.GetTextureFromSkin( s ) } );
            }
        }
    }

    public override void Tick()
    {
        if ( lastBadName != null && Name.Value != lastBadName )
        {
            lastBadName = null;
            LastBadReason = "";
            Name.RemoveClass( "bad" );
        }
        Selected = Selected.Clamp( 0, Skins.Count - 1 );
    }
    string lastBadName = null;
    string LastBadReason = "";
    public void Buzz( string reason )
    { // BAD
        lastBadName = Name.Value;
        LastBadReason = reason;
        Name.AddClass( "bad" );
    }

    public async Task AddSkin( string name )
    {
        name = new Regex( @"\W" ).Replace( name.ToLower(), "" );
        if ( Skins.Any( c => c.Username == name ) )
        {
            Buzz( "Skin already added!" );
            return;
        }

        var tex = await VoxelPlayer.GetTextureFromSkin( name );
        if ( Skins.Any( c => c.Texture == tex ) )
        {
            Buzz( "Skin already added!" );
            return;
        }

        if ( tex == null )
        {
            Buzz( "No skin with that name exists!" );
            return;
        }

        Skins.Add( new Skin { Username = name, Texture = tex } );

        SaveSkins();
    }

    public void SaveSkins()
    {
        FileSystem.Data.WriteAllText( "customskins.txt", string.Join( "\n", Skins.Where( c => c.Username != null ).Select( c => c.Username ) ) );
    }

    protected override int BuildHash() => HashCode.Combine( Skins.GetHashCode(), Selected );
}
