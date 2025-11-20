using System;
using Sandbox.UI;
namespace VoxelParty.UI;

public partial class SpeedBuildHud : PanelComponent
{
    // a HUD used for Speed Build specific stuff
    public static SpeedBuildHud Instance { get; private set; }
    public SpeedBuildHud()
    {
        Instance = this;
    }

    [Sync( SyncFlags.FromHost )]
    public int? KillPercentage { get; set; } = null;

    protected override int BuildHash() => HashCode.Combine( KillPercentage.GetHashCode(), (VoxelPlayer.LocalPlayer?.CorrectBlocksPlaced ?? 0).GetHashCode(), (VoxelPlayer.LocalPlayer?.IncorrectBlocksPlaced ?? 0).GetHashCode(), (VoxelPlayer.LocalPlayer?.TotalBlockArea ?? 0).GetHashCode() );

    protected override void OnTreeBuilt()
    {
        // Update goodbar and badbar widths based on player stats
        var player = VoxelPlayer.LocalPlayer;
        var goodBar = Panel.FindChild( "#score", false )?.FindChild( "goodbar" );
        var badBar = Panel.FindChild( "#score", false )?.FindChild( "badbar" );
        var killBar = Panel.FindChild( "failPointer", false );

        if ( goodBar == null || badBar == null || killBar == null ) return;

        if ( KillPercentage.HasValue )
            killBar.Style.Width = Length.Fraction( KillPercentage.Value / 100f );

        if ( player.TotalBlockArea > 0 )
        {
            var correctPercentage = (float)player.CorrectBlocksPlaced / player.TotalBlockArea;
            var incorrectPercentage = (float)player.IncorrectBlocksPlaced / player.TotalBlockArea;

            goodBar.Style.Width = Length.Percent( correctPercentage * 100 );
            badBar.Style.Width = Length.Percent( incorrectPercentage * 100 );
        }
        else
        {
            goodBar.Style.Width = Length.Percent( 0 );
            badBar.Style.Width = Length.Percent( 0 );
        }
    }
}
