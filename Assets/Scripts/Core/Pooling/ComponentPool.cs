using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastGround.Core.Pooling
{
    /// <summary>
    /// Pool of prefab instances for things that need a Transform (explosions, telegraphs, audio voices, UI rows).
    /// Instanced-render data (zombies, corpses, tracers…) uses struct slot pools instead (TDD_02 §20.1).
    /// </summary>
    public sealed class ComponentPool<T> where T : Component, IPoolable
    {
        readonly T _prefab;
        readonly Transform _root;
        readonly Stack<T> _free;
        readonly bool _allowGrowth;
        int _created;

        public int Created => _created;
        public int FreeCount => _free.Count;

        public ComponentPool(T prefab, Transform root, int prewarm, bool allowGrowth)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            _prefab = prefab;
            _root = root;
            _allowGrowth = allowGrowth;
            _free = new Stack<T>(Mathf.Max(prewarm, 4));
            for (int i = 0; i < prewarm; i++)
                _free.Push(CreateInstance());
        }

        /// <summary>Returns an active instance, or null when empty and growth is not allowed.</summary>
        public T Spawn(Vector3 position, Quaternion rotation)
        {
            T item;
            if (_free.Count > 0)
            {
                item = _free.Pop();
            }
            else if (_allowGrowth)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.LogWarning($"[Pool] {_prefab.name} grew beyond prewarm ({_created}).");
#endif
                item = CreateInstance();
            }
            else
            {
                return null;
            }

            item.transform.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);
            item.OnSpawned();
            return item;
        }

        public void Despawn(T item)
        {
            if (item == null) return;
            item.OnDespawned();
            item.gameObject.SetActive(false);
            _free.Push(item);
        }

        T CreateInstance()
        {
            T item = UnityEngine.Object.Instantiate(_prefab, _root);
            item.gameObject.SetActive(false);
            _created++;
            return item;
        }
    }
}
