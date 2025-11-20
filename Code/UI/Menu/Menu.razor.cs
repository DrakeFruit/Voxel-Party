using System;
namespace VoxelParty.UI;

public partial class Menu : PanelComponent
{
    public bool IsVisible { get; set; } = true;
    public LobbyCreator lobbyCreator { get; set; }
    public ServerList serverList { get; set; }
    //TODO: Render player avatar to a texture and set character img src to it

    public static State menuState { get; set; } = State.None;

    protected override int BuildHash() => HashCode.Combine( IsVisible, menuState );

    protected override void OnUpdate()
    {
        if ( Input.EscapePressed ) Input.EscapePressed = false;
    }

    protected override void OnStart()
    {
        menuState = State.None;
        _ = CharacterCreator.Initialize();
    }

    private void ExitGame()
    {
        Game.Close();
    }

    public enum State
    {
        None,
        Play,
        Create,
        Character,
        Settings,
        News
    }
}