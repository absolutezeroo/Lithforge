namespace Lithforge.Runtime.Audio
{
    /// <summary>
    /// Categories of block-related sound events, used as keys into
    /// <see cref="SoundGroupDefinition"/> clip arrays.
    /// </summary>
    public enum SoundEventType : byte
    {
        Break = 0,
        Place = 1,
        Step = 2,
        Hit = 3,
        Fall = 4,
    }
}
