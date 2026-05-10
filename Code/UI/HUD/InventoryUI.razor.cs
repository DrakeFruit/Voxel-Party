using Sandbox.UI;
using System;
namespace VoxelParty.UI;

public partial class InventoryUI : PanelComponent
{
    PlayerController Controller => Player.GetComponent<PlayerController>();
	VoxelPlayer Player => VoxelPlayer.LocalPlayer;
	Inventory _inventory => Player.inventory;
	Panel HeldItemSlot;

	protected override void OnFixedUpdate()
	{
		Panel.Style.PointerEvents = (Player.HasInventory || Player.HasCreativeInventory) && Player.ShowInventory ?
		PointerEvents.All : PointerEvents.None;
		if (Input.Pressed("score"))
		{
			Player.ShowInventory = !Player.ShowInventory;
			if (!Player.ShowInventory)
				DropHeldItem();
		}
	}

	void DropHeldItem()
	{
		var stack = Player.inventory.GetItem(Player.inventory.CursorSlot);
		if (ItemStack.IsNullOrEmpty(stack)) return;
		Player.SetSlot(Player.inventory.CursorSlot, ItemStack.Empty);
		if (!Player.HasCreativeInventory)
		{
			Player.inventory.PutInFirstAvailableSlot(stack);
		}
	}

	protected override void OnUpdate()
	{
		if (Input.EscapePressed && Player.ShowInventory)
		{
			Input.EscapePressed = false;
			Player.ShowInventory = false;
			DropHeldItem();
		}
		HeldItemSlot.Style.Left = Length.Percent(100f * Panel.MousePosition.x / Panel.Box.Rect.Width);
		HeldItemSlot.Style.Top = Length.Percent(100f * Panel.MousePosition.y / Panel.Box.Rect.Height);
	}

	protected override int BuildHash() => HashCode.Combine(_inventory.GetHashCode(),
	VoxelPlayer.SelectedSlot.GetHashCode(), Player.ShowInventory.GetHashCode());
}