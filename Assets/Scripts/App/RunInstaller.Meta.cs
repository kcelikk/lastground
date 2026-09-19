using System.Collections.Generic;
using LastGround.Core.Net.Session;
using LastGround.Core.Services;
using LastGround.Core.Tick;
using LastGround.Data.Meta;
using LastGround.Data.Weapons;
using LastGround.Gameplay.Meta;

namespace LastGround.App
{
    /// <summary>
    /// M9 wiring: every player's lobby meta selection becomes run state (perk on every device; starting loadout and
    /// the owned-weapon drop pool on the host), and the finished run is banked into this player's profile once.
    /// </summary>
    public sealed partial class RunInstaller
    {
        [UnityEngine.SerializeField] UI.Run.EmoteBar _emoteBar;
        [UnityEngine.SerializeField] UI.Run.EmoteBubbles _emoteBubbles;

        readonly List<PlayerMeta> _sessionMetas = new List<PlayerMeta>(4);
        bool _banked;

        void ApplyMeta(ref RunParts parts)
        {
            MetaService meta = AppServices.TryGet(out MetaService service) ? service : null;
            MetaCatalog catalog = meta != null ? meta.Catalog : null;
            if (catalog == null) return;
            _sessionMetas.Clear();
            IReadOnlyList<LobbyPlayer> roster = parts.Session.Players;
            for (int i = 0; i < roster.Count; i++)
            {
                int player = roster[i].Id.Value;
                PlayerMeta selection = PlayerMeta.Unpack(roster[i].Meta);
                _sessionMetas.Add(selection);
                MetaApplier.ApplyPerk(selection, parts.Builds.Of(player), catalog);
                if (parts.LoadoutAuthority == null) continue;
                MetaApplier.StartLoadout(selection, catalog, _combat, parts.Builds.Of(player), out WeaponDefinition primary, out int grenades);
                parts.LoadoutAuthority.SetStart(player, primary.NetIndex, (byte)grenades);
            }
            if (parts.Registry != null) parts.Registry.Weapons = MetaApplier.DropPool(_sessionMetas, catalog, parts.Registry.Weapons);
        }

        /// <summary>Emote sync on every device, the HUD emote bar with this player's equipped emotes, bubbles over players.</summary>
        void BuildEmotes(in RunParts parts, TickLoop loop)
        {
            if (!AppServices.TryGet(out MetaService meta) || meta.Catalog == null) return;
            var sync = new Networking.Replication.EmoteSync(parts.Session, parts.Players, meta.Catalog.Emotes.Length);
            _disposables.Add(sync);
            loop.Register(TickPhase.NetSend, sync);
            var equipped = new List<EmoteDefinition>();
            meta.Profile.EquippedEmotes(equipped);
            var entries = new List<(byte, string)>();
            foreach (EmoteDefinition emote in equipped)
                entries.Add(((byte)System.Array.IndexOf(meta.Catalog.Emotes, emote), emote.NameKey));
            if (_emoteBar != null) _emoteBar.Bind(entries, sync.Play);
            if (_emoteBubbles != null) _emoteBubbles.Bind(parts.Players, meta.Catalog, _camera);
        }

        /// <summary>Banks the run into this player's profile when the outcome arrives (host or replicated).</summary>
        void BankWhenEnded(TickLoop loop, int localPlayer)
        {
            loop.Register(TickPhase.Presentation, new TickAction(_ =>
            {
                if (_banked || !_outcome.Ended) return;
                _banked = true;
                if (AppServices.TryGet(out MetaService meta)) meta.Bank(_outcome.Result, localPlayer);
            }));
        }
    }
}
