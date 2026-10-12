using System;
using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    public sealed class TournamentEntrant
    {
        readonly FighterBuildData build;
        public int Id { get; }
        public string Name => Id == 0 ? "YOU" : "COM " + Id;
        public FighterBuildData Build => build.Clone();
        public TournamentEntrant(int id, FighterBuildData source)
        {
            Id = id;
            build = source.Clone();
            build.weaponSize = WeaponSize.S;
        }
    }

    public sealed class TournamentMatch
    {
        public int Id { get; }
        public int Round { get; }
        public int LeftId { get; internal set; } = -1;
        public int RightId { get; internal set; } = -1;
        public int WinnerId { get; internal set; } = -1;
        public int Seed { get; internal set; }
        public int Draws { get; internal set; }
        public string FinishReason { get; internal set; }
        public bool InProgress { get; internal set; }
        public bool Complete => WinnerId >= 0;
        public bool Ready => !Complete && !InProgress && LeftId >= 0 && RightId >= 0;
        public bool IncludesPlayer => LeftId == 0 || RightId == 0;
        internal TournamentMatch(int id, int round, int seed) { Id = id; Round = round; Seed = seed; }
    }

    /// <summary>Eight entrants and seven matches. Builds are private snapshots; a draw advances neither slot.</summary>
    public sealed class TournamentSession
    {
        public const string CupName = "ワタナベチャレンジカップ";
        public const int RequiredWins = 3;
        readonly TournamentEntrant[] entrants = new TournamentEntrant[8];
        readonly TournamentMatch[] matches = new TournamentMatch[7];
        readonly MatchRandom random;
        public IReadOnlyList<TournamentEntrant> Entrants { get; }
        public IReadOnlyList<TournamentMatch> Matches { get; }
        public int Seed { get; }
        public bool AchievementIronWall { get; set; } = true;
        public int Wins
        {
            get { int n = 0; foreach (var m in matches) if (m.WinnerId == 0) n++; return n; }
        }
        public bool Eliminated
        {
            get { foreach (var m in matches) if (m.IncludesPlayer && m.Complete && m.WinnerId != 0) return true; return false; }
        }
        public int ChampionId => matches[6].WinnerId;
        public bool Champion => ChampionId == 0 && Wins == RequiredWins;
        public bool Finished => Eliminated || ChampionId >= 0;
        public TournamentMatch NextPlayerMatch
        {
            get
            {
                if (Finished) return null;
                foreach (var m in matches) if (m.Ready && m.IncludesPlayer) return m;
                return null;
            }
        }
        public TournamentMatch NextComputerMatch
        {
            get
            {
                foreach (var m in matches)
                    if (m.Ready && !m.IncludesPlayer && (Eliminated || m.Round < Wins)) return m;
                return null;
            }
        }

        public TournamentSession(FighterBuildData player, IList<FighterBuildData> computers, int seed)
        {
            if (player == null || computers == null || computers.Count != 7)
                throw new ArgumentException("A tournament requires one player and seven COM fighters.");
            Seed = seed;
            random = new MatchRandom(seed);
            entrants[0] = new TournamentEntrant(0, player);
            for (int i = 1; i < 8; i++) entrants[i] = new TournamentEntrant(i, computers[i - 1]);
            for (int i = 0; i < 7; i++) matches[i] = new TournamentMatch(i, i < 4 ? 0 : i < 6 ? 1 : 2, NextSeed());
            for (int i = 0; i < 4; i++) { matches[i].LeftId = i * 2; matches[i].RightId = i * 2 + 1; }
            Entrants = Array.AsReadOnly(entrants);
            Matches = Array.AsReadOnly(matches);
        }

        int NextSeed() => (int)(random.NextUInt() & 0x7fffffffu) | 1;
        public static string RoundName(int round) => round == 0 ? "1回戦" : round == 1 ? "準決勝" : "決勝";

        public MatchConfig BeginMatch(int id, float duration)
        {
            if (id < 0 || id >= matches.Length) throw new ArgumentOutOfRangeException(nameof(id));
            var m = matches[id];
            if (!m.Ready) throw new InvalidOperationException("This match is not ready.");
            if (m.IncludesPlayer && m != NextPlayerMatch || !m.IncludesPlayer && m != NextComputerMatch)
                throw new InvalidOperationException("This match cannot be played yet.");
            m.InProgress = true;
            return new MatchConfig { left = entrants[m.LeftId].Build.ToLoadout(), right = entrants[m.RightId].Build.ToLoadout(),
                seed = m.Seed, durationSeconds = duration };
        }

        public void CancelStart(int id) { if (id >= 0 && id < matches.Length && !matches[id].Complete) matches[id].InProgress = false; }

        public bool RecordResult(int id, MatchResult result)
        {
            if (id < 0 || id >= matches.Length) return false;
            var m = matches[id];
            if (!m.InProgress || m.Complete || result.seed != m.Seed || result.winner < -1 || result.winner > 1) return false;
            m.InProgress = false;
            m.FinishReason = result.finishReason;
            if (result.winner == MatchRules.Draw)
            {
                m.Draws++;
                m.Seed = NextSeed();
                return true;
            }
            m.WinnerId = result.winner == 0 ? m.LeftId : m.RightId;
            if (id < 6)
            {
                var next = matches[id < 4 ? 4 + id / 2 : 6];
                if (id % 2 == 0) next.LeftId = m.WinnerId; else next.RightId = m.WinnerId;
            }
            return true;
        }
    }
}
