# lilToon UnityGLTF Extensions

这个包为 UnityGLTF 增加 Hollow/lil 材质契约的导入支持。它不是 UnityGLTF fork，而是一个小型 import plugin 包，用来在 UnityGLTF 仍然保留原始 `GLTFMaterial.Extensions` 数据时读取自定义 glTF 材质扩展。

## 在整套系统里的定位

`lilToon-UnityGLTF-Extensions` 是 Blender/glTF 创作数据进入 Unity shader 包的导入桥：

- `lilToon`：NPR/角色材质目标。
- `lilPBR`：物理/场景材质目标。
- `lilToon-URP-Extensions`：后续消费材质语义的 RendererFeature 层。
- `lilToon/接口契约.md`：Blender Principled、OpenPBR 和 lil 系列材质之间的共享契约。

## 支持的扩展名

主扩展：

```text
HO_materials_principled_lil
```

旧扩展别名：

```text
HO_materials_openpbr_lil
```

## 当前行为

`Runtime/HoMaterialsPrincipledLilImport.cs` 注册了一个 UnityGLTF `GLTFImportPlugin`，显示名为 `HO_materials_principled_lil`。

导入材质时，它可以：

- 检测 Hollow 材质契约扩展；
- 把原始 extension JSON 保存成 `HoMaterialContractAsset` 子资源；
- 解析 schema、目标 shader family 和 HoGLTF 节点/socket 数据；
- 给导入后的 Unity 材质写入 contract metadata tag；
- 当契约目标是 lilToon 时，可直接切换到 lilToon shader 并写入 HoLil socket 值；
- 可选地应用临时 URP/Lit fallback 映射。

fallback 只是过渡入口。长期目标是把保存下来的契约直接映射成 lilToon 或 lilPBR 材质。

## lilToon 透明度策略

导入器会把契约里的 alpha 判断写成 Unity 材质 tag：

- `HO_AlphaMode`：数字值，`0=Opaque`、`1=Cutout`、`2=Dither`、`3=Transparent`。
- `HO_AlphaModeHint`：可读提示名。

lilToon 自动映射只应用不透明和镂空两类材质状态。即使契约里出现 `Transparent`，导入器也只保留上述 tag，不会强制切到半透明 shader，也不会写 `material.renderQueue`。半透明应在 Unity 侧按 lilToon 自己的预设手动切换；`unity.renderQueue` 只会作为 `HO_UnityRenderQueue` tag 保留给调试或后续工具参考。

## 重要文件

- `Runtime/HoMaterialsPrincipledLilImport.cs`：UnityGLTF 插件入口、lilToon 映射和 fallback 材质映射。
- `Runtime/HoMaterialContractParser.cs`：解析 schema、target 和 HoGLTF 节点输入。
- `Runtime/HoMaterialContractAsset.cs`：保存原始 JSON 和解析结果的 ScriptableObject 子资源。
- `Editor/HoMaterialContractDebugMenu.cs`：打印契约输入的调试菜单。
- `Documentation~/`：设计说明和 UnityGLTF 参考记录。

## 安装

先安装 UnityGLTF，再安装这个包：

```json
{
  "dependencies": {
    "org.khronos.unitygltf": "https://github.com/KhronosGroup/UnityGLTF.git",
    "jp.lilxyzw.liltoon.unitygltf.extensions": "file:D:/Unity_Fork/lilToon-UnityGLTF-Extensions"
  }
}
```

编译通过后，在这里启用插件：

```text
Project Settings > UnityGLTF > Import > Import Extensions and Plugins
```

## 调试导入结果

开启 `preserveContractJson` 后，导入的 glTF 资源下会出现 `HoMaterialContractAsset` 子资源。选中它可以查看解析出的 `HoGltfInputs`，也可以用编辑器右键菜单把契约值打印到 Unity Console。

## 包依赖

直接依赖：

- `com.unity.nuget.newtonsoft-json`

同时要求同一 Unity 项目里已经有 UnityGLTF 相关程序集，例如 `UnityGLTFScripts` 和 `GLTFSerialization`。
