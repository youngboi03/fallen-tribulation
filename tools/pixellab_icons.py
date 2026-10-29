"""Generate one PixelLab icon per weapon/item from tools/icons.json via the REST API (sequential, resumable).
Tokens come from the environment: the free trial tokens first (PIXELLAB_API_TOKEN, _2.._5), then PIXELLAB_API_TOKEN_MAIN once they are refused (402).
Output: Assets/Resources/PixelLab/Icons/w_<id>.png / i_<id>.png, provenance appended to Assets/ThirdParty/PixelLab-Assets.md.
Usage: python -I tools/pixellab_icons.py [size]"""
import base64, json, os, subprocess, sys, time, urllib.error, urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'PixelLab', 'Icons')
LOG = os.path.join(ROOT, 'Assets', 'ThirdParty', 'PixelLab-Assets.md')
SIZE = int(sys.argv[1]) if len(sys.argv) > 1 else 40
tokens = [t for t in (os.environ.get(n) for n in ('PIXELLAB_API_TOKEN', 'PIXELLAB_API_TOKEN_2', 'PIXELLAB_API_TOKEN_3', 'PIXELLAB_API_TOKEN_4', 'PIXELLAB_API_TOKEN_5', 'PIXELLAB_API_TOKEN_MAIN')) if t]
cfg = json.load(open(os.path.join(ROOT, 'tools', 'icons.json'), encoding='utf-8'))
jobs = [('w_' + k, v + cfg['style_weapon']) for k, v in cfg['weapons'].items()] + \
       [('i_' + k, v + cfg['style_item']) for k, v in cfg['items'].items()]
os.makedirs(OUT, exist_ok=True)

def gen(prompt, token):
    body = json.dumps({'description': prompt, 'image_size': {'width': SIZE, 'height': SIZE}, 'no_background': True,
                       'outline': 'single color black outline', 'shading': 'medium shading', 'detail': 'medium detail'}).encode()
    req = urllib.request.Request('https://api.pixellab.ai/v1/generate-image-pixflux', data=body, method='POST',
                                 headers={'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'})
    with urllib.request.urlopen(req, timeout=180) as r:
        return base64.b64decode(json.load(r)['image']['base64'])

ti = 0
for key, prompt in jobs:
    path = os.path.join(OUT, key + '.png')
    if os.path.exists(path): continue
    for attempt in range(6):
        try:
            png = gen(prompt, tokens[ti])
            open(path, 'wb').write(png)
            with open(LOG, 'a', encoding='utf-8') as f:
                f.write(f'| `Assets/Resources/PixelLab/Icons/{key}.png` | REST generate-image-pixflux {SIZE}x{SIZE} (account {ti + 1}) | - | {prompt} |\n')
            print('ok', key, flush=True)
            break
        except urllib.error.HTTPError as e:
            msg = e.read().decode(errors='replace')[:200]
            if e.code in (402, 403) or 'credit' in msg.lower() or 'generation' in msg.lower() and e.code != 429:
                if ti + 1 < len(tokens):
                    ti += 1; print('switching to second account:', e.code, msg, flush=True); continue
                print('out of generations on all accounts:', e.code, msg, flush=True); sys.exit(1)
            print('retry', key, e.code, msg, flush=True); time.sleep(10 * (attempt + 1))
        except Exception as e:
            print('retry', key, repr(e)[:200], flush=True); time.sleep(10 * (attempt + 1))
print('done', sum(os.path.exists(os.path.join(OUT, k + '.png')) for k, _ in jobs), '/', len(jobs))
subprocess.run(['bash', 'tools/asset_backfill.sh', 'Icons'], cwd=ROOT)  # same pixel-clean + intake guard as pixellab_fetch.sh; no-op when nothing is new
