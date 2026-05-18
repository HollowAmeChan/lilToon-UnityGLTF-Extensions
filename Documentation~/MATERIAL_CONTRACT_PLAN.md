# Material Contract Plan

This package is the Unity import side for the material contract described in:

`D:/Unity_Fork/lilToon/接口契约.md`

The contract is authored around Blender Principled BSDF data with optional
OpenPBR-compatible fields and lilToon/lilPBR project extensions.

## Extension Names

Primary:

```text
HO_materials_principled_lil
```

Legacy alias:

```text
HO_materials_openpbr_lil
```

## First Stable Import Slice

The first production importer should support:

- `principled.baseColor.factor`
- `principled.baseColor.texture`
- `principled.metallic.factor`
- `principled.roughness.factor`
- `principled.normal.texture`
- `principled.normal.scale`
- `principled.emission.color`
- `principled.emission.strength`
- `principled.alpha`
- `principled.alphaMode`
- `principled.alphaCutoff`
- `principled.geometry.occlusion`
- `principled.geometry.height`
- `principled.packed.preset = ORM`
- `principled.packed.texture`
- `toon.shadow`
- `toon.rim`
- `toon.outline`
- `unity.doubleSided`
- `unity.renderQueue`

## Mapping Rules

General:

- Treat roughness as the external contract value.
- Convert roughness to Unity smoothness with `smoothness = 1 - roughness`.
- Treat base color and emission textures as sRGB.
- Treat roughness, metallic, AO, height, and mask textures as linear.
- Treat normal textures as normal maps.

Target selection:

- Explicit `target.shaderFamily` wins.
- `toon.mode == toon`, outline, shadow, or rim hints should select lilToon.
- Otherwise prefer lilPBR.

Unsupported data:

- Preserve unsupported OpenPBR fields in metadata.
- Do not fake clear coat as MatCap.
- Do not fake transmission as simple alpha.
- Do not fake fuzz/sheen as rim unless the `toon` block explicitly asks for it.

## Why Not Convert From PBRGraph

UnityGLTF's default import path is good for generic glTF round-tripping, but the
project contract contains data that has no PBRGraph property equivalent. Once
that information is ignored, a later `.mat` converter cannot recover it.

The importer must read the glTF extension before the information disappears.
