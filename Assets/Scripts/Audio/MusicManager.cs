using UnityEngine;

namespace CurlingRoyale.Audio
{
    /// <summary>
    /// Менеджер фоновой музыки.
    /// Кинь на любой GameObject в нужной сцене (MainMenu, GameScene, ...).
    /// НЕ ставим DontDestroyOnLoad -- каждая сцена имеет свой MusicManager
    /// с префиксом trackNamePrefix (Theme_Menu_, Theme_Midcore_, и т.д.).
    /// При переходе между сценами MM уничтожается вместе со сценой, и новая сцена
    /// создаёт свой MM с правильным треком.
    /// Если musicClips задан через inspector -- играет их в случайном порядке с shuffle.
    /// Иначе пытается Resources.LoadAll\&lt;AudioClip\&gt;("Audio").
    /// </summary>
    [DisallowMultipleComponent]
    public class MusicManager : MonoBehaviour
    {
        [Header("Клипы")]
        [Tooltip("Список треков. Если пусто и shuffle=true -- берёт всё из Resources/Audio/.")]
        [SerializeField] private AudioClip[] musicClips;

        [Tooltip("Если true -- треки играются по кругу в случайном порядке. Иначе первый клип в loop.")]
        [SerializeField] private bool shuffle = true;

        [Header("Громкость")]
        [Range(0f, 1f)] [SerializeField] private float volume = 0.25f;

        [Header("Поведение")]
        [Tooltip("Задержка между треками при shuffle.")]
        [Min(0f)] [SerializeField] private float delayBetweenTracks = 1.5f;

        [Tooltip("Fade-in на старте.")]
        [Range(0f, 2f)] [SerializeField] private float fadeInSeconds = 0.4f;

        [Tooltip("Префикс имени для автозагрузки из Resources/Audio. Если пусто -- 'Theme_' (по умолчанию). Используй 'Theme_Menu_' для меню, 'Theme_Midcore_' для геймплея.")]
        [SerializeField] private string trackNamePrefix = "Theme_";

        [Tooltip("Зацикливать каждый трек. Если false -- один раз до конца и стоп.")]
        [SerializeField] private bool loopEachTrack = true;

        private AudioSource source;
        public static MusicManager Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // НЕ ставим DontDestroyOnLoad -- каждая сцена имеет свой MusicManager с треком под неё.
            // При переходе MainMenu -> GameScene MM главного меню уничтожается,
            // и GameScene создаёт свой с правильным треком.

            source = GetComponent<AudioSource>();
            if (source == null)
                source = gameObject.AddComponent<AudioSource>();
            source.loop = loopEachTrack;
            source.playOnAwake = false;
            source.volume = 0f;

            // Fallback: если клипы не заданы -- загружаем из Resources/Audio только треки
            // (имя файла начинается с "Theme_"). SFX_* в музыку не попадают.
            if (musicClips == null || musicClips.Length == 0)
            {
                var all = Resources.LoadAll<AudioClip>("Audio");
                var list = new System.Collections.Generic.List<AudioClip>();
                foreach (var c in all)
                {
                    if (c == null) continue;
                    string prefix = string.IsNullOrEmpty(trackNamePrefix) ? "Theme_" : trackNamePrefix;
                    if (c.name.StartsWith(prefix)) list.Add(c);
                }
                musicClips = list.ToArray();
            }
        }

        void Start()
        {
            if (musicClips == null || musicClips.Length == 0)
            {
                Debug.LogWarning("[MusicManager] musicClips не заданы и Resources/Audio не найдено -- музыка молчит.");
                return;
            }
            if (shuffle) StartCoroutine(ShuffleLoop());
            else StartCoroutine(PlaySingleLoop());
        }

        System.Collections.IEnumerator PlaySingleLoop()
        {
            source.Stop();
            source.clip = musicClips[0];
            source.loop = true;
            Debug.Log($"[MusicManager] Single loop: {musicClips[0].name} (dur {musicClips[0].length:F1}s)");
            source.Play();

            float start = Time.time;
            while (source.volume < volume)
            {
                source.volume = Mathf.Lerp(0f, volume, (Time.time - start) / Mathf.Max(0.0001f, fadeInSeconds));
                yield return null;
            }
            source.volume = volume;
        }

        System.Collections.IEnumerator ShuffleLoop()
        {
            // Плейлист играет по кругу: каждый трек проигрывается один раз,
            // затем случайно выбирается следующий. Чтобы плейлист не зацикливался
            // на одном треке, исключаем последний сыгранный из выбора.
            int lastPlayed = -1;
            while (true)
            {
                int idx = Random.Range(0, musicClips.Length);
                if (musicClips.Length > 1 && idx == lastPlayed)
                    idx = (idx + 1) % musicClips.Length;
                lastPlayed = idx;
                var clip = musicClips[idx];
                if (clip == null)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }
                source.clip = clip;
                source.loop = false; // каждый трек играет один раз, плейлист зациклен while(true)
                source.Play();
                float start = Time.time;
                while (source.volume < volume)
                {
                    source.volume = Mathf.Lerp(0f, volume, (Time.time - start) / Mathf.Max(0.0001f, fadeInSeconds));
                    yield return null;
                }
                source.volume = volume;
                yield return new WaitWhile(() => source.isPlaying);
                if (delayBetweenTracks > 0f)
                    yield return new WaitForSeconds(delayBetweenTracks);
            }
        }

        public void PauseMusic() { if (source != null) source.Pause(); }
        public void ResumeMusic() { if (source != null) source.UnPause(); }
        public void StopMusic() { if (source != null) source.Stop(); }
        public void SetVolume(float v) { volume = Mathf.Clamp01(v); if (source != null) source.volume = volume; }

        public void SwapClip(AudioClip newClip)
        {
            musicClips = new[] { newClip };
            if (source != null) { source.Stop(); source.clip = newClip; source.loop = true; source.Play(); }
        }

        public void AddClip(AudioClip clip)
        {
            var arr = new System.Collections.Generic.List<AudioClip>(musicClips ?? new AudioClip[0]);
            arr.Add(clip);
            musicClips = arr.ToArray();
        }
    }
}
