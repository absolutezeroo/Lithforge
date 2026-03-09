# Lithforge — Data-Driven Content System

## Directory Convention

Unchanged from engine-agnostic design. Minecraft's exact convention:

```
content/{namespace}/assets/{namespace}/   ← Client: visuals, sounds, blockstate→model mapping
content/{namespace}/data/{namespace}/     ← Logic: block properties, recipes, loot, worldgen
```

One file per definition. Each file independently overridable by mods.

## Content Location in Unity

```
Assets/StreamingAssets/content/lithforge/      ← Core content (shipped with build)
{user_data}/mods/{mod_id}/                     ← Mod content (user-installed)
{user_data}/games/{game_id}/                   ← Game content

User data path: Application.persistentDataPath
```

All content is loaded via `System.IO.File.ReadAllText()` from filesystem paths. Content is NOT loaded via Unity's `Resources` or `AssetDatabase` systems — this keeps content loading engine-agnostic and allows mods to add content without Unity asset pipeline involvement.

**Exception**: Textures and sounds require Unity API to create runtime `Texture2D` and `AudioClip` objects. This conversion happens in the Tier 3 runtime layer after Tier 1 content loading reads the raw bytes.

## File Formats

All file formats are identical to the engine-agnostic specification. Reproduced here for completeness with no changes:

### Block Definition

`data/{namespace}/blocks/{id}.json`

```json
{
  "properties": {
    "axis": { "type": "enum", "values": ["x", "y", "z"], "default": "y" }
  },
  "hardness": 2.0,
  "blast_resistance": 2.0,
  "requires_tool": false,
  "sound_group": "wood",
  "collision_shape": "full_cube",
  "render_layer": "opaque",
  "light_emission": 0,
  "light_filter": 15,
  "map_color": "#6B5839",
  "loot_table": "lithforge:blocks/oak_log",
  "tags": ["lithforge:logs", "lithforge:mineable_axe"],
  "flammable": { "burn_odds": 5, "ignite_odds": 5 }
}
```

### BlockState Definition

`assets/{namespace}/blockstates/{id}.json`

Variants mode or multipart mode — see 03_VOXEL_CORE.md for resolution logic.

### Block Model, Item Model, Loot Table, Tags, Recipes, Biomes

All formats identical to the engine-agnostic spec in the original `07_DATA_DRIVEN_CONTENT.md`. No Unity-specific changes.

---

## Content Loading Pipeline (Unity-Adapted)

```
Phase 1: Discovery
  ├─ Scan StreamingAssets/content/ for core namespace
  ├─ Scan {persistentData}/games/{gameId}/ for game content
  ├─ Scan {persistentData}/mods/ for mod manifests (mod.json)
  └─ Build load order (topological sort by dependency, cycle detection)

Phase 2: Block Registration (Tier 1 — pure C#)
  ├─ Parse data/*/blocks/*.json → BlockDefinition[]
  ├─ Validate each definition (ContentValidator)
  ├─ Register in RegistryBuilder<BlockDefinition>
  └─ Compute StateRegistry (cartesian product of properties)

Phase 3: Asset Resolution (Tier 1 parsing + Tier 3 Unity conversion)
  ├─ Parse assets/*/blockstates/*.json → BlockStateDefinition[]
  ├─ Parse assets/*/models/block/*.json → resolve parent inheritance chain
  ├─ Parse assets/*/models/item/*.json
  ├─ Read texture PNGs as byte[] (Tier 1: System.IO)
  ├─ Convert byte[] → Texture2D → build Texture2DArray (Tier 3: Unity API)
  ├─ Assign texture array indices to each face
  └─ Resolve BlockState → model → per-face texture index for each StateId

Phase 4: Gameplay Data (Tier 1)
  ├─ Parse items, tags, loot tables, recipes
  ├─ Merge tags across namespaces (additive or replace)
  └─ Validate cross-references (all model refs exist, all tag entries exist, etc.)

Phase 5: WorldGen Data (Tier 1 parsing + Tier 2 baking)
  ├─ Parse biomes, configured_features, placed_features, noise_settings
  ├─ Build BiomeRegistry
  └─ Bake NativeBiomeData[], NativeOreConfig[] for Burst jobs

Phase 6: Registry Freeze + Native Bake
  ├─ RegistryBuilder.Build() → Registry (read-only, immutable)
  ├─ StateRegistry.BakeNative() → NativeStateRegistry (Allocator.Persistent)
  ├─ AtlasBuilder → NativeAtlasLookup (Allocator.Persistent)
  └─ All registries are now frozen. No further content additions allowed.

Phase 7: Mod Initialization (V2 — code mods only)
  ├─ Call mod init callbacks in load order
  └─ Mods can register additional content via ModContext APIs (before freeze in Phase 6)

Phase 8: Ready
  └─ World generation and rendering can begin
```

### Content Loading Thread Model

```
MAIN THREAD:
  Phase 1 (discovery): filesystem scan — fast
  Phase 3 (texture conversion): Texture2D.LoadImage, Texture2DArray — must be main thread
  Phase 6 (freeze): NativeArray allocation — main thread

BACKGROUND THREAD (Task.Run or Burst job):
  Phase 2 (JSON parsing): CPU-bound, no Unity API needed
  Phase 4 (JSON parsing): CPU-bound
  Phase 5 (JSON parsing): CPU-bound

Pattern: parse JSON on background thread → queue results → main thread validates and registers
```

---

## Asset Loading for Mods

### Textures

```csharp
// Tier 3: TextureAtlasManager.cs
public void LoadModTextures(string modContentPath)
{
    string textureDir = Path.Combine(modContentPath, "assets", modId, "textures");
    foreach (string pngPath in Directory.GetFiles(textureDir, "*.png", SearchOption.AllDirectories))
    {
        byte[] bytes = File.ReadAllBytes(pngPath);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(bytes);
        tex.filterMode = FilterMode.Point;

        // Register in atlas builder with ResourceId derived from path
        string relativePath = Path.GetRelativePath(textureDir, pngPath);
        ResourceId texId = new ResourceId(modId, relativePath.Replace(".png", "").Replace('\\', '/'));
        _atlasBuilder.Add(texId, tex);
    }
}
```

### Sounds

```csharp
// Tier 3: AudioManager.cs
public async Task LoadModSounds(string modContentPath)
{
    string soundDir = Path.Combine(modContentPath, "assets", modId, "sounds");
    foreach (string oggPath in Directory.GetFiles(soundDir, "*.ogg", SearchOption.AllDirectories))
    {
        // Unity requires UnityWebRequest for runtime audio loading
        string uri = "file://" + oggPath;
        using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS))
        {
            await req.SendWebRequest();
            AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
            // Register in sound registry
        }
    }
}
```

---

## Mod Content Integration

Identical to engine-agnostic spec. Mods use the same directory structure:

```
mods/ruby_mod/
├── mod.json
├── assets/ruby_mod/
│   ├── blockstates/ruby_ore.json
│   ├── models/block/ruby_ore.json
│   └── textures/block/ruby_ore.png
└── data/ruby_mod/
    ├── blocks/ruby_ore.json
    ├── items/ruby.json
    ├── tags/blocks/mineable_pickaxe.json   ← adds ruby_ore to vanilla tag
    └── worldgen/configured_feature/ore_ruby.json
```

### Content Validation Rules

| Rule | Severity | Behavior |
|------|----------|----------|
| JSON must parse without errors | ERROR | File skipped entirely |
| Required fields must be present | ERROR | Definition skipped |
| Property values must be within declared type/range | ERROR | Definition skipped |
| ResourceId must match `^[a-z0-9_]+:[a-z0-9_/]+$` | ERROR | Definition skipped |
| Texture must be square and power-of-two (16, 32, 64...) | WARNING | Resized to nearest |
| Unknown JSON fields ignored | WARNING | Logged, field skipped |
| Blockstate model reference must exist | WARNING | Fallback to magenta cube |
| Model parent must exist and not be circular | ERROR | Fallback to magenta cube |
| Tag entries must reference existing blocks/items | WARNING | Entry skipped |

---

## Invariants

1. All content files are UTF-8 encoded JSON.
2. All ResourceIds are lowercase alphanumeric with underscores and forward slashes. Format: `namespace:path/name`.
3. After Phase 6 (freeze), no new content can be registered. Registries are immutable.
4. Tag merging: `replace: false` is additive across all namespaces. `replace: true` discards all prior entries for that tag.
5. A mod's content cannot override another mod's content in the same namespace — only extend via tags and recipes.
6. A game's content is loaded before mods. Mods extend the game.
7. Textures must be square and power-of-two. Non-conforming textures are resized with a warning.
8. Content validation runs synchronously before registry freeze. All errors are collected and reported at once.
