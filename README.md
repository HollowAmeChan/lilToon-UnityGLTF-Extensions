# lilToon UnityGLTF Extensions

UnityGLTF import extensions for Hollow/lil material contracts.

This package is intended to sit next to `jp.lilxyzw.liltoon`,
`jp.lilxyzw.lilpbr`, and `jp.lilxyzw.liltoon.urp.extensions`.
It reads project material contracts embedded in glTF material extensions,
then lets UnityGLTF import generate project-ready Unity materials instead of
losing the authored material data after a generic shader conversion.

## Current Scope

- Registers a UnityGLTF import plugin named `HO_materials_principled_lil`.
- Reads `HO_materials_principled_lil` and legacy `HO_materials_openpbr_lil`
  material extensions from glTF.
- Preserves the original extension JSON as import sub-assets for later material
  rebuilding.
- Relies on UnityGLTF/GLTFSerialization's `DefaultExtension` behavior to keep
  unknown extension JSON alive during deserialization.
- Provides a first-pass fallback mapping from glTF/PBRGraph-style properties to
  `Universal Render Pipeline/Lit`.

The next step is to replace the fallback mapping with direct lilToon/lilPBR
mapping based on the material contract.

## Install

Add this package through Unity Package Manager as a local package, or add it to
the project manifest:

```json
{
  "dependencies": {
    "org.khronos.unitygltf": "https://github.com/KhronosGroup/UnityGLTF.git",
    "jp.lilxyzw.liltoon.unitygltf.extensions": "file:D:/Unity_Fork/Liltoon-UnityGLTF-Extensions"
  }
}
```

UnityGLTF must be installed in the same project. This package references the
`UnityGLTFScripts` and `GLTFSerialization` assemblies, so install UnityGLTF first
when adding packages through the Package Manager UI.

## UnityGLTF Registration

UnityGLTF automatically scans all compiled assemblies for classes derived from
`UnityGLTF.Plugins.GLTFImportPlugin`. Once this package compiles, the plugin
appears in:

`Project Settings > UnityGLTF > Import > Import Extensions and Plugins`

For Editor asset imports, UnityGLTF stores plugin overrides per `.gltf` importer.
For runtime imports, the default Project Settings plugin state is used.

## Reference Source

The implementation follows UnityGLTF's own plugin pattern:

- `Runtime/Scripts/Plugins/Core/GltfPlugin.cs`
- `Runtime/Scripts/Plugins/Core/GltfImportPlugin.cs`
- `Runtime/Scripts/Plugins/Core/ImportContext.cs`
- `Runtime/Scripts/GLTFSettings.cs`
- `Runtime/Scripts/SceneImporter/ImporterMaterials.cs`
- `Runtime/Scripts/Plugins/MaterialExtensionsImport.cs`

In the current BREAK_URP project these are under:

`D:/Unity_Project/BREAK_URP/Library/PackageCache/org.khronos.unitygltf@ce6a5dce952d`

## Why This Exists

A post-import Unity material converter can only see shader properties already
written to a `.mat` asset. It cannot recover custom glTF extension JSON such as
`HO_materials_principled_lil` after the importer has ignored it.

This package moves the conversion point into UnityGLTF's import pipeline, where
the original `GLTFMaterial.Extensions` data is still available.
