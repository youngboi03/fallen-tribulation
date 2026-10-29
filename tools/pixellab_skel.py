"""Walk clip via PixelLab REST estimate-skeleton + animate-with-skeleton-v3 (token PIXELLAB_API_TOKEN_MAIN only).
Builds a front-facing walk cycle by moving the estimated joints (feet lift alternately, body bobs, arms counter-swing).
Usage: python -I tools/pixellab_skel.py IN.png OUT_DIR "what the character looks like" [--frames 8] [--seed N] [--dry-run]"""
import argparse, base64, copy, json, math, os, sys, time, urllib.error, urllib.request

API = 'https://api.pixellab.ai/v2'
ap = argparse.ArgumentParser()
ap.add_argument('src'); ap.add_argument('out_dir'); ap.add_argument('look')
ap.add_argument('--frames', type=int, default=8); ap.add_argument('--seed', type=int, default=0)
ap.add_argument('--dry-run', action='store_true')
a = ap.parse_args()
if not 3 <= a.frames <= 15: sys.exit('--frames must be 3..15')
token = os.environ.get('PIXELLAB_API_TOKEN_MAIN') or sys.exit('PIXELLAB_API_TOKEN_MAIN not set')

def call(path, data=None):
    req = urllib.request.Request(API + path, data=json.dumps(data).encode() if data else None, method='POST' if data else 'GET',
                                 headers={'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req, timeout=180) as r: return json.load(r)
    except urllib.error.HTTPError as e: sys.exit('HTTP %d %s' % (e.code, e.read().decode(errors='replace')[:300]))

img = {'type': 'base64', 'base64': base64.b64encode(open(a.src, 'rb').read()).decode()}
base = [{k: v for k, v in kp.items() if k in ('x', 'y', 'label', 'z_index', 'depth') and v is not None}
        for kp in call('/estimate-skeleton', {'image': img})['keypoints']]
for kp in base: kp['z_index'] = int(kp.get('z_index', 0))

def pose(t):
    p = 2 * math.pi * t / a.frames; ks = copy.deepcopy(base)
    bob = -0.012 * abs(math.sin(p))
    for k in ks:
        n = k['label']; k['y'] += bob
        side = 1 if n.startswith('LEFT') else -1 if n.startswith('RIGHT') else 0
        if not side: continue
        ph = math.sin(p) * side  # +1: this side's foot is planted forward, -1: lifted
        # forward-planted side: lower on screen and nearer the camera; the other foot trails, higher and farther
        if n.endswith(('KNEE', 'LEG')):
            k['y'] += (0.03 if n.endswith('KNEE') else 0.055) * ph - 0.015 * max(0, -ph); k['depth'] = 128 + 40 * ph
        elif n.endswith(('ELBOW', 'ARM')):  # arms swing opposite to the same-side leg
            k['y'] -= (0.03 if n.endswith('ELBOW') else 0.05) * ph; k['depth'] = 128 - 30 * ph
    return ks
motion = [pose(t) for t in range(a.frames)]
body = {'description': a.look, 'action': 'walk', 'direction': 'south', 'view': 'low top-down', 'first_frame': img,
        'first_frame_keypoints': base, 'keypoints': motion, 'seed': a.seed, 'no_background': True}
if a.dry_run: print(json.dumps(motion[2])[:600]); sys.exit(0)

job = call('/animate-with-skeleton-v3', body); jid = job['background_job_id']; print('job', jid, job.get('usage'), flush=True)
while True:
    time.sleep(20); res = call('/background-jobs/' + jid)
    if res['status'] in ('completed', 'failed'): break
    print('...', res['status'], flush=True)
os.makedirs(a.out_dir, exist_ok=True)
json.dump(res, open(os.path.join(a.out_dir, 'job_%s.json' % jid[:8]), 'w'), indent=1)
if res['status'] == 'failed': sys.exit('failed: %s' % json.dumps(res)[:500])
frames = []
def walk(o):
    if isinstance(o, dict):
        if isinstance(o.get('base64'), str): frames.append(o['base64'])
        else: [walk(v) for v in o.values()]
    elif isinstance(o, list): [walk(v) for v in o]
walk(res.get('last_response'))
for i, b in enumerate(frames): open(os.path.join(a.out_dir, 'f%02d.png' % i), 'wb').write(base64.b64decode(b.split(',')[-1]))
print('frames', len(frames), 'usage', res.get('usage'))
