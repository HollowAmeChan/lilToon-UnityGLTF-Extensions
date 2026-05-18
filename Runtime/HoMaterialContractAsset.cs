using UnityEngine;

namespace lilToon.UnityGLTF.Extensions
{
    public sealed class HoMaterialContractAsset : ScriptableObject
    {
        [SerializeField] private string extensionName;
        [SerializeField] private int materialIndex = -1;
        [SerializeField] private string materialName;
        [SerializeField] private string json;

        public string ExtensionName => extensionName;
        public int MaterialIndex => materialIndex;
        public string MaterialName => materialName;
        public string Json => json;

        public void Initialize(string extensionName, int materialIndex, string materialName, string json)
        {
            this.extensionName = extensionName;
            this.materialIndex = materialIndex;
            this.materialName = materialName;
            this.json = json;
        }
    }
}
