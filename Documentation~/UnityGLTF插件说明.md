# UnityGLTF 插件说明

这个包是 UnityGLTF 的扩展包，不是 UnityGLTF fork。

目标是把 Hollow/lil 系列材质导入逻辑放在上游 UnityGLTF 包之外，同时仍然接入 UnityGLTF 的正常导入流程。

## UnityGLTF 已经提供什么

UnityGLTF 的插件系统基于 `ScriptableObject`：

- 导入插件继承 `UnityGLTF.Plugins.GLTFImportPlugin`。
- 导出插件继承 `UnityGLTF.Plugins.GLTFExportPlugin`。
- UnityGLTF 会扫描已加载程序集里的派生类。
- 插件上的 public/serialized 字段会显示在 Project Settings 里。
- 启用的插件会在每次导入时创建一个 `GLTFImportPluginContext`。

这意味着一个外部 UPM 包只要编译出 `GLTFImportPlugin` 派生类，就可以给 UnityGLTF 增加新的 glTF extension 导入逻辑。

## 参考文件

参考包路径：

```text
D:/Unity_Project/BREAK_URP/Library/PackageCache/org.khronos.unitygltf@ce6a5dce952d
```

重点文件：

- `Runtime/Scripts/Plugins/Core/GltfPlugin.cs`  
  定义 `GLTFPlugin`、`Enabled`、`EnabledByDefault`、`Warning`、`PackageMissing`、`DisplayName` 和插件设置模式。

- `Runtime/Scripts/Plugins/Core/GltfImportPlugin.cs`  
  定义 `GLTFImportPlugin` 和 `GLTFImportPluginContext`。材质回调是：

  ```csharp
  OnAfterImportMaterial(GLTFMaterial material, int materialIndex, Material materialObject)
  ```

- `Runtime/Scripts/Plugins/Core/ImportContext.cs`  
  展示 UnityGLTF 如何为每次导入创建插件实例，并在 Editor 导入时暴露 `GLTFImportContext.AssetContext`。

- `Runtime/Scripts/GLTFSettings.cs`  
  展示 UnityGLTF 如何在 Editor 中用 `TypeCache.GetTypesDerivedFrom<T>()` 自动注册插件类型。

- `Runtime/Scripts/SceneImporter/ImporterMaterials.cs`  
  展示材质创建流程，以及 UnityGLTF 调用 `plugin.OnAfterImportMaterial(...)` 的位置。

- `Runtime/Scripts/Plugins/MaterialExtensionsImport.cs`  
  UnityGLTF 内置 KHR 材质 extension 组的一个小插件示例。

- `Editor/Scripts/GLTFSettingsInspector.cs`  
  展示插件如何按程序集分组显示在 Project Settings 中。

- `Editor/Scripts/Plugins/GLTFPluginEditor.cs`  
  展示默认插件 inspector 和 `needs package` 安装提示模式。

## 导入流程

目标流程：

```text
Blender 材质数据
  -> glTF material.extensions.HO_materials_principled_lil
  -> UnityGLTF 反序列化 JSON
  -> HoMaterialsPrincipledLilImport.OnAfterImportMaterial
  -> 保存契约元数据
  -> 可选材质替换或映射
```

这和导入后再转换 `.mat` 不同。`.mat` 转换器只能看到第一次导入后幸存下来的 Unity shader property，看不到 UnityGLTF 没消费的自定义 extension JSON。

## 当前插件

`HoMaterialsPrincipledLilImport` 会被 UnityGLTF 自动发现，显示名为：

```text
HO_materials_principled_lil
```

支持：

- `HO_materials_principled_lil`
- `HO_materials_openpbr_lil` 旧名兼容

当前设置：

- `preserveContractJson`  
  Editor 导入时添加 `HoMaterialContractAsset` 子资源，保留原始 extension JSON。

- `hideContractSubAssets`  
  是否在导入资产层级里隐藏 `HoMaterialContractAsset`。调试阶段默认不隐藏，方便直接查看 `HoGltfInputs`。

- `applyUrpLitFallback`  
  临时过渡口。它读取 UnityGLTF 已经导入到 PBRGraph 风格材质上的属性，并切换到 URP/Lit。它不是最终的 lilToon/lilPBR 路线。

## 如何继续添加 lilToon/lilPBR 映射

保持插件入口稳定，把真正的映射逻辑拆成小 mapper：

```text
Runtime/
  HoMaterialsPrincipledLilImport.cs
  HoMaterialContractAsset.cs
  HoMaterialContractParser.cs
  Mapping/
    HoContractReader.cs
    HoTextureResolver.cs
    LilToonMaterialMapper.cs
    LilPbrMaterialMapper.cs
    UrpLitMaterialMapper.cs
```

mapper 职责：

- 读取 extension 中的 `principled`、`openpbr`、`toon`、`unity`、`extras`。
- 读取 Blender 侧 `HoGLTF` 节点组写出的 `hogltf.inputs` / `hogltf.sockets`。
- 把 glTF 纹理索引解析为 Unity 纹理。
- 根据 `target.shaderFamily` 或项目策略选择目标 shader。
- 直接把 shader property 写入 UnityGLTF 传入的 `materialObject`。
- 把暂不支持的字段继续保存在 `HoMaterialContractAsset`。

只要契约存在，就不要从 PBRGraph 反推含义。契约是 source of truth。

## 目标 shader 策略

建议策略：

1. `target.shaderFamily == lilToon`：直接导入为 lilToon。
2. `target.shaderFamily == lilPBR`：直接导入为 lilPBR。
3. `target.shaderFamily == auto`：如果 `toon` block 启用了风格化，则选 lilToon，否则选 lilPBR。
4. 如果目标 shader 缺失，保留元数据，不破坏 UnityGLTF 默认材质。

## 元数据策略

Editor 导入时始终保留原始契约 JSON。即使当前 mapper 不完整，也能让未来工具在不重新从 Blender 导出的情况下重建材质。

当前存储：

- `HoMaterialContractAsset` 作为导入 glTF 资产的子资源。
- `HoMaterialContractAsset` 内保存原始 JSON 和结构化解析字段。
- 后续 editor 工具可以扫描这些子资源并重建材质。

## 包说明

这个仓库本身就是 UPM 包根目录，根目录包含 `package.json`。

UnityGLTF 需要和本包一起安装：

```text
https://github.com/KhronosGroup/UnityGLTF.git
```

本包 manifest 只声明 `com.unity.nuget.newtonsoft-json`。UnityGLTF 通过 asmdef 名称引用：

```text
UnityGLTFScripts
GLTFSerialization
```

UnityGLTF/GLTFSerialization 会把未知 extension 反序列化为 `DefaultExtension`，所以 `HO_materials_principled_lil` 第一版不需要自定义 extension factory 就能保留原始 JSON。只有当 mapper 需要强类型对象时，再添加 typed factory。

本包暂时不硬依赖 lilToon 或 lilPBR。真正 mapper 实现前，目标 shader 名称只作为项目策略处理，不作为编译期依赖。
