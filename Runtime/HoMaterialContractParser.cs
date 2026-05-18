using Newtonsoft.Json.Linq;
using UnityEngine;

namespace lilToon.UnityGLTF.Extensions
{
    public sealed class HoMaterialContract
    {
        public string Schema { get; set; }
        public int SchemaVersion { get; set; }
        public string TargetShaderFamily { get; set; }
        public bool HasHoGltfNode { get; set; }
        public string HoGltfNode { get; set; }
        public string HoGltfNodeLabel { get; set; }
        public string HoGltfNodeGroup { get; set; }
        public HoMaterialContractInput[] HoGltfInputs { get; set; } = System.Array.Empty<HoMaterialContractInput>();
    }

    public static class HoMaterialContractParser
    {
        public static bool TryParse(string extensionName, string json, out HoMaterialContract contract)
        {
            contract = null;

            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                var token = JToken.Parse(json);
                var body = UnwrapExtension(extensionName, token);
                if (!(body is JObject obj))
                    return false;

                contract = new HoMaterialContract
                {
                    Schema = obj.Value<string>("schema"),
                    SchemaVersion = obj.Value<int?>("schemaVersion") ?? 0,
                    TargetShaderFamily = obj.SelectToken("target.shaderFamily")?.Value<string>(),
                };

                ReadHoGltfNode(obj["hogltf"] as JObject, contract);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to parse HO material contract JSON: {ex.Message}");
                return false;
            }
        }

        private static JToken UnwrapExtension(string extensionName, JToken token)
        {
            if (token is JObject obj &&
                !string.IsNullOrEmpty(extensionName) &&
                obj.TryGetValue(extensionName, out var wrapped))
            {
                return wrapped;
            }

            return token;
        }

        private static void ReadHoGltfNode(JObject nodeObj, HoMaterialContract contract)
        {
            if (nodeObj == null)
                return;

            contract.HasHoGltfNode = true;
            contract.HoGltfNode = nodeObj.Value<string>("node");
            contract.HoGltfNodeLabel = nodeObj.Value<string>("nodeLabel");
            contract.HoGltfNodeGroup = nodeObj.Value<string>("nodeGroup");
            contract.HoGltfInputs = ReadInputs(nodeObj);
        }

        private static HoMaterialContractInput[] ReadInputs(JObject nodeObj)
        {
            var sockets = nodeObj["sockets"] as JArray;
            if (sockets != null && sockets.Count > 0)
                return ReadSocketArray(sockets);

            var inputs = nodeObj["inputs"] as JObject;
            if (inputs == null)
                return System.Array.Empty<HoMaterialContractInput>();

            var result = new HoMaterialContractInput[inputs.Count];
            var index = 0;
            foreach (var property in inputs.Properties())
            {
                result[index++] = CreateInput(
                    property.Name,
                    property.Name,
                    string.Empty,
                    false,
                    property.Value);
            }

            return result;
        }

        private static HoMaterialContractInput[] ReadSocketArray(JArray sockets)
        {
            var result = new HoMaterialContractInput[sockets.Count];

            for (var i = 0; i < sockets.Count; i++)
            {
                var socketObj = sockets[i] as JObject;
                if (socketObj == null)
                {
                    result[i] = CreateInput(string.Empty, string.Empty, string.Empty, false, sockets[i]);
                    continue;
                }

                result[i] = CreateInput(
                    socketObj.Value<string>("name"),
                    socketObj.Value<string>("identifier"),
                    socketObj.Value<string>("type"),
                    socketObj.Value<bool?>("linked") ?? false,
                    socketObj["value"]);
            }

            return result;
        }

        private static HoMaterialContractInput CreateInput(
            string name,
            string identifier,
            string socketType,
            bool linked,
            JToken value)
        {
            var input = new HoMaterialContractInput();
            var kind = GetValueKind(value);
            var valueJson = value?.ToString(Newtonsoft.Json.Formatting.None) ?? "null";
            var numberValue = kind == HoMaterialContractValueKind.Number ? value.Value<float>() : 0f;
            var boolValue = kind == HoMaterialContractValueKind.Boolean && value.Value<bool>();
            var stringValue = kind == HoMaterialContractValueKind.String ? value.Value<string>() : string.Empty;
            var vectorValue = kind == HoMaterialContractValueKind.Array ? ReadVector(value as JArray) : Vector4.zero;

            input.Initialize(
                name ?? string.Empty,
                identifier ?? string.Empty,
                socketType ?? string.Empty,
                linked,
                kind,
                valueJson,
                numberValue,
                boolValue,
                stringValue,
                vectorValue);
            return input;
        }

        private static HoMaterialContractValueKind GetValueKind(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
                return HoMaterialContractValueKind.Null;

            switch (value.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    return HoMaterialContractValueKind.Number;
                case JTokenType.Boolean:
                    return HoMaterialContractValueKind.Boolean;
                case JTokenType.String:
                    return HoMaterialContractValueKind.String;
                case JTokenType.Array:
                    return HoMaterialContractValueKind.Array;
                case JTokenType.Object:
                    return HoMaterialContractValueKind.Object;
                default:
                    return HoMaterialContractValueKind.String;
            }
        }

        private static Vector4 ReadVector(JArray array)
        {
            if (array == null)
                return Vector4.zero;

            return new Vector4(
                ReadFloat(array, 0),
                ReadFloat(array, 1),
                ReadFloat(array, 2),
                ReadFloat(array, 3));
        }

        private static float ReadFloat(JArray array, int index)
        {
            if (index < 0 || index >= array.Count)
                return 0f;

            return array[index].Type == JTokenType.Integer || array[index].Type == JTokenType.Float
                ? array[index].Value<float>()
                : 0f;
        }
    }
}
