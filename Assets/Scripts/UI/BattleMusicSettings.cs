using UnityEngine;

namespace MojiBattle
{
    /// <summary>Keeps the original Assets/Audio clips referenced in player builds.</summary>
    [CreateAssetMenu(menuName = "MojiBattle/Battle Music")]
    public sealed class BattleMusicSettings : ScriptableObject
    {
        public AudioClip battleLoop;
        public AudioClip battleLoop2;
        public AudioClip battleLoop3;
        public AudioClip finalLoop;
        public AudioClip titleLoop;
        public AudioClip menuLoop;
        public AudioClip draftLoop;
        public AudioClip tournamentLoop;
        public AudioClip customizeLoop;
        public AudioClip titleStart;
        public AudioClip battleStart;
    }
}
