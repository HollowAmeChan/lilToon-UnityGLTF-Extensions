using GLTF.Schema;
using UnityEngine;
using UnityEngine.Rendering;
using UnityGLTF.Plugins;

namespace lilToon.UnityGLTF.Extensions
{
    public sealed class HoMaterialsPrincipledLilImport : GLTFImportPlugin
    {
        public const string ExtensionName = "HO_materials_principled_lil";
        public const string LegacyExtensionName = "HO_materials_openpbr_lil";

        [Tooltip("Adds a HoMaterialContractAsset sub-asset to editor imports so later converters can read the original glTF contract.")]
        public bool preserveContractJson = true;

        [Tooltip("Temporary bridge: when the contract exists, replace UnityGLTF/PBRGraph with URP/Lit using UnityGLTF's already-imported material properties.")]
        public bool applyUrpLitFallback = false;

        public override string DisplayName => ExtensionName;
        public override string Description => "Imports Hollow material contracts for lilToon/lilPBR pipelines.";
        public override string HelpUrl => "https://github.com/KhronosGroup/UnityGLTF";

        public override GLTFImportPluginContext CreateInstance(GLTFImportContext context)
        {
            return new HoMaterialsPrincipledLilImportContext(this, context);
        }
    }

    public sealed class HoMaterialsPrincipledLilImportContext : GLTFImportPluginContext
    {
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";

        private readonly HoMaterialsPrincipledLilImport settings;
        private readonly GLTFImportContext context;

        public HoMaterialsPrincipledLilImportContext(HoMaterialsPrincipledLilImport settings, GLTFImportContext context)
        {
            this.settings = settings;
            this.context = context;
        }

        public override void OnAfterImportMaterial(GLTFMaterial material, int materialIndex, Material materialObject)
        {
            if (material == null || materialObject == null)
                return;

            if (!TryGetContractJson(material, out var extensionName, out var json))
                return;

#if UNITY_EDITOR
            if (settings.preserveContractJson)
                AddContractAsset(extensionName, materialIndex, material.Name, json);
#endif

            materialObject.SetOverrideTag("HO_MaterialContract", extensionName);

            if (settings.applyUrpLitFallback)
                ApplyUrpLitFallback(materialObject);
        }

        private static bool TryGetContractJson(GLTFMaterial material, out string extensionName, out string json)
        {
            extensionName = null;
            json = null;

            if (material.Extensions == null)
                return false;

            if (!material.Extensions.TryGetValue(HoMaterialsPrincipledLilImport.ExtensionName, out var extension) &&
                !material.Extensions.TryGetValue(HoMaterialsPrincipledLilImport.LegacyExtensionName, out extension))
            {
                return false;
            }

            extensionName = material.Extensions.ContainsKey(HoMaterialsPrincipledLilImport.ExtensionName)
                ? HoMaterialsPrincipledLilImport.ExtensionName
                : HoMaterialsPrincipledLilImport.LegacyExtensionName;
            json = extension.Serialize().ToString();
            return true;
        }

#if UNITY_EDITOR
        private void AddContractAsset(string extensionName, int materialIndex, string materialName, string json)
        {
            if (context.AssetContext == null)
                return;

            var asset = ScriptableObject.CreateInstance<HoMaterialContractAsset>();
            asset.name = $"HO Material Contract {materialIndex} {SanitizeName(materialName)}";
            asset.hideFlags = HideFlags.HideInHierarchy;
            asset.Initialize(extensionName, materialIndex, materialName, json);
            context.AssetContext.AddObjectToAsset(asset.name, asset);
        }
#endif

        private static void ApplyUrpLitFallback(Material material)
        {
            var urpLit = Shader.Find(UrpLitShaderName);
            if (urpLit == null)
            {
                Debug.LogWarning("HO material contract import found no URP/Lit shader. Contract JSON was preserved, but no fallback shader was applied.", material);
                return;
            }

            var baseColor = GetColor(material, "baseColorFactor", Color.white);
            var baseMap = GetTexture(material, "baseColorTexture");
            var metallic = GetFloat(material, "metallicFactor", 0f);
            var roughness = GetFloat(material, "roughnessFactor", 0.5f);
            var normalMap = GetTexture(material, "normalTexture");
            var normalScale = GetFloat(material, "normalScale", 1f);
            var occlusionMap = GetTexture(material, "occlusionTexture");
            var occlusionStrength = GetFloat(material, "occlusionStrength", 1f);
            var emissionMap = GetTexture(material, "emissiveTexture");
            var emissionColor = GetColor(material, "emissiveFactor", Color.black);
            var cutoff = GetFloat(material, "alphaCutoff", 0.5f);
            var cull = GetFloat(material, "_Cull", 2f);
            var transparent = material.GetTag("RenderType", false) == "Transparent" ||
                              GetFloat(material, "_Surface", 0f) > 0.5f ||
                              material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");

            material.shader = urpLit;
            SetColor(material, "_BaseColor", baseColor);
            SetTexture(material, "_BaseMap", baseMap);
            SetFloat(material, "_Metallic", metallic);
            SetFloat(material, "_Smoothness", Mathf.Clamp01(1f - roughness));
            SetTexture(material, "_BumpMap", normalMap);
            SetFloat(material, "_BumpScale", normalScale);
            SetKeyword(material, "_NORMALMAP", normalMap != null);
            SetTexture(material, "_OcclusionMap", occlusionMap);
            SetFloat(material, "_OcclusionStrength", occlusionStrength);
            SetTexture(material, "_EmissionMap", emissionMap);
            SetColor(material, "_EmissionColor", emissionColor);
            SetKeyword(material, "_EMISSION", emissionMap != null || emissionColor.maxColorComponent > 0.0001f);
            SetFloat(material, "_Cull", cull);
            ApplySurfaceSettings(material, transparent, false, cutoff);
        }

        private static void ApplySurfaceSettings(Material material, bool transparent, bool alphaClip, float cutoff)
        {
            SetFloat(material, "_AlphaClip", alphaClip ? 1f : 0f);
            SetFloat(material, "_Cutoff", cutoff);
            SetKeyword(material, "_ALPHATEST_ON", alphaClip);

            if (transparent)
            {
                SetFloat(material, "_Surface", 1f);
                SetFloat(material, "_Blend", 0f);
                SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
                SetFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                SetFloat(material, "_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                return;
            }

            SetFloat(material, "_Surface", 0f);
            SetFloat(material, "_SrcBlend", (float)BlendMode.One);
            SetFloat(material, "_DstBlend", (float)BlendMode.Zero);
            SetFloat(material, "_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", alphaClip ? "TransparentCutout" : "Opaque");
            material.renderQueue = alphaClip ? (int)RenderQueue.AlphaTest : -1;
        }

        private static Texture GetTexture(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) ? material.GetTexture(propertyName) : null;
        }

        private static float GetFloat(Material material, string propertyName, float fallback)
        {
            return material.HasProperty(propertyName) ? material.GetFloat(propertyName) : fallback;
        }

        private static Color GetColor(Material material, string propertyName, Color fallback)
        {
            return material.HasProperty(propertyName) ? material.GetColor(propertyName) : fallback;
        }

        private static void SetTexture(Material material, string propertyName, Texture value)
        {
            if (material.HasProperty(propertyName))
                material.SetTexture(propertyName, value);
        }

        private static void SetFloat(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
                material.SetFloat(propertyName, value);
        }

        private static void SetColor(Material material, string propertyName, Color value)
        {
            if (material.HasProperty(propertyName))
                material.SetColor(propertyName, value);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

#if UNITY_EDITOR
        private static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Unnamed";

            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
#endif
    }
}
