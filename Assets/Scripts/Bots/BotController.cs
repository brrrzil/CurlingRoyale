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
        [Tooltip("Дистанция опережения точки попадания за целью (мировые единицы). " +
                 "Чем больше, тем глубже бот пытается бить в спину. 0 = фронтальный удар.")]
        [Min(0f)] [SerializeField] private float leadDistance = 1.6f;

        [Header("Цвета charge ring (авто-создаваемого)")]
        public Color ringMinColor = new Color(0.3f, 0.85f, 0.4f, 0.85f);
        public Color ringMaxColor = new Color(0.95f, 0.3f, 0.3f, 0.85f);
        public float ringStartRadius = 0.5f;
        public float ringEndRadius = 1.5f;
        public int ringSortingOrder = 10;

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

        // авто-создаваемый ring
        private SpriteRenderer chargeRingRenderer;
        private Color baseRingColor;

        void Awake()
        {
            physicsBody = GetComponent<CustomPhysicsBody>();
            reload = GetComponent<ReloadController>();
            combat = GetComponent<CurlingRoyale.Combat.StoneCombat>();
            EnsureChargeRing();
        }

        void OnEnable()
        {
            // Если GameManager уже есть, подпишемся.
            if (GameManager.Instance != null)
                GameManager.Instance.onStateChanged += OnGameStateChanged;
            EnsureChargeRing();
            SetRingActive(false);
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

            // Логика 'целиться в спину':
            //   -- Подвижная цель: целимся в точку на leadDistance ПОЗАДИ неё (против направления движения).
            //      Камень летит через позицию цели, попадает ей в спину.
            //   -- Стоящая цель: целимся прямо в неё (фронтальный удар, низкий урон).
            Vector2 toTarget = (Vector2)currentTarget.position - (Vector2)transform.position;
            Vector2 aimDir;

            var targetRb = currentTarget.GetComponent<Rigidbody2D>();
            if (targetRb != null && targetRb.linearVelocity.sqrMagnitude > 0.01f)
            {
                Vector2 backTarget = (Vector2)currentTarget.position
                                     - targetRb.linearVelocity.normalized * leadDistance;
                aimDir = (backTarget - (Vector2)transform.position).normalized;
            }
            else
            {
                aimDir = toTarget.normalized;
            }

            float spread = Random.Range(-aimSpreadDegrees, aimSpreadDegrees);
            currentDirection = (Quaternion.Euler(0, 0, spread) * (Vector3)aimDir).normalized;
            aimTimer = Random.Range(0.12f, 0.35f);
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

        // ─── Ring (программное создание) ──────────────────────────

        private void EnsureChargeRing()
        {
            if (chargeRingRenderer != null) return;
            var go = new GameObject("BotChargeRing");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            // Sprite 1x1 при PPU=100 → 0.01 world units. Умножаем желаемый радиус на 100.
            go.transform.localScale = Vector3.one * (ringStartRadius * 100f);
            chargeRingRenderer = go.AddComponent<SpriteRenderer>();
            chargeRingRenderer.sprite = GetOrCreateRingSprite();
            chargeRingRenderer.sortingOrder = ringSortingOrder;
            chargeRingRenderer.color = ringMinColor;
        }

        private static Sprite cachedRingSprite;

        private static Sprite GetOrCreateRingSprite()
        {
            if (cachedRingSprite != null) return cachedRingSprite;
            var tex = Texture2D.whiteTexture;
            cachedRingSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
            cachedRingSprite.name = "BotRingSprite";
            return cachedRingSprite;
        }

        private void SetRingActive(bool active)
        {
            if (chargeRingRenderer != null && chargeRingRenderer.gameObject.activeSelf != active)
                chargeRingRenderer.gameObject.SetActive(active);
        }

        private void UpdateRingVisual()
        {
            if (chargeRingRenderer == null) return;
            float t = Mathf.Clamp01((Time.time - chargeStartTime) / Mathf.Max(0.01f, chargeDuration));
            float radius = Mathf.Lerp(ringStartRadius, ringEndRadius, t);
            // 1×1 sprite при PPU=100, мир 0.01; умножаем радиус на 100.
            float scale = radius * 100f;
            chargeRingRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            chargeRingRenderer.color = Color.Lerp(ringMinColor, ringMaxColor, t);
        }
    }
}
