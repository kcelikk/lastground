#!/usr/bin/env python3
"""
Downloads the CC0 environment sources (D-020): Poly Haven props (FBX + 1K PNG textures) into
Assets/ThirdParty/PolyHaven/<id>/ and ambientCG materials (1K JPG) into Assets/ThirdParty/AmbientCG/<id>/.
Both are CC0 (no attribution required, redistribution allowed), so the files are committed; this script
documents where they came from and restores them. Existing files are skipped.
    python3 Tools/Environment/fetch_cc0.py
"""
import io
import json
import os
import urllib.request
import zipfile

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'ThirdParty')

# Props chosen against docs/reference/maps (industrial district, foundry yard, gas station, hospital yard).
POLY_HAVEN = [
    'Barrel_01', 'Barrel_02', 'barrel_03', 'concrete_road_barrier_02', 'covered_car', 'modular_chainlink_fence',
    'street_lamp_01', 'street_lamp_02', 'utility_box_01', 'utility_box_02', 'old_tyre', 'metal_trash_can',
    'wooden_crate_01', 'old_military_crate', 'portable_generator', 'propane_tank', 'water_manhole_cover',
    'rollershutter_door', 'security_light', 'exterior_aircon_unit', 'trashbag', 'plastic_crate_03',
    'metal_jerrycan_green', 'fire_hydrant', 'overhead_crane', 'medical_box', 'ammo_box', 'hand_truck',
]

# Ground, walls and roofs.
AMBIENT_CG = [
    'Asphalt033', 'Road007', 'Concrete034', 'Concrete047A', 'Bricks075A', 'Bricks097', 'CorrugatedSteel007A',
    'Metal041B', 'PaintedPlaster017', 'Tiles107', 'Ground103', 'Gravel043',
    # Rusty roofs, weathered container steel, red brick, dark concrete, door metal (closer to the reference mood).
    'CorrugatedSteel007C', 'CorrugatedSteel007B', 'Bricks104', 'Concrete042A', 'Metal053B',
]


def get(url):
    request = urllib.request.Request(url, headers={'User-Agent': 'LastGround-asset-fetch/1.0'})
    with urllib.request.urlopen(request, timeout=120) as response:
        return response.read()


def fetch_poly_haven(asset):
    folder = os.path.join(ROOT, 'PolyHaven', asset)
    files = json.loads(get('https://api.polyhaven.com/files/' + asset))['fbx']['1k']['fbx']
    targets = {os.path.join(folder, asset + '_1k.fbx'): files['url']}
    for relative, info in files.get('include', {}).items():
        targets[os.path.join(folder, relative)] = info['url']
    for path, url in targets.items():
        if os.path.exists(path):
            continue
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, 'wb') as f:
            f.write(get(url))
    print('polyhaven', asset, flush=True)


def fetch_ambient_cg(asset):
    folder = os.path.join(ROOT, 'AmbientCG', asset)
    if os.path.isdir(folder) and os.listdir(folder):
        return
    os.makedirs(folder, exist_ok=True)
    data = get('https://ambientcg.com/get?file=%s_1K-JPG.zip' % asset)
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        for name in archive.namelist():
            # Colour, normal (GL), roughness and AO are enough for URP Lit.
            if any(k in name for k in ('_Color.', '_NormalGL.', '_Roughness.', '_AmbientOcclusion.')):
                archive.extract(name, folder)
    print('ambientcg', asset, flush=True)


def main():
    for asset in POLY_HAVEN:
        fetch_poly_haven(asset)
    for asset in AMBIENT_CG:
        fetch_ambient_cg(asset)


if __name__ == '__main__':
    main()
