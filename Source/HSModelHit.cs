using UnityEngine;

// Custom bundle ModelEntities: Unity stores tags by index, so T_Block from the bundle does not survive.
// Without T_Block + RootTransformRefParent on every piece, tools "ting" and the voxel never takes damage.
public static class HSModelHit
{
    public static void Wire(BlockEntityData ebcd)
    {
        if (ebcd == null || ebcd.transform == null) return;
        try
        {
            SetHitTag(ebcd.transform, ebcd.transform);
        }
        catch (System.Exception e)
        {
            Log.Warning("[HSWorkingStuff] model hit wire: " + e.Message);
        }
    }

    public static bool IsBundleModel(Block block)
    {
        string model;
        return block != null && block.Properties != null
            && block.Properties.Values.TryGetValue("Model", out model)
            && model != null && model.Contains("HSWorkingStuff.unity3d?");
    }

    static void SetHitTag(Transform root, Transform t)
    {
        t.tag = "T_Block";
        foreach (Transform child in t)
            SetHitTag(root, child);
        if (root == t) return;
        var href = t.GetComponent<RootTransformRefParent>();
        if (href == null) href = t.gameObject.AddComponent<RootTransformRefParent>();
        href.RootTransform = root;
    }
}
