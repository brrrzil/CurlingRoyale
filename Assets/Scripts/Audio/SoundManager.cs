using UnityEngine;

namespace CurlingRoyale.Audio
{
    /// <summary>
    /// Центральный звуковой диспетчер для одного кадра.
    /// Держит ОДИН AudioSource и проигрывает звуки через PlayOneShot --
    /// они НЕ прерывают друг друга и не путаются с музыкой.
    ///
    /// StoneCombat.OnCollisionEnter2D при столкновении вызывает:
    ///   SoundManager.Instance.PlayCollision(position, intensity)
    /// При таком подходе:
    ///   -- оба камня могут вызвать один и тот же звук, но PlayOneShot
    ///      автоматически накладывает на уже играющие кадры (mix)
    ///   -- можно привязать intensity (в нашем случае -- нанесённый урон) к громкости
    ///   -- можно потом перейти на стерео (left/right) по позиции -- PlayOneShot
    ///      с position параметром автоматически использует SpatialBlend AudioSource-а.
    /// </summary>
    [DisallowMultipleComponent]
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Клипы")]
        [SerializeField] private AudioClip[] collisionClips;

        [Header("Громкость")]
        [Range(0f, 1f)] [SerializeField] private float baseVolume = 0.7f;

        [Header("Рандом pitch (разнообразие)")]
        [Range(0.5f, 1.5f)] [SerializeField] private float minPitch = 0.85f;
        [Range(0.5f, 1.5f)] [SerializeField] private float maxPitch = 1.15f;

        [Header("Spatial blend (0=2D, 1=3D)")]
        [Range(0f, 1f)] [SerializeField] private float spatialBlend = 0f;

        private AudioSource source;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            source = GetComponent<AudioSource>();
            if (source == null)
                source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = spatialBlend;
            source.volume = 1f;

            // Если клипы не заданы в инспекторе -- пробуем Resources/Audio.
            if (collisionClips == null || collisionClips.Length == 0)
            {
                var c = Resources.Load<AudioClip>("Audio/Collision_Stone");
                if (c != null) collisionClips = new[] { c };
            }
        }

        /// <summary>
        /// Проиграть звук столкновения. intensity (0..1) влияет на громкость.
        /// </summary>
        public void PlayCollision(Vector3 worldPos, float intensity = 0.6f)
        {
            if (collisionClips == null || collisionClips.Length == 0) return;
            var clip = collisionClips[Random.Range(0, collisionClips.Length)];
            if (clip == null) return;

            float vol = Mathf.Clamp01(intensity) * baseVolume;
            float pitch = Random.Range(minPitch, maxPitch);
            source.pitch = pitch;
            // Сначала создаём временный GameObject на позиции (если spatialBlend > 0).
            if (spatialBlend > 0.001f)
            {
                AudioSource.PlayClipAtPoint(clip, worldPos, vol);
            }
            else
            {
                source.PlayOneShot(clip, vol);
            }
        }

        public void SetVolume(float v) => baseVolume = Mathf.Clamp01(v);
        public void SetSpatialBlend(float s)
        {
            spatialBlend = Mathf.Clamp01(s);
            if (source != null) source.spatialBlend = spatialBlend;
        }
    }
}
