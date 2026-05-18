using UnityEngine;

namespace lilToon.UnityGLTF.Extensions
{
    public enum HoMaterialContractValueKind
    {
        Null,
        Number,
        Boolean,
        String,
        Array,
        Object
    }

    [System.Serializable]
    public sealed class HoMaterialContractInput
    {
        [SerializeField] private string name;
        [SerializeField] private string identifier;
        [SerializeField] private string socketType;
        [SerializeField] private bool linked;
        [SerializeField] private HoMaterialContractValueKind valueKind;
        [SerializeField] private string valueJson;
        [SerializeField] private float numberValue;
        [SerializeField] private bool boolValue;
        [SerializeField] private string stringValue;
        [SerializeField] private Vector4 vectorValue;

        public string Name => name;
        public string Identifier => identifier;
        public string SocketType => socketType;
        public bool Linked => linked;
        public HoMaterialContractValueKind ValueKind => valueKind;
        public string ValueJson => valueJson;
        public float NumberValue => numberValue;
        public bool BoolValue => boolValue;
        public string StringValue => stringValue;
        public Vector4 VectorValue => vectorValue;

        public void Initialize(
            string name,
            string identifier,
            string socketType,
            bool linked,
            HoMaterialContractValueKind valueKind,
            string valueJson,
            float numberValue,
            bool boolValue,
            string stringValue,
            Vector4 vectorValue)
        {
            this.name = name;
            this.identifier = identifier;
            this.socketType = socketType;
            this.linked = linked;
            this.valueKind = valueKind;
            this.valueJson = valueJson;
            this.numberValue = numberValue;
            this.boolValue = boolValue;
            this.stringValue = stringValue;
            this.vectorValue = vectorValue;
        }
    }

    public sealed class HoMaterialContractAsset : ScriptableObject
    {
        [SerializeField] private string extensionName;
        [SerializeField] private int materialIndex = -1;
        [SerializeField] private string materialName;
        [SerializeField] private string json;
        [SerializeField] private string schema;
        [SerializeField] private int schemaVersion;
        [SerializeField] private string targetShaderFamily;
        [SerializeField] private bool hasHoGltfNode;
        [SerializeField] private string hoGltfNode;
        [SerializeField] private string hoGltfNodeLabel;
        [SerializeField] private string hoGltfNodeGroup;
        [SerializeField] private HoMaterialContractInput[] hoGltfInputs = System.Array.Empty<HoMaterialContractInput>();

        public string ExtensionName => extensionName;
        public int MaterialIndex => materialIndex;
        public string MaterialName => materialName;
        public string Json => json;
        public string Schema => schema;
        public int SchemaVersion => schemaVersion;
        public string TargetShaderFamily => targetShaderFamily;
        public bool HasHoGltfNode => hasHoGltfNode;
        public string HoGltfNode => hoGltfNode;
        public string HoGltfNodeLabel => hoGltfNodeLabel;
        public string HoGltfNodeGroup => hoGltfNodeGroup;
        public HoMaterialContractInput[] HoGltfInputs => hoGltfInputs;

        public void Initialize(string extensionName, int materialIndex, string materialName, string json)
        {
            this.extensionName = extensionName;
            this.materialIndex = materialIndex;
            this.materialName = materialName;
            this.json = json;

            if (HoMaterialContractParser.TryParse(extensionName, json, out var contract))
                ApplyContract(contract);
        }

        private void ApplyContract(HoMaterialContract contract)
        {
            schema = contract.Schema;
            schemaVersion = contract.SchemaVersion;
            targetShaderFamily = contract.TargetShaderFamily;
            hasHoGltfNode = contract.HasHoGltfNode;
            hoGltfNode = contract.HoGltfNode;
            hoGltfNodeLabel = contract.HoGltfNodeLabel;
            hoGltfNodeGroup = contract.HoGltfNodeGroup;
            hoGltfInputs = contract.HoGltfInputs ?? System.Array.Empty<HoMaterialContractInput>();
        }
    }
}
