namespace Lithforge.Runtime.Audio
{
    /// <summary>
    ///     Groups all audio system references injected into GameLoop.
    /// </summary>
    public sealed class GameLoopAudioState
    {
        public FootstepController FootstepController { get; set; }

        public FallSoundDetector FallSoundDetector { get; set; }

        public SfxSourcePool SfxSourcePool { get; set; }

        public AudioEnvironmentController AudioEnvironmentController { get; set; }
    }
}
