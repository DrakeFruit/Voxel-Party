using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;
using Editor.Assets;
using Editor.Audio;

[EditorTool]
[Title( "Voxel Builder" )]
[Icon( "view_in_ar" )]
[Alias( "voxelbuilder" )]
[Group( "8" )]
public partial class VoxelBuilder : EditorTool
{

	public class Watcher
	{
		public Widget Target = null;// When this widget becomes invalid, so does the watcher.
		public Func<object> GetValue = null;
		public Func<object, int> BuildHash = ( object v ) => v.GetHashCode();
		public Action<object> onChange = null;
		public int LastHash = 0;
	}

	const float MAX_DISTANCE = 8096;
	static public int SelectedItemID { get; private set; } = 0; // Default to Grass block
																// static public int SelectedToolID { get; set; } = 0; // Default to Place Block tool
	static VoxelTool SelectedTool = null;

	public static void SelectItem( int item )
	{
		if ( itemButtons.ContainsKey( item ) )
		{
			if ( itemButtons.ContainsKey( SelectedItemID ) )
			{
				itemButtons[SelectedItemID].Tint = Color.White.WithAlpha( 0f );
			}
			SelectedItemID = item;
			itemButtons[item].Tint = Color.White.WithAlpha( 0.2f );
		}
	}

	public static void SelectTool( VoxelTool tool )
	{
		SelectedTool?.OnDeselected(); // Deselect the last tool
		SelectedTool = tool; // Set the new tool as selected
		SelectedTool.OnSelected(); // Select the new tool
	}

	public VoxelTool[] VoxelTools = [
		new PlaceBlockTool(),
		new DeleteBlockTool(),
		new SquarePlaceTool(),
		new SelectTool(),
		new EyeDropper(),
	];

	private static List<Watcher> watchers = new List<Watcher>(); // I hate that this is object instead of Watcher, but C# hates me too.
	public static void RegisterWatcher( Watcher watcher )
	{
		if ( watcher == null || watcher.Target == null || watcher.GetValue == null )
			return; // Invalid watcher, don't register it.

		watchers.Add( watcher ); // Cast to object to store in the list.
	}

	static Dictionary<int, ImageButton> itemButtons = new Dictionary<int, ImageButton>();
	public static WidgetWindow ToolOptionsWindow;
	public override void OnEnabled()
	{
		AllowGameObjectSelection = false;

		var palleteWindow = new WidgetWindow( SceneOverlay );

		palleteWindow.Layout = Layout.Column();

		var scrollArea = new ScrollArea( palleteWindow );
		scrollArea.VerticalScrollbarMode = ScrollbarMode.On;

		palleteWindow.Layout.Add( scrollArea );

		var wiget = new Widget();
		var gridLayout = Layout.Grid();
		wiget.Layout = gridLayout;
		scrollArea.Canvas = wiget;
		gridLayout.SizeConstraint = SizeConstraint.SetMinAndMaxSize;

		gridLayout.Margin = 16;
		palleteWindow.WindowTitle = "Voxel Pallete";

		if ( SelectedTool == null || !VoxelTools.Contains( SelectedTool ) )
		{
			SelectedTool = VoxelTools[0]; // Default to the first tool if the current one is bad
		}

		int i = 0;
		foreach ( var item in ResourceLibrary.GetAll<Item>().DistinctBy( c => c.ID ).OrderBy( c => c.ID ) )
		{
			if ( item.ID == 0 ) continue; // Skip empty item
			if ( item.IsBlock )
			{
				var icon = new ImageButton();
				itemButtons[item.ID] = icon;
				using ( var tex = EditorHelpers.RenderItem( item ) )
				{
					icon.Texture = tex;
				}
				icon.FixedSize = new Vector2( 32, 32 );
				icon.Tint = SelectedItemID == item.ID ? Color.White.WithAlpha( 0.2f ) : Color.White.WithAlpha( 0f );
				icon.Clicked = () =>
				{
					if ( itemButtons.ContainsKey( SelectedItemID ) )
					{
						itemButtons[SelectedItemID].Tint = Color.White.WithAlpha( 0f );
					}
					SelectedItemID = item.ID;
					icon.Tint = Color.White.WithAlpha( 0.2f );
				};
				icon.ToolTip = $"{item.Name} (ID: {item.ID})";
				gridLayout.AddCell( i % 6, i++ / 6, icon );
			}
		}

		AddOverlay( palleteWindow, TextFlag.LeftTop, 10 );

		ToolOptionsWindow = new WidgetWindow( SceneOverlay );
		ToolOptionsWindow.MinimumSize = new Vector2( 200, 16 );
		ToolOptionsWindow.Layout = Layout.Column();
		ToolOptionsWindow.WindowTitle = "Tool Options";
		ToolOptionsWindow.Layout.Margin = 16;


		AddOverlay( ToolOptionsWindow, TextFlag.RightBottom, 10 );

		var toolWindow = new WidgetWindow( SceneOverlay );
		toolWindow.Layout = Layout.Row();
		toolWindow.WindowTitle = "Voxel Builder Tools";
		toolWindow.Layout.Margin = 16;

		var toolBin = new ToolBar( toolWindow, "Tools" );
		for ( i = 0; i < VoxelTools.Length; i++ )
		{
			int _i = i;
			var tool = VoxelTools[i];
			tool.ToolID = i; // Assign the tool ID
			var name = (tool.Shortcut != null) ? $"{tool.Name} ({EditorShortcuts.GetKeys( tool.Shortcut )})" : tool.Name;
			var opt = toolBin.AddOption( name, tool.Icon, () =>
			{
				SelectedTool?.OnDeselected(); // Deselect the last tool
				SelectedTool = tool; // Set the new tool as selected
				SelectedTool.OnSelected(); // Select the new tool
			} );
			/*toolBin.AddButton( name, tool.Icon, () =>
			{
				SelectedTool?.OnDeselected(); // Deselect the last tool
				SelectedTool = tool; // Set the new tool as selected
				SelectedTool.OnSelected(); // Select the new tool
			}, () => SelectedTool == tool );*/
		}

		toolWindow.Layout.Add( toolBin );

		AddOverlay( toolWindow, TextFlag.LeftBottom, 10 );

	}

	public static (Vector3Int pos, Direction face) BlockTrace()
	{
		var trace = World.Active
			.Trace( Gizmo.CurrentRay.Position, Gizmo.CurrentRay.Position + Gizmo.CurrentRay.Forward * MAX_DISTANCE )
			.Run();
		var hitPos = trace.HitBlockPosition;
		var hitDirection = trace.HitFace;
		if ( !trace.Hit )
		{
			var planeTrace = new Plane( Vector3.Zero, Vector3.Up ).Trace( Gizmo.CurrentRay, true, MAX_DISTANCE );
			if ( planeTrace == null ) return (Vector3Int.Zero, Direction.None); // No hit, return default values

			hitPos = Helpers.WorldToVoxel( planeTrace.Value ) - Vector3Int.Up;
			hitDirection = Direction.Up;
		}
		return (hitPos, hitDirection);
	}

	public override void OnDisabled()
	{
		base.OnDisabled();
		Camera?.Scene.RenderAttributes.Set( "MAXDISTANCE", 100f );
	}

	public static Dictionary<string, bool> WasKeyDown = new Dictionary<string, bool>();
	private static VoxelTool LastTool = null;
	private static TimeSince holdStart = 0f;
	public override void OnUpdate()
	{

		Camera?.Scene.RenderAttributes.Set( "MAXDISTANCE", 10000f );
		watchers.RemoveAll( w => w == null || w.Target == null || !w.Target.IsValid() );
		foreach ( var watcher in watchers )
		{
			if ( watcher.Target == null || !watcher.Target.IsValid() )
				continue; // Impossible?

			var value = watcher.GetValue();
			var hash = watcher.BuildHash( value );
			if ( hash != watcher.LastHash )
			{
				watcher.onChange?.Invoke( value );
				watcher.LastHash = hash;
			}
		}

		foreach ( var t in VoxelTools.Where( t => t.Shortcut != null ) )
		{
			if ( EditorShortcuts.IsDown( t.Shortcut ) )
			{
				if ( !WasKeyDown.GetValueOrDefault( t.Shortcut, false ) )
				{
					holdStart = 0f;
					LastTool = SelectedTool;
				}
				if ( SelectedTool != t )
				{
					// Deselect the previous tool
					SelectedTool?.OnDeselected();
					SelectedTool = t;
					// Select the new tool
					t.OnSelected();
				}
			}
		}


		var tool = SelectedTool;
		if ( tool == null )
			return; // IMPOSSIBLE?


		if ( tool.Shortcut != null && !EditorShortcuts.IsDown( tool.Shortcut ) )
		{
			if ( LastTool != null && holdStart > 0.3f ) // If we're holding it down instead of pressing it, return to the LastTool on release.
			{
				tool.OnDeselected();
				SelectedTool = LastTool; // Fallback to the first tool if LastTool is null
				LastTool.OnSelected();
				tool = LastTool;
			}
			LastTool = null;
		}

		(Vector3Int hitPos, Direction hitDirection) = BlockTrace();
		using ( Gizmo.Scope( "ToolGizmos" ) )
			tool.DrawGizmos( hitPos, hitDirection );
		if ( Gizmo.WasLeftMousePressed )
		{
			tool.LeftMousePressed( hitPos, hitDirection );
		}
		else if ( Gizmo.IsLeftMouseDown )
		{
			tool.LeftMouseDown( hitPos, hitDirection );
		}
		else if ( Gizmo.WasLeftMouseReleased )
		{
			tool.LeftMouseUp( hitPos, hitDirection );
		}

		foreach ( var shortcut in EditorShortcuts.Entries )
		{
			WasKeyDown[shortcut.Identifier] = EditorShortcuts.IsDown( shortcut.Identifier );
		}
	}
}
