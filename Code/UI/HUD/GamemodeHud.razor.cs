using System;
using Sandbox.UI;

namespace VoxelParty.UI;

public partial class GamemodeHud : PanelComponent
{
    // a HUD used for Speed Build specific stuff
    protected GameTimer timer;
    protected Panel message;

    [Sync( SyncFlags.FromHost )]
    public TimeUntil TimerEnd { get; set; } = 0f; // Default to 60 seconds

    [Sync( SyncFlags.FromHost )]
    public float TotalTime { get; set; } = 0f; // Default to 60 seconds
    [Sync( SyncFlags.FromHost )]
    public bool HasTimer { get; set; } = false; // Default to true, indicating that the timer is active
    [Sync( SyncFlags.FromHost )]
    public bool HasReadyCheck { get; set; } = true; // Default to true, indicating that the ready check is active

    [Sync( SyncFlags.FromHost )] public string Message { get; set; } = "";

    protected TextEntry textBox;

    public static GamemodeHud Instance = null;
    public GamemodeHud() : base()
    {
        Instance = this;
    }

    [Rpc.Broadcast]
    public void RequestTextBox()
    {
        textBox.Value = "";
        textBox.Focus();
    }

    protected override void OnFixedUpdate()
    {
        if ( VoxelPlayer.LocalPlayer.IsValid() )
            VoxelPlayer.LocalPlayer.TextBoxValue = textBox?.Value ?? "";
    }

    protected override int BuildHash() => HashCode.Combine(
        HashCode.Combine(
            Message.GetHashCode(),
            TimerEnd.Absolute.GetHashCode(),
            TotalTime.GetHashCode(),
            HasTimer.GetHashCode(),
            HasReadyCheck.GetHashCode() ),
        VoxelPlayer.LocalPlayer?.TotalBlockArea.GetHashCode(),
        VoxelPlayer.LocalPlayer?.CorrectBlocksPlaced.GetHashCode(),
        VoxelPlayer.LocalPlayer?.IncorrectBlocksPlaced.GetHashCode(),
        VoxelPlayer.LocalPlayer?.IsReady.GetHashCode() );
}
