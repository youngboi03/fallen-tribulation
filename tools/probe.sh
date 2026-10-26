#!/usr/bin/env bash
# Run several autotest bots in parallel and summarize per-wave results.
# Usage: tools/probe.sh "name|args" "name|args" ...
#   e.g. tools/probe.sh "k0|-char 0 -kiep 0 -smart" "k3|-char 1 -kiep 3"
cd "$(dirname "$0")/.."
ROOT="$(pwd -W 2>/dev/null || pwd)"
mkdir -p Logs/probe
for spec in "$@"; do
  name="${spec%%|*}"; args="${spec#*|}"
  rm -rf "Logs/probe/$name"; mkdir -p "Logs/probe/$name"
  ./Builds/Windows/VanKiepTuTien.exe -autotest -waves 20 -speed 3 -shots "$ROOT/Logs/probe/$name" \
    -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile "$ROOT/Logs/probe/$name.log" $args &
  sleep 2
done
wait
for spec in "$@"; do
  name="${spec%%|*}"
  echo "== $name (${spec#*|})"
  grep -E "AUTOTEST\] wave|RUN END|boss wave timeout" "Logs/probe/$name.log" | sed -E 's/.*AUTOTEST\] //; s/peakEnemies/pk/; s/realTime/rt/; s/kills [0-9]+ //'
  echo "errors=$(grep -c Exception "Logs/probe/$name.log")"
done
