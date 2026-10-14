using UnityEngine.Scripting;

// Sits where the campfire fuel window would be on the electric cookers.
[Preserve]
public class HSStovePowerWindow : XUiController
{
    bool powered;
    bool known;
    float nextCheck;

    public override void OnOpen()
    {
        base.OnOpen();
        known = false;
        nextCheck = 0f;
        IsDirty = true;
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);
        if (UnityEngine.Time.unscaledTime >= nextCheck)
        {
            nextCheck = UnityEngine.Time.unscaledTime + 0.25f;
            bool on = Check();
            if (!known || on != powered)
            {
                known = true;
                powered = on;
                IsDirty = true;
            }
        }
        if (!IsDirty)
            return;
        RefreshBindings();
        IsDirty = false;
    }

    bool Check()
    {
        XUiWindowGroup wg = WindowGroup;
        var group = wg != null ? wg.Controller as XUiC_WorkstationWindowGroup : null;
        XUiM_Workstation data = group != null ? group.WorkstationData : null;
        TileEntityWorkstation te = data != null ? data.TileEntity : null;
        return te != null && HSStovePower.IsPowered(te);
    }

    public override bool GetBindingValueInternal(ref string _value, string _bindingName)
    {
        if (_bindingName == "power")
        {
            _value = powered ? HSLoc.Get("hsStovePanelOn", "Powered") : HSLoc.Get("hsStovePanelOff", "Unpowered");
            return true;
        }
        if (_bindingName == "powercolor")
        {
            _value = powered ? "255,214,64,255" : "150,150,150,255";
            return true;
        }
        return base.GetBindingValueInternal(ref _value, _bindingName);
    }
}
