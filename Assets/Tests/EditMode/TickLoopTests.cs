using LastGround.Core.Tick;
using NUnit.Framework;

namespace LastGround.Tests
{
    public class TickLoopTests
    {
        sealed class Counter : ITickable
        {
            public int Calls;
            public float LastDt;

            public void Tick(float dt, uint tick)
            {
                Calls++;
                LastDt = dt;
            }
        }

        [Test]
        public void SimPhases_RunAtFixedRate_IndependentOfFrameRate()
        {
            var loop = new TickLoop(30);
            var sim = new Counter();
            var frame = new Counter();
            loop.Register(TickPhase.ZombieSim, sim);
            loop.Register(TickPhase.Presentation, frame);

            for (int i = 0; i < 60; i++)
                loop.Advance(1f / 60f); // one second at 60 FPS

            Assert.AreEqual(60, frame.Calls);
            Assert.That(sim.Calls, Is.InRange(29, 30));
            Assert.AreEqual(1f / 30f, sim.LastDt, 1e-6f);
        }

        [Test]
        public void LongHitch_IsClamped()
        {
            var loop = new TickLoop(30);
            var sim = new Counter();
            loop.Register(TickPhase.Combat, sim);

            int steps = loop.Advance(2f);

            Assert.AreEqual(TickLoop.MaxStepsPerFrame, steps);
            Assert.AreEqual(0, loop.Advance(0f));
        }

        [Test]
        public void Pause_StopsSimButNotFramePhases()
        {
            var loop = new TickLoop(30);
            var sim = new Counter();
            var input = new Counter();
            loop.Register(TickPhase.Director, sim);
            loop.Register(TickPhase.Input, input);

            loop.SimPaused = true;
            loop.Advance(0.5f);

            Assert.AreEqual(0, sim.Calls);
            Assert.AreEqual(1, input.Calls);
        }

        [Test]
        public void Phases_RunInDeclaredOrder()
        {
            var loop = new TickLoop(30);
            var order = new System.Collections.Generic.List<TickPhase>();
            foreach (TickPhase phase in System.Enum.GetValues(typeof(TickPhase)))
                loop.Register(phase, new Recorder(order, phase));

            loop.Advance(1f / 30f + 1e-4f);

            for (int i = 1; i < order.Count; i++)
                Assert.Less((int)order[i - 1], (int)order[i]);
            Assert.AreEqual(TickPhaseInfo.Count, order.Count);
        }

        sealed class Recorder : ITickable
        {
            readonly System.Collections.Generic.List<TickPhase> _order;
            readonly TickPhase _phase;

            public Recorder(System.Collections.Generic.List<TickPhase> order, TickPhase phase)
            {
                _order = order;
                _phase = phase;
            }

            public void Tick(float dt, uint tick) => _order.Add(_phase);
        }
    }
}
