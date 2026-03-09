using System;

namespace Lithforge.Core.Events
{
    /// <summary>
    /// Synchronous, main-thread-only event bus for decoupled communication.
    /// </summary>
    public interface IEventBus
    {
        public void Subscribe<T>(Action<T> handler);

        public void Unsubscribe<T>(Action<T> handler);

        public void Publish<T>(T evt);
    }
}
