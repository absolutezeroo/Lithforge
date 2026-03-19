using Lithforge.Runtime.Rendering;

namespace Lithforge.Runtime.Tick
{
    /// <summary>
    /// Advances TimeOfDayController at fixed tick rate.
    /// Material updates (_SunLightFactor, directional light rotation) still happen
    /// in TimeOfDayController.Update() at frame rate for smooth visuals.
    /// </summary>
    public sealed class TimeOfDayTickAdapter : ITickable
    {
        /// <summary>The time-of-day controller to advance.</summary>
        private readonly TimeOfDayController _controller;

        /// <summary>Creates a time-of-day tick adapter wrapping the given controller.</summary>
        public TimeOfDayTickAdapter(TimeOfDayController controller)
        {
            _controller = controller;
        }

        /// <summary>Advances the day/night cycle by one fixed tick interval.</summary>
        public void Tick(float tickDt)
        {
            _controller.AdvanceTick(tickDt);
        }
    }
}
