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
        [SerializeField] private string description;
        [SerializeField] private string priority;
        [SerializeField] private string target;
        [SerializeField] private string socketGroup;
        [SerializeField] private string role;
        [SerializeField] private string blend;
        [SerializeField] private string metadataJson;
        [SerializeField] private string linkJson;
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
        public string Description => description;
        public string Priority => priority;
        public string Target => target;
        public string SocketGroup => socketGroup;
        public string Role => role;
        public string Blend => blend;
        public string MetadataJson => metadataJson;
        public string LinkJson => linkJson;
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
            string description,
            string priority,
            string target,
            string socketGroup,
            string role,
            string blend,
            string metadataJson,
            string linkJson,
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
            this.description = description;
            this.priority = priority;
            this.target = target;
            this.socketGroup = socketGroup;
            this.role = role;
            this.blend = blend;
            this.metadataJson = metadataJson;
            this.linkJson = linkJson;
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
        [SerializeField] private string sourceTool;
        [SerializeField] private string sourceBlenderMaterial;
        [SerializeField] private string targetShaderFamily;
        [SerializeField] private string targetShaderVariant;
        [SerializeField] private string targetRenderingMode;
        [SerializeField] private bool hasUnityState;
        [SerializeField] private bool unityDoubleSided;
        [SerializeField] private int unityCullMode = -1;
        [SerializeField] private int unityRenderQueue = -1;
        [SerializeField] private string principledJson;
        [SerializeField] private string toonJson;
        [SerializeField] private string unityJson;
        [SerializeField] private string extrasJson;
        [SerializeField] private bool hasHoGltfNode;
        [SerializeField] private string hoGltfNode;
        [SerializeField] private string hoGltfNodeLabel;
        [SerializeField] private string hoGltfNodeGroup;
        [SerializeField] private string hoGltfNodeGroupContract;
        [SerializeField] private string hoGltfNodeGroupVariant;
        [SerializeField] private HoMaterialContractInput[] hoGltfInputs = System.Array.Empty<HoMaterialContractInput>();

        public string ExtensionName => extensionName;
        public int MaterialIndex => materialIndex;
        public string MaterialName => materialName;
        public string Json => json;
        public string Schema => schema;
        public int SchemaVersion => schemaVersion;
        public string SourceTool => sourceTool;
        public string SourceBlenderMaterial => sourceBlenderMaterial;
        public string TargetShaderFamily => targetShaderFamily;
        public string TargetShaderVariant => targetShaderVariant;
        public string TargetRenderingMode => targetRenderingMode;
        public bool HasUnityState => hasUnityState;
        public bool UnityDoubleSided => unityDoubleSided;
        public int UnityCullMode => unityCullMode;
        public int UnityRenderQueue => unityRenderQueue;
        public string PrincipledJson => principledJson;
        public string ToonJson => toonJson;
        public string UnityJson => unityJson;
        public string ExtrasJson => extrasJson;
        public bool HasHoGltfNode => hasHoGltfNode;
        public string HoGltfNode => hoGltfNode;
        public string HoGltfNodeLabel => hoGltfNodeLabel;
        public string HoGltfNodeGroup => hoGltfNodeGroup;
        public string HoGltfNodeGroupContract => hoGltfNodeGroupContract;
        public string HoGltfNodeGroupVariant => hoGltfNodeGroupVariant;
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
            sourceTool = contract.SourceTool;
            sourceBlenderMaterial = contract.SourceBlenderMaterial;
            targetShaderFamily = contract.TargetShaderFamily;
            targetShaderVariant = contract.TargetShaderVariant;
            targetRenderingMode = contract.TargetRenderingMode;
            hasUnityState = contract.HasUnityState;
            unityDoubleSided = contract.UnityDoubleSided;
            unityCullMode = contract.UnityCullMode;
            unityRenderQueue = contract.UnityRenderQueue;
            principledJson = contract.PrincipledJson;
            toonJson = contract.ToonJson;
            unityJson = contract.UnityJson;
            extrasJson = contract.ExtrasJson;
            hasHoGltfNode = contract.HasHoGltfNode;
            hoGltfNode = contract.HoGltfNode;
            hoGltfNodeLabel = contract.HoGltfNodeLabel;
            hoGltfNodeGroup = contract.HoGltfNodeGroup;
            hoGltfNodeGroupContract = contract.HoGltfNodeGroupContract;
            hoGltfNodeGroupVariant = contract.HoGltfNodeGroupVariant;
            hoGltfInputs = contract.HoGltfInputs ?? System.Array.Empty<HoMaterialContractInput>();
        }
    }
}
