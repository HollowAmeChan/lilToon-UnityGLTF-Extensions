using System.Collections.Generic;
using System.IO;
using GLTF.Schema;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityGLTF.Plugins;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace lilToon.UnityGLTF.Extensions
{
    public sealed class HoMaterialsPrincipledLilImport : GLTFImportPlugin
    {
        public const string ExtensionName = "HO_materials_principled_lil";
        public const string LegacyExtensionName = "HO_materials_openpbr_lil";

        [Tooltip("Adds a HoMaterialContractAsset sub-asset to editor imports so later converters can read the original glTF contract.")]
        public bool preserveContractJson = true;

        [Tooltip("Hides preserved HoMaterialContractAsset sub-assets in the imported asset hierarchy.")]
        public bool hideContractSubAssets = false;

        [Tooltip("Temporary bridge: when the contract exists, replace UnityGLTF/PBRGraph with URP/Lit using UnityGLTF's already-imported material properties.")]
        public bool applyUrpLitFallback = false;

        [Tooltip("When the HoGLTF contract targets lilToon, switch the imported material to a lilToon shader and apply HoLil socket values.")]
        public bool applyLilToonMapping = true;

        [Tooltip("Logs HO material import decisions to the Console. Enable only while diagnosing import setup issues.")]
        public bool logImportDiagnostics = false;

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
        private readonly Dictionary<int, Texture> texturesByIndex = new Dictionary<int, Texture>();
        private readonly Dictionary<string, Texture> texturesByName = new Dictionary<string, Texture>(System.StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, HoMaterialContract> contractsByMaterialIndex = new Dictionary<int, HoMaterialContract>();

        public HoMaterialsPrincipledLilImportContext(HoMaterialsPrincipledLilImport settings, GLTFImportContext context)
        {
            this.settings = settings;
            this.context = context;
        }

        public override void OnAfterImportTexture(GLTFTexture texture, int textureIndex, Texture textureObject)
        {
            if (textureObject == null)
                return;

            texturesByIndex[textureIndex] = textureObject;
            RegisterTextureName(textureObject.name, textureObject);

            if (texture == null)
                return;

            RegisterTextureName(texture.Name, textureObject);
            RegisterTextureName(textureIndex.ToString(), textureObject);

            var image = GetTextureImage(texture);
            if (image == null)
                return;

            RegisterTextureName(image.Name, textureObject);
            RegisterTextureName(image.Uri, textureObject);
        }

        public override void OnAfterImportMaterial(GLTFMaterial material, int materialIndex, Material materialObject)
        {
            if (material == null || materialObject == null)
                return;

            if (!TryGetContractJson(material, out var extensionName, out var json))
                return;

            var mappedToLilToon = false;
            if (HoMaterialContractParser.TryParse(extensionName, json, out var contract))
            {
                if (materialIndex >= 0)
                    contractsByMaterialIndex[materialIndex] = contract;

                ApplyContractTags(materialObject, contract);

                var isLilToon = IsLilToonContract(contract);
                LogImportDiagnostic(materialIndex, material.Name, $"contract target={contract.TargetShaderFamily}/{contract.TargetShaderVariant}/{contract.TargetRenderingMode}, nodeGroup={contract.HoGltfNodeGroup}, applyLilToonMapping={settings.applyLilToonMapping}, isLilToon={isLilToon}");

                if (settings.applyLilToonMapping && isLilToon)
                    mappedToLilToon = ApplyLilToonMapping(materialObject, contract);
            }
            else
            {
                LogImportDiagnostic(materialIndex, material.Name, "failed to parse HO material contract JSON");
            }

#if UNITY_EDITOR
            if (settings.preserveContractJson)
                AddContractAsset(extensionName, materialIndex, material.Name, json);
#endif

            materialObject.SetOverrideTag("HO_MaterialContract", extensionName);

            if (settings.applyUrpLitFallback && !mappedToLilToon)
                ApplyUrpLitFallback(materialObject);
        }

        public override void OnAfterImport()
        {
            if (contractsByMaterialIndex.Count == 0 || context.SceneImporter?.MaterialCache == null)
                return;

            var materialCache = context.SceneImporter.MaterialCache;
            foreach (var pair in contractsByMaterialIndex)
            {
                var materialIndex = pair.Key;
                if (materialIndex < 0 || materialIndex >= materialCache.Length)
                    continue;

                var cacheData = materialCache[materialIndex];
                if (cacheData == null)
                    continue;

                ApplyContractToFinalMaterial(cacheData.UnityMaterial, materialIndex, pair.Value, "base");
                ApplyContractToFinalMaterial(cacheData.UnityMaterialWithVertexColor, materialIndex, pair.Value, "vertex-color");
            }
        }

        private void ApplyContractToFinalMaterial(Material material, int materialIndex, HoMaterialContract contract, string role)
        {
            if (material == null)
                return;

            ApplyContractTags(material, contract);

            var mappedToLilToon = false;
            var isLilToon = IsLilToonContract(contract);
            if (settings.applyLilToonMapping && isLilToon)
                mappedToLilToon = ApplyLilToonMapping(material, contract);

            material.SetOverrideTag("HO_MaterialContract", HoMaterialsPrincipledLilImport.ExtensionName);

            if (settings.applyUrpLitFallback && !mappedToLilToon)
                ApplyUrpLitFallback(material);

            LogImportDiagnostic(materialIndex, material.name, $"applied contract to final {role} material, shader='{(material.shader == null ? "<null>" : material.shader.name)}', mappedToLilToon={mappedToLilToon}");
        }

        private static bool IsLilToonContract(HoMaterialContract contract)
        {
            return string.Equals(contract.TargetShaderFamily, "lilToon", System.StringComparison.OrdinalIgnoreCase) ||
                   (contract.HoGltfNodeGroup != null && contract.HoGltfNodeGroup.StartsWith("HoLilToon", System.StringComparison.Ordinal));
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
            json = extension.Serialize().Value.ToString(Newtonsoft.Json.Formatting.None);
            return true;
        }

        private static void ApplyContractTags(Material material, HoMaterialContract contract)
        {
            material.SetOverrideTag("HO_TargetShaderFamily", contract.TargetShaderFamily ?? string.Empty);
            material.SetOverrideTag("HO_TargetShaderVariant", contract.TargetShaderVariant ?? string.Empty);
            material.SetOverrideTag("HO_TargetRenderingMode", contract.TargetRenderingMode ?? string.Empty);
            material.SetOverrideTag("HO_HasHoGLTFNode", contract.HasHoGltfNode ? "True" : "False");
            material.SetOverrideTag("HO_HoGLTFNodeGroup", contract.HoGltfNodeGroup ?? string.Empty);
            material.SetOverrideTag("HO_HoGLTFNodeGroupContract", contract.HoGltfNodeGroupContract ?? string.Empty);
            material.SetOverrideTag("HO_HoGLTFNodeGroupVariant", contract.HoGltfNodeGroupVariant ?? string.Empty);

            if (contract.HasUnityState)
            {
                material.SetOverrideTag("HO_UnityDoubleSided", contract.UnityDoubleSided ? "True" : "False");
                material.SetOverrideTag("HO_UnityCullMode", contract.UnityCullMode.ToString());
                material.SetOverrideTag("HO_UnityRenderQueue", contract.UnityRenderQueue.ToString());
            }
        }

#if UNITY_EDITOR
        private void AddContractAsset(string extensionName, int materialIndex, string materialName, string json)
        {
            if (context.AssetContext == null)
                return;

            var asset = ScriptableObject.CreateInstance<HoMaterialContractAsset>();
            asset.name = $"{SanitizeName(materialName)}_HoGLTF";
            asset.hideFlags = settings.hideContractSubAssets ? HideFlags.HideInHierarchy : HideFlags.None;
            asset.Initialize(extensionName, materialIndex, materialName, json);
            context.AssetContext.AddObjectToAsset(asset.name, asset);
        }
#endif

        private bool ApplyLilToonMapping(Material material, HoMaterialContract contract)
        {
            var source = CaptureUnityGltfMaterialValues(material);
            var alphaMode = GetInputInt(contract, "AlphaMode", RenderingModeToAlphaMode(contract.TargetRenderingMode));
            var transparentMode = GetInputInt(contract, "TransparentMode", 0);
            var outline = GetInputFloat(contract, "UseOutline", 0f) > 0.5f;
            var shaderName = SelectLilToonShader(contract, alphaMode, transparentMode, outline);
            var shader = Shader.Find(shaderName);
            LogImportDiagnostic(-1, material.name, $"selected shader '{shaderName}', found={shader != null}, alphaMode={alphaMode}, transparentMode={transparentMode}, outline={outline}");
            if (shader == null)
            {
                Debug.LogWarning($"HO material contract requested lilToon shader '{shaderName}', but it was not found. Contract tags were preserved.", material);
                return false;
            }

            material.shader = shader;
            LogImportDiagnostic(-1, material.name, $"assigned shader '{material.shader.name}'");

            var baseColor = GetInputColor(contract, "BaseColor", source.BaseColor);
            var baseMap = GetInputTexture(contract, source.BaseMap, "BaseTex", "BaseColorTex", "MainTex");
            baseColor.a = GetInputFloat(contract, "Alpha", baseColor.a);
            SetColor(material, "_Color", baseColor);
            SetColor(material, "_BaseColor", baseColor);
            SetTexture(material, "_MainTex", baseMap);
            SetTexture(material, "_BaseMap", baseMap);
            SetTexture(material, "_BaseColorMap", baseMap);
            SetTexture(material, "_AlphaMask", GetInputTexture(contract, null, "AlphaMaskTex", "AlphaMask"));

            SetFloat(material, "_Cutoff", GetInputFloat(contract, "AlphaCutoff", source.AlphaCutoff));
            SetFloat(material, "_Cull", GetInputFloat(contract, "CullMode", contract.HasUnityState && contract.UnityCullMode >= 0 ? contract.UnityCullMode : source.CullMode));

            var normalMap = GetInputTexture(contract, source.NormalMap, "NormalTex", "BumpMap");
            SetTexture(material, "_BumpMap", normalMap);
            SetFloat(material, "_BumpScale", GetInputFloat(contract, "NormalScale", source.NormalScale));
            SetFloat(material, "_UseBumpMap", normalMap != null ? 1f : 0f);

            SetFloat(material, "_Metallic", GetInputFloat(contract, "Metallic", source.Metallic));
            SetFloat(material, "_Smoothness", Mathf.Clamp01(1f - GetInputFloat(contract, "Roughness", source.Roughness)));
            SetFloat(material, "_Reflectance", GetInputFloat(contract, "Reflectance", 0.04f));
            SetTexture(material, "_MetallicGlossMap", GetInputTexture(contract, null, "MetallicTex", "MetallicGlossMap"));
            SetTexture(material, "_SmoothnessTex", GetInputTexture(contract, null, "SmoothnessTex"));

            ApplyLilToonShadow(material, contract);
            ApplyLilToonRim(material, contract);
            ApplyLilToonOutline(material, contract, outline);
            ApplyLilToonEmission(material, contract, source);
            ApplyLilToonSurfaceState(material, alphaMode, transparentMode, outline);

            if (contract.HasUnityState && contract.UnityRenderQueue >= 0)
                material.renderQueue = contract.UnityRenderQueue;

            return true;
        }

        private void LogImportDiagnostic(int materialIndex, string materialName, string message)
        {
            if (!settings.logImportDiagnostics)
                return;

            var index = materialIndex >= 0 ? materialIndex.ToString() : "?";
            Debug.Log($"[HoGLTF] material[{index}] '{materialName}': {message}");
        }

        private static UnityGltfMaterialValues CaptureUnityGltfMaterialValues(Material material)
        {
            return new UnityGltfMaterialValues
            {
                BaseColor = GetColor(material, "baseColorFactor", GetColor(material, "_BaseColor", Color.white)),
                BaseMap = GetTexture(material, "baseColorTexture") ?? GetTexture(material, "_BaseMap") ?? GetTexture(material, "_MainTex"),
                Metallic = GetFloat(material, "metallicFactor", GetFloat(material, "_Metallic", 0f)),
                Roughness = GetFloat(material, "roughnessFactor", 1f - GetFloat(material, "_Smoothness", 0.5f)),
                NormalMap = GetTexture(material, "normalTexture") ?? GetTexture(material, "_BumpMap"),
                NormalScale = GetFloat(material, "normalScale", GetFloat(material, "_BumpScale", 1f)),
                EmissionMap = GetTexture(material, "emissiveTexture") ?? GetTexture(material, "_EmissionMap"),
                EmissionColor = GetColor(material, "emissiveFactor", GetColor(material, "_EmissionColor", Color.black)),
                AlphaCutoff = GetFloat(material, "alphaCutoff", GetFloat(material, "_Cutoff", 0.5f)),
                CullMode = GetFloat(material, "_Cull", 2f),
            };
        }

        private static string SelectLilToonShader(HoMaterialContract contract, int alphaMode, int transparentMode, bool outline)
        {
            var variant = (contract.TargetShaderVariant ?? contract.HoGltfNodeGroupVariant ?? string.Empty).Trim();
            if (variant.Equals("lite", System.StringComparison.OrdinalIgnoreCase))
                return SelectLilToonLiteShader(alphaMode, transparentMode, outline);
            if (variant.Equals("tessellation", System.StringComparison.OrdinalIgnoreCase))
                return SelectLilToonTessellationShader(alphaMode, transparentMode, outline);
            if (variant.Equals("gem", System.StringComparison.OrdinalIgnoreCase))
                return "Hidden/lilToonGem";
            if (variant.Equals("refraction", System.StringComparison.OrdinalIgnoreCase))
                return "Hidden/lilToonRefraction";
            if (variant.Equals("fur", System.StringComparison.OrdinalIgnoreCase))
            {
                if (alphaMode == 1 || alphaMode == 2)
                    return "Hidden/lilToonFurCutout";
                if (alphaMode == 3 && transparentMode == 2)
                    return "Hidden/lilToonFurTwoPass";
                return "Hidden/lilToonFur";
            }
            if (variant.Equals("furOnly", System.StringComparison.OrdinalIgnoreCase))
            {
                if (alphaMode == 1 || alphaMode == 2)
                    return "_lil/[Optional] lilToonFurOnlyCutout";
                if (alphaMode == 3 && transparentMode == 2)
                    return "_lil/[Optional] lilToonFurOnlyTwoPass";
                return "_lil/[Optional] lilToonFurOnlyTransparent";
            }
            if (variant.Equals("fakeShadow", System.StringComparison.OrdinalIgnoreCase))
                return "_lil/[Optional] lilToonFakeShadow";

            return SelectLilToonStandardShader(alphaMode, transparentMode, outline);
        }

        private static string SelectLilToonStandardShader(int alphaMode, int transparentMode, bool outline)
        {
            if (alphaMode == 1 || alphaMode == 2)
                return outline ? "Hidden/lilToonCutoutOutline" : "Hidden/lilToonCutout";
            if (alphaMode == 3)
            {
                if (transparentMode == 1)
                    return outline ? "Hidden/lilToonOnePassTransparentOutline" : "Hidden/lilToonOnePassTransparent";
                if (transparentMode == 2)
                    return outline ? "Hidden/lilToonTwoPassTransparentOutline" : "Hidden/lilToonTwoPassTransparent";
                return outline ? "Hidden/lilToonTransparentOutline" : "Hidden/lilToonTransparent";
            }

            return outline ? "Hidden/lilToonOutline" : "lilToon";
        }

        private static string SelectLilToonLiteShader(int alphaMode, int transparentMode, bool outline)
        {
            if (alphaMode == 1 || alphaMode == 2)
                return outline ? "Hidden/lilToonLiteCutoutOutline" : "Hidden/lilToonLiteCutout";
            if (alphaMode == 3)
            {
                if (transparentMode == 1)
                    return outline ? "Hidden/lilToonLiteOnePassTransparentOutline" : "Hidden/lilToonLiteOnePassTransparent";
                if (transparentMode == 2)
                    return outline ? "Hidden/lilToonLiteTwoPassTransparentOutline" : "Hidden/lilToonLiteTwoPassTransparent";
                return outline ? "Hidden/lilToonLiteTransparentOutline" : "Hidden/lilToonLiteTransparent";
            }

            return outline ? "Hidden/lilToonLiteOutline" : "Hidden/lilToonLite";
        }

        private static string SelectLilToonTessellationShader(int alphaMode, int transparentMode, bool outline)
        {
            if (alphaMode == 1 || alphaMode == 2)
                return outline ? "Hidden/lilToonTessellationCutoutOutline" : "Hidden/lilToonTessellationCutout";
            if (alphaMode == 3)
            {
                if (transparentMode == 1)
                    return outline ? "Hidden/lilToonTessellationOnePassTransparentOutline" : "Hidden/lilToonTessellationOnePassTransparent";
                if (transparentMode == 2)
                    return outline ? "Hidden/lilToonTessellationTwoPassTransparentOutline" : "Hidden/lilToonTessellationTwoPassTransparent";
                return outline ? "Hidden/lilToonTessellationTransparentOutline" : "Hidden/lilToonTessellationTransparent";
            }

            return outline ? "Hidden/lilToonTessellationOutline" : "Hidden/lilToonTessellation";
        }

        private void ApplyLilToonShadow(Material material, HoMaterialContract contract)
        {
            var useShadow = GetInputFloat(contract, "UseShadow", 0f);
            SetFloat(material, "_UseShadow", useShadow);
            SetColor(material, "_ShadowColor", GetInputColor(contract, "ShadowColor", new Color(0.82f, 0.76f, 0.85f, 1f)));
            SetTexture(material, "_ShadowColorTex", GetInputTexture(contract, null, "ShadowTex", "ShadowColorTex"));
            SetFloat(material, "_ShadowBorder", GetInputFloat(contract, "ShadowBorder", 0.5f));
            SetFloat(material, "_ShadowBlur", GetInputFloat(contract, "ShadowBlur", 0.1f));
            SetFloat(material, "_ShadowStrength", GetInputFloat(contract, "ShadowStrength", 1f));
        }

        private void ApplyLilToonRim(Material material, HoMaterialContract contract)
        {
            SetFloat(material, "_UseRim", GetInputFloat(contract, "UseRim", 0f));
            SetColor(material, "_RimColor", GetInputColor(contract, "RimColor", new Color(0.66f, 0.5f, 0.48f, 1f)));
            SetTexture(material, "_RimColorTex", GetInputTexture(contract, null, "RimTex", "RimColorTex"));
            SetFloat(material, "_RimBorder", GetInputFloat(contract, "RimBorder", 0.5f));
            SetFloat(material, "_RimBlur", GetInputFloat(contract, "RimBlur", 0.65f));
            SetFloat(material, "_RimFresnelPower", GetInputFloat(contract, "RimFresnelPower", 3.5f));
        }

        private void ApplyLilToonOutline(Material material, HoMaterialContract contract, bool outline)
        {
            SetFloat(material, "_UseOutline", outline ? 1f : 0f);
            SetColor(material, "_OutlineColor", GetInputColor(contract, "OutlineColor", new Color(0.6f, 0.56f, 0.73f, 1f)));
            SetTexture(material, "_OutlineTex", GetInputTexture(contract, null, "OutlineTex"));
            SetTexture(material, "_OutlineWidthMask", GetInputTexture(contract, null, "OutlineWidthMask", "OutlineWidthTex"));
            SetFloat(material, "_OutlineWidth", GetInputFloat(contract, "OutlineWidth", 0.08f));
            SetFloat(material, "_OutlineFixWidth", GetInputFloat(contract, "OutlineFixWidth", 0.5f));
        }

        private void ApplyLilToonEmission(Material material, HoMaterialContract contract, UnityGltfMaterialValues source)
        {
            var emissionColor = GetInputColor(contract, "EmissionColor", source.EmissionColor);
            var emissionMap = GetInputTexture(contract, source.EmissionMap, "EmissionTex", "EmissionMap");
            var useEmission = GetInputFloat(contract, "UseEmission", emissionMap != null || emissionColor.maxColorComponent > 0.0001f ? 1f : 0f);
            SetFloat(material, "_UseEmission", useEmission);
            SetColor(material, "_EmissionColor", emissionColor);
            SetTexture(material, "_EmissionMap", emissionMap);
        }

        private static void ApplyLilToonSurfaceState(Material material, int alphaMode, int transparentMode, bool outline)
        {
            var transparent = alphaMode == 3;
            var cutout = alphaMode == 1 || alphaMode == 2;

            if (transparent)
            {
                SetFloat(material, "_SrcBlend", (float)BlendMode.One);
                SetFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                SetFloat(material, "_AlphaToMask", 0f);
                SetFloat(material, "_ZWrite", transparentMode == 2 ? 1f : 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                SetFloat(material, "_SrcBlend", (float)BlendMode.One);
                SetFloat(material, "_DstBlend", (float)BlendMode.Zero);
                SetFloat(material, "_AlphaToMask", cutout ? 1f : 0f);
                SetFloat(material, "_ZWrite", 1f);
                material.SetOverrideTag("RenderType", cutout ? "TransparentCutout" : string.Empty);
                material.renderQueue = cutout ? (int)RenderQueue.AlphaTest : -1;
            }

            if (outline)
            {
                SetFloat(material, "_OutlineSrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
                SetFloat(material, "_OutlineDstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
                SetFloat(material, "_OutlineAlphaToMask", cutout ? 1f : 0f);
                SetFloat(material, "_OutlineZWrite", 1f);
            }
        }

        private static int RenderingModeToAlphaMode(string renderingMode)
        {
            if (string.Equals(renderingMode, "Cutout", System.StringComparison.OrdinalIgnoreCase))
                return 1;
            if (string.Equals(renderingMode, "Dither", System.StringComparison.OrdinalIgnoreCase))
                return 2;
            if (string.Equals(renderingMode, "Transparent", System.StringComparison.OrdinalIgnoreCase))
                return 3;
            return 0;
        }

        private static HoMaterialContractInput FindInput(HoMaterialContract contract, string name)
        {
            if (contract.HoGltfInputs == null)
                return null;

            foreach (var input in contract.HoGltfInputs)
            {
                if (string.Equals(input.Identifier, name, System.StringComparison.Ordinal) ||
                    string.Equals(input.Name, name, System.StringComparison.Ordinal))
                {
                    return input;
                }
            }

            return null;
        }

        private static float GetInputFloat(HoMaterialContract contract, string name, float fallback)
        {
            var input = FindInput(contract, name);
            if (input == null)
                return fallback;
            if (input.ValueKind == HoMaterialContractValueKind.Number)
                return input.NumberValue;
            if (input.ValueKind == HoMaterialContractValueKind.Boolean)
                return input.BoolValue ? 1f : 0f;
            return fallback;
        }

        private static int GetInputInt(HoMaterialContract contract, string name, int fallback)
        {
            return Mathf.RoundToInt(GetInputFloat(contract, name, fallback));
        }

        private static Color GetInputColor(HoMaterialContract contract, string name, Color fallback)
        {
            var input = FindInput(contract, name);
            if (input == null || input.ValueKind != HoMaterialContractValueKind.Array)
                return fallback;

            return new Color(input.VectorValue.x, input.VectorValue.y, input.VectorValue.z, input.VectorValue.w);
        }

        private Texture GetInputTexture(HoMaterialContract contract, Texture fallback, params string[] names)
        {
            if (names == null)
                return fallback;

            foreach (var name in names)
            {
                var input = FindInput(contract, name);
                if (TryResolveLinkedTexture(input, out var texture))
                    return texture;
            }

            return fallback;
        }

        private bool TryResolveLinkedTexture(HoMaterialContractInput input, out Texture texture)
        {
            texture = null;
            if (input == null || !input.Linked || string.IsNullOrWhiteSpace(input.LinkJson) || input.LinkJson == "null")
                return false;

            try
            {
                var link = JObject.Parse(input.LinkJson);
                var image = link["image"] as JObject;

                if (TryResolveTextureAlias(image?.Value<string>("filepath"), out texture))
                    return true;
                if (TryResolveTextureAlias(image?.Value<string>("name"), out texture))
                    return true;
                if (TryResolveTextureAlias(image?.Value<string>("uri"), out texture))
                    return true;
                if (TryResolveTextureAlias(link.Value<string>("fromNode"), out texture))
                    return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to resolve HoGLTF linked texture for '{input.Name}': {ex.Message}");
            }

            return false;
        }

        private bool TryResolveTextureAlias(string alias, out Texture texture)
        {
            texture = null;
            if (string.IsNullOrWhiteSpace(alias))
                return false;

            foreach (var key in BuildTextureKeys(alias))
            {
                if (texturesByName.TryGetValue(key, out texture) && texture != null)
                    return true;
            }

#if UNITY_EDITOR
            return TryResolveTextureAsset(alias, out texture);
#else
            return false;
#endif
        }

        private static GLTFImage GetTextureImage(GLTFTexture texture)
        {
            if (texture?.Source == null)
                return null;

            try
            {
                return texture.Source.Value;
            }
            catch
            {
                return null;
            }
        }

        private void RegisterTextureName(string alias, Texture texture)
        {
            if (texture == null || string.IsNullOrWhiteSpace(alias))
                return;

            foreach (var key in BuildTextureKeys(alias))
            {
                if (!texturesByName.ContainsKey(key))
                    texturesByName.Add(key, texture);
            }
        }

        private static IEnumerable<string> BuildTextureKeys(string alias)
        {
            var normalized = NormalizeTextureKey(alias);
            if (string.IsNullOrEmpty(normalized))
                yield break;

            yield return normalized;

            var withoutBlenderPrefix = normalized.StartsWith("//", System.StringComparison.Ordinal)
                ? normalized.Substring(2)
                : normalized;
            if (!string.Equals(withoutBlenderPrefix, normalized, System.StringComparison.Ordinal))
                yield return withoutBlenderPrefix;

            var fileName = LastPathSegment(withoutBlenderPrefix);
            if (!string.IsNullOrEmpty(fileName) && !string.Equals(fileName, withoutBlenderPrefix, System.StringComparison.Ordinal))
                yield return fileName;

            var withoutExtension = Path.GetFileNameWithoutExtension(fileName);
            if (!string.IsNullOrEmpty(withoutExtension) && !string.Equals(withoutExtension, fileName, System.StringComparison.Ordinal))
                yield return withoutExtension;
        }

        private static string NormalizeTextureKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var key = value.Trim().Replace('\\', '/');
            var queryIndex = key.IndexOfAny(new[] { '?', '#' });
            if (queryIndex >= 0)
                key = key.Substring(0, queryIndex);

            try
            {
                key = System.Uri.UnescapeDataString(key);
            }
            catch
            {
            }

            return key.Trim();
        }

        private static string LastPathSegment(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var normalized = value.Replace('\\', '/');
            var lastSlash = normalized.LastIndexOf('/');
            return lastSlash >= 0 ? normalized.Substring(lastSlash + 1) : normalized;
        }

#if UNITY_EDITOR
        private bool TryResolveTextureAsset(string alias, out Texture texture)
        {
            texture = null;
            if (string.IsNullOrWhiteSpace(alias))
                return false;

            var normalized = NormalizeTextureKey(alias);
            var fileName = LastPathSegment(normalized);
            if (string.IsNullOrEmpty(fileName))
                return false;

            var gltfPath = context.FilePath;
            var gltfFolder = string.IsNullOrEmpty(gltfPath) ? string.Empty : Path.GetDirectoryName(gltfPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(gltfFolder))
            {
                foreach (var candidate in BuildSiblingTextureAssetPaths(gltfFolder, normalized, fileName))
                {
                    texture = AssetDatabase.LoadAssetAtPath<Texture>(candidate);
                    if (texture != null)
                    {
                        RegisterTextureName(candidate, texture);
                        RegisterTextureName(fileName, texture);
                        return true;
                    }
                }
            }

            var searchFolder = !string.IsNullOrEmpty(gltfFolder) && gltfFolder.StartsWith("Assets", System.StringComparison.Ordinal)
                ? new[] { gltfFolder }
                : null;
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var guids = searchFolder == null
                ? AssetDatabase.FindAssets($"{nameWithoutExtension} t:Texture")
                : AssetDatabase.FindAssets($"{nameWithoutExtension} t:Texture", searchFolder);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileName(path).Equals(fileName, System.StringComparison.OrdinalIgnoreCase) &&
                    !Path.GetFileNameWithoutExtension(path).Equals(nameWithoutExtension, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                if (texture == null)
                    continue;

                RegisterTextureName(path, texture);
                RegisterTextureName(fileName, texture);
                return true;
            }

            return false;
        }

        private static IEnumerable<string> BuildSiblingTextureAssetPaths(string gltfFolder, string normalizedAlias, string fileName)
        {
            if (!string.IsNullOrEmpty(normalizedAlias) && normalizedAlias.StartsWith("Assets/", System.StringComparison.Ordinal))
                yield return normalizedAlias;

            if (!string.IsNullOrEmpty(normalizedAlias) && !Path.IsPathRooted(normalizedAlias) && !normalizedAlias.StartsWith("//", System.StringComparison.Ordinal))
                yield return CombineAssetPath(gltfFolder, normalizedAlias);

            yield return CombineAssetPath(gltfFolder, fileName);
            yield return CombineAssetPath(gltfFolder, "textures/" + fileName);
            yield return CombineAssetPath(gltfFolder, "Textures/" + fileName);
            yield return CombineAssetPath(gltfFolder, "tex/" + fileName);
            yield return CombineAssetPath(gltfFolder, "Tex/" + fileName);
        }

        private static string CombineAssetPath(string folder, string relative)
        {
            return (folder.TrimEnd('/', '\\') + "/" + relative.TrimStart('/', '\\')).Replace('\\', '/');
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

        private struct UnityGltfMaterialValues
        {
            public Color BaseColor;
            public Texture BaseMap;
            public float Metallic;
            public float Roughness;
            public Texture NormalMap;
            public float NormalScale;
            public Texture EmissionMap;
            public Color EmissionColor;
            public float AlphaCutoff;
            public float CullMode;
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
