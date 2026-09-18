using System;

namespace LastGround.Core.Tick
{
    /// <summary>Adapts a method to <see cref="ITickable"/> (e.g. a second phase of the same system). Allocates once at install.</summary>
    public sealed class TickAction : ITickable
    {
        readonly Action<float> _action;

        public TickAction(Action<float> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public void Tick(float dt, uint tick) => _action(dt);
    }
}
