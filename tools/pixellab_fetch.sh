#!/usr/bin/env bash
# Download a finished PixelLab image job into the project and log its provenance.
# Usage: tools/pixellab_fetch.sh <job_id> <Assets/Resources/PixelLab/...png> "<tool>" "<prompt>"
set -eu
cd "$(dirname "$0")/.."
job="$1"; out="$2"; tool="$3"; prompt="$4"
mkdir -p "$(dirname "$out")"
curl -sfL "https://api.pixellab.ai/mcp/images/$job/download" -o "$out"
log="Assets/ThirdParty/PixelLab-Assets.md"
if [ ! -f "$log" ]; then
  printf '# PixelLab assets\n\nGenerated with PixelLab (https://www.pixellab.ai).\n\n| File | Tool | Job | Prompt |\n|---|---|---|---|\n' > "$log"
fi
printf '| `%s` | %s | %s | %s |\n' "$out" "$tool" "$job" "$prompt" >> "$log"
# intake: keep the raw download, pixel-clean it (floors skipped: their texture noise would band), then compare raw vs cleaned
# (asset_qa.py undoes the clean if it changed >1% of pixels, logs, and writes tools/.intake/sheets/<folder>_<name>.png to look at once)
mkdir -p tools/.intake/raw
raw="tools/.intake/raw/$(basename "$(dirname "$out")")_$(basename "$out")"
cp "$out" "$raw"
case "$out" in */Floors/*) ;; *) [ -x "${ASEPRITE:-/d/Programs/Aseprite/aseprite.exe}" ] && bash tools/ase_clean.sh --apply "$out" || true
  python -I tools/asset_qa.py intake "$out" "$raw" || echo "INTAKE FAIL: see above; raw copy is $raw" ;; esac
