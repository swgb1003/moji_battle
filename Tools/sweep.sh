#!/bin/sh
cd "$(dirname "$0")/.."
rm -f Assets/Resources/Balance/CombatBalance.asset Assets/Resources/Balance/CombatBalance.asset.meta Assets/Resources/Balance/CustomizeBalance.asset Assets/Resources/Balance/CustomizeBalance.asset.meta
Tools/unity.sh setup -executeMethod MojiBattle.EditorTools.ProjectSetup.Run -quit | grep -E "exit=|error CS"
rm -f Reports/sweep.xml; Tools/unity.sh sweep -runTests -testPlatform PlayMode -testFilter "MojiBattle.Tests.BattlePlayModeTests.P1_OneVsUtsu_SeedSweep" -testResults "$(pwd -W)/Reports/sweep.xml" > /dev/null
python Tools/results.py Reports/sweep.xml | tail -2
sed -n '3,22p' Reports/p1_seed_sweep.md
