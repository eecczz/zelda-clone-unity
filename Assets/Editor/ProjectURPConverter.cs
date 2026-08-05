#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class ProjectURPConverter
{
    private const string SettingsFolder = "Assets/Settings";
    private const string PipelinePath = SettingsFolder + "/NAN_URP.asset";
    private const string RendererPath = SettingsFolder + "/NAN_URP_Renderer.asset";
    private const string FaeSkyboxPath = "Assets/AssetstoreTools/Map/Fantasy Adventure Environment/Materials/Skybox/FAE_Skybox.mat";
    private static bool queued;

    private sealed class MaterialState
    {
        public readonly Dictionary<string, float> floats = new();
        public readonly Dictionary<string, Color> colors = new();
        public readonly Dictionary<string, Vector4> vectors = new();
        public readonly Dictionary<string, Texture> textures = new();
        public readonly Dictionary<string, Vector2> scales = new();
        public readonly Dictionary<string, Vector2> offsets = new();
    }

    static ProjectURPConverter()
    {
        if (queued) return;
        queued = true;
        EditorApplication.delayCall += () =>
        {
            queued = false;
            RepairFaeSkybox();
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath) == null)
                ConvertProjectToURP();
        };
    }

    [MenuItem("Tools/NAN/Repair Fantasy Adventure Demo Lighting")]
    private static void RepairFaeSkybox()
    {
        Material skybox = AssetDatabase.LoadAssetAtPath<Material>(FaeSkyboxPath);
        Shader cubemap = Shader.Find("Skybox/Cubemap");
        if (skybox == null || cubemap == null || skybox.shader == cubemap) return;
        skybox.shader = cubemap;
        EditorUtility.SetDirty(skybox);
        AssetDatabase.SaveAssetIfDirty(skybox);
        DynamicGI.UpdateEnvironment();
        Debug.Log("FAE demo repair complete: restored FAE_Skybox to Skybox/Cubemap.");
    }

    [MenuItem("Tools/NAN/Convert Entire Project To URP (Preserve Materials)")]
    public static void ConvertProjectToURP()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ConvertProjectToURP;
            return;
        }

        EnsurePipeline();
        int converted = 0, preserved = 0, failed = 0, missingTextures = 0;
        var visited = new HashSet<int>();
        string[] paths = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)).ToArray();

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string path in paths)
            {
                foreach (Material material in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    if (material == null || !visited.Add(material.GetInstanceID())) continue;
                    try
                    {
                        Shader target = ResolveTargetShader(material.shader);
                        if (target == null) { failed++; continue; }
                        if (material.shader == target || IsURPShader(material.shader)) { preserved++; continue; }

                        MaterialState state = Capture(material);
                        material.shader = target;
                        RestoreMatching(material, state);
                        RestoreStandardMappings(material, state);
                        ConfigureSurface(material, state);
                        EditorUtility.SetDirty(material);
                        converted++;
                        missingTextures += CountMissingTextureReferences(material);
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Debug.LogWarning($"URP material conversion skipped: {path} / {material.name}\n{ex.Message}");
                    }
                }
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Shader.SetGlobalFloat("_URPConversionComplete", 1f);
        Debug.Log($"URP CONVERSION COMPLETE — converted: {converted}, already URP/preserved: {preserved}, failed: {failed}, missing texture references: {missingTextures}");
    }

    private static void EnsurePipeline()
    {
        if (!AssetDatabase.IsValidFolder(SettingsFolder)) AssetDatabase.CreateFolder("Assets", "Settings");
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.name = "NAN Universal Renderer";
            AssetDatabase.CreateAsset(renderer, RendererPath);
        }

        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            pipeline.name = "NAN Universal Render Pipeline";
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList");
            if (list != null)
            {
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            }
            var index = serialized.FindProperty("m_DefaultRendererIndex");
            if (index != null) index.intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        EditorUtility.SetDirty(pipeline);
        AssetDatabase.SaveAssets();
    }

    private static bool IsURPShader(Shader shader)
    {
        if (shader == null) return false;
        string path = AssetDatabase.GetAssetPath(shader).Replace('\\', '/');
        return shader.name.StartsWith("Universal Render Pipeline/", StringComparison.OrdinalIgnoreCase)
               || path.Contains("/Shaders/URP/", StringComparison.OrdinalIgnoreCase);
    }

    private static Shader ResolveTargetShader(Shader source)
    {
        if (source == null) return Shader.Find("Universal Render Pipeline/Lit");
        if (IsURPShader(source)) return source;
        string name = source.name.ToLowerInvariant();
        string path = AssetDatabase.GetAssetPath(source).Replace('\\', '/');

        // Skybox shaders are pipeline-independent. Converting them to URP/Lit
        // keeps the texture references but makes Unity render a regular surface
        // instead of a sky, which is especially visible in the FAE demo scenes.
        if (name.StartsWith("skybox/", StringComparison.OrdinalIgnoreCase))
            return source;

        if (path.Contains("Fantasy Adventure Environment", StringComparison.OrdinalIgnoreCase) || name.Contains("fae"))
        {
            string graph = null;
            if (name.Contains("tree") && name.Contains("trunk")) graph = "FAE_TreeTrunk.shadergraph";
            else if (name.Contains("tree") && (name.Contains("branch") || name.Contains("leaf"))) graph = "FAE_TreeBranch.shadergraph";
            else if (name.Contains("billboard")) graph = "FAE_TreeBillboard.shadergraph";
            else if (name.Contains("foliage") || name.Contains("grass")) graph = "FAE_Foliage.shadergraph";
            else if (name.Contains("fog")) graph = "FAE_FogSheet.shadergraph";
            else if (name.Contains("cliff") && name.Contains("coverage")) graph = "FAE_Cliff_Coverage.shadergraph";
            else if (name.Contains("cliff")) graph = "FAE_Cliff.shadergraph";
            else if (name.Contains("water")) graph = "FAE_Water.shadergraph";
            else if (name.Contains("sunshaft")) graph = "FAE_Sunshaft.shadergraph";
            if (graph != null)
            {
                string graphPath = "Assets/AssetstoreTools/Map/Fantasy Adventure Environment/Shaders/URP/" + graph;
                Shader faeShader = AssetDatabase.LoadAssetAtPath<Shader>(graphPath);
                if (faeShader != null) return faeShader;
            }
        }

        if (name.Contains("particle"))
            return Shader.Find(name.Contains("additive") ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Particles/Lit");
        if (name.Contains("unlit")) return Shader.Find("Universal Render Pipeline/Unlit");
        if (name.Contains("speedtree")) return Shader.Find("Universal Render Pipeline/Nature/SpeedTree8") ?? Shader.Find("Universal Render Pipeline/Lit");
        return Shader.Find("Universal Render Pipeline/Lit");
    }

    private static MaterialState Capture(Material material)
    {
        var state = new MaterialState();
        Shader shader = material.shader;
        if (shader == null) return state;
        int count = ShaderUtil.GetPropertyCount(shader);
        for (int i = 0; i < count; i++)
        {
            string n = ShaderUtil.GetPropertyName(shader, i);
            switch (ShaderUtil.GetPropertyType(shader, i))
            {
                case ShaderUtil.ShaderPropertyType.Color: state.colors[n] = material.GetColor(n); break;
                case ShaderUtil.ShaderPropertyType.Vector: state.vectors[n] = material.GetVector(n); break;
                case ShaderUtil.ShaderPropertyType.Float:
                case ShaderUtil.ShaderPropertyType.Range: state.floats[n] = material.GetFloat(n); break;
                case ShaderUtil.ShaderPropertyType.TexEnv:
                    state.textures[n] = material.GetTexture(n);
                    state.scales[n] = material.GetTextureScale(n);
                    state.offsets[n] = material.GetTextureOffset(n);
                    break;
            }
        }
        return state;
    }

    private static void RestoreMatching(Material material, MaterialState state)
    {
        foreach (var p in state.floats) if (material.HasProperty(p.Key)) material.SetFloat(p.Key, p.Value);
        foreach (var p in state.colors) if (material.HasProperty(p.Key)) material.SetColor(p.Key, p.Value);
        foreach (var p in state.vectors) if (material.HasProperty(p.Key)) material.SetVector(p.Key, p.Value);
        foreach (var p in state.textures) if (material.HasProperty(p.Key)) SetTexture(material, p.Key, p.Value, state.scales.GetValueOrDefault(p.Key, Vector2.one), state.offsets.GetValueOrDefault(p.Key));
    }

    private static void RestoreStandardMappings(Material material, MaterialState state)
    {
        CopyTexture(material, "_BaseMap", state, "_BaseMap", "_MainTex", "_BaseColorMap", "_AlbedoMap", "_Diffuse");
        CopyColor(material, "_BaseColor", state, "_BaseColor", "_Color", "_TintColor");
        CopyTexture(material, "_BumpMap", state, "_BumpMap", "_NormalMap", "_Normal");
        CopyTexture(material, "_MetallicGlossMap", state, "_MetallicGlossMap", "_MaskMap", "_MetallicMap");
        CopyTexture(material, "_OcclusionMap", state, "_OcclusionMap", "_AOMap");
        CopyTexture(material, "_EmissionMap", state, "_EmissionMap");
        CopyColor(material, "_EmissionColor", state, "_EmissionColor");
        CopyFloat(material, "_Smoothness", state, "_Smoothness", "_Glossiness");
        CopyFloat(material, "_Metallic", state, "_Metallic");
        CopyFloat(material, "_Cutoff", state, "_Cutoff", "_AlphaCutoff");
    }

    private static void ConfigureSurface(Material material, MaterialState state)
    {
        bool cutout = state.floats.GetValueOrDefault("_AlphaClip") > .5f || state.floats.ContainsKey("_Cutoff") && state.floats["_Cutoff"] > 0f;
        bool transparent = state.floats.GetValueOrDefault("_Surface") > .5f || state.floats.GetValueOrDefault("_Mode") >= 2f;
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", cutout ? 1 : 0);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", transparent ? 1 : 0);
        if (cutout) material.EnableKeyword("_ALPHATEST_ON");
        if (transparent) material.renderQueue = (int)RenderQueue.Transparent;
        if (material.GetTexture("_BumpMap") != null) material.EnableKeyword("_NORMALMAP");
        if (material.GetTexture("_EmissionMap") != null || material.HasProperty("_EmissionColor") && material.GetColor("_EmissionColor").maxColorComponent > 0) material.EnableKeyword("_EMISSION");
    }

    private static void CopyTexture(Material m, string target, MaterialState s, params string[] sources)
    {
        if (!m.HasProperty(target)) return;
        foreach (string source in sources) if (s.textures.TryGetValue(source, out Texture t) && t != null)
        { SetTexture(m, target, t, s.scales.GetValueOrDefault(source, Vector2.one), s.offsets.GetValueOrDefault(source)); return; }
    }
    private static void SetTexture(Material m,string property,Texture texture,Vector2 scale,Vector2 offset){m.SetTexture(property,texture);m.SetTextureScale(property,scale);m.SetTextureOffset(property,offset);}
    private static void CopyColor(Material m,string target,MaterialState s,params string[] sources){if(!m.HasProperty(target))return;foreach(string source in sources)if(s.colors.TryGetValue(source,out Color c)){m.SetColor(target,c);return;}}
    private static void CopyFloat(Material m,string target,MaterialState s,params string[] sources){if(!m.HasProperty(target))return;foreach(string source in sources)if(s.floats.TryGetValue(source,out float f)){m.SetFloat(target,f);return;}}
    private static int CountMissingTextureReferences(Material material){int missing=0;foreach(string p in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap","_OcclusionMap","_EmissionMap"})if(material.HasProperty(p)&&material.GetTexture(p)!=null&&string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material.GetTexture(p))))missing++;return missing;}
}
#endif
