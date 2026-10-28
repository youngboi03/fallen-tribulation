"""Summarise boss fights in probe logs: python -I tools/boss_stats.py Logs/probe_c1/*.log
Per run: name, win/death wave, boss kill times, top 3 moves by [BOSSHIT] count, max pct."""
import re, sys, glob
from collections import Counter

def stats(path):
    kills, hits, mx, mxmove, end = [], Counter(), 0.0, "", "no RUN END"
    for ln in open(path, encoding="utf-8", errors="replace"):
        m = re.search(r"\[BOSS\] kill (\w+) t=([\d.]+)", ln)
        if m: kills.append(f"{m[1]}:{m[2]}s"); continue
        m = re.search(r"\[BOSSHIT\] (\w+) pct=([\d.]+)", ln)
        if m:
            hits[m[1]] += 1
            if float(m[2]) > mx: mx, mxmove = float(m[2]), m[1]
            continue
        m = re.search(r"RUN END win=(\w+) wave=(\d+)", ln)
        if m: end = ("WIN" if m[1].lower() == "true" else "DEAD") + " w" + m[2]
    return end, kills, hits.most_common(3), mx, mxmove

if __name__ == "__main__":
    for arg in sys.argv[1:]:
        for p in sorted(glob.glob(arg)):
            end, kills, top, mx, mv = stats(p)
            print(f"{p.split('/')[-1].split(chr(92))[-1][:-4]:10} {end:10} kills[{' '.join(kills)}] top{top} max={mx:.2f}({mv})")
