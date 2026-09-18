namespace LastGround.Core.Tick
{
    /// <summary>
    /// Ordered update phases driven by <see cref="TickScheduler"/> (TDD_02 §22.1).
    /// Phases marked "sim" run on the fixed 30 Hz simulation step; the rest run once per rendered frame.
    /// </summary>
    public enum TickPhase
    {
        /// <summary>Per frame: read touch/gamepad into input frames.</summary>
        Input = 0,
        /// <summary>Per frame: local player motion (client-side, immediate).</summary>
        LocalPlayer = 1,
        /// <summary>Per frame: drain incoming network messages.</summary>
        NetReceive = 2,
        /// <summary>Sim (host): horde director.</summary>
        Director = 3,
        /// <summary>Sim (host): zombie simulation jobs.</summary>
        ZombieSim = 4,
        /// <summary>Sim: combat resolution.</summary>
        Combat = 5,
        /// <summary>Sim: loot, pickups, map events.</summary>
        LootEvents = 6,
        /// <summary>Sim: build and flush outgoing network messages.</summary>
        NetSend = 7,
        /// <summary>Per frame: camera, render prep, UI, audio.</summary>
        Presentation = 8,
    }
}
