#!/bin/sh
# Usage: Tools/unity.sh <logname> [extra unity args...]
U="/c/Program Files/Unity/Hub/Editor/6000.0.61f1/Editor/Unity.exe"
ROOT="$(cd "$(dirname "$0")/.." && pwd -W)"
LOG="$ROOT/Logs/$1.log"; shift
"$U" -batchmode -projectPath "$ROOT" -logFile "$LOG" "$@"
code=$?
echo "exit=$code log=$LOG"
grep -E "error CS|Compilation failed|Exception|\[MojiBattle\]" "$LOG" | head -60
exit $code
