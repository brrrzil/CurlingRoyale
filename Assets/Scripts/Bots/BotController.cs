using UnityEngine;
using CurlingRoyale.Combat;
using CurlingRoyale.Game;

namespace CurlingRoyale.Bots
{
    /// <summary>
    /// Простой FSM-бот. Активен когда GameManager.State == InProgress.
    /// Цикл: Targeting → Aiming → Charging → Released → Cooldown → Targeting.
    /// Каждый переход Targeting ждёт ReloadController.IsReady (перезарядка).
    /// chargeRing sprite создаётся программно — в Inspector ничего ставить не надо.
    /// </summary>
    [RequireComponent(typeof(CustomPhysicsBody))]
    [RequireComponent(typeof(ReloadController))]
    public class BotController : MonoBehaviour
    {
        public enum State
        {
            Idle, Targeting, Aiming, Charging, Released, Cooldown
        }

        [Header("Параметры бота")]
        [Min(0f)] public float minForce = 4f;
        [Min(1f)] public float maxForce = 14f;
        [Min(0f)] public float minChargeTime = 0.6f;
        [Min(0.1f)] public float maxChargeTime = 1.4f;
        [Range(0f, 60f)] public float aimSpreadDegrees = 12f;
        [Range(0f, 1f)] public float randomTargetChance = 0.15f;
        [Min(0.1f)] public float cooldownDuration = 1.2f;
        [Tooltip("Опережение точки прицеливания ВПЕРЁД относительно движения цели (мировые единицы). " +
                 "Камень прилетает в эту точку когда цель уже в ней -- удар выходит в спину.")]
        [Min(0f)] [SerializeField] private float aimLeadDistance = 1.2f;

        [Header("Цвета charge ring (авто-создаваемого)")]
        public Color ringMinColor = new Color(0.3f, 0.85f, 0.4f, 0.85f);
        public Color ringMaxColor = new Color(0.95f, 0.3f, 0.3f, 0.85f);

        [Header("Звук зарядки")]
        [SerializeField] private AudioSource chargeAudioSource;
        [SerializeField] private AudioClip chargeLoopClip;
        [Range(0f, 1f)] [SerializeField] private float chargeLoopVolume = 0.25f;

        // ─── Состояние ─────────────────────────────────────────────
        public State CurrentState { get; private set; } = State.Idle;
        private CustomPhysicsBody physicsBody;
        private ReloadController reload;
        private CurlingRoyale.Combat.StoneCombat combat;
        private Transform currentTarget;
        private Vector2 currentDirection;
        private float chargeStartTime;
        private float chargeDuration;
        private float aimTimer;
        private float cooldownTimer;

        // Ring: UI Image с Type=Filled, FillMethod=Radial360, Clockwise=true. Из префаба.
        [Tooltip("UI Image кольца зарядки (в префабе). fillAmount = t при зарядке (0..1).")]
        [SerializeField] private UnityEngine.UI.Image chargeRingFill;
        private bool hasChargeRing;

        void Awake()
        {
            physicsBody = GetComponent<CustomPhysicsBody>();
            reload = GetComponent<ReloadController>();
            combat = GetComponent<CurlingRoyale.Combat.StoneCombat>();
            // Fallback: ищем Image "ChargeRingFill" в любом дочернем Canvas.
            if (chargeRingFill == null)
            {
                foreach (var img in GetComponentsInChildren<UnityEngine.UI.Image>(true))
                {
                    if (img.gameObject.name == "ChargeRingFill")
                    {
                        chargeRingFill = img;
                        break;
                    }
                }
            }
            hasChargeRing = chargeRingFill != null;
        }

        void OnEnable()
        {
            // Если GameManager уже есть, подпишемся.
            if (GameManager.Instance != null)
                GameManager.Instance.onStateChanged += OnGameStateChanged;
            if (hasChargeRing) SetRingActive(false);
        }

        void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.onStateChanged -= OnGameStateChanged;
            SetRingActive(false);
        }

        private void OnGameStateChanged(GameManager.MatchState newState)
        {
            if (newState != GameManager.MatchState.InProgress)
            {
                CurrentState = State.Idle;
                SetRingActive(false);
                return;
            }
            CurrentState = State.Targeting;
        }

        // Self-healing: если GameManager.Instance был null при OnEnable, подпишемся позже.
        private void TrySubscribeIfNeeded()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.onStateChanged -= OnGameStateChanged; // защита от двойной подписки
            GameManager.Instance.onStateChanged += OnGameStateChanged;
            // Также подтянем текущее состояние.
            if (GameManager.Instance.State == GameManager.MatchState.InProgress && CurrentState == State.Idle)
                CurrentState = State.Targeting;
        }

        // ─── FSM ───────────────────────────────────────────────────

        void Update()
        {
            // Если мы ещё не подписаны (GameManager.Instance появился позже) — подпишемся.
            if (GameManager.Instance != null && CurrentState == State.Idle &&
                GameManager.Instance.State == GameManager.MatchState.InProgress)
            {
                TrySubscribeIfNeeded();
            }

            // Мёртвый камень не пытается стрелять.
            if (combat != null && combat.IsDead)
            {
                if (CurrentState != State.Idle) SetRingActive(false);
                CurrentState = State.Idle;
                return;
            }

            if (CurrentState == State.Idle) return;
            if (GameManager.Instance == null || GameManager.Instance.State != GameManager.MatchState.InProgress)
                return;

            float dt = Time.deltaTime;
            switch (CurrentState)
            {
                case State.Targeting:
                    // Ждём reload перед поиском цели.
                    if (!reload.IsReady) return;
                    PickTarget();
                    BeginAiming();
                    break;

                case State.Aiming:
                    aimTimer -= dt;
                    if (aimTimer <= 0f) BeginCharging();
                    break;

                case State.Charging:
                    UpdateRingVisual();
                    if (Time.time - chargeStartTime >= chargeDuration) ReleaseShot();
                    break;

                case State.Released:
                    if (physicsBody.GetVelocity().sqrMagnitude < 0.04f)
                        CurrentState = State.Cooldown;
                    else if (Time.time - chargeStartTime > 5f)
                        CurrentState = State.Cooldown;
                    break;

                case State.Cooldown:
                    cooldownTimer -= dt;
                    if (cooldownTimer <= 0f) CurrentState = State.Targeting;
                    break;
            }
        }

        private void PickTarget()
        {
            StoneCombat[] all = FindObjectsByType<StoneCombat>(FindObjectsSortMode.None);
            StoneCombat best = null;
            float bestDist = float.MaxValue;
            bool pickRandom = Random.value < randomTargetChance;
            int candidates = 0;

            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s == null || s.IsDead) continue;
                if (s.gameObject == gameObject) continue;
                candidates++;
                if (pickRandom)
                {
                    if (Random.value < 1f / Mathf.Max(1, candidates)) { best = s; }
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

            // Точка прицеливания:
            //   -- Подвижная цель: целимся в точку, где цель БУДЕТ через leadTime.
            //      Камень, выпущенный в эту точку, догоняет цель сзади.
            //      Удар приходит со спины => back hit (максимальный урон).
            //   -- Стоящая цель: целимся прямо в неё (фронтальный удар).
            Vector2 aimPoint = (Vector2)currentTarget.position;

            var targetRb = currentTarget.GetComponent<Rigidbody2D>();
            if (targetRb != null && targetRb.linearVelocity.sqrMagnitude > 0.01f)
            {
                // Lead time: время пока камень долетает до цели. ~0.6s для камня с силой 8-10.
                aimPoint = aimPoint + targetRb.linearVelocity.normalized * aimLeadDistance;
            }

            Vector2 aimDir = (aimPoint - (Vector2)transform.position).normalized;
            float spread = Random.Range(-aimSpreadDegrees, aimSpreadDegrees);
            currentDirection = (Quaternion.Euler(0, 0, spread) * (Vector3)aimDir).normalized;
            aimTimer = Random.Range(0.12f, 0.3f);
            CurrentState = State.Aiming;
        }

        private void BeginCharging()
        {
            chargeDuration = Random.Range(minChargeTime, maxChargeTime);
            chargeStartTime = Time.time;
            CurrentState = State.Charging;
            SetRingActive(true);
            UpdateRingVisual();

            if (chargeLoopClip != null && chargeAudioSource != null)
            {
                chargeAudioSource.clip = chargeLoopClip;
                chargeAudioSource.loop = true;
                chargeAudioSource.volume = chargeLoopVolume;
                chargeAudioSource.Play();
            }
        }

        private void ReleaseShot()
        {
            float t = Mathf.Clamp01((Time.time - chargeStartTime) / chargeDuration);
            float force = Mathf.Lerp(minForce, maxForce, t);
            physicsBody.ApplyForce(currentDirection, force);
            SetRingActive(false);
            CurrentState = State.Released;

            if (chargeAudioSource != null && chargeAudioSource.isPlaying)
                chargeAudioSource.Stop();
        }

        // ─── Ring (radial fill clockwise) ──────────────────────────

        private void SetRingActive(bool active)
        {
            if (chargeRingFill != null && chargeRingFill.gameObject.activeSelf != active)
                chargeRingFill.gameObject.SetActive(active);
        }

        void LateUpdate()
        {
            // Канвас с кольцом: фиксируем world rotation = identity (см. PlayerController).
            if (chargeRingFill != null && chargeRingFill.canvas != null)
            {
                chargeRingFill.canvas.transform.rotation = Quaternion.identity;
            }
        }

        private void UpdateRingVisual()
        {
            if (chargeRingFill == null) return;
            float t = Mathf.Clamp01((Time.time - chargeStartTime) / Mathf.Max(0.01f, chargeDuration));
            // fillAmount: 0 → 1 по часовой стрелке (кольцо заполняется по мере зарядки).
            chargeRingFill.fillAmount = t;
            // Color: green (ready) → red (charging) by t.
            chargeRingFill.color = Color.Lerp(ringMinColor, ringMaxColor, t);
        }
    }
}
