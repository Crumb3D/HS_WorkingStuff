using UnityEngine.Scripting;

[Preserve]
public class XUiC_HSStorageGrid : XUiC_ItemStackGrid
{
    IHSFridgeInv fridge;

    public override XUiC_ItemStack.StackLocationTypes StackLocation
    {
        get { return XUiC_ItemStack.StackLocationTypes.LootContainer; }
    }

    public void Bind(IHSFridgeInv te)
    {
        fridge = te;
        if (fridge == null)
            return;
        fridge.EnsureItems();
        SetStacks(fridge.Items);
    }

    public override void HandleSlotChangedEvent(int _slotNumber, ItemStack _itemStack)
    {
        if (fridge == null || fridge.Items == null)
            return;
        if (_slotNumber < 0 || _slotNumber >= fridge.Items.Length)
            return;
        fridge.Items[_slotNumber] = _itemStack != null ? _itemStack.Clone() : ItemStack.Empty.Clone();
        fridge.SetModified();
    }
}
