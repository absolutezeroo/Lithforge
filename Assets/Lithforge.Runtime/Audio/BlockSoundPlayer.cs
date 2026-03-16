using System.Collections.Generic;
using Lithforge.Voxel.Block;
using Unity.Mathematics;
using UnityEngine;

namespace Lithforge.Runtime.Audio
{
    /// <summary>
    /// Plays positional block sounds (break, place, hit) by looking up the
    /// block's sound group in the registry. Applies pitch/volume randomization
    /// and enforces a per-group+event cooldown to prevent voice pile-up.
    /// Zero per-frame allocation.
    /// </summary>
    public sealed class BlockSoundPlayer
    {
        private readonly SoundGroupRegistry _registry;
        private readonly StateRegistry _stateRegistry;
        private readonly SfxSourcePool _pool;
        private readonly System.Random _rng;
        private readonly float _cooldownSeconds;

        // Compound key: soundGroup hashcode ^ eventType — maps to last play time
        private readonly Dictionary<long, float> _cooldowns = new Dictionary<long, float>();

        public BlockSoundPlayer(
            SoundGroupRegistry registry,
            StateRegistry stateRegistry,
            SfxSourcePool pool,
            int cooldownMs)
        {
            _registry = registry;
            _stateRegistry = stateRegistry;
            _pool = pool;
            _rng = new System.Random();
            _cooldownSeconds = cooldownMs / 1000f;
        }

        /// <summary>
        /// Plays a sound for the given event type at the block's world position.
        /// Looks up the block's sound group via StateRegistryEntry.
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
        /// Plays a sound from a named sound group at the given world position.
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

        private static Vector3 BlockCenter(int3 coord)
        {
            return new Vector3(coord.x + 0.5f, coord.y + 0.5f, coord.z + 0.5f);
        }

        private static long ComputeKey(string group, SoundEventType eventType)
        {
            return ((long)group.GetHashCode() << 8) | (long)eventType;
        }
    }
}
