namespace MojiBattle
{
    /// <summary>Tournament preparation never overwrites the regular battle's saved builds.</summary>
    public static class TournamentRuntimeStore
    {
        static FighterBuildData draft;
        public static TournamentSession Current { get; private set; }
        public static FighterBuildData Draft
        {
            get
            {
                if (draft == null) { draft = BuildRuntimeStore.Get(0).Clone(); draft.weaponSize = WeaponSize.S; }
                return draft.Clone();
            }
        }
        public static void SetDraft(FighterBuildData build)
        {
            if (Current != null && !Current.Finished) return;
            draft = build.Clone();
            draft.weaponSize = WeaponSize.S;
        }
        public static void Begin(FighterBuildData player, int seed)
        {
            if (Current != null && !Current.Finished) throw new System.InvalidOperationException("The current tournament is locked.");
            var opponents = TournamentOpponentGenerator.Generate(seed);
            Current = new TournamentSession(player, opponents, seed);
        }
        public static void Clear() => Current = null;
    }
}
