using LastGround.Core.Events;
using LastGround.Core.Tick;
using LastGround.Gameplay.Interactables;
using UnityEngine;

namespace LastGround.Rendering.Interactables
{
    /// <summary>
    /// Shows and hides the scene objects of barrels and fuel tanks as they blow up and come back (TDD_01 §12.3). The map
    /// builder names them "Interactable_&lt;id&gt;"; they are toggled, never instantiated or destroyed at runtime.
    /// </summary>
    public sealed class InteractableViews : ITickable
    {
        readonly InteractableTable _table;
        readonly GameObject[] _objects;
        EventReader<InteractableChanged> _reader;

        public InteractableViews(InteractableTable table, Transform mapRoot, string prefix)
        {
            _table = table;
            _objects = new GameObject[table.Count];
            _reader = table.Changed.CreateReader();
            if (mapRoot == null) return;
            for (int i = 0; i < mapRoot.childCount; i++)
            {
                Transform child = mapRoot.GetChild(i);
                if (!child.name.StartsWith(prefix, System.StringComparison.Ordinal)) continue;
                if (int.TryParse(child.name.Substring(prefix.Length), System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out int id) && (uint)id < (uint)_objects.Length)
                    _objects[id] = child.gameObject;
            }
        }

        public void Tick(float dt, uint tick)
        {
            while (_table.Changed.TryRead(ref _reader, out InteractableChanged change))
            {
                GameObject go = _objects[change.Id];
                if (go != null && go.activeSelf != change.Intact) go.SetActive(change.Intact);
            }
        }
    }
}
