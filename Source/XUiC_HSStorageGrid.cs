using UnityEngine.Scripting;

[Preserve]
public class XUiC_HSStorageGrid : XUiC_ItemStackGrid
{
    IHSFridgeInv fridge;
    int offset;

    public override XUiC_ItemStack.StackLocationTypes StackLocation
    {
        get { return XUiC_ItemStack.StackLocationTypes.LootContainer; }
    }

    public void Bind(IHSFridgeInv te)
    {
        Bind(te, 0);
    }

    // Shows one compartment: ViewSlots stacks starting at from.
    public void Bind(IHSFridgeInv te, int from)
    {
        fridge = te;
        offset = from;
        if (fridge == null)
            return;
        fridge.EnsureItems();
        SetStacks(HSFridgeInv.Slice(fridge.Items, offset, TileEntityHSFridge.ViewSlots));
    }

    public override void HandleSlotChangedEvent(int _slotNumber, ItemStack _itemStack)
    {
        if (fridge == null || fridge.Items == null)
            return;
        int i = offset + _slotNumber;
        if (_slotNumber < 0 || _slotNumber >= TileEntityHSFridge.ViewSlots || i >= fridge.Items.Length)
            return;
        fridge.Items[i] = _itemStack != null ? _itemStack.Clone() : ItemStack.Empty.Clone();
        fridge.SetModified();
    }
}
