using System;
using System.Collections.Generic;

namespace Lithforge.Runtime.Bootstrap
{
    public sealed class ServiceContainer
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<T>(T instance)
        {
            _services[typeof(T)] = instance;
        }

        public T Get<T>()
        {
            if (_services.TryGetValue(typeof(T), out object service))
            {
                return (T)service;
            }

            throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
        }

        public bool TryGet<T>(out T service)
        {

            if (_services.TryGetValue(typeof(T), out object obj))
            {
                service = (T)obj;

                return true;
            }

            service = default;

            return false;
        }
    }
}
