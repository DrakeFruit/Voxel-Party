using System;
using Sandbox.UI;

namespace VoxelParty.UI;

public partial class GameTimer : Panel
{
    public float TotalTime { get; set; } = 60f; // Default to 60 seconds
    public float DanceAmount { get; set; } = 5f; // Amount of degrees left and right to rotate between.
    public TimeUntil TimerEnd { get; set; }

    public override void Tick()
    {
        float DanceSpeed = 1f;

        var text = GetChild( 1 ).GetChild( 0 ) as Label;
        text.Text = $"{(int)(TimerEnd / 60)}:{(int)(TimerEnd % 60):D2}"; // Format as MM:SS

        if ( TimerEnd <= 0 )
        {
            DanceSpeed = 4f;
            text.Text = "Time's Up!";
            text.Style.FontColor = Color.Green; // Change text color to yellow when time is up
        }
        else if ( TimerEnd < 30 )
        {
            DanceSpeed = 2f;
            text.Style.FontColor = Color.FromRgb( 0xFF4444 ); // Change text color to red when less than 30 seconds left
        }
        var danceTransform = new PanelTransform();
        float danceRotation = (float)(Math.Sin( Time.Now * Math.PI * DanceSpeed ) * DanceAmount);
        danceTransform.AddRotation( new Vector3( 0f, 0f, danceRotation ) );
        Style.Transform = danceTransform;

        var hand = GetChild( 0 );
        var transform = new PanelTransform();
        float rotation = (MathF.Max( 0f, TimerEnd ) / TotalTime) * 360f; // Calculate rotation based on time left
        transform.AddRotation( new Vector3( 0f, 0f, rotation + danceRotation ) );
        transform.AddRotation( new Vector3( 0f, 0f, -danceRotation ) );
        hand.Style.Transform = transform;
    }
}
