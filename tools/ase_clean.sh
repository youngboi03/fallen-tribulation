#!/usr/bin/env bash
# Pixel-clean PixelLab PNGs with headless Aseprite (tools/ase/clean.lua): one stats line per file, dry run by default.
# Usage: tools/ase_clean.sh [--apply] [--dist N] [--rare F] [--preview DIR] FILE.png...
#        tools/ase_clean.sh --selftest
set -eu
ase="${ASEPRITE:-/d/Programs/Aseprite/aseprite.exe}"
lua="$(dirname "$0")/ase/clean.lua"
apply=0 dist=8 rare=0.015 preview=""
while [ $# -gt 0 ]; do
  case "$1" in
    --selftest) exec "$ase" -b --script-param selftest=1 --script "$lua" ;;
    --apply) apply=1 ;;
    --dist) dist="$2"; shift ;;
    --rare) rare="$2"; shift ;;
    --preview) preview="$2"; mkdir -p "$preview"; shift ;;
    *) break ;;
  esac
  shift
done
for f in "$@"; do
  args=(--script-param "src=$f" --script-param "dist=$dist" --script-param "rare=$rare" --script-param "apply=$apply")
  [ -n "$preview" ] && args+=(--script-param "preview=$preview/$(basename "$(dirname "$f")")_$(basename "$f")")
  "$ase" -b "${args[@]}" --script "$lua"
done
