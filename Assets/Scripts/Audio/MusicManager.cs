using UnityEngine;

namespace CurlingRoyale.Audio
{
    /// <summary>
    /// Менеджер фоновой музыки.
    /// Кинь на любой GameObject в первой сцене (например, GameManager),
    /// перетащи AudioClip в musicClip. DontDestroyOnLoad -- живёт между сценами.
    ///
    /// Если clip не назначен, менеджер просто ничего не делает.
    /// </summary>
    [DisallowMultipleComponent]
    public class MusicManager : MonoBehaviour
    {
        [Header("Клип")]
        [Tooltip("Если задан -- проигрывается в loop начиная со Start().")]
        [SerializeField] private AudioClip musicClip;

        [Header("Громкость")]
        [Range(0f, 1f)] [SerializeField] private float volume = 0.5f;

        [Header("Поведение")]
        [Tooltip("Если true -- не выключать музыку при MatchEnd.")]
        [SerializeField] private bool keepPlayingOnMatchEnd = true;

        [Tooltip("Pause-resume через fade, если нужно (не обязательно).")]
        [Range(0f, 2f)] [SerializeField] private float fadeInSeconds = 0.3f;

        private AudioSource source;
        private static MusicManager instance;

        void Awake()
        {
            // Singleton.
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
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
        }

        void Start()
        {
            StartCoroutine(PlayAfterDelay());
        }

        System.Collections.IEnumerator PlayAfterDelay()
        {
            yield return null;
            if (musicClip == null)
            {
                Debug.LogWarning("[MusicManager] musicClip не назначен -- музыка не играет.");
                yield break;
            }
            source.clip = musicClip;
            source.loop = true;
            source.Play();
            float t = 0f;
            float start = Time.time;
            while (t < volume)
            {
                t = Mathf.Lerp(0f, volume, (Time.time - start) / Mathf.Max(0.0001f, fadeInSeconds));
                source.volume = Mathf.Min(t, volume);
                yield return null;
            }
            source.volume = volume;
        }

        public void PauseMusic() { if (source != null) source.Pause(); }
        public void ResumeMusic() { if (source != null) source.UnPause(); }
        public void StopMusic() { if (source != null) source.Stop(); }
        public void SetVolume(float v) { volume = Mathf.Clamp01(v); if (source != null) source.volume = volume; }

        public void SwapClip(AudioClip newClip)
        {
            musicClip = newClip;
            if (source != null && newClip != null)
            {
                source.Stop();
                source.clip = newClip;
                source.loop = true;
                source.Play();
            }
        }
    }
}
