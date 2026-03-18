using System;
using System.Collections.Generic;

using Lithforge.Runtime.Bootstrap;
using Lithforge.Runtime.World;

namespace Lithforge.Runtime.Session
{
    /// <summary>
    ///     Typed service locator for subsystem cross-references within a session.
    ///     Each subsystem registers its owned objects during <see cref="IGameSubsystem.Initialize" />,
    ///     and other subsystems retrieve them during <see cref="IGameSubsystem.PostInitialize" />.
    /// </summary>
    public sealed class SessionContext
    {
        private readonly Dictionary<Type, object> _services = new();

        public SessionContext(
            SessionConfig config,
            AppContext app,
            ContentPipelineResult content,
            SessionLifetimeTracker lifetime)
        {
            Config = config;
            App = app;
            Content = content;
            Lifetime = lifetime;
        }

        /// <summary>The session configuration that created this session.</summary>
        public SessionConfig Config { get; }

        /// <summary>App-lifetime context (settings, logger, profiler, screen manager).</summary>
        public AppContext App { get; }

        /// <summary>Content pipeline result (registries, atlases, items, etc.).</summary>
        public ContentPipelineResult Content { get; }

        /// <summary>Tracks disposables in initialization order for reverse disposal.</summary>
        public SessionLifetimeTracker Lifetime { get; }

        /// <summary>
        ///     Registers an object by its exact type. Subsystems call this in Initialize
        ///     so other subsystems can retrieve it in PostInitialize.
        /// </summary>
        public void Register<T>(T service) where T : class
        {
            Type key = typeof(T);

            if (_services.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"Service of type {key.Name} is already registered.");
            }

            _services[key] = service;
        }

        /// <summary>
        ///     Retrieves a registered service by type. Throws if not found.
        /// </summary>
        public T Get<T>() where T : class
        {
            Type key = typeof(T);

            if (_services.TryGetValue(key, out object service))
            {
                return (T)service;
            }

            throw new InvalidOperationException(
                $"Service of type {key.Name} is not registered. " +
                "Ensure the subsystem that owns it is included and initialized first.");
        }

        /// <summary>
        ///     Tries to retrieve a registered service by type. Returns false if not found.
        /// </summary>
        public bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out object obj))
            {
                service = (T)obj;

                return true;
            }

            service = null;

            return false;
        }
    }
}
