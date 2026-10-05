#!/bin/sh
# 調整サイクル: コード既定値から CombatBalance.asset を再生成 → PlayMode テスト
cd "$(dirname "$0")/.."
rm -f Assets/Resources/Balance/CombatBalance.asset Assets/Resources/Balance/CombatBalance.asset.meta
Tools/unity.sh setup -executeMethod MojiBattle.EditorTools.ProjectSetup.Run -quit | grep -E "exit=|error CS"
rm -f Reports/playmode.xml; Tools/unity.sh playmode -runTests -testPlatform PlayMode -testResults "$(pwd -W)/Reports/playmode.xml" "$@" > /dev/null
grep -E "^\[(P0|DETERMINISM|KO|WALL|SHOTS)\]" Logs/playmode.log
python Tools/results.py Reports/playmode.xml
