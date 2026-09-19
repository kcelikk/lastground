using LastGround.Audio;
using LastGround.Core.Tick;
using LastGround.Data.Combat;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Loot;
using LastGround.Gameplay.Players;
using LastGround.Rendering;
using LastGround.Rendering.Carnage;
using LastGround.Rendering.Combat;
using LastGround.Rendering.Crowd;
using LastGround.Rendering.Interactables;
using LastGround.Gameplay.Objectives;
using LastGround.Rendering.Lighting;
using LastGround.Rendering.Loot;
using LastGround.Rendering.Objectives;
using LastGround.UI.Run;
using UnityEngine;

namespace LastGround.App
{
    public sealed partial class RunInstaller
    {
        ZombieRenderSystem _crowdRenderer;

        /// <summary>Scene naming shared with the editor map builder.</summary>
        const string MapRootName = "Map_Industrial";
        const string InteractablePrefix = "Interactable_";

        /// <summary>
        /// Presentation phase, in order: remote shots (feeds the shot stream), camera, crowd, blood, tracers,
        /// damage numbers, players, sound. None of it knows whether an event was simulated here or replicated.
        /// </summary>
        void BuildPresentation(RunParts parts, TickLoop loop)
        {
            loop.Register(TickPhase.Presentation, new RemoteShotEmitter(parts.Players, parts.Crowd, parts.Nav, parts.Loadouts, parts.Shots));
            if (!parts.Session.IsAuthority)
                loop.Register(TickPhase.Presentation, new RemoteTurretEmitter(parts.Objective, parts.Crowd, parts.Nav, parts.Shots));
            GameObject mapRoot = GameObject.Find(MapRootName);
            loop.Register(TickPhase.Presentation, new InteractableViews(parts.Interactables, mapRoot != null ? mapRoot.transform : null, InteractablePrefix));
            // The camera looks ahead with the raw sticks: auto-aim target switches must not swing it around.
            var cameraRig = new TopDownCameraRig(_camera, parts.Players, _input, _cameraProfile)
            {
                Weapon = parts.Weapon, Spectator = _spectator,
            };
            loop.Register(TickPhase.Presentation, cameraRig);
            BuildBossPresentation(parts, loop, cameraRig);

            float worldSize = parts.Nav.Width * parts.Nav.CellSize;
            LightingGrid.Lamp[] lamps = _map != null ? LightingGrid.FromMap(_map) : LightingGrid.GreyboxLamps();
            _disposables.Add(new LightingGrid(new Vector2(parts.Nav.Origin.x, parts.Nav.Origin.y), worldSize, 256, lamps));
            _crowdRenderer = new ZombieRenderSystem(parts.Crowd, _crowdCatalog, _camera, parts.Preset, parts.Deaths, parts.Hits)
            {
                EliteGlows = EliteGlows(),
            };
            _disposables.Add(_crowdRenderer);
            loop.Register(TickPhase.Presentation, _crowdRenderer);

            var blood = new BloodSystem(parts.Deaths, parts.Preset, _worldRoot, _bloodParticleMaterial, _bloodSplatMaterial, parts.Hits);
            _disposables.Add(blood);
            loop.Register(TickPhase.Presentation, blood);

            var zoneMarker = new ZoneMarker(parts.Objective, Zones, _tracerMaterial);
            _disposables.Add(zoneMarker);
            loop.Register(TickPhase.Presentation, zoneMarker);
            var tracers = new TracerSystem(parts.Shots, _tracerMaterial, parts.Preset.TracerCap);
            _disposables.Add(tracers);
            loop.Register(TickPhase.Presentation, tracers);
            loop.Register(TickPhase.Presentation, new DamageNumbers(parts.Hits, _camera, _worldRoot, parts.Preset.DamageNumberCap));
            // Players: baked Mixamo bodies (M9); the capsule views stay for slots without a body (benchmark, no catalog).
            if (!BuildPlayerBodies(parts, loop))
                loop.Register(TickPhase.Presentation, new PlayerViews(parts.Players, _worldRoot, _playerMesh, _playerMaterial));
            loop.Register(TickPhase.Presentation, new PickupRenderSystem(parts.Pickups, parts.Players, PickupLooks()));
            Mesh sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var projectiles = new ProjectileRenderSystem(parts.Projectiles, _camera, sphere, _grenadeMaterial, _tracerMaterial);
            _disposables.Add(projectiles);
            loop.Register(TickPhase.Presentation, projectiles);
            PlayerStateTable players = parts.Players;
            var blasts = new ExplosionEffects(parts.Blasts, parts.Projectiles, cameraRig, () => LocalPosition(players), parts.Preset,
                _worldRoot, _tracerMaterial, _bloodParticleMaterial);
            _disposables.Add(blasts);
            loop.Register(TickPhase.Presentation, blasts);
            float blastRadius = _combat.Grenade != null ? _combat.Grenade.Explosion.Radius : 4f;
            var aimMarker = new GrenadeAimMarker(parts.Players, () => _input.GrenadeAiming ? _input.GrenadeAimOffset : (Vector2?)null,
                blastRadius, _tracerMaterial);
            _disposables.Add(aimMarker);
            loop.Register(TickPhase.Presentation, aimMarker);

            var sfx = new SfxPlayer(transform, parts.Preset.AudioVoices, ProceduralSfx.CreateAll());
            _disposables.Add(sfx);
            loop.Register(TickPhase.Presentation, new CombatAudio(sfx, parts.Players, parts.Shots, parts.Hits, parts.Deaths)
            {
                Weapons = _combat.Weapons, Crowd = parts.Crowd, LocalWeapon = parts.Weapon, Blasts = parts.Blasts, Projectiles = parts.Projectiles,
            });
            loop.Register(TickPhase.Presentation, new BossAudio(sfx, parts.Players, parts.Boss, _boss, parts.Crowd, parts.Extraction));

            _hud.Bind(_service, parts.Crowd);
        }

        PickupLook[] PickupLooks()
        {
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var looks = new PickupLook[5];
            looks[(int)PickupType.Coin] = new PickupLook
            {
                Mesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"), Material = _coinMaterial, Size = new Vector3(0.4f, 0.06f, 0.4f), Flat = true,
            };
            looks[(int)PickupType.Medkit] = new PickupLook { Mesh = cube, Material = _medkitMaterial, Size = new Vector3(0.45f, 0.3f, 0.45f) };
            looks[(int)PickupType.Ammo] = new PickupLook { Mesh = cube, Material = _ammoMaterial, Size = new Vector3(0.5f, 0.28f, 0.32f) };
            looks[(int)PickupType.Grenade] = new PickupLook
            {
                Mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx"), Material = _grenadeMaterial, Size = new Vector3(0.3f, 0.36f, 0.3f),
            };
            looks[(int)PickupType.Weapon] = new PickupLook { Mesh = cube, Material = _weaponPickupMaterial, Size = new Vector3(0.18f, 0.18f, 1f) };
            return looks;
        }

        Color[] EliteGlows()
        {
            var glows = new Color[_combat.Elites.Length + 1];
            foreach (EliteModifierDefinition elite in _combat.Elites) glows[elite.NetIndex] = elite.Glow;
            return glows;
        }

        static Vector2 LocalPosition(PlayerStateTable players)
        {
            if (!players.Local.IsValid) return Vector2.zero;
            int me = players.Local.Value;
            return new Vector2(players.X[me], players.Z[me]);
        }
    }
}
