using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace lilToon.UnityGLTF.Extensions.Editor
{
    internal static class HoMaterialContractDebugMenu
    {
        private const string MenuPath = "Assets/lilToon UnityGLTF/打印 HoGLTF 材质契约输入";

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

        private static List<HoMaterialContractAsset> FindContractsInSelection()
        {
            var result = new List<HoMaterialContractAsset>();
            var seen = new HashSet<int>();

            foreach (var selected in Selection.objects)
            {
                AddContract(selected, result, seen);

                var path = AssetDatabase.GetAssetPath(selected);
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
            builder.AppendLine("没有在当前选择中找到 HoMaterialContractAsset。");
            builder.AppendLine("常见原因：glTF/GLB 里没有 HO_materials_principled_lil 扩展，或导入后还没有重新导入该资源。");
            builder.AppendLine("当前选择诊断：");

            foreach (var selected in Selection.objects)
            {
                if (selected == null)
                    continue;

                var path = AssetDatabase.GetAssetPath(selected);
                builder.AppendLine($"- {selected.name} ({selected.GetType().FullName})");
                builder.AppendLine($"  Path: {(string.IsNullOrEmpty(path) ? "<none>" : path)}");

                if (string.IsNullOrEmpty(path))
                    continue;

                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                if (assets == null || assets.Length == 0)
                {
                    builder.AppendLine("  SubAssets: 无");
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

        private static string BuildMessage(HoMaterialContractAsset contract)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"HoGLTF 材质契约: {contract.name}");
            builder.AppendLine($"  Material: [{contract.MaterialIndex}] {contract.MaterialName}");
            builder.AppendLine($"  Extension: {contract.ExtensionName}");
            builder.AppendLine($"  Target: {contract.TargetShaderFamily}");
            builder.AppendLine($"  HoGLTF Node: {(contract.HasHoGltfNode ? contract.HoGltfNodeGroup : "无")}");

            if (contract.HoGltfInputs == null || contract.HoGltfInputs.Length == 0)
            {
                builder.AppendLine("  Inputs: 无");
                return builder.ToString();
            }

            builder.AppendLine("  Inputs:");
            foreach (var input in contract.HoGltfInputs)
                builder.AppendLine($"    {input.Name} = {FormatValue(input)} ({input.ValueKind}, {input.SocketType})");

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
