# Retired lilToon variants

The Ho pipeline no longer provides lilToon Gem, Liquid, Lite, or tessellation variants. The importer keeps the original material contract and reports a retired-variant diagnostic instead of resolving a deleted shader or silently selecting Standard. Existing UnityGLTF material state remains available; any configured URP/Lit fallback keeps its existing behavior.

Fur and FurOnly routing is retained while the referenced Alue/TAIL material's retirement or migration is awaiting a user decision. Standard, cutout, transparent, outline, refraction, and fake-shadow routing is unchanged.

Validation on 2026-10-05: the runtime sources compile against the active Unity 6000.3.15f1 project references with zero errors. A managed reflection check verified that Gem/Lite/Liquid/tessellation return no shader selection, while Standard/refraction/Fur still resolve their original names. The Newtonsoft netstandard 2.0-to-2.1 compiler warning is recorded; no full glTF asset reimport or visual comparison was performed for this change.
