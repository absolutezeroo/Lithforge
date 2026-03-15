namespace Lithforge.Runtime.Tick
{
    /// <summary>
    /// Implemented by any simulation system that participates in the fixed tick loop.
    /// Tick() is called once per fixed interval (FixedTickRate.TickDeltaTime).
    /// </summary>
    public interface ITickable
    {
        public void Tick(float tickDt);
    }
}
