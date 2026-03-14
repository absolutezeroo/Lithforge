# CONVENTIONS.md — Unity 6 Project Naming Conventions

> **A complete reference for naming conventions across C# code, Unity APIs, DOTS/ECS, assets, folders, shaders, and namespaces.** Designed for human developers and AI coding assistants (like Claude Code) to maintain consistency. Covers Microsoft C#, Unity official, and Google C# style guides with concrete examples for every category.
>
> **Note:** Lithforge project-specific overrides (e.g., `no var`, `no expression-bodied methods`, `one file per type`) live in the root `CLAUDE.md`. When CLAUDE.md and this document conflict, **CLAUDE.md wins**.

---

## 1. C# code conventions

### Recommended baseline

This guide recommends **Microsoft/.NET conventions as the primary baseline**, with Unity-specific additions where needed. This aligns with the broader C# ecosystem, modern IDE tooling defaults, and the Google C# style guide. Unity's internal `m_`/`k_`/`s_` prefixes are documented as alternatives.

### Master naming table

| Element | Convention | Example |
|---|---|---|
| **Class** | PascalCase, noun | `PlayerController`, `WeaponDatabase` |
| **Struct** | PascalCase, noun | `DamageInfo`, `PlayerData` |
| **Interface** | `I` + PascalCase, adjective | `IDamageable`, `IInteractable` |
| **Enum** | PascalCase, singular noun | `WeaponType`, `DamageCategory` |
| **Enum (Flags)** | PascalCase, **plural** noun | `StatusEffects`, `DamageTypes` |
| **Enum values** | PascalCase | `Fire`, `Ice`, `Lightning` |
| **Method** | PascalCase, verb phrase | `TakeDamage()`, `GetDirection()` |
| **Property (public)** | PascalCase | `Health`, `IsActive` |
| **Public field** | PascalCase | `MaxHealth`, `MovementSpeed` |
| **Private field** | `_camelCase` | `_health`, `_currentVelocity` |
| **Private static field** | `s_camelCase` | `s_instanceCount` |
| **Const** | PascalCase | `MaxHealth`, `DefaultSpeed` |
| **Static readonly** | PascalCase (public), `s_camelCase` (private) | `DefaultGravity`, `s_sharedInstance` |
| **Readonly instance field** | `_camelCase` | `_maxHealth` |
| **Local variable** | camelCase | `itemCount`, `elapsedTime` |
| **Parameter** | camelCase | `damageAmount`, `isActive` |
| **Generic type param** | `T` + PascalCase | `TPoolable`, `TInput`, `TOutput` |
| **Delegate** | PascalCase | `DamageHandler`, `Converter<TInput, TOutput>` |
| **Event** | PascalCase, verb phrase | `HealthChanged`, `DoorOpened` |
| **Namespace** | PascalCase, dot-separated | `MyGame.Combat`, `Lithforge.Voxel` |

### Where the three major guides diverge

| Element | Microsoft/.NET | Unity Official/Internal | Google (for Unity) |
|---|---|---|---|
| Private instance fields | `_camelCase` | `m_camelCase` or `m_PascalCase` | `_camelCase` |
| Private static fields | `s_camelCase` | `s_camelCase` | `_camelCase` (static ignored) |
| Constants (private) | `PascalCase` | `k_PascalCase` | `_camelCase` (const ignored) |
| Public fields | `PascalCase` | `PascalCase` or `camelCase` | `PascalCase` |
| Braces | Allman (new line) | Allman | K&R (same line) |
| Indentation | 4 spaces | 4 spaces | 2 spaces |

**Google's core principle**: naming is unaffected by `const`, `static`, or `readonly` modifiers — a private field is always `_camelCase`. **Unity's internal principle**: use Hungarian-style prefixes `m_`, `s_`, `k_` to encode scope at the identifier level. Choose one system for your project and apply it consistently.

### Comprehensive code example

```csharp
using System;
using UnityEngine;

namespace MyGame.Combat
{
    public enum DamageType { Physical, Fire, Ice, Lightning }

    [Flags]
    public enum StatusEffects { None = 0, Burning = 1, Frozen = 2, Stunned = 4 }

    public interface IDamageable
    {
        int Health { get; }
        void TakeDamage(int amount);
        bool IsAlive();
    }

    public delegate void DamageHandler(int amount, DamageType type);

    public class ObjectPool<TPoolable> where TPoolable : Component
    {
        public int MaxPoolSize = 50;
        private int _currentCount;
        private static int s_totalPools;
        public const int MaxItems = 100;
        public static readonly float DefaultGrowthFactor = 1.5f;
        private readonly float _spawnInterval;
    }

    public class PlayerController : MonoBehaviour, IDamageable
    {
        // Events — PascalCase, verb phrase
        public event Action<int> HealthChanged;
        public event Action Dying;       // "before" — present participle
        public event Action Died;        // "after" — past tense

        // Serialized fields — visible in Inspector
        [SerializeField] private int _maxHealth = 100;
        [SerializeField] private float _moveSpeed = 5f;

        // Private runtime state — not serialized
        private int _currentHealth;
        private bool _isDead;

        // Properties — PascalCase
        public int Health => _currentHealth;

        // Methods — PascalCase, start with verb
        public bool IsAlive() => !_isDead;

        public void TakeDamage(int damageAmount)
        {
            int actualDamage = Mathf.Max(0, damageAmount); // local: camelCase
            _currentHealth -= actualDamage;

            if (_currentHealth <= 0)
            {
                _currentHealth = 0;
                _isDead = true;
                OnDied();
            }
            HealthChanged?.Invoke(_currentHealth);
        }

        // Event raiser — "On" prefix
        private void OnDied() { Died?.Invoke(); }
    }

    public struct DamageInfo
    {
        public int Amount;
        public DamageType Type;
        public bool IsCritical;
    }
}
```

### Boolean naming

Always prefix booleans with a verb to form a question:

```csharp
// Fields and properties
private bool _isDead;
private bool _hasWeapon;
public bool IsActive { get; set; }
public bool CanJump => _isGrounded && !_isDead;

// Methods returning bool — phrase as question
public bool IsAlive() => _currentHealth > 0;
public bool HasStartedTurn() => _turnStarted;
```

---

## 2. Unity-specific conventions

### MonoBehaviour scripts

**Filename must exactly match the class name.** `PlayerMovement.cs` contains `public class PlayerMovement : MonoBehaviour`. One MonoBehaviour per file. Delete empty lifecycle methods (`Start`, `Update`) — they still incur a call overhead even when empty.

**Common component suffixes:**

| Suffix | Meaning | Examples |
|---|---|---|
| `Controller` | Per-object behavior (multiple instances) | `PlayerController`, `CameraController` |
| `Manager` | Singleton / global orchestrator | `GameManager`, `AudioManager` |
| `System` | System-level logic | `HealthSystem`, `InventorySystem` |
| `Handler` | Event/input handling | `InputHandler`, `CollisionHandler` |
| `Spawner` | Object instantiation | `EnemySpawner`, `ItemSpawner` |
| `UI` / `Panel` | UI components | `HealthBarUI`, `SettingsPanel` |
| `Data` / `Config` | Data container (on ScriptableObject) | `WeaponData`, `EnemyConfig` |

### ScriptableObjects

**Class naming** — use descriptive suffixes indicating data purpose:

```csharp
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Game Data/Weapon")]
public class WeaponData : ScriptableObject
{
    public string WeaponName;
    public int Damage;
    public float AttackSpeed;
    public Sprite Icon;
}
```

| Pattern | When to use | Examples |
|---|---|---|
| `XxxData` | Raw data definitions | `WeaponData`, `EnemyData` |
| `XxxConfig` | Tunable configuration | `DifficultyConfig`, `GameConfig` |
| `XxxSettings` | System settings | `AudioSettings`, `GraphicsSettings` |
| `XxxDefinition` | Authored content definitions | `ItemDefinition`, `AbilityDefinition` |

**Asset instance naming on disk**: use descriptive PascalCase or `Type_Variant` — e.g., `Weapon_Sword.asset`, `PlayerStats.asset`. Avoid leaving default `NewWeapon.asset` names.

**CreateAssetMenu** `menuName` uses forward-slash hierarchy: `"Game Data/Weapon"`, `"Characters/Stats"`.

### SerializeField naming and Inspector display

Unity's `ObjectNames.NicifyVariableName()` strips `m_`, `_`, and `k` prefixes and inserts spaces before capitals. All three private field conventions display identically in the Inspector:

```csharp
_moveSpeed      → "Move Speed"
m_MoveSpeed     → "Move Speed"
moveSpeed       → "Move Speed"
```

**Best practice**: prefer `[SerializeField] private` over `public` fields. This exposes fields to the Inspector while maintaining encapsulation. Add a read-only property for public API access:

```csharp
[SerializeField] private float _moveSpeed = 5f;
public float MoveSpeed => _moveSpeed;
```

### Editor scripts

Editor scripts **must** live inside a folder named `Editor/` (excluded from builds). Naming follows predictable suffix patterns:

| Type | Pattern | Example |
|---|---|---|
| Custom Editor | `{Target}Editor` | `PlayerControllerEditor : Editor` |
| PropertyDrawer | `{Type}Drawer` or `{Type}PropertyDrawer` | `ReadOnlyPropertyDrawer : PropertyDrawer` |
| EditorWindow | `{Purpose}Window` or `{Purpose}Editor` | `TileMapEditorWindow : EditorWindow` |

### Custom attributes

The `Attribute` class goes in **runtime** code (inherits `PropertyAttribute`). Its matching `PropertyDrawer` goes in an **Editor/** folder:

```csharp
// Runtime — ReadOnlyAttribute.cs
[System.AttributeUsage(System.AttributeTargets.Field)]
public class ReadOnlyAttribute : PropertyAttribute { }

// Editor — ReadOnlyPropertyDrawer.cs
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyPropertyDrawer : PropertyDrawer { /* ... */ }
```

Common custom attributes: `ReadOnlyAttribute`, `ConditionalHideAttribute`, `MinMaxRangeAttribute`, `RequiredAttribute`.

---

## 3. Jobs system and Burst conventions

### Job struct naming

All job structs use the **`Job` suffix** and follow `{DescriptiveAction}Job` pattern:

```csharp
[BurstCompile]
public struct MoveJob : IJob
{
    [ReadOnly] public NativeArray<float3> Positions;
    public NativeArray<float3> Results;
    public void Execute() { /* ... */ }
}

[BurstCompile]
public struct DamageCalculationJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float> BaseDamage;
    [ReadOnly] public NativeArray<float> Multipliers;
    public NativeArray<float> Results;
    public void Execute(int index)
    {
        Results[index] = BaseDamage[index] * Multipliers[index];
    }
}

[BurstCompile]
partial struct RotationJob : IJobEntity
{
    public float DeltaTime;
    void Execute(ref LocalTransform transform, in RotationSpeed speed)
    {
        transform = transform.RotateY(speed.RadiansPerSecond * DeltaTime);
    }
}
```

**Execute method signatures** differ by job type:
- `IJob` → `void Execute()`
- `IJobFor` / `IJobParallelFor` → `void Execute(int index)`
- `IJobEntity` → `void Execute(ref Component1 c1, in Component2 c2)` (parameters define the query; `ref` = read-write, `in` = read-only)

**IJobEntity** requires `partial struct` for source generation.

### Job struct field naming

Job struct fields use **PascalCase** (they are public struct fields). Signal data direction with attributes:

```csharp
[ReadOnly] public NativeArray<float3> TargetPositions;   // input
[WriteOnly] public NativeArray<float3> Results;           // output
public NativeArray<float3> Positions;                     // read-write
public float DeltaTime;                                   // scalar input
```

### NativeContainer variable naming

```csharp
// In local scope — camelCase
NativeArray<float3> positions = new NativeArray<float3>(count, Allocator.TempJob);
NativeList<int> results = new NativeList<int>(Allocator.Persistent);
NativeParallelHashMap<int, float3> entityPositions;

// In job struct fields — PascalCase
[ReadOnly] public NativeArray<float3> SeekerPositions;
public NativeArray<float3> NearestTargetPositions;
```

**Allocator choice**:
- `Allocator.Temp` — single method, very short-lived
- `Allocator.TempJob` — lives for the duration of a job (up to 4 frames)
- `Allocator.Persistent` — lives until explicitly disposed

### Schedule/Complete patterns

```csharp
// Variable naming: camelCase with "Handle" or descriptive suffix
JobHandle moveHandle = moveJob.Schedule(count, 64);
JobHandle damageHandle = damageJob.Schedule(moveHandle); // chain dependency

// In ISystem — use state.Dependency
state.Dependency = new MoveJob { DeltaTime = dt }.ScheduleParallel(state.Dependency);

// Complete (avoid immediate Complete — reduces parallelism)
moveHandle.Complete();
```

### Burst conventions

```csharp
// On job structs — attribute on the struct
[BurstCompile]
public struct FindNearestJob : IJob { /* ... */ }

// On ISystem — attribute on EACH METHOD, not the struct
public partial struct MovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state) { /* ... */ }
}

// On static utility classes — attribute on BOTH class and method
[BurstCompile]
public static class MathUtility
{
    [BurstCompile]
    public static void ComputeDamage(in float baseDmg, in float armor, out float result)
    {
        result = math.max(0, baseDmg - armor);
    }
}
```

Use `Unity.Mathematics` types (`float3`, `quaternion`, `math.*`) instead of `UnityEngine` types for Burst-compatible code.

---

## 4. ECS/DOTS conventions

### IComponentData — no "Component" suffix

Unity's own ECS packages **never** use a `Component` suffix. Components are named after the data they represent:

```csharp
// Correct — matches Unity's own convention
public struct Velocity : IComponentData { public float3 Value; }
public struct Health : IComponentData { public float Current; public float Max; }
public struct RotationSpeed : IComponentData { public float RadiansPerSecond; }

// Wrong
public struct VelocityComponent : IComponentData { }  // Don't add "Component"
```

**Tag components** (empty structs for query filtering) often use a `Tag` suffix:

```csharp
public struct PlayerTag : IComponentData { }
public struct DestroyTag : IComponentData { }
```

### Complete ECS naming table

| Type | Suffix | Struct/Class | Example |
|---|---|---|---|
| IComponentData | None (or `Tag` if empty) | `struct` | `Velocity`, `Health`, `PlayerTag` |
| ISystem | `System` | `partial struct` | `MovementSystem`, `DamageSystem` |
| SystemBase (legacy) | `System` | `class` | `LegacyMovementSystem` |
| ComponentSystemGroup | `SystemGroup` | `class` | `CombatSystemGroup` |
| IAspect | `Aspect` | `readonly partial struct` | `MovementAspect`, `TransformAspect` |
| IBufferElementData | `Element` or `BufferElement` | `struct` | `DamageBufferElement` |
| ISharedComponentData | None | `struct` | `TeamColor`, `RenderMesh` |
| Authoring (MonoBehaviour) | `Authoring` | `class` | `PlayerAuthoring`, `SpawnerAuthoring` |
| Baker | `Baker` (or nested `Baker`) | `class` | `PlayerBaker` or nested `Baker` |
| EntityCommandBufferSystem | `EntityCommandBufferSystem` | `class` | `BeginSimulationEntityCommandBufferSystem` |

### Authoring and Baker patterns

```csharp
// Authoring component — always a MonoBehaviour, always "Authoring" suffix
public class PlayerAuthoring : MonoBehaviour
{
    public float MoveSpeed;
    public int MaxHealth;

    // Preferred: nested Baker class (Unity docs call this "the cleanest style")
    class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new MoveSpeed { Value = authoring.MoveSpeed });
            AddComponent(entity, new Health
            {
                Current = authoring.MaxHealth,
                Max = authoring.MaxHealth
            });
            AddComponent<PlayerTag>(entity);
        }
    }
}

// Alternative: separate Baker class with "{Name}Baker" suffix
class PlayerBaker : Baker<PlayerAuthoring>
{
    public override void Bake(PlayerAuthoring authoring) { /* ... */ }
}
```

### System example

```csharp
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct MovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        foreach ((RefRW<LocalTransform> transform, RefRO<Velocity> velocity) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRO<Velocity>>())
        {
            transform.ValueRW.Position += velocity.ValueRO.Value * deltaTime;
        }
    }
}
```

### Built-in SystemGroup names (for `[UpdateInGroup]`)

`InitializationSystemGroup`, `SimulationSystemGroup`, `PresentationSystemGroup`, `TransformSystemGroup`, `FixedStepSimulationSystemGroup`.

---

## 5. Asset naming conventions

### General pattern

```
[Prefix_]BaseAssetName[_Variant][_Suffix]
```

All asset names use **PascalCase**. No spaces. The prefix denotes asset type; the suffix denotes subtype or variant.

### Asset type prefixes

| Asset Type | Prefix | Example |
|---|---|---|
| Static Mesh | `SM_` | `SM_Rock_01`, `SM_Wall_Brick` |
| Skeletal Mesh | `SK_` | `SK_Character_Bob` |
| Material | `M_` | `M_Rock`, `M_Character_Skin` |
| Material Instance | `MI_` | `MI_Rock_Snow` |
| Physical Material | `PM_` | `PM_Ice`, `PM_Wood` |
| Prefab | *(none — PascalCase name)* | `WoodenCrate`, `EnemySpawner` |
| Animation Clip | `A_` | `A_Character_Walk` |
| Animation Controller | `AC_` | `AC_MainCharacter` |
| Avatar Mask | `AM_` | `AM_UpperBody` |
| VFX Graph | `VFX_` | `VFX_Explosion_Fire` |
| Particle System | `PS_` | `PS_Smoke_Trail` |
| UI element | `UI_` | `UI_HealthBar`, `UI_Button_Primary` |
| Font | `Font_` | `Font_Roboto_Bold` |

### Texture naming — prefix `T_` with type suffix

| Texture Type | Suffix | Full Example |
|---|---|---|
| Albedo / Base Color | `_D` or `_BC` or `_Albedo` | `T_Rock_D` |
| Normal Map | `_N` | `T_Rock_N` |
| Roughness | `_R` | `T_Rock_R` |
| Metallic | `_M` or `_MT` | `T_Rock_MT` |
| Ambient Occlusion | `_AO` | `T_Rock_AO` |
| Emission | `_E` | `T_Rock_E` |
| Height / Displacement | `_H` | `T_Rock_H` |
| Mask (packed channels) | `_Mask` | `T_Rock_Mask` |
| Alpha / Opacity | `_A` | `T_Rock_A` |
| Packed (multi-channel) | Combined letters | `T_Rock_ORM` (AO+Roughness+Metallic) |

### Audio naming

The generic `A_` prefix creates ambiguity with animation clips. Prefer **explicit category prefixes** for audio:

| Audio Type | Prefix | Example |
|---|---|---|
| Sound Effect | `SFX_` | `SFX_Weapon_Rifle_Fire_01` |
| Music | `MUS_` | `MUS_Level_Forest_Loop` |
| Ambient | `AMB_` | `AMB_Cave_Drip_01` |
| Dialogue / Voice | `DV_` | `DV_NPC_Greeting_01` |
| Audio Mixer | `MIX_` | `MIX_Master` |

### Scene naming

Scenes use descriptive names without type prefixes. Organize with contextual suffixes for sub-scenes:

```
MainMenu.unity
Gameplay_Forest.unity
Level_01_Village.unity
Level_01_Village_Lighting.unity    // lighting sub-scene
Level_01_Village_Audio.unity       // audio sub-scene
```

### FBX animation import convention

The `@` symbol binds an animation clip to a model's avatar: `Bob@Walk.FBX`, `Bob@Run.FBX`, `Bob@Idle.FBX`.

---

## 6. Folder and directory structure

### Recommended project layout

```
Assets/
├── _Project/                    # All project assets (underscore sorts to top)
│   ├── Art/
│   │   ├── Materials/
│   │   ├── Models/
│   │   ├── Textures/
│   │   └── UI/
│   ├── Audio/
│   │   ├── Music/
│   │   ├── SFX/
│   │   └── Ambience/
│   ├── Prefabs/
│   │   ├── Characters/
│   │   ├── Environment/
│   │   └── UI/
│   ├── Scenes/
│   ├── Runtime/                 # C# scripts
│   │   ├── MyGame.Runtime.asmdef
│   │   ├── Core/
│   │   ├── Gameplay/
│   │   │   ├── Player/
│   │   │   ├── Enemies/
│   │   │   └── Items/
│   │   ├── UI/
│   │   └── Audio/
│   ├── Editor/                  # Editor-only scripts
│   │   ├── MyGame.Editor.asmdef
│   │   ├── CustomInspectors/
│   │   └── Tools/
│   ├── Tests/
│   │   ├── Runtime/
│   │   │   └── MyGame.Tests.Runtime.asmdef
│   │   └── Editor/
│   │       └── MyGame.Tests.Editor.asmdef
│   ├── Shaders/
│   ├── VFX/
│   └── Animation/
├── ThirdParty/                  # Third-party assets, isolated
├── Plugins/                     # Native DLLs (compiled first)
├── Resources/                   # Use sparingly; prefer Addressables
└── StreamingAssets/             # Raw files copied to build verbatim
```

### Special Unity folders

| Folder | Rule | Purpose |
|---|---|---|
| `Editor/` | Can appear anywhere; multiple allowed | Excluded from builds. Required for CustomEditors, PropertyDrawers, EditorWindows. |
| `Resources/` | Can appear anywhere; multiple allowed | Assets loaded via `Resources.Load()`. **Warning**: all Resources assets are always included in the build. Prefer Addressables. |
| `StreamingAssets/` | Exactly `Assets/StreamingAssets/` | Files copied verbatim to build. Not processed by the import pipeline. |
| `Plugins/` | Exactly `Assets/Plugins/` | Native DLLs. Scripts compile into `Assembly-CSharp-firstpass.dll`. |
| `Gizmos/` | Exactly `Assets/Gizmos/` | Custom gizmo graphics for scene view. |

### Type-based vs feature-based organization

**Type-based** (group by asset type: `Materials/`, `Textures/`, `Scripts/`) is simpler and works well for small-to-medium projects. **Feature-based** (group by game feature: `Player/`, `Weapons/`, `Enemies/` with mixed asset types) scales better for large teams working on isolated features. **The practical hybrid** is most common in professional projects: top-level split by broad category, then by feature within code folders and by type within art folders.

### Namespace-to-folder mapping

Namespaces should mirror the folder hierarchy under your scripts root:

```
Assets/_Project/Runtime/Gameplay/Player/PlayerController.cs
→ namespace MyGame.Gameplay.Player

Assets/_Project/Runtime/Core/EventBus.cs
→ namespace MyGame.Core

Assets/_Project/Editor/Tools/LevelBuilderWindow.cs
→ namespace MyGame.Editor.Tools
```

### Scene hierarchy organization

Use empty GameObjects as organizational folders in the scene hierarchy:

```
@System              # Script-only containers (@ prefix)
@Debug
@Management
Cameras
Lights
  Volumes
World
  Architecture
  Terrain
  Props
Gameplay
  Actors
  Items
  Triggers
_Dynamic             # Runtime-instantiated objects go here
```

---

## 7. Namespace conventions

### Package-to-namespace mapping

Package names use lowercase reverse-domain notation. C# namespaces use PascalCase and drop the TLD:

| Package Name | C# Namespace |
|---|---|
| `com.unity.entities` | `Unity.Entities` |
| `com.lithforge.voxel` | `Lithforge.Voxel` |
| `com.mycompany.networking` | `MyCompany.Networking` |

### Runtime vs Editor namespace separation

```
Company.Package              → Runtime code
Company.Package.Editor       → Editor-only code
Company.Package.Tests        → Runtime tests
Company.Package.Editor.Tests → Editor tests
```

This directly maps to assembly definition naming:

| Code Type | Assembly Definition | Namespace |
|---|---|---|
| Runtime | `MyGame.Runtime.asmdef` | `MyGame` or `MyGame.Runtime` |
| Editor | `MyGame.Editor.asmdef` | `MyGame.Editor` |
| Runtime Tests | `MyGame.Tests.Runtime.asmdef` | `MyGame.Tests` |
| Editor Tests | `MyGame.Tests.Editor.asmdef` | `MyGame.Editor.Tests` |

### Namespace depth

Keep namespaces **2–4 levels** deep. Beyond 4 is excessive:

```csharp
Lithforge                          // Level 1: Company
Lithforge.Voxel                    // Level 2: Product/Package
Lithforge.Voxel.Meshing            // Level 3: Feature/Module
Lithforge.Voxel.Meshing.Greedy     // Level 4: Sub-feature (rare)
```

### Critical rules

- **Always use namespaces** for all scripts to prevent naming conflicts.
- **Don't name namespaces after common Unity types** (e.g., avoid `MyProject.GUI` which clashes with `UnityEngine.GUI`).
- A file must not contain a MonoBehaviour in one namespace and other classes in a different namespace — Unity won't resolve the MonoBehaviour.

---

## 8. Shader and compute shader conventions

### Shader menu paths

Shader dropdown paths use `/` hierarchy. Use `Hidden/` to exclude from the material dropdown:

```hlsl
Shader "MyGame/Environment/Water" { }
Shader "MyGame/Effects/Dissolve" { }
Shader "Hidden/MyGame/PostProcess/Bloom" { }
```

Unity built-in patterns: `Universal Render Pipeline/Lit`, `HDRP/Lit`, `Sprites/Default`.

### Shader property naming

**All shader properties use underscore prefix + PascalCase** (`_PropertyName`). This is a firm Unity convention:

| Property | HLSL Name | Type |
|---|---|---|
| Base Color | `_BaseColor` | Color |
| Base Map | `_BaseMap` | 2D |
| Normal Map | `_BumpMap` | 2D |
| Bump Scale | `_BumpScale` | Float |
| Metallic | `_Metallic` | Float |
| Smoothness | `_Smoothness` | Float |
| Emission Map | `_EmissionMap` | 2D |
| Emission Color | `_EmissionColor` | Color |
| Occlusion Map | `_OcclusionMap` | 2D |
| Cutoff | `_Cutoff` | Float |
| Custom | `_DissolveAmount`, `_WaveSpeed` | Any |

### Shader keywords

Keywords use **UPPERCASE_WITH_UNDERSCORES**, prefixed with `_` for local/custom keywords:

```hlsl
#pragma multi_compile _ _NORMALMAP
#pragma multi_compile _ _EMISSION
#pragma shader_feature _ALPHATEST_ON
#pragma multi_compile FOG_LINEAR FOG_EXP FOG_EXP2
```

### Shader pass names

Standardized PascalCase pass names for SRP compatibility:

`"ForwardLit"` (URP), `"ShadowCaster"`, `"DepthOnly"`, `"DepthNormals"`, `"GBuffer"`, `"Meta"`, `"MotionVectors"`.

### Compute shader conventions

**File naming**: `ParticleSimulation.compute`, `NoiseGenerator.compute` — PascalCase.

**Kernel naming**: PascalCase verbs describing the operation. `CSMain` for single-kernel shaders:

```hlsl
#pragma kernel CSMain                    // single kernel (default)
#pragma kernel InitializeParticles       // multi-kernel: descriptive PascalCase
#pragma kernel UpdatePositions
#pragma kernel ComputeForces
```

### HLSL variable naming inside shaders

| Category | Convention | Example |
|---|---|---|
| Material properties / uniforms | `_PascalCase` | `_BaseColor`, `_Time`, `_WorldSpaceCameraPos` |
| Local variables | `camelCase` | `worldPos`, `normalWS`, `attenuation` |
| Struct members | `camelCase` | `positionWS`, `normalWS`, `uv` |
| Macros / defines | `UPPER_SNAKE_CASE` | `SAMPLE_TEXTURE2D`, `UNITY_MATRIX_VP` |
| Functions | `PascalCase` | `TransformObjectToWorld()`, `SampleNormal()` |
| Semantics | `UPPER_CASE` | `SV_POSITION`, `TEXCOORD0` |
| CBUFFER names | `UPPER_SNAKE_CASE` | `CBUFFER_START(UnityPerMaterial)` |

### Buffer naming in C#

```csharp
// Private fields — follow standard C# field convention
private ComputeBuffer _particleBuffer;
private GraphicsBuffer _meshVertexBuffer;

// Cache property IDs to avoid string lookups
private static readonly int s_particleBufferId = Shader.PropertyToID("_ParticleBuffer");

// Set buffers using cached IDs
computeShader.SetBuffer(kernelIndex, s_particleBufferId, _particleBuffer);
```

### Shader Graph conventions

- **Sub-Graphs**: PascalCase file names — `TriplanarMapping.shadersubgraph`, `FresnelEffect.shadersubgraph`
- **Custom Function nodes**: PascalCase function name in the Name field. HLSL implementation requires precision suffix (`MyFunction_float`, `MyFunction_half`) but the Shader Graph Name field omits it.
- **Properties**: set the Reference field to the HLSL name (e.g., `_BaseColor`) while the Display Name is human-readable ("Base Color").

---

## 9. Industry references

### Primary style guides

**Microsoft C# Coding Conventions** — The canonical C# naming guide. PascalCase for types/methods/properties, `_camelCase` for private fields, `I` prefix for interfaces, no Hungarian notation.
`https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions`

**.NET Runtime Coding Style** — Microsoft's internal style used in .NET runtime. Adds `s_` for static fields, `t_` for thread-static.
`https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md`

**Unity Official Style Guide (Unity 6 Edition)** — Unity's recommended starting point. Uses `m_`/`s_`/`k_` prefixes. Includes downloadable e-book and GitHub example.
`https://unity.com/resources/c-sharp-style-guide-unity-6`
`https://github.com/thomasjacobsen-unity/Unity-Code-Style-Guide`

**Google C# Style Guide** — Created for C# at Google, including Unity projects. Key differentiator: naming is unaffected by `static`/`const`/`readonly` modifiers — always `_camelCase` for private fields.
`https://google.github.io/styleguide/csharp-style.html`

### Open-source Unity projects with good conventions

| Project | Focus |
|---|---|
| Unity Open Project #1 (Chop Chop) | Full game following Unity's official style |
| justinwasilenko/Unity-Style-Guide | Comprehensive asset naming (Unreal convention ported to Unity) |
| thomasjacobsen-unity/Unity-Code-Style-Guide | Unity's official code style example repo |
| stillwwater/UnityStyleGuide | Namespace-to-directory mapping, alternative texture suffixes |
| Unity EntityComponentSystemSamples | Official DOTS/ECS naming patterns |

### Unreal-to-Unity crossover

Unreal's prefix system (`T_`, `SM_`, `SK_`, `M_`, `BP_`) is the most established asset naming convention in games. Unity projects increasingly adopt **selective** Unreal patterns — especially texture type suffixes (`_D`, `_N`, `_AO`) and mesh prefixes (`SM_`, `SK_`). The justinwasilenko/Unity-Style-Guide is a direct port. However, Unity's file-extension-based type system and Project window icons make type prefixes less necessary than in Unreal's single-extension `.uasset` system. Adopt prefix conventions when your team benefits from filename-level type identification; skip them when the extra characters add noise without value.

---

## Quick-reference decision table

For teams that want a single consistent ruleset, here is the recommended convention that maximizes compatibility across Microsoft, Google, and Unity ecosystems:

| Decision | Recommendation |
|---|---|
| Private field prefix | `_camelCase` (Microsoft + Google alignment) |
| Static field prefix | `s_camelCase` (Microsoft + Unity alignment) |
| Constants | `PascalCase` (Microsoft standard) |
| Public fields | `PascalCase` (universal agreement) |
| Braces | Allman style (Microsoft + Unity alignment) |
| Indentation | 4 spaces (Microsoft + Unity alignment) |
| Asset prefixes | Use `T_`, `SM_`, `SK_`, `M_` prefixes + type suffixes for textures |
| Folder structure | Hybrid: type-based for art, feature-based for code |
| Namespaces | Match folder hierarchy, 2–4 levels deep |
| ECS components | Bare noun, no "Component" suffix |
| Job structs | `{Action}Job` suffix |
| Systems | `{Behavior}System` suffix |
| Authoring | `{Name}Authoring` suffix |