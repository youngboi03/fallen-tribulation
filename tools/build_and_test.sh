#!/usr/bin/env bash
# Build the Windows player in batchmode, then let the autotest bot play it.
# Usage: tools/build_and_test.sh [waves] [extra player args...]
#   e.g. tools/build_and_test.sh 6 -god
set -u
cd "$(dirname "$0")/.."
UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.27f1/Editor/Unity.exe"
ROOT="$(pwd -W 2>/dev/null || pwd)"
WAVES="${1:-6}"; shift || true

"$UNITY" -batchmode -quit -projectPath "$ROOT" -executeMethod VK.EditorTools.BuildTools.BuildWindows -logFile "$ROOT/Logs/build.log"
if grep -q "error CS" Logs/build.log; then grep "error CS" Logs/build.log | sort -u; exit 1; fi
grep "\[VK\] BUILD RESULT" Logs/build.log

rm -rf Logs/shots; mkdir -p Logs/shots
timeout "${TEST_TIMEOUT:-900}" ./Builds/Windows/VanKiepTuTien.exe -autotest -waves "$WAVES" -speed 2 -shots "$ROOT/Logs/shots" \
  -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile "$ROOT/Logs/run.log" "$@"
grep -E "AUTOTEST\]|\[VK\] wave|Exception" Logs/run.log | grep -v " shot " | head -80
