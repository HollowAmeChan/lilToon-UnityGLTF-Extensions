using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace lilToon.UnityGLTF.Extensions.Editor
{
    internal static class HoMaterialContractDebugMenu
    {
        private const string MenuPath = "Assets/lilToon UnityGLTF/Print HoGLTF Material Contract";
        private const string ForceReimportMenuPath = "Assets/lilToon UnityGLTF/Force Reimport And Print HoGLTF Materials";

        [MenuItem(MenuPath, true)]
        private static bool ValidatePrintSelectedContracts()
        {
            return Selection.objects != null && Selection.objects.Length > 0;
        }

        [MenuItem(MenuPath)]
        private static void PrintSelectedContracts()
        {
            var contracts = FindContractsInSelection();
            if (contracts.Count == 0)
            {
                Debug.LogWarning(BuildMissingContractDiagnostic());
                return;
            }

            foreach (var contract in contracts)
                Debug.Log(BuildMessage(contract), contract);
        }

        [MenuItem(ForceReimportMenuPath, true)]
        private static bool ValidateForceReimportAndPrint()
        {
            return Selection.objects != null && Selection.objects.Length > 0;
        }

        [MenuItem(ForceReimportMenuPath)]
        private static void ForceReimportAndPrint()
        {
            foreach (var selected in Selection.objects)
            {
                var path = ResolveAssetPath(selected);
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogWarning($"Cannot resolve an asset path for selection '{selected.name}' ({selected.GetType().FullName}). Select the .gltf asset in the Project window, or select an instance that still has a prefab source.");
                    Debug.Log(BuildSceneObjectMaterialDiagnostic(selected), selected);
                    continue;
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                Debug.Log(BuildMaterialDiagnostic(path), selected);
            }
        }

        private static List<HoMaterialContractAsset> FindContractsInSelection()
        {
            var result = new List<HoMaterialContractAsset>();
            var seen = new HashSet<int>();

            foreach (var selected in Selection.objects)
            {
                AddContract(selected, result, seen);

                var path = ResolveAssetPath(selected);
                if (string.IsNullOrEmpty(path))
                    continue;

                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    AddContract(asset, result, seen);
            }

            return result;
        }

        private static void AddContract(Object asset, List<HoMaterialContractAsset> result, HashSet<int> seen)
        {
            var contract = asset as HoMaterialContractAsset;
            if (contract == null)
                return;

            var id = contract.GetInstanceID();
            if (!seen.Add(id))
                return;

            result.Add(contract);
        }

        private static string BuildMissingContractDiagnostic()
        {
            var builder = new StringBuilder();
            builder.AppendLine("No HoMaterialContractAsset found in the current selection.");
            builder.AppendLine("Common causes: the glTF/GLB has no HO_materials_principled_lil extension, or the asset has not been reimported.");
            builder.AppendLine("Current selection:");

            foreach (var selected in Selection.objects)
            {
                if (selected == null)
                    continue;

                var path = ResolveAssetPath(selected);
                builder.AppendLine($"- {selected.name} ({selected.GetType().FullName})");
                builder.AppendLine($"  Path: {(string.IsNullOrEmpty(path) ? "<none>" : path)}");

                if (string.IsNullOrEmpty(path))
                    continue;

                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                if (assets == null || assets.Length == 0)
                {
                    builder.AppendLine("  SubAssets: none");
                    continue;
                }

                builder.AppendLine("  SubAssets:");
                foreach (var asset in assets)
                {
                    if (asset == null)
                        continue;

                    builder.AppendLine($"    - {asset.name} ({asset.GetType().FullName})");
                }
            }

            return builder.ToString();
        }

        private static string ResolveAssetPath(Object selected)
        {
            if (selected == null)
                return string.Empty;

            var path = AssetDatabase.GetAssetPath(selected);
            if (!string.IsNullOrEmpty(path))
                return path;

            var gameObject = selected as GameObject;
            if (gameObject != null)
            {
                var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                path = AssetDatabase.GetAssetPath(prefabSource);
                if (!string.IsNullOrEmpty(path))
                    return path;

                var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
                if (prefabRoot != null)
                {
                    prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(prefabRoot);
                    path = AssetDatabase.GetAssetPath(prefabSource);
                    if (!string.IsNullOrEmpty(path))
                        return path;
                }
            }

            var component = selected as Component;
            if (component != null)
                return ResolveAssetPath(component.gameObject);

            return string.Empty;
        }

        private static string BuildSceneObjectMaterialDiagnostic(Object selected)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"HoGLTF scene object diagnostic: {selected.name} ({selected.GetType().FullName})");

            var gameObject = selected as GameObject;
            var component = selected as Component;
            if (gameObject == null && component != null)
                gameObject = component.gameObject;

            if (gameObject == null)
            {
                builder.AppendLine("  Selection is not a GameObject or Component.");
                return builder.ToString();
            }

            var renderers = gameObject.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                builder.AppendLine("  Renderers: none");
                return builder.ToString();
            }

            foreach (var renderer in renderers)
            {
                builder.AppendLine($"  Renderer: {renderer.name}");
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        builder.AppendLine("    Material: <null>");
                        continue;
                    }

                    builder.AppendLine($"    Material: {material.name}");
                    builder.AppendLine($"      Shader: {(material.shader == null ? "<null>" : material.shader.name)}");
                    builder.AppendLine($"      AssetPath: {AssetDatabase.GetAssetPath(material)}");
                    builder.AppendLine($"      HO_MaterialContract: {material.GetTag("HO_MaterialContract", false, "<none>")}");
                    builder.AppendLine($"      HO_TargetShaderFamily: {material.GetTag("HO_TargetShaderFamily", false, "<none>")}");
                    builder.AppendLine($"      HO_HoGLTFNodeGroup: {material.GetTag("HO_HoGLTFNodeGroup", false, "<none>")}");
                }
            }

            return builder.ToString();
        }

        private static string BuildMaterialDiagnostic(string path)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"HoGLTF imported asset diagnostic: {path}");

            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null || assets.Length == 0)
            {
                builder.AppendLine("  SubAssets: none");
                return builder.ToString();
            }

            foreach (var asset in assets)
            {
                var material = asset as Material;
                if (material == null)
                    continue;

                builder.AppendLine($"  Material: {material.name}");
                builder.AppendLine($"    Shader: {(material.shader == null ? "<null>" : material.shader.name)}");
                builder.AppendLine($"    HO_MaterialContract: {material.GetTag("HO_MaterialContract", false, "<none>")}");
                builder.AppendLine($"    HO_TargetShaderFamily: {material.GetTag("HO_TargetShaderFamily", false, "<none>")}");
                builder.AppendLine($"    HO_HoGLTFNodeGroup: {material.GetTag("HO_HoGLTFNodeGroup", false, "<none>")}");
                builder.AppendLine($"    MainTex: {FormatTexture(material, "_MainTex")}");
                builder.AppendLine($"    ShadowColorTex: {FormatTexture(material, "_ShadowColorTex")}");
            }

            foreach (var contract in FindContractsAtPath(path))
                builder.AppendLine(BuildMessage(contract));

            return builder.ToString();
        }

        private static List<HoMaterialContractAsset> FindContractsAtPath(string path)
        {
            var result = new List<HoMaterialContractAsset>();
            var seen = new HashSet<int>();

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                AddContract(asset, result, seen);

            return result;
        }

        private static string FormatTexture(Material material, string propertyName)
        {
            if (!material.HasProperty(propertyName))
                return "<no property>";

            var texture = material.GetTexture(propertyName);
            return texture == null ? "<null>" : texture.name;
        }

        private static string BuildMessage(HoMaterialContractAsset contract)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"HoGLTF material contract: {contract.name}");
            builder.AppendLine($"  Material: [{contract.MaterialIndex}] {contract.MaterialName}");
            builder.AppendLine($"  Extension: {contract.ExtensionName}");
            builder.AppendLine($"  Source: {contract.SourceTool} / {contract.SourceBlenderMaterial}");
            builder.AppendLine($"  Target: {contract.TargetShaderFamily} / {contract.TargetShaderVariant} / {contract.TargetRenderingMode}");

            if (contract.HasUnityState)
                builder.AppendLine($"  Unity: cull={contract.UnityCullMode}, doubleSided={contract.UnityDoubleSided}, renderQueue={contract.UnityRenderQueue}");

            builder.AppendLine($"  HoGLTF Node: {(contract.HasHoGltfNode ? contract.HoGltfNodeGroup : "none")}");
            if (contract.HasHoGltfNode)
                builder.AppendLine($"  HoGLTF Contract: {contract.HoGltfNodeGroupContract} / {contract.HoGltfNodeGroupVariant}");

            if (contract.HoGltfInputs == null || contract.HoGltfInputs.Length == 0)
            {
                builder.AppendLine("  Inputs: none");
                return builder.ToString();
            }

            builder.AppendLine("  Inputs:");
            foreach (var input in contract.HoGltfInputs)
            {
                builder.AppendLine(
                    $"    {input.Name} = {FormatValue(input)} " +
                    $"({input.ValueKind}, {input.SocketType}, {input.SocketGroup}/{input.Role}, target={input.Target})");
                if (input.Linked && !string.IsNullOrEmpty(input.LinkJson))
                    builder.AppendLine($"      Link: {input.LinkJson}");
            }

            return builder.ToString();
        }

        private static string FormatValue(HoMaterialContractInput input)
        {
            switch (input.ValueKind)
            {
                case HoMaterialContractValueKind.Number:
                    return input.NumberValue.ToString("0.########");
                case HoMaterialContractValueKind.Boolean:
                    return input.BoolValue ? "true" : "false";
                case HoMaterialContractValueKind.String:
                    return input.StringValue ?? string.Empty;
                case HoMaterialContractValueKind.Array:
                    return input.VectorValue.ToString("F4");
                default:
                    return input.ValueJson ?? "null";
            }
        }
    }
}
