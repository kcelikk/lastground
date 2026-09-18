using System;
using System.Collections.Generic;

namespace LastGround.Core.Services
{
    /// <summary>
    /// App-lifetime service registry filled by the composition root (AppBootstrap).
    /// Allowed callers: installers, App, UI/presentation bindings. Gameplay systems receive dependencies through
    /// constructors instead of calling this.
    /// </summary>
    public static class AppServices
    {
        static readonly Dictionary<Type, object> Registry = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            Registry[typeof(T)] = service ?? throw new ArgumentNullException(nameof(service));
        }

        public static T Get<T>() where T : class
        {
            if (Registry.TryGetValue(typeof(T), out object service))
                return (T)service;
            throw new InvalidOperationException("Service not registered: " + typeof(T).Name);
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Registry.TryGetValue(typeof(T), out object value))
            {
                service = (T)value;
                return true;
            }

            service = null;
            return false;
        }

        public static void Clear()
        {
            Registry.Clear();
        }
    }
}
