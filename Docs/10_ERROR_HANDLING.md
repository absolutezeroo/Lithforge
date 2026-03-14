# Lithforge — Error Handling & Recovery

## Error Categories

### Category 1: Content Validation Errors (load time)

Detected during content loading pipeline. Strict by default.

| Error | Policy | Fallback |
|-------|--------|----------|
| ScriptableObject missing/null | **FAIL** — log error, skip this asset | Block/item not registered |
| Missing required field | **FAIL** — log error, skip this asset | Block/item not registered |
| Unknown field | **WARN** — log warning, ignore field | Continue loading |
| Invalid property type | **FAIL** — skip this block definition | Block not registered |
| Blockstate references non-existent model | **WARN** — use fallback cube model | Magenta cube |
| Model parent chain is circular | **FAIL** — log error, use fallback | Magenta cube |
| Texture file missing | **WARN** — use fallback texture | Magenta/black checkerboard |
| Tag references non-existent block | **WARN** — skip entry | Tag without that entry |
| Loot table references non-existent item | **WARN** — skip entry | No drops |
| Duplicate ResourceId across namespaces | **FAIL** — second definition rejected | First definition wins |
| Mod dependency not found | **FAIL** — mod not loaded | Log error, skip mod |
| Mod dependency version mismatch | **FAIL** — mod not loaded | Log error, skip mod |

### Category 2: Runtime Errors (gameplay)

| Error | Policy | Recovery |
|-------|--------|----------|
| Chunk generation produces NaN | Replace NaN positions with stone | Log warning with coordinates |
| Chunk deserialization fails (save corruption) | Regenerate chunk from seed | Log error, mark chunk as regenerated |
| Unknown StateId in saved chunk (mod removed) | Replace with air | Log warning |
| Mesh build produces zero vertices for non-empty chunk | Retry once, then mark as empty | Log warning |
| NativeContainer not disposed | Safety system detects in editor | Hard error in editor, silent in build |
| Job produces invalid mesh indices | Discard mesh, retry | Log error |

### Category 3: Mod Runtime Errors (V2, code mods)

| Error | Policy | Recovery |
|-------|--------|----------|
| Mod throws exception during init | Disable mod, continue engine startup | Log error, show in UI |
| Mod throws during gameplay callback | Catch, log, disable the specific callback | Engine continues |
| Mod exhausts memory | Not recoverable | Engine crash (OS-level) |

## Fallback Resources

These are compiled into the engine and cannot be overridden by mods:

| Resource | Fallback | Appearance |
|----------|----------|-----------|
| Missing texture | `_FallbackTexture` | 16×16 magenta/black checkerboard |
| Missing block model | `_FallbackBlockModel` | Full cube using fallback texture |
| Missing item model | `_FallbackItemModel` | Flat quad using fallback texture |
| Missing sound | Silent | No sound played |
| Unknown block (removed mod) | `lithforge:air` | Invisible, no collision |
| Invalid biome | `_FallbackBiome` | Plains-like surface, no decorations |

## Content Validation Pipeline

Validation runs as a separate pass after ScriptableObject loading, before registry freeze:

```
Phase 1: Load ScriptableObjects via Resources.LoadAll<T>()
Phase 2: Validate each definition:
  - Required fields present?
  - Field types correct?
  - Property values within declared ranges?
  - ResourceId format valid (namespace:name)?
Phase 3: Cross-reference validation:
  - All blockstate model references resolve?
  - All model parent references resolve (no cycles)?
  - All tag entries reference existing blocks/items?
  - All recipe ingredients reference existing items/tags?
  - All loot table items reference existing items?
Phase 4: Report
  - List of errors (loading halted for these items)
  - List of warnings (fallbacks applied)
  - Written to log and optionally to validation_report.json
```

## Save Format Recovery

### Chunk-Level Recovery

Each chunk section in the region file includes a CRC32 checksum. On load:

```
1. Read chunk header
2. Decompress zstd payload
3. Verify CRC32
4. If CRC mismatch:
   a. Log corruption error with chunk coordinates
   b. Mark chunk for regeneration from world seed
   c. Any entity data in corrupted chunk is lost
5. If unknown StateId encountered (palette entry not in registry):
   a. Replace with air
   b. Log warning: "Unknown state {id} at ({x},{y},{z}), mod may have been removed"
```

### World-Level Recovery

`WorldMetadata` includes engine version and content hash. On load:

```
1. Check engine version compatibility
2. If content hash differs (mods added/removed):
   a. Log which blocks/items are no longer registered
   b. Continue loading — unknown StateIds handled per-chunk
3. If engine version is newer:
   a. Apply migration steps (version-specific)
   b. Re-save with new version
4. If engine version is older than save:
   a. FAIL — refuse to load (forward compatibility not guaranteed)
```
