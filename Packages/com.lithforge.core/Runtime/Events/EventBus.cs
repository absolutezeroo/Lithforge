using System;
using System.Collections.Generic;

namespace Lithforge.Core.Events
{
    /// <summary>
    /// Synchronous, main-thread-only event bus.
    /// Stores handlers per event type using Dictionary&lt;Type, List&lt;Delegate&gt;&gt;.
    /// NOT thread-safe — must only be used from the main thread.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Type key = typeof(T);

            if (!_handlers.TryGetValue(key, out List<Delegate> list))
            {
                list = new List<Delegate>();
                _handlers[key] = list;
            }

            list.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Type key = typeof(T);

            if (_handlers.TryGetValue(key, out List<Delegate> list))
            {
                list.Remove(handler);
            }
        }

        public void Publish<T>(T evt)
        {
            Type key = typeof(T);

            if (!_handlers.TryGetValue(key, out List<Delegate> list))
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                ((Action<T>)list[i]).Invoke(evt);
            }
        }
    }
}
