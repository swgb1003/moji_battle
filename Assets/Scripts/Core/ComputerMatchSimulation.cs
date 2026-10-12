using UnityEngine;

namespace MojiBattle
{
    /// <summary>Runs unchanged combat steps in larger batches while a cup board is loading.</summary>
    public static class ComputerMatchSimulation
    {
        public const double BoardBudgetSeconds = .024;
        public const double BattleBudgetSeconds = .003;

        public static void Configure(BattleInstance battle)
        {
            battle.Director.enabled = false;
            battle.Director.SilentSimulation = true;
            battle.Director.CollectStepTelemetry = false;
            battle.Director.FastSimulationQueries = true;
            battle.Director.CapturePresentationPose = false;
            foreach (var view in battle.Root.GetComponentsInChildren<StickmanView>()) view.enabled = false;
            foreach (var renderer in battle.Root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        }

        public static void Advance(MatchDirector director, bool duringBattle = false)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + (duringBattle ? BattleBudgetSeconds : BoardBudgetSeconds);
            int limit = duringBattle ? 512 : 8192;
            for (int step = 0; step < limit && !director.Ended; step++)
            {
                director.SimulateStep();
                // A clock read for every physics step adds overhead in WebGL.
                if ((duringBattle || (step & 15) == 15) && Time.realtimeSinceStartupAsDouble >= deadline) break;
            }
        }
    }
}
