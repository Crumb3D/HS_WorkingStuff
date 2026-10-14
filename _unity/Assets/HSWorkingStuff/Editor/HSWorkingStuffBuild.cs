using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Import settings for the Blender FBX files: 1 unit = 1 m, axis already baked by the exporter.
public class HSWorkingStuffModelImport : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.Replace('\\', '/').Contains("/HSWorkingStuff/Models/")) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f;
        mi.useFileScale = true;
        mi.bakeAxisConversion = false;
        mi.preserveHierarchy = true;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.importBlendShapes = false;
        mi.addCollider = false;
        mi.isReadable = false;
        mi.importNormals = ModelImporterNormals.Import;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
        mi.meshCompression = ModelImporterMeshCompression.Off;
    }
}

public static class HSWorkingStuffBuild
{
    const string Root = "Assets/HSWorkingStuff";
    const string BundleName = "hsworkingstuff";
    const string BlockTag = "T_Block";

    [MenuItem("HS Working Stuff/Build Bundle")]
    public static void BuildMenu()
    {
        Run(false);
    }

    // Batch entry: Unity.exe -batchmode -projectPath _unity -executeMethod HSWorkingStuffBuild.Build -quit
    public static void Build()
    {
        Run(true);
    }

    static void Run(bool exit)
    {
        var log = new StringBuilder();
        int code = 0;
        try
        {
            EnsureTag(BlockTag);
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Prefabs");
            var mats = EnsureMaterials();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            int made = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (src == null) continue;
                var go = UnityEngine.Object.Instantiate(src);
                go.name = src.name;
                Process(go, mats);
                var prefabPath = Root + "/Prefabs/" + src.name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                var b = RenderBounds(go);
                var bc = go.GetComponent<BoxCollider>();
                UnityEngine.Object.DestroyImmediate(go);
                AssetImporter.GetAtPath(prefabPath).SetAssetBundleNameAndVariant(BundleName, "");
                log.AppendLine(string.Format("{0}: bounds min {1} max {2}, collider centre {3} size {4}", src.name, V(b.min), V(b.max),
                    bc != null ? V(bc.center) : "-", bc != null ? V(bc.size) : "-"));
                made++;
            }
            AssetDatabase.SaveAssets();
            if (made == 0) throw new Exception("No models found in " + Root + "/Models");

            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BundleOut"));
            Directory.CreateDirectory(outDir);
            var manifest = BuildPipeline.BuildAssetBundles(outDir,
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
                BuildTarget.StandaloneWindows64);
            if (manifest == null) throw new Exception("BuildAssetBundles returned null");

            var modResources = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Resources"));
            Directory.CreateDirectory(modResources);
            var dest = Path.Combine(modResources, "HSWorkingStuff.unity3d");
            File.Copy(Path.Combine(outDir, BundleName), dest, true);
            log.AppendLine("Bundle: " + dest + " (" + new FileInfo(dest).Length + " bytes, " + made + " prefabs)");
            log.AppendLine("DONE");
        }
        catch (Exception e)
        {
            code = 1;
            log.AppendLine("FAILED: " + e);
        }
        var report = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "build_report.txt"));
        File.WriteAllText(report, log.ToString());
        Debug.Log("[HSWorkingStuffBuild]\n" + log);
        if (exit) EditorApplication.Exit(code);
    }

    static string V(Vector3 v)
    {
        return string.Format("({0:0.###}, {1:0.###}, {2:0.###})", v.x, v.y, v.z);
    }

    static Bounds RenderBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds();
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    // Hits register as the block only if T_Block and the collider sit on the ROOT.
    static void Process(GameObject go, Dictionary<string, Material> mats)
    {
        go.tag = BlockTag;
        go.layer = 16;
        var combined = new Bounds(Vector3.zero, Vector3.one);
        bool anyCol = false;
        var colObjs = new List<GameObject>();
        var split = new List<Bounds>();
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = 16;
            if (t != go.transform) t.gameObject.tag = "Untagged";
            bool own = t.name.StartsWith("COLS_", StringComparison.OrdinalIgnoreCase);
            if (own || t.name.StartsWith("COL_", StringComparison.OrdinalIgnoreCase))
            {
                var mf = t.GetComponent<MeshFilter>();
                var mb = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
                var one = new Bounds();
                for (int i = 0; i < 8; i++)
                {
                    var lp = mb.center + Vector3.Scale(mb.extents, new Vector3(
                        (i & 1) == 0 ? -1f : 1f,
                        (i & 2) == 0 ? -1f : 1f,
                        (i & 4) == 0 ? -1f : 1f));
                    var rootLocal = go.transform.InverseTransformPoint(t.TransformPoint(lp));
                    if (i == 0) one = new Bounds(rootLocal, Vector3.zero);
                    else one.Encapsulate(rootLocal);
                }
                if (own) split.Add(one);
                else if (!anyCol) { combined = one; anyCol = true; }
                else combined.Encapsulate(one);
                colObjs.Add(t.gameObject);
                continue;
            }
            var mr = t.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            var shared = mr.sharedMaterials;
            for (int i = 0; i < shared.Length; i++)
            {
                var key = shared[i] != null ? CleanName(shared[i].name) : "";
                Material m;
                if (mats.TryGetValue(key, out m)) shared[i] = m;
                else Debug.LogWarning("[HSWorkingStuffBuild] No material for slot '" + key + "' on " + go.name);
            }
            mr.sharedMaterials = shared;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }
        for (int i = 0; i < colObjs.Count; i++)
            UnityEngine.Object.DestroyImmediate(colObjs[i]);
        foreach (var old in go.GetComponents<BoxCollider>())
            UnityEngine.Object.DestroyImmediate(old);
        if (split.Count > 0)
        {
            foreach (var sb in split)
            {
                var sc = go.AddComponent<BoxCollider>();
                sc.center = sb.center;
                sc.size = Vector3.Max(sb.size, new Vector3(0.05f, 0.05f, 0.05f));
                sc.isTrigger = false;
            }
            return;
        }
        var bc = go.AddComponent<BoxCollider>();
        if (!anyCol)
        {
            bc.center = new Vector3(0f, 0.5f, 0f);
            bc.size = Vector3.one;
        }
        else
        {
            bc.center = combined.center;
            var sz = combined.size;
            if (sz.x < 0.2f) sz.x = 0.2f;
            if (sz.y < 0.2f) sz.y = 0.2f;
            if (sz.z < 0.2f) sz.z = 0.2f;
            bc.size = sz;
        }
        bc.isTrigger = false;
    }

    static string CleanName(string n)
    {
        n = n.Replace(" (Instance)", "");
        int dot = n.IndexOf('.');
        if (dot > 0) n = n.Substring(0, dot);
        return n.Trim();
    }

    static Dictionary<string, Material> EnsureMaterials()
    {
        var d = new Dictionary<string, Material>();
        d["HS_White"] = Opaque("HS_White", new Color(0.93f, 0.93f, 0.91f), 0f, 0.62f);
        d["HS_Grey"] = Opaque("HS_Grey", new Color(0.36f, 0.37f, 0.39f), 0.2f, 0.4f);
        d["HS_Dark"] = Opaque("HS_Dark", new Color(0.05f, 0.05f, 0.06f), 0.1f, 0.5f);
        d["HS_Chrome"] = Opaque("HS_Chrome", new Color(0.82f, 0.83f, 0.85f), 0.9f, 0.8f);
        d["HS_Copper"] = Opaque("HS_Copper", new Color(0.72f, 0.42f, 0.22f), 0.85f, 0.55f);
        d["HS_Display"] = Glow("HS_Display", new Color(0.25f, 0.65f, 1f), 1.4f);
        d["HS_Led"] = Glow("HS_Led", new Color(0.25f, 1f, 0.35f), 2f);
        d["HS_Screen"] = Glow("HS_Screen", new Color(0.02f, 0.02f, 0.025f), 0.2f);
        d["HS_Screen"].SetFloat("_Glossiness", 0.9f);
        d["HS_Red"] = Opaque("HS_Red", new Color(0.78f, 0.08f, 0.06f), 0f, 0.55f);
        d["HS_Neon"] = Glow("HS_Neon", new Color(1f, 0.25f, 0.1f), 2f);
        d["HS_Green"] = Opaque("HS_Green", new Color(0.30f, 0.52f, 0.16f), 0f, 0.25f);
        d["HS_Straw"] = Opaque("HS_Straw", new Color(0.78f, 0.60f, 0.28f), 0f, 0.2f);
        d["HS_Wheat"] = Opaque("HS_Wheat", new Color(0.86f, 0.64f, 0.24f), 0f, 0.2f);
        d["HS_Wood"] = Opaque("HS_Wood", new Color(0.55f, 0.38f, 0.20f), 0f, 0.25f);
        d["HS_Cord"] = Opaque("HS_Cord", new Color(0.85f, 0.84f, 0.78f), 0f, 0.2f);
        d["HS_ClothBlue"] = Opaque("HS_ClothBlue", new Color(0.16f, 0.32f, 0.70f), 0f, 0.15f);
        d["HS_ClothYellow"] = Opaque("HS_ClothYellow", new Color(0.95f, 0.78f, 0.20f), 0f, 0.15f);
        d["HS_ClothPink"] = Opaque("HS_ClothPink", new Color(0.92f, 0.50f, 0.62f), 0f, 0.15f);
        d["HS_Denim"] = Opaque("HS_Denim", new Color(0.20f, 0.27f, 0.42f), 0f, 0.15f);
        d["HS_Water"] = Opaque("HS_Water", new Color(0.25f, 0.55f, 0.82f), 0.1f, 0.7f);
        d["HS_Soil"] = Opaque("HS_Soil", new Color(0.32f, 0.22f, 0.10f), 0f, 0.15f);
        AssetDatabase.SaveAssets();
        return d;
    }

    static Material Load(string name)
    {
        var path = Root + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = Shader.Find("Standard");
        return m;
    }

    static Material Opaque(string name, Color c, float metallic, float smooth)
    {
        var m = Load(name);
        m.SetFloat("_Mode", 0f);
        m.SetOverrideTag("RenderType", "");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        m.SetInt("_ZWrite", 1);
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = -1;
        m.color = c;
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Glossiness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Glow(string name, Color c, float intensity)
    {
        var m = Opaque(name, c, 0f, 0.6f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * intensity);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void EnsureTag(string tag)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return;
        var so = new SerializedObject(assets[0]);
        var tags = so.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }
}
