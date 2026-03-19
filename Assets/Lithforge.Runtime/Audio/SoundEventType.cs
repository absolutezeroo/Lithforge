namespace Lithforge.Runtime.Audio
{
    /// <summary>
    /// Categories of block-related sound events, used as keys into
    /// <see cref="SoundGroupDefinition"/> clip arrays.
    /// </summary>
    public enum SoundEventType : byte
    {
        /// <summary>Sound played when a block is broken.</summary>
        Break = 0,

        /// <summary>Sound played when a block is placed.</summary>
        Place = 1,

        /// <summary>Sound played when the player steps on a block.</summary>
        Step = 2,

        /// <summary>Sound played when a block is hit during mining.</summary>
        Hit = 3,

        /// <summary>Sound played when the player lands on a block after falling.</summary>
        Fall = 4,
    }
}
