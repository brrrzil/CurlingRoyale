using UnityEngine;

namespace CurlingRoyale.Audio
{
    /// <summary>
    /// Менеджер фоновой музыки.
    /// Кинь на любой GameObject в первой сцене (например, GameManager).
    /// DontDestroyOnLoad -- живёт между сценами.
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
        [Range(0f, 1f)] [SerializeField] private float volume = 0.5f;

        [Header("Поведение")]
        [Tooltip("Задержка между треками при shuffle.")]
        [Min(0f)] [SerializeField] private float delayBetweenTracks = 1.5f;

        [Tooltip("Fade-in на старте.")]
        [Range(0f, 2f)] [SerializeField] private float fadeInSeconds = 0.4f;

        private AudioSource source;
        private static MusicManager instance;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            source = GetComponent<AudioSource>();
            if (source == null)
                source = gameObject.AddComponent<AudioSource>();
            source.loop = false;
            source.playOnAwake = false;
            source.volume = 0f;

            // Fallback: если клипы не заданы -- загружаем все AudioClip из Resources/Audio.
            if (musicClips == null || musicClips.Length == 0)
            {
                musicClips = Resources.LoadAll<AudioClip>("Audio");
                // Из них исключаем Collision_Stone (это не музыка).
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
            source.clip = musicClips[0];
            source.loop = true;
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
            while (true)
            {
                var clip = musicClips[Random.Range(0, musicClips.Length)];
                if (clip == null)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }
                source.clip = clip;
                source.loop = false;
                source.Play();
                float start = Time.time;
                while (source.volume < volume)
                {
                    source.volume = Mathf.Lerp(0f, volume, (Time.time - start) / Mathf.Max(0.0001f, fadeInSeconds));
                    yield return null;
                }
                source.volume = volume;
                yield return new WaitWhile(() => source.isPlaying);
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
