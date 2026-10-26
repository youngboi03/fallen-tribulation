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
echo "saved $out ($(wc -c < "$out") bytes)"
