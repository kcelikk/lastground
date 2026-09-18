using UnityEngine;

namespace LastGround.Core.Tick
{
    /// <summary>
    /// The only gameplay MonoBehaviour with an Update(). Drives a <see cref="TickLoop"/>.
    /// Created by a scene installer; systems register through <see cref="Loop"/>.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class TickScheduler : MonoBehaviour
    {
        [SerializeField, Range(10, 60)] int _simRate = 30;

        public TickLoop Loop { get; private set; }

        void Awake()
        {
            Loop = new TickLoop(_simRate);
        }

        void Update()
        {
            Loop.Advance(Time.deltaTime);
        }
    }
}
