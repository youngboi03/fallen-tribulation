"""Thần Thông damage share per wave band from autotest logs.

Usage: python -I tools/skill_share.py Logs/probe/*.log
Prints, per log: mean skill share (skill damage / all damage) for waves 1-5, 9-12 and 18-20,
the run result (win, or the wave it died on) and the exception count.
Targets (spec 2026-10-27-than-thong-design.md section 1):
  -smart: 20-35% / 8-15% / under 8%     -smart -skillbuild: at least 35% from wave 10.
"""
import re
import sys

WAVE = re.compile(r"\[AUTOTEST\] wave (\d+) done: .*? skill ([\d.]+)%")
END = re.compile(r"RUN END win=(\w+) wave=(\d+)")
BANDS = [(1, 5), (9, 12), (18, 20)]


def band(shares, lo, hi):
    vals = [v for w, v in shares.items() if lo <= w <= hi]
    return f"{sum(vals) / len(vals):5.1f}%" if vals else "    -"


def main(paths):
    print(f"{'log':<16} {'w1-5':>7} {'w9-12':>7} {'w18-20':>7} {'w10+':>7}  result      errors")
    for p in paths:
        shares, result, errors = {}, "?", 0
        with open(p, encoding="utf-8", errors="replace") as f:
            for line in f:
                m = WAVE.search(line)
                if m:
                    shares[int(m.group(1))] = float(m.group(2))
                m = END.search(line)
                if m:
                    result = "win" if m.group(1) == "True" else f"died w{m.group(2)}"
                if "Exception" in line:
                    errors += 1
        late = [v for w, v in shares.items() if w >= 10]
        late_s = f"{sum(late) / len(late):5.1f}%" if late else "    -"
        name = re.sub(r".*[\/]", "", p).removesuffix(".log")
        print(f"{name:<16} {band(shares, *BANDS[0]):>7} {band(shares, *BANDS[1]):>7} {band(shares, *BANDS[2]):>7} {late_s:>7}  {result:<11} {errors}")


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    main(sys.argv[1:])
