using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

namespace LittleBeakCluck.Infrastructure
{
    [Obfuscation(Feature = "rename", Exclude = true, ApplyToMembers = true)]
    public class ServiceLocator : IServiceLocator
    {
        private static ServiceLocator _instance;
        public static ServiceLocator Instance => _instance ?? (_instance = new ServiceLocator());

        private readonly Dictionary<System.Type, IGameService> _services = new Dictionary<System.Type, IGameService>();

        public void Register<T>(T service) where T : IGameService
        {
            if (service == null)
                return;

            var type = typeof(T);

            if (_services.TryGetValue(type, out var existing))
            {
                if (!IsUnityObjectAlive(existing) || !ReferenceEquals(existing, service))
                {
                    _services[type] = service;
                }

                return;
            }

            _services.Add(type, service);
        }

        public T Get<T>() where T : IGameService
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var service))
            {
                if (!IsUnityObjectAlive(service))
                {
                    _services.Remove(type);
                    return default;
                }

                return (T)service;
            }

            return default;
        }

        private static bool IsUnityObjectAlive(IGameService service)
        {
            if (service is UnityEngine.Object unityObject)
            {
                return unityObject != null;
            }

            return service != null;
        }
    }
}
