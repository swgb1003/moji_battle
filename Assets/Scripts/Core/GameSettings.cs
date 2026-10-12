using System;
using UnityEngine;

namespace MojiBattle
{
    [Serializable]
    public sealed class GameSettingsData
    {
        public float masterVolume = .8f, musicVolume = .6f, effectsVolume = .9f;
        public int battleMusic; // 0: random, 1..3: fixed track
        public int spectatorSpeed = 1;
        public bool showInstructions = true, showDamage = true;
        public bool reducedEffects, simpleSpecial;
        public GameSettingsData Clone() => (GameSettingsData)MemberwiseClone();
        public void Sanitize()
        {
            masterVolume = Volume(masterVolume, .8f);
            musicVolume = Volume(musicVolume, .6f);
            effectsVolume = Volume(effectsVolume, .9f);
            battleMusic = Mathf.Clamp(battleMusic, 0, 3);
            if (spectatorSpeed != 1 && spectatorSpeed != 2 && spectatorSpeed != 4) spectatorSpeed = 1;
        }
        static float Volume(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);
    }

    /// <summary>User presentation preferences. Combat data and random generators are independent.</summary>
    public static class GameSettings
    {
        public const string PrefKey = "MojiBattle.Settings.v1";
        static GameSettingsData current;
        public static event Action Changed;
        public static GameSettingsData Current { get { Load(); return current.Clone(); } }
        public static float MusicGain { get { Load(); return current.masterVolume * current.musicVolume; } }
        public static float EffectsGain { get { Load(); return current.masterVolume * current.effectsVolume; } }
        public static bool ReducedEffects { get { Load(); return current.reducedEffects; } }
        public static bool ShowDamage { get { Load(); return current.showDamage; } }
        public static bool ShowInstructions { get { Load(); return current.showInstructions; } }
        public static bool SimpleSpecial { get { Load(); return current.simpleSpecial; } }
        public static int BattleMusic { get { Load(); return current.battleMusic; } }
        public static int InitialSpeed { get { Load(); return current.spectatorSpeed; } }
        public static void Change(Action<GameSettingsData> edit)
        {
            var next = Current; edit(next); next.Sanitize(); current = next; Save(); Changed?.Invoke();
        }
        public static void Reset()
        {
            current = new GameSettingsData();
            PlayerPrefs.SetInt("MenuReducedMotion", 0);
            Save(); Changed?.Invoke();
        }
        public static void SetMenuMotion(bool reduced)
        {
            PlayerPrefs.SetInt("MenuReducedMotion", reduced ? 1 : 0);
            PlayerPrefs.Save(); Changed?.Invoke();
        }
        public static void Reload() { current = null; Load(); Changed?.Invoke(); }
        static void Load()
        {
            if (current != null) return;
            try { current = JsonUtility.FromJson<GameSettingsData>(PlayerPrefs.GetString(PrefKey, "")); }
            catch (Exception e) { Debug.LogWarning("設定を読めません: " + e.Message); }
            current = current ?? new GameSettingsData(); current.Sanitize();
        }
        static void Save() { PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(current)); PlayerPrefs.Save(); }
    }
}
