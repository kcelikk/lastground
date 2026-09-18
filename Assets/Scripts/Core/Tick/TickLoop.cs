using System;
using System.Collections.Generic;

namespace LastGround.Core.Tick
{
    /// <summary>
    /// Engine-independent fixed-step loop. Frame phases run once per <see cref="Advance"/>;
    /// sim phases run 0..N times per frame at <see cref="SimRate"/> Hz using an accumulator.
    /// Registration allocates; ticking does not.
    /// </summary>
    public sealed class TickLoop
    {
        /// <summary>Upper bound of sim steps per frame, so a long hitch cannot spiral.</summary>
        public const int MaxStepsPerFrame = 4;

        readonly List<ITickable>[] _phases = new List<ITickable>[TickPhaseInfo.Count];
        readonly float _simDt;
        float _accumulator;

        public int SimRate { get; }
        public float SimDeltaTime => _simDt;

        /// <summary>Number of completed sim steps since start.</summary>
        public uint SimTick { get; private set; }

        /// <summary>0..1 fraction between the last and next sim step, for render interpolation.</summary>
        public float SimAlpha => _accumulator / _simDt;

        /// <summary>When true, sim phases do not advance (solo level-up pause, TDD_01 §7.5).</summary>
        public bool SimPaused { get; set; }

        public TickLoop(int simRate = 30)
        {
            if (simRate <= 0) throw new ArgumentOutOfRangeException(nameof(simRate));
            SimRate = simRate;
            _simDt = 1f / simRate;
            for (int i = 0; i < _phases.Length; i++)
                _phases[i] = new List<ITickable>(8);
        }

        public void Register(TickPhase phase, ITickable tickable)
        {
            if (tickable == null) throw new ArgumentNullException(nameof(tickable));
            var list = _phases[(int)phase];
            if (!list.Contains(tickable)) list.Add(tickable);
        }

        public void Unregister(TickPhase phase, ITickable tickable)
        {
            _phases[(int)phase].Remove(tickable);
        }

        /// <summary>Advances one rendered frame. Returns the number of sim steps executed.</summary>
        public int Advance(float frameDt)
        {
            if (frameDt < 0f) frameDt = 0f;

            RunPhase(TickPhase.Input, frameDt);
            RunPhase(TickPhase.LocalPlayer, frameDt);
            RunPhase(TickPhase.NetReceive, frameDt);

            int steps = 0;
            if (!SimPaused)
            {
                _accumulator += frameDt;
                while (_accumulator >= _simDt && steps < MaxStepsPerFrame)
                {
                    _accumulator -= _simDt;
                    for (int p = (int)TickPhase.Director; p <= (int)TickPhase.NetSend; p++)
                        RunPhase((TickPhase)p, _simDt);
                    SimTick++;
                    steps++;
                }
                // Drop the backlog after a hitch instead of fast-forwarding later.
                if (steps == MaxStepsPerFrame && _accumulator > _simDt)
                    _accumulator = 0f;
            }

            RunPhase(TickPhase.Presentation, frameDt);
            return steps;
        }

        void RunPhase(TickPhase phase, float dt)
        {
            var list = _phases[(int)phase];
            for (int i = 0; i < list.Count; i++)
                list[i].Tick(dt, SimTick);
        }
    }
}
