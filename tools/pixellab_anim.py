"""Animate one sprite via PixelLab REST POST /v2/animate-pixminimax (token PIXELLAB_API_TOKEN_MAIN only).
Saves the job JSON and each returned frame (frame 0 = the input) to OUT_DIR; frames still need tools/anim_import.py.
Usage: python -I tools/pixellab_anim.py IN.png OUT_DIR "motion" [--frames 8] [--seed N] [--dry-run]
       --subject "..." --pose "..." --view side|... --direction east|... override the AI-read caption fields."""
import argparse, base64, json, os, sys, time, urllib.error, urllib.request

API = 'https://api.pixellab.ai/v2'
ap = argparse.ArgumentParser()
ap.add_argument('src'); ap.add_argument('out_dir'); ap.add_argument('motion')
ap.add_argument('--frames', type=int, default=8); ap.add_argument('--seed', type=int, default=0)
ap.add_argument('--subject'); ap.add_argument('--pose'); ap.add_argument('--view'); ap.add_argument('--direction')
ap.add_argument('--dry-run', action='store_true')
a = ap.parse_args()
if a.frames % 4 or not 4 <= a.frames <= 40: sys.exit('--frames must be a multiple of 4 in 4..40')

body = {'first_frame': {'type': 'base64', 'base64': base64.b64encode(open(a.src, 'rb').read()).decode()},
        'description': a.motion, 'frame_count': a.frames, 'seed': a.seed, 'no_background': True}
for k, v in (('subject_description', a.subject), ('initial_pose', a.pose), ('view', a.view), ('direction', a.direction)):
    if v: body[k] = v
if a.dry_run:
    print(json.dumps({k: (v if k != 'first_frame' else '<png %d b64 chars>' % len(v['base64'])) for k, v in body.items()}, indent=1))
    sys.exit(0)

token = os.environ.get('PIXELLAB_API_TOKEN_MAIN') or sys.exit('PIXELLAB_API_TOKEN_MAIN not set')
def call(path, data=None):
    req = urllib.request.Request(API + path, data=json.dumps(data).encode() if data else None, method='POST' if data else 'GET',
                                 headers={'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req, timeout=180) as r: return json.load(r)
    except urllib.error.HTTPError as e: sys.exit('HTTP %d %s' % (e.code, e.read().decode(errors='replace')[:300]))

job = call('/animate-pixminimax', body)
jid = job['background_job_id']; print('job', jid, 'usage', job.get('usage'), flush=True)
while True:
    time.sleep(15); res = call('/background-jobs/' + jid)
    if res['status'] in ('completed', 'failed'): break
    print('...', res['status'], flush=True)
os.makedirs(a.out_dir, exist_ok=True)
json.dump(res, open(os.path.join(a.out_dir, 'job_%s.json' % jid[:8]), 'w'), indent=1)
if res['status'] == 'failed': sys.exit('failed: %s' % json.dumps(res)[:500])

frames = []
def walk(o):  # frames come back as {"base64": ...} objects somewhere in last_response
    if isinstance(o, dict):
        if isinstance(o.get('base64'), str): frames.append(o['base64'])
        else: [walk(v) for v in o.values()]
    elif isinstance(o, list): [walk(v) for v in o]
walk(res.get('last_response'))
for i, b in enumerate(frames): open(os.path.join(a.out_dir, 'f%02d.png' % i), 'wb').write(base64.b64decode(b))
print('frames', len(frames), 'usage', res.get('usage'))
