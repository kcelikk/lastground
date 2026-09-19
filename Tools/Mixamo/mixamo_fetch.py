#!/usr/bin/env python3
"""
Re-downloads the Mixamo sources of the crowd bodies (not stored in git: licence and size).

Token: log in at https://www.mixamo.com, open the browser developer tools, Application → Local Storage →
www.mixamo.com → copy the value of `access_token`, then run
    MIXAMO_TOKEN=<value> python3 Tools/Mixamo/mixamo_fetch.py
The token is only read from the environment; it is never printed or written to disk.
Files land in Assets/ThirdParty/Mixamo/{Characters,Animations}; existing files are skipped.
"""
import json
import os
import sys
import time
import urllib.parse
import urllib.request

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'ThirdParty', 'Mixamo')

# Character exports: FBX for Unity, T-pose, skin and embedded textures.
CHARACTERS = {
    'Yaku_J_Ignite': '456c351c-71fb-420b-9e85-e1aca7273d2d',
    'Zombiegirl_W_Kurniawan': '2f8e576f-f69d-453e-830e-969a2f0217ea',
    'Copzombie_L_Actisdato': '3d9daeb8-c2d5-45ce-b835-7cd403c72fc7',
    'Warzombie_F_Pedroso': '3576fd60-beef-49ec-a3d0-f93231f4fc29',
    'Romero': '576b18a3-2e3e-4f50-b665-cbca337e0757',
    'Mutant': 'cccc84b6-d072-4972-99da-75c5702e25f6',
    'Parasite_L_Starkie': '13c20d1a-c2b7-4725-99da-fe3bbeac2805',
    'Survivor_A_Lusth': '52dcdacb-b43e-4efc-ab6d-9d2d6e09bc95',
    # Player characters (M9, D-022).
    'Swat_Guy': 'cf73b862-b7ca-40e9-a156-1b95393d232e',
    'Erika_Archer': 'd0496a75-08b9-4f4e-9f1d-f65820323cc2',
}

# Motion exports: FBX for Unity, without skin, 30 fps, in place. (file name, motion id, exported on character)
MOTIONS = [
    ('Zombie_Idle', 'c9cbd649-b96c-11e4-a802-0aaa78deedf9', 'Yaku_J_Ignite'),
    ('Zombie_Walk', 'c9cbd4d9-b96c-11e4-a802-0aaa78deedf9', 'Yaku_J_Ignite'),
    ('Zombie_Walk_Creeping', 'c9c61776-b96c-11e4-a802-0aaa78deedf9', 'Yaku_J_Ignite'),
    ('Zombie_Run', 'c9c6b0ab-b96c-11e4-a802-0aaa78deedf9', 'Yaku_J_Ignite'),
    ('Zombie_Attack_Swipe', 'c9cbd7ad-b96c-11e4-a802-0aaa78deedf9', 'Yaku_J_Ignite'),
    ('Zombie_Attack_RightHand', 'c9c68115-b96c-11e4-a802-0aaa78deedf9', 'Yaku_J_Ignite'),
    ('Zombie_Hit', 'c9c68a0f-b96c-11e4-a802-0aaa78deedf9', 'Yaku_J_Ignite'),
    ('Zombie_Crawl', 'c9cbee0c-b96c-11e4-a802-0aaa78deedf9', 'Yaku_J_Ignite'),
    ('Zombie_Death_Back', 'c9cbda5a-b96c-11e4-a802-0aaa78deedf9', 'Yaku_J_Ignite'),
    ('Zombie_Death_Forward', 'c9cc240a-b96c-11e4-a802-0aaa78deedf9', 'Yaku_J_Ignite'),
    ('Zombie_Scream', 'c9ccbc37-b96c-11e4-a802-0aaa78deedf9', 'Yaku_J_Ignite'),
    ('Mutant_Idle', 'c9c93f13-b96c-11e4-a802-0aaa78deedf9', 'Mutant'),
    ('Mutant_Walk', 'c9c93a8c-b96c-11e4-a802-0aaa78deedf9', 'Mutant'),
    ('Mutant_Run', 'c9c93cdc-b96c-11e4-a802-0aaa78deedf9', 'Mutant'),
    ('Mutant_Punch', 'c9ccb404-b96c-11e4-a802-0aaa78deedf9', 'Mutant'),
    ('Mutant_Swipe', 'c9c93c1d-b96c-11e4-a802-0aaa78deedf9', 'Mutant'),
    ('Mutant_Death', 'c9c93d98-b96c-11e4-a802-0aaa78deedf9', 'Mutant'),
    ('Mutant_Roar', 'c9ccb4c2-b96c-11e4-a802-0aaa78deedf9', 'Mutant'),
    # Player motions (M9): aimed rifle set, hit, death, emotes.
    ('Rifle_Aiming_Idle', 'c9c84492-b96c-11e4-a802-0aaa78deedf9', 'Swat_Guy'),
    ('Rifle_Run', 'c9c814a0-b96c-11e4-a802-0aaa78deedf9', 'Swat_Guy'),
    ('Rifle_Run_Backwards', 'c9c86c17-b96c-11e4-a802-0aaa78deedf9', 'Swat_Guy'),
    ('Rifle_Firing', 'c9c94dfa-b96c-11e4-a802-0aaa78deedf9', 'Swat_Guy'),
    ('Rifle_Hit', 'c9c6bd18-b96c-11e4-a802-0aaa78deedf9', 'Swat_Guy'),
    ('Rifle_Death', 'c9c871f6-b96c-11e4-a802-0aaa78deedf9', 'Swat_Guy'),
    ('Emote_Waving', 'c9c5ed32-b96c-11e4-a802-0aaa78deedf9', 'Swat_Guy'),
    ('Emote_Salute', 'c9cb0ab6-b96c-11e4-a802-0aaa78deedf9', 'Swat_Guy'),
    ('Emote_Cheering', 'c9c66dfd-b96c-11e4-a802-0aaa78deedf9', 'Swat_Guy'),
]

TOKEN = os.environ.get('MIXAMO_TOKEN', '')


def call(method, path, body=None):
    request = urllib.request.Request(
        'https://www.mixamo.com/api/v1/' + path, method=method,
        data=json.dumps(body).encode() if body is not None else None,
        headers={'Authorization': 'Bearer ' + TOKEN, 'X-Api-Key': 'mixamo2', 'X-Requested-With': 'XMLHttpRequest',
                 'Content-Type': 'application/json', 'Accept': 'application/json'})
    with urllib.request.urlopen(request, timeout=60) as response:
        return json.loads(response.read() or b'{}')


def export(character_id, body, destination):
    call('POST', 'animations/export', body)
    for _ in range(100):
        time.sleep(3)
        monitor = call('GET', 'characters/%s/monitor' % character_id)
        if monitor.get('status') == 'completed':
            with urllib.request.urlopen(monitor['job_result'], timeout=300) as response, open(destination, 'wb') as f:
                f.write(response.read())
            return
        if monitor.get('status') == 'failed':
            raise RuntimeError('export failed: %s' % monitor.get('message'))
    raise TimeoutError(destination)


def fetch_character(name, character_id):
    body = {'character_id': character_id, 'gms_hash': None, 'product_name': name, 'type': 'Character',
            'preferences': {'format': 'fbx7_unity', 'mesh': 't-pose'}}
    export(character_id, body, os.path.join(ROOT, 'Characters', name + '.fbx'))


def fetch_motion(name, motion_id, character_id):
    info = call('GET', 'products/%s?similar=0&character_id=%s' % (motion_id, character_id))
    gms = dict(info['details']['gms_hash'])
    params = gms.get('params', [])
    gms['params'] = ','.join(str(v[1]) for v in params) if isinstance(params, list) else params
    gms['inplace'] = True
    gms.setdefault('trim', [0, 100])
    body = {'character_id': character_id, 'gms_hash': [gms], 'product_name': name, 'type': 'Motion',
            'preferences': {'format': 'fbx7_unity', 'skin': 'false', 'fps': '30', 'reducekf': '0'}}
    export(character_id, body, os.path.join(ROOT, 'Animations', name + '.fbx'))


def main():
    if not TOKEN:
        sys.exit('Set MIXAMO_TOKEN (see the module docstring).')
    for name, character_id in CHARACTERS.items():
        if not os.path.exists(os.path.join(ROOT, 'Characters', name + '.fbx')):
            fetch_character(name, character_id)
            print('character', name, flush=True)
    for name, motion_id, on in MOTIONS:
        if not os.path.exists(os.path.join(ROOT, 'Animations', name + '.fbx')):
            fetch_motion(name, motion_id, CHARACTERS[on])
            print('motion', name, flush=True)


if __name__ == '__main__':
    main()
