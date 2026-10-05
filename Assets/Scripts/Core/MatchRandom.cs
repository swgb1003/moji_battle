namespace MojiBattle
{
    /// <summary>
    /// 試合専用PRNG（xorshift64*）。UnityEngine.Random を使わず、seed から決定的に値を出す。
    /// AI判断（攻撃開始タイミング、狙い、回避/ガード、連撃数）にだけ使い、物理結果の補正には使わない。
    /// </summary>
    public sealed class MatchRandom
    {
        ulong state;

        public MatchRandom(int seed)
        {
            state = SplitMix((ulong)(uint)seed ^ 0x9E3779B97F4A7C15UL);
            if (state == 0) state = 0x2545F4914F6CDD1DUL;
        }

        static ulong SplitMix(ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }

        public uint NextUInt()
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            return (uint)((state * 2685821657736338717UL) >> 32);
        }

        /// <summary>[0, 1)</summary>
        public float Value() => (NextUInt() >> 8) * (1f / 16777216f);

        public float Range(float min, float max) => min + (max - min) * Value();

        /// <summary>[min, max] の整数。</summary>
        public int RangeInclusive(int min, int max)
        {
            if (max <= min) return min;
            int v = min + (int)(Value() * (max - min + 1));
            return v > max ? max : v;
        }

        public bool Chance(float p) => Value() < p;

        /// <summary>陣営ごとに独立した系列を作る。</summary>
        public MatchRandom Fork(int salt) => new MatchRandom((int)(NextUInt() ^ (uint)(salt * 7919)));
    }
}
