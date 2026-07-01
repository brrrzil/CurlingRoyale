using UnityEngine;
using UnityEngine.Events;

namespace CurlingRoyale.Combat
{
    /// <summary>
    /// Общая логика перезарядки для камней (игрока и ботов).
    /// - Когда камень движется (velocity > threshold) — IsReady = false.
    /// - Когда остановился — стартует 1 сек таймер.
    /// - По завершении таймера — IsReady = true (можно снова стрелять).
    /// - PlayerController показывает/прячет charge ring по IsReady.
    /// - BotController начинает Targeting FSM только если IsReady.
    ///
    /// На матче старте IsReady = true сразу.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class ReloadController : MonoBehaviour
    {
        [Header("Параметры перезарядки")]
        [Tooltip("Длительность перезарядки после остановки камня (сек).")]
        [Min(0f)] public float reloadDuration = 1f;

        [Tooltip("Если скорость выше этого значения — камень считается 'в движении'.")]
        [Min(0.01f)] public float movingThreshold = 0.5f;

        [Tooltip("Стартовое состояние (до первого выстрела).")]
        public bool startReady = true;

        public bool IsReady { get; private set; }
        public float ReloadProgress01 => Mathf.Clamp01((Time.time - stoppedAt) / Mathf.Max(0.01f, reloadDuration));

        public UnityEvent onReloadStart;   // камень только что остановился
        public UnityEvent onReloadComplete; // камень готов к стрельбе

        private Rigidbody2D rb;
        private float stoppedAt;
        private bool wasInFlight;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            IsReady = startReady;
        }

        void Update()
        {
            if (rb == null) return;
            float speed = rb.linearVelocity.magnitude;

            if (speed > movingThreshold)
            {
                wasInFlight = true;
                if (IsReady)
                {
                    IsReady = false;
                    stoppedAt = 0f;
                }
            }
            else if (wasInFlight)
            {
                // Камень только что остановился — стартуем таймер.
                if (stoppedAt == 0f)
                {
                    stoppedAt = Time.time;
                    onReloadStart?.Invoke();
                }

                if (!IsReady && Time.time - stoppedAt >= reloadDuration)
                {
                    IsReady = true;
                    wasInFlight = false;
                    stoppedAt = 0f;
                    onReloadComplete?.Invoke();
                }
            }
        }

        /// <summary>
        /// Принудительный сброс (например, при старте матча).
        /// </summary>
        public void ForceReady()
        {
            IsReady = true;
            wasInFlight = false;
            stoppedAt = 0f;
        }
    }
}
