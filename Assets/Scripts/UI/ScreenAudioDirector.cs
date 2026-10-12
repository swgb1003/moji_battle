using UnityEngine;
using UnityEngine.SceneManagement;

namespace MojiBattle
{
    /// <summary>Menu music and button cues survive scene changes. Combat owns its gong-timed BGM.</summary>
    public sealed class ScreenAudioDirector : MonoBehaviour
    {
        public static ScreenAudioDirector Instance { get; private set; }
        [Range(0f, 1f)] public float MusicVolume = .28f;
        [Range(0f, 1f)] public float ButtonVolume = .8f;
        public AudioSource MusicSource { get; private set; }
        public AudioSource ButtonSource { get; private set; }
        BattleMusicSettings settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize() => EnsureInstance();

        static ScreenAudioDirector EnsureInstance()
        {
            if (Instance == null) new GameObject("ScreenAudio").AddComponent<ScreenAudioDirector>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            transform.position = Vector3.back * 110f;
            settings = Resources.Load<BattleMusicSettings>("Audio/BattleMusic");
            if (settings == null) Debug.LogError("Missing screen audio settings: Audio/BattleMusic");
            MusicSource = Source(true, 150);
            ButtonSource = Source(false, 20);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void Start()
        {
            // Editor Play can begin with the scene already present. Resolve that scene once
            // after its camera has initialized, as well as listening for later scene changes.
            if (Instance == this) OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        AudioSource Source(bool loop, int priority)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.pitch = 1f;
            source.priority = priority;
            return source;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // COM simulations and test battles use additive physics scenes.
            if (mode != LoadSceneMode.Single) return;
            if (FindFirstObjectByType<AudioListener>() == null && Camera.main != null)
                Camera.main.gameObject.AddComponent<AudioListener>();
            AudioClip clip = null;
            if (settings != null)
            {
                switch (scene.name)
                {
                    case GameFlow.TitleScene: clip = settings.titleLoop; break;
                    case GameFlow.ModeSelectScene: clip = settings.menuLoop; break;
                    case GameFlow.AchievementsScene: clip = settings.menuLoop; break;
                    case GameFlow.ChallengeScene: clip = settings.menuLoop; break;
                    case GameFlow.RunScene: clip = settings.menuLoop; break;
                    case GameFlow.DraftScene: clip = settings.draftLoop; break;
                    case GameFlow.FriendRoomScene: clip = settings.menuLoop; break;
                    case GameFlow.TournamentScene: clip = settings.tournamentLoop; break;
                    case GameFlow.WorldCupScene: clip = settings.tournamentLoop; break;
                    case GameFlow.CustomizeScene: clip = settings.customizeLoop; break;
                }
            }
            // Stop menu music immediately on entry to Battle, including its countdown.
            if (MusicSource.clip == clip && MusicSource.isPlaying) return;
            MusicSource.Stop();
            MusicSource.clip = clip;
            MusicSource.volume = MusicVolume * GameSettings.MusicGain;
            MusicSource.mute = !TimeController.PresentationEffectsEnabled;
            if (clip != null) MusicSource.Play();
        }

        void Update()
        {
            MusicSource.volume = MusicVolume * GameSettings.MusicGain;
            MusicSource.mute = !TimeController.PresentationEffectsEnabled || SettingsScreen.Previewing;
            ButtonSource.volume = ButtonVolume * GameSettings.EffectsGain;
            ButtonSource.mute = !TimeController.PresentationEffectsEnabled;
        }

        public static void PlayTitleStart()
        {
            var audio = EnsureInstance();
            audio.PlayButton(audio.settings != null ? audio.settings.titleStart : null);
        }

        public static void PlayBattleStart()
        {
            var audio = EnsureInstance();
            audio.PlayButton(audio.settings != null ? audio.settings.battleStart : null);
        }

        void PlayButton(AudioClip clip)
        {
            if (!TimeController.PresentationEffectsEnabled || clip == null) return;
            ButtonSource.clip = clip;
            ButtonSource.volume = ButtonVolume * GameSettings.EffectsGain;
            ButtonSource.mute = false;
            ButtonSource.Play();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void MojiResumeAudio();

        // The Web template calls this from a real click after the player has loaded.
        [UnityEngine.Scripting.Preserve]
        public void ResumeBrowserAudio()
        {
            MojiResumeAudio();
            if (MusicSource.clip != null) MusicSource.Play();
        }
#endif
    }
}
