using System;
using Sandbox.Network;
using Sandbox.UI;

namespace VoxelParty.UI;

public partial class LobbyCreator : Panel
{
    TextEntry LobbyNameInput { get; set; }
    Dropdown PrivacyOption { get; set; }
    Dropdown GamemodeChoice { get; set; }
    string LobbyName => LobbyNameInput?.Text.Length > 0 ? LobbyNameInput?.Text : Connection.Local.DisplayName + "'s Lobby";
    int MaxPlayers { get; set; } = 8;
    bool Hidden { get; set; } = false;
    bool DestroyWhenHostLeaves { get; set; } = false;
    bool AutoSwitchToBestHost { get; set; } = true;
    bool ShowPrivacyDropdown { get; set; }

    protected override int BuildHash() => HashCode.Combine( IsVisible );

    private void AdjustMaxPlayers( int delta )
    {
        MaxPlayers = Math.Clamp( MaxPlayers + delta, 1, 8 );
        StateHasChanged();
    }

    private void ApplyLobbyCreation()
    {
        Log.Info( $"Creating lobby: {LobbyName}, Max Players: {MaxPlayers}, Privacy: {PrivacyOption.Text}, Hidden: {Hidden}, DestroyWhenHostLeaves: {DestroyWhenHostLeaves}, AutoSwitchToBestHost: {AutoSwitchToBestHost}" );
        var lobbyConfig = new LobbyConfig
        {
            Name = LobbyName,
            MaxPlayers = MaxPlayers,
            Privacy = GetPrivacyLabel( (string)PrivacyOption.Text ),
            Hidden = Hidden,
            DestroyWhenHostLeaves = DestroyWhenHostLeaves,
            AutoSwitchToBestHost = AutoSwitchToBestHost
        };

        Networking.CreateLobby( lobbyConfig );
        _ = Transition.Run( () =>
        {
            Scene.LoadFromFile( GamemodeChoice.Text == "Speed-Build" ? "scenes/speed build.scene" : "scenes/telephone.scene" );
        } );
    }

    private LobbyPrivacy GetPrivacyLabel( string privacy )
    {
        return privacy switch
        {
            "Private" => LobbyPrivacy.Private,
            "Friends" => LobbyPrivacy.FriendsOnly,
            "Public" => LobbyPrivacy.Public,
            _ => LobbyPrivacy.Private
        };
    }
}