# Lithforge — Project Analysis Report

**Date** : 2026-03-18
**Commit** : `c59f0f300ff94d90824dfa51793bc8ad0e815839`
**Analysé par** : Claude Code (Opus 4.6)

---

## Executive Summary

Lithforge est un moteur voxel Unity 6 d'environ 77 000 lignes de C# réparties en 7 packages UPM + un runtime, avec une architecture three-tier globalement respectée (2 violations de frontière mineures). Le code est d'une propreté de convention remarquable (zéro `var`, zéro expression-bodied method, zéro accessibilité implicite), tous les Burst jobs sont conformes, et le pipeline GPU-driven rendering est correctement implémenté avec un format vertex 16 octets parfaitement aligné entre C# et HLSL. Le multiplayer possède une fondation solide (always-server, prédiction client Gambetta, chunk streaming) mais présente des bottlenecks O(N²) critiques et des lacunes fonctionnelles (inventaire réseau, entités) qui empêchent le passage à 200 joueurs. Les risques principaux sont l'absence de `FloatMode.Deterministic` sur les jobs worldgen (non-déterminisme cross-plateforme), l'absence de récupération `.bak` au démarrage (perte de données possible), et quelques allocations per-frame dans le game loop.

---

## Métriques globales

| Métrique | Valeur |
|----------|--------|
| Fichiers .cs total | 560 |
| Lignes de code (estimation) | ~77 000 |
| Fichiers Tier 1 (com.lithforge.core) | 9 |
| Fichiers Tier 2 (voxel+worldgen+meshing+physics+network+item) | 217 |
| Fichiers Tier 3 (Assets/Lithforge.Runtime/) | 333 |
| Fichiers de test | 31 |
| Ratio test/production | 31/560 = 5.5% |
| Violations de convention détectées | 1 mineure (DirectChannel.cs : 2 types dans 1 fichier) |
| TODOs/FIXMEs sans issue | 1 (LiquidSimJob.cs:234) |
| NativeContainer leaks potentiels | 0 confirmé, 1 théorique (TempJob sous stutter extrême) |
| Packages UPM | 7 (core, voxel, worldgen, meshing, physics, network, item) |
| Shaders | 8 (5 voxel/sky, 3 player model) |
| Compute shaders | 3 (FrustumCull, HiZGenerate, BufferCopy) |
| Message types réseau | 18 |
| ChunkState valeurs | 8 (dont 2 orphelines) |

---

## Findings

### [CRITICAL] Absence de FloatMode.Deterministic sur les jobs worldgen

**Fichier(s)** : `Packages/com.lithforge.worldgen/Runtime/Stages/*.cs` (7 jobs : ClimateNoiseJob, TerrainShapeJob, RiverNoiseJob, RiverCarveJob, CaveCarverJob, SurfaceBuilderJob, OreGenerationJob)
**Description** : Aucun job Burst du projet n'utilise `[BurstCompile(FloatMode = FloatMode.Deterministic)]`. Le `FloatMode.Default` autorise des optimisations flottantes spécifiques au hardware (FMA, fast reciprocal, réordonnancement). Sur une même machine, la génération est déterministe, mais cross-plateforme (ARM vs x86, SSE vs AVX), les résultats de `noise.snoise`/`noise.cnoise` peuvent diverger.
**Impact** : En multiplayer client-serveur sur plateformes différentes, les biomes, caves et rivières aux frontières pourraient différer. Cela invalide la vérification côté client et rend la prédiction de terrain impossible.
**Recommandation** : Ajouter `[BurstCompile(FloatMode = FloatMode.Deterministic)]` aux 7 jobs de génération. Les jobs de light (entiers uniquement) n'en ont pas besoin.

### [CRITICAL] O(N²) dans le broadcast des player states

**Fichier(s)** : `Packages/com.lithforge.network/Runtime/Server/ServerGameLoop.cs:310`
**Description** : `BroadcastPlayerStates()` itère N joueurs × N observateurs. À 200 joueurs : 40 000 envois/tick × 30 TPS = 1,2 million d'envois/s. Chaque `PlayerStateMessage` ≈ 50 octets → ~60 MB/s de bande passante sortante pour les positions seules.
**Impact** : Le serveur ne peut pas dépasser ~50 joueurs sans saturer le CPU et la bande passante.
**Recommandation** : Implémenter un filtre d'intérêt spatial (Area of Interest) : seuls les joueurs dans le rayon de rendu reçoivent les states. Utiliser un spatial hash pour le lookup O(1).

### [CRITICAL] O(N²) dans la détection de présence joueur

**Fichier(s)** : `Packages/com.lithforge.network/Runtime/Server/ServerGameLoop.cs:377`
**Description** : `BroadcastPlayerPresenceChanges()` effectue N² lookups HashSet par tick pour détecter les transitions de visibilité spawn/despawn.
**Impact** : Même bottleneck que le broadcast des states.
**Recommandation** : Intégrer dans le même système AOI que le broadcast des states.

### [HIGH] Allocations per-frame dans GameLoopPoco

**Fichier(s)** : `Assets/Lithforge.Runtime/Session/GameLoopPoco.cs:102-103, 308`
**Description** : `new NullFrameProfiler()` et `new NullPipelineStats()` sont créés à chaque appel `Update()` et `LateUpdate()` quand les champs correspondants de `_config` sont null. Ce sont des allocations sur le heap à chaque frame.
**Impact** : Pression GC continue. Faible en volume (~64 octets/frame) mais viole le principe zero-alloc du projet.
**Recommandation** : Cacher les instances comme `static readonly` singletons ou champs privés.

### [HIGH] Allocations per-tick dans ChunkDirtyTracker

**Fichier(s)** : `Packages/com.lithforge.network/Runtime/Chunk/ChunkDirtyTracker.cs:68, 77, 29`
**Description** : `GetDirtyChunks()` crée un `new List<int3>` à chaque appel. `FlushAll()` crée un `new Dictionary<>` snapshot à chaque tick. `OnBlockChanged()` crée un `new List<BlockChangeEntry>` par chunk dirty sans pooling.
**Impact** : 30 allocations Dictionary/s + N allocations List/s. Pression GC significative sur serveur actif.
**Recommandation** : Utiliser le fill pattern (l'appelant passe la collection, le callee clear+add). Pooler les listes de BlockChangeEntry.

### [HIGH] Sérialisation de chunks sur le main thread

**Fichier(s)** : `Packages/com.lithforge.network/Runtime/Server/NetworkChunkStreamingStrategy.cs`
**Description** : `StreamChunk()` appelle `ChunkNetSerializer.SerializeFullChunk()` synchronement sur le main thread. À 200 joueurs × 2 chunks/tick steady-state = 400 sérialisations/tick. Chaque sérialisation implique construction de palette, compression zstd, et `stream.ToArray()`.
**Impact** : Dominera le temps de tick. Budget 33ms (30 TPS) sera dépassé.
**Recommandation** : Offloader la sérialisation sur un worker thread. Implémenter un cache de chunks sérialisés (keyed par coord + version).

### [HIGH] Absence de récupération .bak au démarrage

**Fichier(s)** : `Packages/com.lithforge.voxel/Runtime/Storage/RegionFile.cs:127`, `WorldMetadata.cs:103`
**Description** : L'écriture atomique (tmp → bak → rename) est correctement implémentée. Cependant, si un crash survient entre le rename original→bak et le rename tmp→original, les données existent dans le .bak mais ne sont jamais récupérées au chargement suivant. Aucun code ne cherche les fichiers .bak au démarrage.
**Impact** : Perte de données (chunk ou métadonnées monde) dans un scénario de crash étroit mais réel.
**Recommandation** : Au démarrage de `RegionFile` et `WorldMetadata.Load()`, vérifier si un .bak existe sans fichier original correspondant, et le restaurer.

### [HIGH] Pas de synchronisation d'inventaire réseau

**Fichier(s)** : `Packages/com.lithforge.item/Runtime/Item/Inventory.cs`, Docs/14_MULTIPLAYER.md (P5)
**Description** : L'inventaire opère entièrement en local. Aucun message réseau pour la synchronisation d'inventaire, pas de validation server-side des actions de crafting/container, pas de state ID pour la réconciliation.
**Impact** : Duplication d'items, désynchronisation, exploitation en multiplayer.
**Recommandation** : P5 du plan multiplayer (Inventory Sync) est la priorité fonctionnelle suivante.

### [HIGH] Pas de système d'entités

**Fichier(s)** : Aucun (planned, not implemented)
**Description** : DOTS ECS est planifié mais non implémenté. Aucun système de mobs, projectiles, ou items au sol n'existe. Pour 200 joueurs, la simulation d'entités nécessite partitionnement spatial, gestion d'intérêt, et broadcast d'états.
**Impact** : Fonctionnalité gameplay manquante critique.
**Recommandation** : Concevoir le système entity avec le networking en tête dès le départ (AOI, autorité serveur, interpolation).

### [MEDIUM] Violation de frontière Tier 2 : UnityEngine.Rendering dans Meshing

**Fichier(s)** : `Packages/com.lithforge.meshing/Runtime/MeshVertex.cs:5`
**Description** : `using UnityEngine.Rendering;` pour `VertexAttributeDescriptor`, `VertexAttribute`, `VertexAttributeFormat`. Ce sont des types UnityEngine dans un package Tier 2 qui ne devrait référencer que Burst/Collections/Mathematics/Jobs.
**Impact** : Viole la frontière de tier documentée. Empêche la compilation du package en isolation hors UnityEngine.
**Recommandation** : Déplacer `MeshVertex` (qui utilise des vertex attributes Unity standard) vers Tier 3, ou dupliquer les constantes nécessaires comme des ints dans Tier 2.

### [MEDIUM] Violation de frontière Tier 2 : UnityEngine.Profiling dans Voxel

**Fichier(s)** : `Packages/com.lithforge.voxel/Runtime/Storage/WorldStorage.cs:12`
**Description** : `using UnityEngine.Profiling;` pour `Profiler.BeginSample()`/`EndSample()`. Dépendance au Profiler Unity dans un package Tier 2.
**Impact** : Même violation de frontière que ci-dessus, mais moins grave (profiling conditionnel).
**Recommandation** : Utiliser `Unity.Profiling.ProfilerMarker` (qui fait partie de Unity.Collections, autorisé en Tier 2) ou conditionner avec `#if UNITY_EDITOR`.

### [MEDIUM] noEngineReferences manquant sur 4 packages Tier 2

**Fichier(s)** : `.asmdef` de com.lithforge.voxel, com.lithforge.worldgen, com.lithforge.meshing, com.lithforge.physics
**Description** : Seuls `com.lithforge.core` et `com.lithforge.item` ont `noEngineReferences: true`. Les 4 autres Tier 2 packages ont `false`, permettant l'utilisation accidentelle de UnityEngine sans erreur de compilation.
**Impact** : Les 2 violations ci-dessus n'auraient pas été possibles avec `noEngineReferences: true`.
**Recommandation** : Activer `noEngineReferences: true` sur tous les Tier 2 (après correction des 2 violations).

### [MEDIUM] États orphelins dans ChunkState

**Fichier(s)** : `Packages/com.lithforge.voxel/Runtime/Chunk/ChunkState.cs`
**Description** : `Loading` (1) et `Decorating` (3) sont définis dans l'enum mais jamais assignés par aucun code. L'histogramme alloue 8 slots mais n'en utilise que 6.
**Impact** : Code mort, confusion potentielle dans les diagnostics. L'invariant ordinal (`>=` comparisons) reste correct grâce au positionnement.
**Recommandation** : Supprimer les états inutilisés ou documenter qu'ils sont réservés pour usage futur.

### [MEDIUM] Pas de validation des recettes au chargement

**Fichier(s)** : `Packages/com.lithforge.item/Runtime/Crafting/CraftingEngine.cs`
**Description** : Le constructeur stocke la liste de recettes sans vérification. Pas de détection de conflits (doublons), pas de validation que les keys de pattern correspondent à des entrées valides, pas de vérification que les ResourceId d'ingrédients/résultats existent dans l'ItemRegistry.
**Impact** : Les erreurs de contenu (recettes mal configurées) sont silencieuses — la recette ne matche jamais sans diagnostic.
**Recommandation** : Ajouter une phase de validation dans ContentPipeline qui log les warnings pour recettes invalides.

### [MEDIUM] Pas d'authentification réseau

**Fichier(s)** : `Packages/com.lithforge.network/Runtime/Messages/HandshakeRequestMessage.cs`
**Description** : Le handshake valide le content hash et accepte un nom de joueur string, mais aucune authentification (UUID, token, vérification de compte). Tout client peut se connecter avec n'importe quel nom.
**Impact** : Inadéquat pour un serveur public. Usurpation d'identité, grief sans responsabilité.
**Recommandation** : Implémenter au minimum un UUID persistant côté client, idéalement un système de token/session.

### [MEDIUM] MessageSerializer buffer statique non thread-safe

**Fichier(s)** : `Packages/com.lithforge.network/Runtime/Message/MessageSerializer.cs:13`
**Description** : `_sendBuffer` est un `static byte[]` partagé, commenté "main thread only". Si la sérialisation est un jour déplacée vers un worker thread (ce qui sera nécessaire à l'échelle), c'est une corruption de données.
**Impact** : Pas de problème aujourd'hui (tout main thread), mais piège architectural.
**Recommandation** : Utiliser `[ThreadStatic]` ou un pool de buffers.

### [MEDIUM] BlockInteraction.cs : fan-out de dépendances excessif

**Fichier(s)** : `Assets/Lithforge.Runtime/Input/BlockInteraction.cs`
**Description** : Ce fichier importe 9 namespaces Tier 2 distincts (Core.Data, Physics, Voxel.Block, Voxel.Chunk, Voxel.Command, Item, Item.Loot, Voxel.Item, Voxel.Loot, Voxel.Tag). C'est le plus large fan-out du projet.
**Impact** : Fat controller difficile à tester et maintenir.
**Recommandation** : Décomposer en handlers spécialisés (MiningHandler, PlacementHandler, LootHandler, ContainerHandler).

### [LOW] Allocation per-frame dans BlockHighlight

**Fichier(s)** : `Assets/Lithforge.Runtime/Rendering/BlockHighlight.cs:65`
**Description** : `new Vector3[8]` alloué à chaque appel de `SetTarget()` (per-frame quand le joueur regarde un bloc).
**Impact** : 96 octets/frame. Négligeable en volume mais viole le principe.
**Recommandation** : Cacher le tableau comme champ privé.

### [LOW] Allocation dans LiquidScheduler

**Fichier(s)** : `Assets/Lithforge.Runtime/Scheduling/LiquidScheduler.cs:624`
**Description** : `new List<int>(pending.OutputActiveSet.Length)` créé à chaque liquid job complété.
**Impact** : Faible fréquence (jobs liquid complètent rarement), mais devrait pooler.
**Recommandation** : Utiliser un pool de listes ou réutiliser une liste existante.

### [LOW] com.lithforge.item non documenté dans CLAUDE.md

**Fichier(s)** : `CLAUDE.md` (section Package Structure), `Packages/com.lithforge.item/`
**Description** : Le package `com.lithforge.item` (50 fichiers, 3181 lignes) n'est pas listé dans la section "Three-Tier Architecture" de CLAUDE.md ni dans la "Package Structure". Il est seulement mentionné dans MEMORY.md.
**Impact** : Documentation incomplète pour les contributeurs.
**Recommandation** : Ajouter com.lithforge.item à la doc.

### [LOW] PeerRegistry.AllocatePlayerId overflow ushort

**Fichier(s)** : `Packages/com.lithforge.network/Runtime/Connection/PeerRegistry.cs`
**Description** : `_nextPlayerId` est un `ushort` incrémental. Après 65 534 connexions cumulées, overflow. Les IDs des joueurs déconnectés ne sont pas recyclés.
**Impact** : Extrêmement long terme. Seulement problématique pour des serveurs à très longue durée de vie.
**Recommandation** : Recycler les IDs ou passer à `uint`.

### [LOW] Deux types dans DirectChannel.cs

**Fichier(s)** : `Packages/com.lithforge.network/Runtime/Transport/DirectChannel.cs`
**Description** : Contient `DirectChannel` (class) et `DirectPacket` (struct internal). Viole la règle "one file per type".
**Impact** : Cosmétique. `DirectPacket` est un détail d'implémentation étroitement couplé.
**Recommandation** : Extraire `DirectPacket` dans son propre fichier ou le faire nested type de `DirectChannel`.

### [LOW] _PartTransforms dupliqué dans HeldItem DepthOnly pass

**Fichier(s)** : `Assets/Lithforge.Runtime/Rendering/Shaders/LithforgeHeldItem.shader`
**Description** : Le DepthOnly pass redéclare `_PartTransforms` au lieu d'inclure `LithforgePlayerModelCommon.hlsl`. Si le layout change, il faut mettre à jour 2 endroits.
**Recommandation** : Inclure le common header ou extraire la déclaration dans un include partagé.

---

## Tier Boundary Compliance

| Package | Tier | noEngineReferences | UnityEngine Usage | Verdict |
|---------|------|--------------------|-------------------|---------|
| com.lithforge.core | 1 | **true** | Aucun | PASS |
| com.lithforge.voxel | 2 | false | `UnityEngine.Profiling` (WorldStorage.cs:12) | **VIOLATION** |
| com.lithforge.worldgen | 2 | false | Aucun | PASS (mais noEngineReferences=false) |
| com.lithforge.meshing | 2 | false | `UnityEngine.Rendering` (MeshVertex.cs:5) | **VIOLATION** |
| com.lithforge.physics | 2 | false | Aucun | PASS (mais noEngineReferences=false) |
| com.lithforge.network | 2 | false | Aucun | PASS |
| com.lithforge.item | 2 | **true** | Aucun | PASS |
| Lithforge.Runtime | 3 | false | Attendu | PASS |

---

## Architecture Strengths

1. **Convention enforcement parfaite** : Sur 560 fichiers, zéro violation de `var`, zéro expression-bodied method, zéro accessibilité implicite. L'`.editorconfig` avec severity `error` fonctionne.

2. **Burst compliance intégrale** : Les 21 fichiers avec `[BurstCompile]` sont tous conformes — pas de try/catch, pas de string ops, pas de managed `new`, pas de virtual calls, `[ReadOnly]` correctement appliqué sur tous les inputs non modifiés.

3. **Zéro Schedule+Complete anti-pattern en production** : Toutes les occurrences sont dans les tests unitaires. Les schedulers font correctement schedule → poll → complete.

4. **GPU-driven rendering bien implémenté** : PackedMeshVertex 16 octets parfaitement aligné entre C# et HLSL (vérifié bit par bit), MegaMeshBuffer avec free-list à coalescence, dirty range tracking (16 intervalles disjoints), Hi-Z occlusion, compute frustum culling. Zéro GPU readback.

5. **NativeContainer ownership rigoureux** : Chaque container a un propriétaire documenté, un chemin de dispose normal et un chemin de cancellation. ChunkPool utilise un HashSet de checkout pour tracking des fuites. GenerationHandle a Dispose() vs DisposeAll() séparant transfert d'ownership et nettoyage complet.

6. **Architecture always-server** : Le choix structurel le plus difficile (SP = serveur embarqué) est fait et implémenté avec DirectTransport zero-copy.

7. **Client prediction Gambetta correcte** : Ring buffer d'inputs, réconciliation 4 niveaux (ignore/smooth/reconcile/teleport), block prediction optimiste avec rollback.

8. **Sérialisation réseau efficace** : Palette + zstd pour les chunks, optimisation single-value (4 octets pour chunks homogènes), batch pour les block changes.

9. **Pipeline de génération linéaire strict** : 9 stages avec dépendances JobHandle explicites, pas de data race, pas de fork/join ambigu.

10. **Écriture atomique correcte** : Pattern tmp → bak → rename avec rollback on failure dans RegionFile et WorldMetadata.

---

## Technical Debt Register

| # | Sévérité | Composant | Description | Effort estimé |
|---|----------|-----------|-------------|---------------|
| 1 | Critical | WorldGen | FloatMode.Deterministic manquant sur 7 jobs | 1h |
| 2 | Critical | Network/Server | O(N²) broadcast player states + présence | 2-3j (AOI system) |
| 3 | High | GameLoopPoco | Allocations per-frame NullFrameProfiler/NullPipelineStats | 15min |
| 4 | High | ChunkDirtyTracker | Allocations per-tick (Dictionary, List) | 1h |
| 5 | High | ChunkStreaming | Sérialisation chunks main thread, pas de cache | 2-3j |
| 6 | High | Storage | Récupération .bak manquante | 2h |
| 7 | High | Network | Synchronisation inventaire absente | 1-2 semaines |
| 8 | High | Gameplay | Système d'entités absent | 3-6 semaines |
| 9 | Medium | Meshing | MeshVertex.cs dépendance UnityEngine.Rendering | 1h |
| 10 | Medium | Voxel | WorldStorage.cs dépendance UnityEngine.Profiling | 30min |
| 11 | Medium | Voxel | noEngineReferences=false sur 4 packages Tier 2 | 30min |
| 12 | Medium | ChunkState | 2 états orphelins (Loading, Decorating) | 15min |
| 13 | Medium | Crafting | Pas de validation recettes au chargement | 2h |
| 14 | Medium | Network | Pas d'authentification | 1-2j |
| 15 | Medium | Network | MessageSerializer buffer statique | 30min |
| 16 | Medium | Input | BlockInteraction.cs fat controller | 1j |
| 17 | Low | Rendering | BlockHighlight Vector3[8] per-frame | 5min |
| 18 | Low | Scheduling | LiquidScheduler List allocation | 15min |
| 19 | Low | Documentation | com.lithforge.item manquant dans CLAUDE.md | 15min |
| 20 | Low | Network | PeerRegistry ushort overflow | 15min |

---

## Multiplayer Readiness Assessment

**Score : 6/10**

**Justification factuelle :**

Points forts (valent +6) :
- Architecture always-server implémentée et fonctionnelle (+2)
- Client prediction Gambetta avec 4 niveaux de correction (+1)
- Chunk streaming interest-based avec rate limiting (+1)
- Block prediction optimiste avec validation serveur 8 checks (+1)
- Subsystem architecture propre avec SessionConfig discriminated union (+0.5)
- CompositeTransport + DirectTransport pour SP/Host zero-copy (+0.5)

Points faibles (valent -4) :
- O(N²) broadcast → mur à ~50 joueurs (-1.5)
- Pas de synchronisation inventaire (-1)
- Pas de système d'entités (-0.5)
- Chunk serialization main thread (-0.5)
- Pas d'authentification (-0.25)
- Pas de world partitioning (-0.25)

**Résumé** : Le foundation networking est au-dessus de la moyenne pour un projet à ce stade. Les primitives (transport, sérialisation, handshake, prediction, streaming) sont solides. Les gaps sont attendus — ce sont des features P5+ non encore implémentées. Le problème O(N²) est le seul défaut architectural dans le code existant ; le reste est manque de features.

---

## Recommandations prioritisées

| # | Action | Impact | Effort | Ratio |
|---|--------|--------|--------|-------|
| 1 | Ajouter `FloatMode.Deterministic` aux 7 jobs worldgen | Élimine le non-déterminisme cross-plateforme | 1h | Très élevé |
| 2 | Cacher NullFrameProfiler/NullPipelineStats comme singletons | Élimine allocations per-frame | 15min | Très élevé |
| 3 | Corriger ChunkDirtyTracker : fill pattern + pooling | Élimine allocations per-tick serveur | 1h | Élevé |
| 4 | Ajouter récupération .bak dans RegionFile/WorldMetadata.Load() | Prévient perte de données | 2h | Élevé |
| 5 | Activer `noEngineReferences: true` sur 4 packages Tier 2 | Prévient futures violations de frontière | 30min (+ fix des 2 violations) | Élevé |
| 6 | Supprimer états orphelins Loading/Decorating de ChunkState | Nettoie le code mort | 15min | Modéré |
| 7 | Implémenter AOI system pour le broadcast player states | Débloque le scaling au-delà de 50 joueurs | 2-3j | Critique (MP) |
| 8 | Offloader la sérialisation de chunks vers worker thread + cache | Réduit la charge tick serveur | 2-3j | Critique (MP) |
| 9 | Ajouter validation des recettes dans ContentPipeline | Détecte les erreurs de contenu tôt | 2h | Modéré |
| 10 | Décomposer BlockInteraction.cs en handlers spécialisés | Réduit le couplage, améliore la testabilité | 1j | Modéré |