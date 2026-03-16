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
        private readonly AudioEnvironmentController _controller;

        public AudioEnvironmentTickAdapter(AudioEnvironmentController controller)
        {
            _controller = controller;
        }

        public void Tick(float tickDt)
        {
            _controller.Tick();
        }
    }
}
