# UnityGLTF Plugin Notes

This package is a UnityGLTF extension package, not a fork of UnityGLTF.
The goal is to keep Hollow/lil material import logic outside the upstream
UnityGLTF package while still participating in UnityGLTF's normal import flow.

## What UnityGLTF Already Provides

UnityGLTF has a plugin system based on `ScriptableObject` classes:

- Import plugins inherit `UnityGLTF.Plugins.GLTFImportPlugin`.
- Export plugins inherit `UnityGLTF.Plugins.GLTFExportPlugin`.
- UnityGLTF discovers plugins by scanning loaded assemblies for derived types.
- Public serialized fields on plugins appear in Project Settings.
- Enabled plugins create a per-import `GLTFImportPluginContext`.

This means an external UPM package can add a new glTF extension importer simply
by compiling a class derived from `GLTFImportPlugin`.

## Reference Files

Reference package:

`D:/Unity_Project/BREAK_URP/Library/PackageCache/org.khronos.unitygltf@ce6a5dce952d`

Important files:

- `Runtime/Scripts/Plugins/Core/GltfPlugin.cs`
  Defines `GLTFPlugin`, `Enabled`, `EnabledByDefault`, `Warning`,
  `PackageMissing`, `DisplayName`, and the serialized settings pattern.

- `Runtime/Scripts/Plugins/Core/GltfImportPlugin.cs`
  Defines `GLTFImportPlugin` and `GLTFImportPluginContext`.
  The material hook is:
  `OnAfterImportMaterial(GLTFMaterial material, int materialIndex, Material materialObject)`.

- `Runtime/Scripts/Plugins/Core/ImportContext.cs`
  Shows how UnityGLTF creates plugin instances for each import and exposes
  `GLTFImportContext.AssetContext` for Editor imports.

- `Runtime/Scripts/GLTFSettings.cs`
  Shows how UnityGLTF uses `TypeCache.GetTypesDerivedFrom<T>()` in the Editor
  to register all plugin types automatically.

- `Runtime/Scripts/SceneImporter/ImporterMaterials.cs`
  Shows material creation and the point where UnityGLTF calls
  `plugin.OnAfterImportMaterial(...)`.

- `Runtime/Scripts/Plugins/MaterialExtensionsImport.cs`
  Small built-in plugin example for the KHR material extension group.

- `Editor/Scripts/GLTFSettingsInspector.cs`
  Shows how plugins are displayed in Project Settings, grouped by assembly.

- `Editor/Scripts/Plugins/GLTFPluginEditor.cs`
  Shows the default plugin inspector and the "needs package" install pattern.

## Import Flow

The intended import flow is:

```text
Blender material data
  -> glTF material.extensions.HO_materials_principled_lil
  -> UnityGLTF parses JSON
  -> HoMaterialsPrincipledLilImport.OnAfterImportMaterial
  -> preserve contract metadata
  -> optional material replacement/mapping
```

This is different from a post-import Unity material converter. A converter that
starts from `.mat` files can only see Unity shader properties that survived the
first import. It cannot recover custom extension JSON that UnityGLTF did not
consume.

## Current Plugin

`HoMaterialsPrincipledLilImport` is discovered by UnityGLTF and appears as:

`HO_materials_principled_lil`

It supports:

- `HO_materials_principled_lil`
- `HO_materials_openpbr_lil` as a legacy alias

Current settings:

- `preserveContractJson`
  Adds a `HoMaterialContractAsset` sub-asset during Editor import. This keeps
  the original extension JSON available for later conversion tools.

- `applyUrpLitFallback`
  A temporary bridge. It uses UnityGLTF's already-imported PBRGraph-style
  material properties and switches the material to URP/Lit. This is not the
  final lilToon/lilPBR path.

## How To Add Real lilToon/lilPBR Mapping

Keep the high-level plugin class stable and move mapping code into small mapper
classes. Suggested future split:

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

Mapper responsibilities:

- Read `principled`, `openpbr`, `toon`, `unity`, and `extras` from the extension.
- Resolve glTF texture indices to Unity textures.
- Select a target shader from `target.shaderFamily` or project policy.
- Apply shader properties directly to the `materialObject` passed by UnityGLTF.
- Preserve unsupported fields in `HoMaterialContractAsset`.

Avoid deriving meaning from PBRGraph when the contract is present. The contract
is the source of truth.

## Target Policy

Recommended policy:

1. If `target.shaderFamily` is `lilToon`, import directly as lilToon.
2. If `target.shaderFamily` is `lilPBR`, import directly as lilPBR.
3. If `target.shaderFamily` is `auto`, choose lilToon when the `toon` block has
   enabled stylization; otherwise choose lilPBR.
4. If the target shader is missing, preserve metadata and leave UnityGLTF's
   default material intact.

## Metadata Policy

Always preserve the original contract JSON during Editor import. Even when the
current mapper is incomplete, this makes the import recoverable and lets future
tools rebuild materials without re-exporting from Blender.

Suggested metadata storage:

- `HoMaterialContractAsset` sub-assets on the imported glTF asset.
- Later, optional editor tooling can find these sub-assets and rebuild materials.

## Package Notes

This repository is a UPM package repository. The package root is the repository
root and contains `package.json`.

Install UnityGLTF alongside this package from:

`https://github.com/KhronosGroup/UnityGLTF.git`

The package manifest only declares `com.unity.nuget.newtonsoft-json`. UnityGLTF
is referenced by asmdef name (`UnityGLTFScripts`, `GLTFSerialization`), because
Unity Package Manager dependencies in package manifests are safest when they are
registry/package-version dependencies. Put the UnityGLTF Git dependency in the
project manifest or install it through Package Manager.

UnityGLTF/GLTFSerialization keeps unknown extension JSON by deserializing it as
`DefaultExtension`, so `HO_materials_principled_lil` does not need a custom
extension factory just to preserve raw contract JSON. Add a typed factory later
only if the mapper needs strongly typed contract objects.

It intentionally does not hard-depend on lilToon or lilPBR yet. Until the real
mapper is implemented, target shader names should be treated as project policy,
not package-level compile dependencies.
