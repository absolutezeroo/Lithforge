# Lithforge — Platform Architecture

## Lithforge as a Platform

Lithforge is not just a voxel engine runtime — it is a **game-creation platform**. This means it must support game creators who build complete games on top of the engine, modders who extend those games, and players who consume the content.

## Content Hierarchy

```
┌─────────────────────────────────────────┐
│  Lithforge Engine                        │
│  (Packages/ — core, voxel, meshing, etc) │
│  Provides: runtime, rendering, physics,  │
│  worldgen, crafting, entity simulation   │
└──────────────────┬──────────────────────┘
                   │
┌──────────────────▼──────────────────────┐
│  Game                                    │
│  (A complete game built on Lithforge)    │
│  Provides: game rules, custom UI, mobs,  │
│  progression, win conditions, branding   │
│  Defined by: game.json manifest          │
│  Example: "Stonebound" — a survival RPG  │
└──────────────────┬──────────────────────┘
                   │
┌──────────────────▼──────────────────────┐
│  Mods (extend a game)                    │
│  Provide: new blocks, items, mobs, etc   │
│  Defined by: mod.json manifest           │
│  Can depend on: engine version + game ID │
└──────────────────┬──────────────────────┘
                   │
┌──────────────────▼──────────────────────┐
│  Content Packs (data-only, no code)      │
│  Provide: texture packs, sound packs,    │
│  language packs                          │
│  Defined by: pack.json manifest          │
└──────────────────────────────────────────┘
```

## Game Manifest

A game is defined by a `game.json` at its root:

```json
{
  "id": "stonebound",
  "name": "Stonebound",
  "version": "1.0.0",
  "engine_version": ">=0.1.0",
  "description": "A survival RPG in a voxel world",
  "authors": ["Studio Name"],
  "license": "MIT",
  "entry_content": "stonebound",
  "main_menu_scene": "stonebound:main_menu",
  "default_worldgen": "stonebound:overworld",
  "features": {
    "crafting": true,
    "inventory_size": 36,
    "hotbar_size": 9,
    "health": { "enabled": true, "max": 20 },
    "hunger": { "enabled": true, "max": 20 },
    "day_night_cycle": { "enabled": true, "day_length_seconds": 600 }
  }
}
```

### Game Directory Structure

```
games/stonebound/
├── game.json
├── assets/stonebound/
│   ├── blockstates/
│   ├── models/
│   ├── textures/
│   └── sounds/
├── data/stonebound/
│   ├── blocks/
│   ├── items/
│   ├── recipes/
│   ├── loot_tables/
│   ├── tags/
│   └── worldgen/
└── scripts/                   # V2: game-specific C# code (optional)
    └── StoneFoundInit.cs
```

## Game Customization Points

A game can override or extend engine behavior through:

| Extension Point | Mechanism | Example |
|----------------|-----------|---------|
| Add blocks/items/biomes | Data files in game namespace | New ore types, weapons |
| Override core tags | `"replace": true` in tag file | Change what pickaxe can mine |
| Custom world generation | NoiseSettings + custom features | Unique terrain shape |
| Custom UI | Override UI prefabs/UXML | Themed inventory screen |
| Game rules | `game.json` features | Enable/disable hunger |
| Custom entities | Entity definitions + behaviors | Boss mobs |
| Custom recipes | Recipe files | New crafting categories |

## Mod ↔ Game ↔ Engine Relationship

```
Engine provides:
  - Block/item/entity/biome registries
  - WorldGen pipeline
  - Meshing/rendering
  - Physics/collision
  - Crafting engine
  - Input/UI/Audio infrastructure
  - Modding API

Game provides:
  - All content definitions (blocks, items, etc.)
  - Game rules configuration
  - Custom UI themes
  - Initial world settings
  - Progression system (optional)

Mod provides:
  - Additional content (new blocks, items, etc.)
  - Extensions to existing tags
  - Additional worldgen features
  - Must declare which game it targets
```

## Distribution

### Standalone Game Build

A game built on Lithforge is distributed as a standalone Unity build:

```
StoneFoundGame/
├── Stonebound.exe              # Unity player
├── Stonebound_Data/
│   ├── StreamingAssets/
│   │   ├── engine/             # Lithforge core content
│   │   ├── game/               # Game content
│   │   └── mods/               # User-installed mods
│   └── ...                     # Unity runtime files
```

### Mod Distribution

Mods are distributed as folders or zip archives:

```
mods/ruby_mod/
├── mod.json
├── assets/ruby_mod/
│   ├── blockstates/
│   ├── models/
│   └── textures/
└── data/ruby_mod/
    ├── blocks/
    ├── items/
    └── tags/
```

## Version Compatibility

### Engine → Game

The engine declares a version. Games declare `engine_version: ">=X.Y.Z"`. The engine checks this at game load time.

- **Patch versions** (0.1.X → 0.1.Y): Always compatible. No breaking changes.
- **Minor versions** (0.X → 0.Y): New features added. Existing features not removed. Games should work.
- **Major versions** (X → Y): Breaking changes possible. Games must be updated.

### Game → Mod

Same semver scheme. Mods declare `game: "stonebound"` and `game_version: ">=1.0.0"`.

### Modding API Stability

- **Stable API**: Block, item, recipe, tag, loot table, biome, worldgen feature registration. These are data-driven and versioned by the data format.
- **Unstable API**: Code modding hooks (V2). Marked with `[Unstable]` attribute. May change between minor versions.
- **Internal API**: Engine internals not exposed to mods. Can change at any time.

## Content Loading Order

```
1. Load engine core content: content/lithforge/
2. Detect and validate game: games/{game_id}/game.json
3. Load game content: games/{game_id}/assets/ + data/
4. Discover mods: mods/*/mod.json
5. Validate mod dependencies (game ID, engine version, inter-mod deps)
6. Topological sort mods by dependency
7. Load mods in order: each mod's assets/ + data/
8. Merge tags (additive for replace:false, override for replace:true)
9. Build registries, freeze, bake native data
10. World ready
```
