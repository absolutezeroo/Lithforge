using System.Collections.Generic;

using Lithforge.Voxel.Block;

using Unity.Mathematics;

using UnityEngine;

using Random = System.Random;

namespace Lithforge.Runtime.Audio
{
    /// <summary>
    ///     Plays positional block sounds (break, place, hit) by looking up the
    ///     block's sound group in the registry. Applies pitch/volume randomization
    ///     and enforces a per-group+event cooldown to prevent voice pile-up.
    ///     Zero per-frame allocation.
    /// </summary>
    public sealed class BlockSoundPlayer
    {
        /// <summary>Maps compound key (sound group hash + event type) to last play time for cooldown enforcement.</summary>
        private readonly Dictionary<long, float> _cooldowns = new();

        /// <summary>Minimum seconds between plays of the same group+event combination.</summary>
        private readonly float _cooldownSeconds;

        /// <summary>Pre-allocated pool of AudioSources for spatial playback.</summary>
        private readonly SfxSourcePool _pool;

        /// <summary>Registry mapping sound group names to their definitions.</summary>
        private readonly SoundGroupRegistry _registry;

        /// <summary>Random number generator for clip and pitch variation.</summary>
        private readonly Random _rng;

        /// <summary>State registry for looking up block sound groups by StateId.</summary>
        private readonly StateRegistry _stateRegistry;

        /// <summary>Creates the player with the given registry, state registry, pool, and cooldown.</summary>
        public BlockSoundPlayer(
            SoundGroupRegistry registry,
            StateRegistry stateRegistry,
            SfxSourcePool pool,
            int cooldownMs)
        {
            _registry = registry;
            _stateRegistry = stateRegistry;
            _pool = pool;
            _rng = new Random();
            _cooldownSeconds = cooldownMs / 1000f;
        }

        /// <summary>
        ///     Plays a sound for the given event type at the block's world position.
        ///     Looks up the block's sound group via StateRegistryEntry.
        /// </summary>
        public void PlayBlockSound(StateId stateId, SoundEventType eventType, int3 blockCoord)
        {
            StateRegistryEntry entry = _stateRegistry.GetEntryForState(stateId);

            if (entry == null)
            {
                return;
            }

            PlayGroupSound(entry.SoundGroup, eventType, BlockCenter(blockCoord));
        }

        /// <summary>
        ///     Plays a sound from a named sound group at the given world position.
        /// </summary>
        public void PlayGroupSound(string soundGroup, SoundEventType eventType, Vector3 position)
        {
            SoundGroupDefinition definition = _registry.Get(soundGroup);

            if (definition == null)
            {
                return;
            }

            // Check cooldown
            long key = ComputeKey(soundGroup, eventType);
            float now = Time.time;

            if (_cooldowns.TryGetValue(key, out float lastTime))
            {
                if (now - lastTime < _cooldownSeconds)
                {
                    return;
                }
            }

            AudioClip clip = definition.GetRandomClip(eventType, _rng);

            if (clip == null)
            {
                return;
            }

            float volume = definition.GetVolume(eventType);
            float pitch = definition.GetRandomPitch(eventType, _rng);

            _pool.Play(clip, position, volume, pitch);
            _cooldowns[key] = now;
        }

        /// <summary>Returns the world-space center of the given block coordinate.</summary>
        private static Vector3 BlockCenter(int3 coord)
        {
            return new Vector3(coord.x + 0.5f, coord.y + 0.5f, coord.z + 0.5f);
        }

        /// <summary>Computes a compound cooldown key from a sound group name and event type.</summary>
        private static long ComputeKey(string group, SoundEventType eventType)
        {
            return (long)group.GetHashCode() << 8 | (long)eventType;
        }
    }
}
