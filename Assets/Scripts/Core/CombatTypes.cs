using System;
using UnityEngine;

namespace MojiBattle
{
    public enum FontStyleId { Gothic, Mincho, RoundedGothic, Brush }
    public enum BodyPart { Head, Torso, Arm, Leg }
    public enum FighterState
    {
        Approach, AttackWindup, AttackActive, AttackRecovery,
        Guard, Evade, Stagger, Knockdown, Recover, KO
    }
    public enum WeightClass { Light, Medium, Heavy }
    /// <summary>Approach 状態内の移動意図。離脱（Retreat）も Approach 状態として扱う。</summary>
    public enum MoveIntent { Advance, Hold, Retreat }
    public enum HitQuality { Graze, Normal, Center }
    public enum EvadeKind { Backstep, HopOver }
    /// <summary>
    /// 振り下ろし（上から）/ 斬り上げ（下から）/ 刺す（腕を伸ばして先端で突く）/ 横薙ぎ（画面の奥を回り込んで前へ振る）/
    /// 足払い（屈んで低く奥から払う）/ 盾当て（字形を前に構えて体当たり）/ 打ち上げ（下から跳ね上げて浮かせる）/ 回転斬り（一回転して前後を払う）/
    /// 投げ（手を離して字形を投げつける。拾うまで素手）。
    /// </summary>
    public enum AttackStyle { Overhead, Rising, Thrust, Sweep, LowSweep, Bash, Launch, Spin, Throw }

    [Serializable]
    public struct GlyphFeatures
    {
        public float inkRatio, widthRatio, heightRatio, edgeRatio, balance;
        /// <summary>黒画素の平均座標（マスク画素空間、x右・y上）。</summary>
        public Vector2 centerOfMass;
        /// <summary>Collider矩形（マスク画素空間）。</summary>
        public Rect[] colliderRects;
        public RectInt inkBounds;
        /// <summary>握り位置（マスク画素空間）。外接矩形の左下寄りのインク画素。</summary>
        public Vector2 gripPoint;
        public int componentCount;
        public int maskSize;
        public float emSizePx;
        public int colliderGridUsed;
    }

    [Serializable]
    public struct NormalizedGlyphFeatures
    {
        public float A, W, H, C, B;
        public override string ToString() => $"A={A:F1} W={W:F1} H={H:F1} C={C:F1} B={B:F1}";
    }

    [Serializable]
    public struct FighterStats
    {
        public int attack, defense, speed, durability, weightScore;
        public float maxHp, weaponMass, bodyMass;
        public Vector2 weaponCenterOfMass;

        /// <summary>UI表示用の重量★（1〜6）。</summary>
        public int WeightStars => Mathf.Clamp(Mathf.CeilToInt(weightScore / 100f * 6f), 1, 6);

        public override string ToString() =>
            $"ATK {attack} DEF {defense} SPD {speed} DUR {durability} M {weightScore} HP {maxHp:F0} wMass {weaponMass:F2} bMass {bodyMass:F2}";
    }

    [Serializable]
    public struct FighterLoadout
    {
        public string grapheme;
        public FontStyleId font;
        /// <summary>カスタマイズ内容。null なら P1/P2 の検証どおりの既定（字形固有の握り・補正なし）。</summary>
        public FighterBuildData build;
        public FighterLoadout(string grapheme, FontStyleId font) { this.grapheme = grapheme; this.font = font; build = null; }
    }

    [Serializable]
    public struct MatchConfig
    {
        public FighterLoadout left, right;
        public int seed;
        public float durationSeconds;
    }

    [Serializable]
    public class FighterMetrics
    {
        public float damageDealt, damageTaken, envDamageTaken;
        public int attacksStarted, attacksWhiffed, hitsLanded, criticals, grazes, centerHits;
        public int guards, guardBreaks, evades, backsteps, hopOvers, knockdowns, envImpacts, clashes;
        /// <summary>この選手が叩きつけられた回数</summary>
        public int slamsTaken;
        public int[] partHits = new int[4];
        public float maxKnockbackDistance;
    }

    [Serializable]
    public struct MatchResult
    {
        /// <summary>0=左, 1=右, -1=引き分け。</summary>
        public int winner;
        public float elapsedSeconds;
        /// <summary>"KO" / "DOUBLE_KO" / "TIME_UP" / "TIME_UP_DRAW"。</summary>
        public string finishReason;
        public FighterMetrics[] metrics;
        public float[] hpRemaining;
        public float[] maxHp;
        public int seed;
    }
}
