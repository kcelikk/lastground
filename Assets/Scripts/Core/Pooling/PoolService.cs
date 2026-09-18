using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastGround.Core.Pooling
{
    /// <summary>
    /// Run-scoped owner of component pools. Created by the run installer (not a static singleton) and
    /// disposed with the run, so nothing leaks between runs.
    /// </summary>
    public sealed class PoolService : IDisposable
    {
        readonly Transform _root;
        readonly Dictionary<int, object> _pools = new Dictionary<int, object>();

        public PoolService(Transform root)
        {
            _root = root;
        }

        /// <summary>Creates (or returns the existing) pool for a prefab. Call during loading, not in gameplay.</summary>
        public ComponentPool<T> GetOrCreate<T>(T prefab, int prewarm, bool allowGrowth) where T : Component, IPoolable
        {
            int key = prefab.GetInstanceID();
            if (_pools.TryGetValue(key, out object existing))
                return (ComponentPool<T>)existing;

            var group = new GameObject(prefab.name + "_Pool").transform;
            group.SetParent(_root, false);
            var pool = new ComponentPool<T>(prefab, group, prewarm, allowGrowth);
            _pools.Add(key, pool);
            return pool;
        }

        public void Dispose()
        {
            _pools.Clear();
            if (_root != null)
            {
                for (int i = _root.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(_root.GetChild(i).gameObject);
            }
        }
    }
}
