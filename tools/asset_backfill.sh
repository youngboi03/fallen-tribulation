#!/usr/bin/env bash
# Backfill: run the intake pipeline (raw copy -> ase_clean --apply -> asset_qa intake) over assets that are already in the project.
# Usage: tools/asset_backfill.sh [folder...]   (default: Chars Enemies Bosses Icons Portraits Backgrounds; Floors are never touched)
# ASSET_REVERT_AT=0.07 raises the "undo the clean if it changed more than 1% of the pixels" guard. Review with `git diff --stat`.
set -eu
cd "$(dirname "$0")/.."
mkdir -p tools/.intake/raw
for dir in "${@:-Chars Enemies Bosses Icons Portraits Backgrounds}"; do
  for d in $dir; do
    for f in Assets/Resources/PixelLab/$d/*.png; do
      raw="tools/.intake/raw/${d}_$(basename "$f")"
      cp "$f" "$raw"
      bash tools/ase_clean.sh --apply "$f" > /dev/null
      python -I tools/asset_qa.py intake "$f" "$raw" | grep -E "^(WARN|FAIL)" || true
    done
  done
done
git status --short Assets/Resources/PixelLab | sed 's/^/changed: /'
