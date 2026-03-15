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
        private readonly TimeOfDayController _controller;

        public TimeOfDayTickAdapter(TimeOfDayController controller)
        {
            _controller = controller;
        }

        public void Tick(float tickDt)
        {
            _controller.AdvanceTick(tickDt);
        }
    }
}
