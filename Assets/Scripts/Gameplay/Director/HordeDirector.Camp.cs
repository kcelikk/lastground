using LastGround.Gameplay.Players;
using Unity.Mathematics;

namespace LastGround.Gameplay.Director
{
    /// <summary>
    /// Anti-camping (TDD_01 §9.8): when the team's centre stays within a small circle for too long, the spawn rate
    /// rises and Spitters (ranged pressure) become more likely until the team moves. Map events do the rest by pulling
    /// the team elsewhere.
    /// </summary>
    public sealed partial class HordeDirector
    {
        float2 _campCentre;
        float _campTime;

        /// <summary>True while the team has been holding one spot for longer than the profile allows.</summary>
        public bool Camping => _campTime >= _profile.CampSeconds;

        /// <summary>Samples the team's centre (called with the intensity sample, 2 Hz).</summary>
        void SampleCamp(float dt)
        {
            float2 sum = float2.zero;
            int count = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_players.CanAct(p)) continue;
                sum += new float2(_players.X[p], _players.Z[p]);
                count++;
            }
            if (count == 0) return;
            float2 centre = sum / count;
            if (math.distance(centre, _campCentre) > _profile.CampRadius)
            {
                _campCentre = centre;
                _campTime = 0f;
            }
            else
            {
                _campTime += dt;
            }
            if (_deck != null) _deck.SpitterBoost = Camping ? _profile.CampSpitterWeight : 1f;
        }

        float CampRate() => Camping ? _profile.CampRateMultiplier : 1f;
    }
}
