using LastGround.Audio;
using LastGround.Core.Tick;
using LastGround.Gameplay.Combat;
using LastGround.Rendering;
using LastGround.Rendering.Carnage;
using LastGround.Rendering.Combat;
using LastGround.Rendering.Crowd;
using LastGround.Rendering.Lighting;
using LastGround.Rendering.Loot;
using LastGround.UI.Run;
using UnityEngine;

namespace LastGround.App
{
    public sealed partial class RunInstaller
    {
        ZombieRenderSystem _crowdRenderer;

        /// <summary>
        /// Presentation phase, in order: remote shots (feeds the shot stream), camera, crowd, blood, tracers,
        /// damage numbers, players, sound. None of it knows whether an event was simulated here or replicated.
        /// </summary>
        void BuildPresentation(RunParts parts, TickLoop loop)
        {
            loop.Register(TickPhase.Presentation, new RemoteShotEmitter(parts.Players, parts.Crowd, parts.Nav, _weapon, parts.Shots));
            // The camera looks ahead with the raw sticks: auto-aim target switches must not swing it around.
            loop.Register(TickPhase.Presentation, new TopDownCameraRig(_camera, parts.Players, _input, _cameraProfile));

            float worldSize = parts.Nav.Width * parts.Nav.CellSize;
            _disposables.Add(new LightingGrid(new Vector2(parts.Nav.Origin.x, parts.Nav.Origin.y), worldSize, 256, LightingGrid.GreyboxLamps()));
            _crowdRenderer = new ZombieRenderSystem(parts.Crowd, _crowdCatalog, _camera, parts.Preset, parts.Deaths, parts.Hits);
            _disposables.Add(_crowdRenderer);
            loop.Register(TickPhase.Presentation, _crowdRenderer);

            var blood = new BloodSystem(parts.Deaths, parts.Preset, _worldRoot, _bloodParticleMaterial, _bloodSplatMaterial, parts.Hits);
            _disposables.Add(blood);
            loop.Register(TickPhase.Presentation, blood);

            var tracers = new TracerSystem(parts.Shots, _tracerMaterial, parts.Preset.TracerCap);
            _disposables.Add(tracers);
            loop.Register(TickPhase.Presentation, tracers);
            loop.Register(TickPhase.Presentation, new DamageNumbers(parts.Hits, _camera, _worldRoot, parts.Preset.DamageNumberCap));
            loop.Register(TickPhase.Presentation, new PlayerViews(parts.Players, _worldRoot, _playerMesh, _playerMaterial));
            loop.Register(TickPhase.Presentation, new PickupRenderSystem(parts.Pickups, parts.Players,
                Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"), _coinMaterial, Resources.GetBuiltinResource<Mesh>("Cube.fbx"), _medkitMaterial));

            var sfx = new SfxPlayer(transform, parts.Preset.AudioVoices, ProceduralSfx.CreateAll());
            _disposables.Add(sfx);
            loop.Register(TickPhase.Presentation, new CombatAudio(sfx, parts.Players, parts.Shots, parts.Hits, parts.Deaths));

            _hud.Bind(_service, parts.Crowd);
        }
    }
}
