using Lithforge.Runtime.Audio;

namespace Lithforge.Runtime.Tick
{
    /// <summary>
    /// Drives <see cref="AudioEnvironmentController.Tick"/> at 30 TPS
    /// from the fixed tick loop. Slow-changing audio state (biome, underwater,
    /// enclosure) is re-evaluated at tick rate; fast parameters (filter cutoff,
    /// reverb, crossfade) interpolate at frame rate in Update().
    /// </summary>
    public sealed class AudioEnvironmentTickAdapter : ITickable
    {
        /// <summary>The audio environment controller to tick.</summary>
        private readonly AudioEnvironmentController _controller;

        /// <summary>Creates an audio environment tick adapter wrapping the given controller.</summary>
        public AudioEnvironmentTickAdapter(AudioEnvironmentController controller)
        {
            _controller = controller;
        }

        /// <summary>Re-evaluates slow-changing audio state (biome, underwater, enclosure).</summary>
        public void Tick(float tickDt)
        {
            _controller.Tick();
        }
    }
}
