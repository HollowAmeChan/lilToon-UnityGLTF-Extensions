# Liltoon UnityGLTF Extensions 开发说明

这个仓库是一个独立的 Unity Package，目的不是 fork UnityGLTF，而是在
UnityGLTF 的导入流程里接住自己的 glTF 材质契约。

## 为什么要做这个包

UnityGLTF 默认会把 glTF 材质导成自己的 PBRGraph 材质。导入完成以后再右键转
URP/Lit 或 lilToon，只能读到已经写进 `.mat` 的 Unity shader property。

如果 glTF 里有自定义材质契约，比如：

```text
material.extensions.HO_materials_principled_lil
```

普通 `.mat` 转换器看不到这段原始 JSON。这个包把转换入口前移到
UnityGLTF import plugin，这样在 `GLTFMaterial.Extensions` 还活着的时候读取并保
存契约。

## 当前实现

入口类：

```text
Runtime/HoMaterialsPrincipledLilImport.cs
```

它继承：

```csharp
UnityGLTF.Plugins.GLTFImportPlugin
```

UnityGLTF 会自动扫描所有程序集里的 import plugin，并把它显示在：

```text
Project Settings > UnityGLTF > Import > Import Extensions and Plugins
```

当前插件名：

```text
HO_materials_principled_lil
```

目前做了三件事：

1. 检查 `HO_materials_principled_lil`，兼容旧名 `HO_materials_openpbr_lil`。
2. 把原始 extension JSON 保存成 `HoMaterialContractAsset` 子资源。
3. 提供一个临时的 `applyUrpLitFallback`，把 UnityGLTF/PBRGraph 风格属性切到
   URP/Lit。

第三步只是过渡口。最终目标是直接把契约映射到 lilToon 或 lilPBR。

## 重要结论

UnityGLTF/GLTFSerialization 对未知 extension 会使用 `DefaultExtension` 保存
原始 JSON，所以第一版不需要先写 typed extension factory。

也就是说，只要 glTF 材质里真的带了：

```json
"extensions": {
  "HO_materials_principled_lil": {}
}
```

UnityGLTF 反序列化以后，插件可以从 `GLTFMaterial.Extensions` 里取到它。

以后如果要做强类型解析，再添加自己的 factory 和 contract model。

## 下一步怎么写

建议保持 plugin 入口稳定，把真正的材质映射拆出去：

```text
Runtime/
  HoMaterialsPrincipledLilImport.cs
  HoMaterialContractAsset.cs
  Mapping/
    HoContractReader.cs
    HoTextureResolver.cs
    LilToonMaterialMapper.cs
    LilPbrMaterialMapper.cs
    UrpLitMaterialMapper.cs
```

推荐策略：

1. `target.shaderFamily == lilToon` 时直接生成 lilToon 材质。
2. `target.shaderFamily == lilPBR` 时直接生成 lilPBR 材质。
3. `target.shaderFamily == auto` 时，看 toon block 是否启用风格化；启用则
   lilToon，否则 lilPBR。
4. 如果目标 shader 不存在，只保存契约，不破坏 UnityGLTF 默认材质。

不要从 PBRGraph 反推自定义材质含义。只要 contract 存在，它就是 source of
truth。

## 参考仓库和文件

当前 BREAK_URP 项目的 UnityGLTF 参考路径：

```text
D:/Unity_Project/BREAK_URP/Library/PackageCache/org.khronos.unitygltf@ce6a5dce952d
```

重点文件：

```text
Runtime/Scripts/Plugins/Core/GltfPlugin.cs
Runtime/Scripts/Plugins/Core/GltfImportPlugin.cs
Runtime/Scripts/Plugins/Core/ImportContext.cs
Runtime/Scripts/GLTFSettings.cs
Runtime/Scripts/SceneImporter/ImporterMaterials.cs
Runtime/Scripts/Plugins/MaterialExtensionsImport.cs
Editor/Scripts/GLTFSettingsInspector.cs
Editor/Scripts/Plugins/GLTFPluginEditor.cs
Runtime/Plugins/GLTFSerialization/Schema/IExtension.cs
```

本地同系列包参考：

```text
D:/Unity_Fork/lilToon-URP-Extensions
D:/Unity_Fork/lilPBR
D:/Unity_Fork/lilToon
```

包作者、license、Unity 版本风格按旁边包保持为同一套：

```json
"author": {
  "name": "Hollow"
}
```

## 项目里怎么拉包

在项目 `Packages/manifest.json` 里同时放 UnityGLTF 和这个包：

```json
{
  "dependencies": {
    "org.khronos.unitygltf": "https://github.com/KhronosGroup/UnityGLTF.git",
    "jp.lilxyzw.liltoon.unitygltf.extensions": "file:D:/Unity_Fork/Liltoon-UnityGLTF-Extensions"
  }
}
```

如果用 Package Manager UI，先添加 UnityGLTF，再添加本地包：

```text
D:/Unity_Fork/Liltoon-UnityGLTF-Extensions
```

