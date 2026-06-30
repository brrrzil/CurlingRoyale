using UnityEngine;
using CurlingRoyale.Game;

namespace CurlingRoyale.Bots
{
    /// <summary>
    /// Простой FSM-бот для камня. Подписывается на GameManager.MatchState.
    /// Цикл: Targeting → Aiming → Charging → Released → Cooldown → Targeting.
    /// Бот выбирает живую цель и стреляет в её сторону.
    ///
    /// Параметры баланса берутся из Round 0 TZ (maxCharge = 1.5, угол разброс ±15°).
    /// </summary>
    [RequireComponent(typeof(CustomPhysicsBody))]
    public class BotController : MonoBehaviour
    {
        public enum State
        {
            Idle,        // матч не активен — ждём
            Targeting,   // выбираем цель
            Aiming,      // пауза перед зарядкой (имитация «думаю»)
            Charging,    // накапливаем силу
            Released,    // выстрел произведён, ждём остановки
            Cooldown,    // короткая пауза между выстрелами
        }

        [Header("Параметры бота")]
        [Tooltip("Минимальная сила выстрела.")]
        [Min(0f)] public float minForce = 4f;

        [Tooltip("Максимальная сила выстрела (слабее игрока).")]
        [Min(1f)] public float maxForce = 14f;

        [Tooltip("Минимальное время зарядки (сек).")]
        [Min(0f)] public float minChargeTime = 0.6f;

        [Tooltip("Максимальное время зарядки (сек).")]
        [Min(0.1f)] public float maxChargeTime = 1.4f;

        [Tooltip("Разброс прицела в градусах (анти-имба: бот промахивается).")]
        [Range(0f, 60f)] public float aimSpreadDegrees = 18f;

        [Tooltip("Шанс выбрать случайную цель вместо ближайшей (0..1).")]
        [Range(0f, 1f)] public float randomTargetChance = 0.15f;

        [Tooltip("Время кд между выстрелами (сек).")]
        [Min(0.1f)] public float cooldownDuration = 1.2f;

        [Header("Визуал (опционально)")]
        public SpriteRenderer chargeRingRenderer; // кружок силы, как у игрока

        // ─── Состояние ──────────────────────────────────────────────
        public State CurrentState { get; private set; } = State.Idle;
        private CustomPhysicsBody physicsBody;
        private Transform currentTarget;
        private Vector2 currentDirection;
        private float chargeStartTime;
        private float chargeDuration;
        private float aimTimer;
        private float cooldownTimer;

        void Awake()
        {
            physicsBody = GetComponent<CustomPhysicsBody>();
            if (chargeRingRenderer != null) chargeRingRenderer.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.onStateChanged += OnGameStateChanged;
        }

        void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.onStateChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(GameManager.MatchState newState)
        {
            // Любое изменение состояния вне InProgress — на Idle.
            if (newState != GameManager.MatchState.InProgress)
            {
                CurrentState = State.Idle;
                HideChargeVisual();
                return;
            }
            // Вход в InProgress: начинаем с Targeting.
            CurrentState = State.Targeting;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            switch (CurrentState)
            {
                case State.Idle:
                    return;

                case State.Targeting:
                    PickTarget();
                    BeginAiming();
                    break;

                case State.Aiming:
                    aimTimer -= dt;
                    if (aimTimer <= 0f) BeginCharging();
                    break;

                case State.Charging:
                    UpdateChargeVisual();
                    float elapsed = Time.time - chargeStartTime;
                    if (elapsed >= chargeDuration) ReleaseShot();
                    break;

                case State.Released:
                    // Ждём, пока камень замедлится (или жёсткий таймаут).
                    if (physicsBody.GetVelocity().sqrMagnitude < 0.04f)
                        CurrentState = State.Cooldown;
                    else if (Time.time - chargeStartTime > 5f) // safety: даже если застрял
                        CurrentState = State.Cooldown;
                    break;

                case State.Cooldown:
                    cooldownTimer -= dt;
                    if (cooldownTimer <= 0f) CurrentState = State.Targeting;
                    break;
            }
        }

        // ─── Переходы FSM ───────────────────────────────────────────

        private void PickTarget()
        {
            // Простая эвристика: ищем все StoneCombat на сцене, фильтруем живых и не нас.
            StoneCombat[] all = FindObjectsByType<StoneCombat>(FindObjectsSortMode.None);
            StoneCombat best = null;
            float bestDist = float.MaxValue;
            bool pickRandom = Random.value < randomTargetChance;

            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s == null || s.IsDead) continue;
                if (s.gameObject == gameObject) continue;

                if (pickRandom)
                {
                    if (Random.value < 1f / (all.Length - i))
                    {
                        best = s;
                        break;
                    }
                }
                else
                {
                    float d = ((Vector2)s.transform.position - (Vector2)transform.position).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; best = s; }
                }
            }

            currentTarget = best != null ? best.transform : null;
        }

        private void BeginAiming()
        {
            if (currentTarget == null)
            {
                CurrentState = State.Cooldown;
                cooldownTimer = 0.5f;
                return;
            }

            // Целься в сторону, ПРОТИВОПОЛОЖНУЮ направлению движения цели (в спину).
            Vector2 toTarget = (Vector2)currentTarget.position - (Vector2)transform.position;
            Vector2 targetFacing = currentTarget.TryGetComponent<Rigidbody2D>(out var rb) && rb.linearVelocity.sqrMagnitude > 0.05f
                ? rb.linearVelocity.normalized
                : -toTarget.normalized;

            // Направление удара = в спину цели: вдоль targetFacing (нам надо лететь за ней).
            Vector2 desired = targetFacing;
            // Разброс прицела.
            float spread = Random.Range(-aimSpreadDegrees, aimSpreadDegrees);
            desired = (Quaternion.Euler(0, 0, spread) * (Vector3)desired).normalized;

            // Дальность подбираем так, чтобы прицел был близко к точному.
            currentDirection = desired;
            aimTimer = Random.Range(0.15f, 0.5f);
            CurrentState = State.Aiming;
        }

        private void BeginCharging()
        {
            chargeDuration = Random.Range(minChargeTime, maxChargeTime);
            chargeStartTime = Time.time;
            CurrentState = State.Charging;
            ShowChargeVisual();
        }

        private void ReleaseShot()
        {
            float t = Mathf.Clamp01((Time.time - chargeStartTime) / chargeDuration);
            float force = Mathf.Lerp(minForce, maxForce, t);
            physicsBody.ApplyForce(currentDirection, force);

            HideChargeVisual();
            CurrentState = State.Released;
        }

        // ─── Визуал зарядки ─────────────────────────────────────────

        private void ShowChargeVisual()
        {
            if (chargeRingRenderer != null)
            {
                chargeRingRenderer.gameObject.SetActive(true);
                chargeRingRenderer.color = Color.green;
                chargeRingRenderer.transform.localScale = Vector3.one * 0.5f;
            }
        }

        private void UpdateChargeVisual()
        {
            if (chargeRingRenderer == null) return;
            float t = Mathf.Clamp01((Time.time - chargeStartTime) / chargeDuration);
            chargeRingRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.5f, t);
            chargeRingRenderer.color = Color.Lerp(Color.green, Color.red, t);
        }

        private void HideChargeVisual()
        {
            if (chargeRingRenderer != null) chargeRingRenderer.gameObject.SetActive(false);
        }

        // ─── Репортинг в GameManager ──────────────────────────────────

        /// <summary>
        /// Сообщить GameManager-у о нашей смерти. Вызывать вручную или
        /// через событие StoneCombat.OnDeath.
        /// </summary>
        public void NotifySelfDied()
        {
            // GameManager каждые 0.25 сек сам считает живых StoneCombat-ов
            // через FindObjectsByType и сам завершит матч при alive <= 1.
            // Здесь можно ничего не делать; метод оставлен для совместимости.
        }
    }
}
