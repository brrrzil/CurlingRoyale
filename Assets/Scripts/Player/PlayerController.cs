using UnityEngine;
using UnityEngine.InputSystem;
using CurlingRoyale.Combat;

namespace CurlingRoyale.Player
{
    /// <summary>
    /// Управление камнем игрока: зажатие ЛКМ/тача — зарядка, отпускание — разгон.
    /// Использует New Input System.
    /// Перезарядка — через ReloadController (1 сек после остановки).
    /// Charge ring виден только когда IsReady && не заряжаем.
    /// </summary>
    [RequireComponent(typeof(CustomPhysicsBody))]
    [RequireComponent(typeof(ReloadController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Параметры удара")]
        public float minForce = 3f;
        public float maxForce = 16f;
        public float maxChargeTime = 2f;

        [Header("Визуал")]
        [Tooltip("SpriteRenderer с Drone_Arrow. Появляется только во время зарядки, направлена к курсору.")]
        public SpriteRenderer aimArrowSprite;
        [Tooltip("Image (UnityEngine.UI) с Type=Filled, FillMethod=Radial360, Clockwise=true. " +
                 "Использует Drone_ChargeWedge.png. Заполняется зелёным (ready) / красным (charging) " +
                 "по часовой стрелке.")]
        public UnityEngine.UI.Image chargeRingFill;
        [Tooltip("SpriteRenderer (опционально) — фон для ring (полное кольцо). Оставь null если не нужно.")]
        public SpriteRenderer chargeRingBackground;
        [Range(0.05f, 0.6f)] public float chargeRingRadius = 0.45f; // в world units
        public Color chargeReadyColor = new Color(0.2f, 1f, 0.3f, 0.85f); // green
        public Color chargeFiringColor = new Color(1f, 0.2f, 0.2f, 0.95f); // red
        [Tooltip("Длина стрелки в world units. Если 0 -- автоматически по дистанции до pointer.")]
        public float aimArrowMaxLength = 5f;

        [Header("Звук зарядки")]
        [Tooltip("AudioSource для проигрывания зарядки. Если null -- GetComponent.")]
        [SerializeField] private AudioSource chargeAudioSource;
        [Tooltip("Клип, играемый при зарядке. Должен быть loop=true.")]
        [SerializeField] private AudioClip chargeLoopClip;
        [Range(0f, 1f)] [SerializeField] private float chargeLoopVolume = 0.4f;

        private CustomPhysicsBody physicsBody;
        private ReloadController reload;
        private CurlingRoyale.Combat.StoneCombat combat;
        private Vector2 direction;
        private float chargeStartTime;
        private bool isCharging;
        private Camera mainCam;
        private bool wasDeadLastUpdate;

        // ─── Init ──────────────────────────────────────────────────

        void Awake()
        {
            physicsBody = GetComponent<CustomPhysicsBody>();
            reload = GetComponent<ReloadController>();
            combat = GetComponent<CurlingRoyale.Combat.StoneCombat>();
            mainCam = Camera.main;
            if (chargeAudioSource == null) chargeAudioSource = GetComponent<AudioSource>();
            HideChargeVisual();
        }

        void Start()
        {
            // При спауне камня — убедимся, что он стартует готовым.
            reload.ForceReady();
            ShowChargeVisualIfReady();
        }

        // ─── Update ────────────────────────────────────────────────

        void Update()
        {
            // Определяем переход: был мёртв -> стал жив. Это момент возрождения (после Restart).
            bool isDead = combat != null && combat.IsDead;
            if (wasDeadLastUpdate && !isDead) ResetPlayerState();
            wasDeadLastUpdate = isDead;

            // Мёртвый игрок не может управлять камнем. После ResetToOriginal -- currentHP>0
            // снова, и Update возобновится.
            if (isDead) return;

            if (mainCam == null) return;

            Vector2? pointerWorld = GetPointerWorldPosition();
            if (!pointerWorld.HasValue) { UpdateChargeRing(); return; }

            // Старт зарядки — только если ready.
            if (!isCharging && reload.IsReady && WasPointerPressedThisFrame())
            {
                if (Vector2.Distance(pointerWorld.Value, transform.position) < 1f)
                {
                    StartCharge();
                }
            }

            if (isCharging)
            {
                UpdateChargeVisual(pointerWorld.Value);
            }

            if (isCharging && WasPointerReleasedThisFrame())
            {
                ReleaseCharge(pointerWorld.Value);
            }

            UpdateChargeRing();
        }

        // ─── Зарядка ──────────────────────────────────────────────

        private void StartCharge()
        {
            isCharging = true;
            chargeStartTime = Time.time;

            if (aimArrowSprite != null) aimArrowSprite.gameObject.SetActive(true);

            if (chargeLoopClip != null && chargeAudioSource != null)
            {
                chargeAudioSource.clip = chargeLoopClip;
                chargeAudioSource.loop = true;
                chargeAudioSource.volume = chargeLoopVolume;
                chargeAudioSource.Play();
            }
        }

        private void ReleaseCharge(Vector2 pointerWorld)
        {
            isCharging = false;
            float chargeTime = Mathf.Min(Time.time - chargeStartTime, maxChargeTime);
            float force = Mathf.Lerp(minForce, maxForce, chargeTime / maxChargeTime);

            Vector2 pullDir = pointerWorld - (Vector2)transform.position;
            Vector2 finalDir = -pullDir.normalized;

            physicsBody.ApplyForce(finalDir, force);
            HideChargeVisual();
            // Stop charge loop.
            if (chargeAudioSource != null && chargeAudioSource.isPlaying)
                chargeAudioSource.Stop();
            // ReloadController автоматически перейдёт в IsReady=false после ApplyForce.
        }

        /// <summary>
        /// Сбросить локальное состояние при возрождении (IsDead перешёл с true на false).
        /// Сбрасывает зарядку/таймеры/визуал/аудио + форсирует ReloadController в Ready.
        /// </summary>
        private void ResetPlayerState()
        {
            isCharging = false;
            chargeStartTime = 0f;
            direction = Vector2.zero;
            HideChargeVisual();
            if (chargeAudioSource != null && chargeAudioSource.isPlaying) chargeAudioSource.Stop();
            if (reload != null) reload.ForceReady();
            UpdateChargeRing();
        }

        private void UpdateChargeVisual(Vector2 pointerWorld)
        {
            Vector2 pullDir = pointerWorld - (Vector2)transform.position;
            direction = -pullDir.normalized;
            float chargeTime = Mathf.Min(Time.time - chargeStartTime, maxChargeTime);
            float t = chargeTime / maxChargeTime;

            // Arrow: SpriteRenderer с Drone_Arrow.png. Sprite pivot = left.
            // transform.position = drone center. right = направление. scale.x = длина.
            if (aimArrowSprite != null && aimArrowSprite.sprite != null)
            {
                float dist = pullDir.magnitude;
                float length = aimArrowMaxLength > 0f ? aimArrowMaxLength : dist;
                float worldWidth = aimArrowSprite.sprite.bounds.size.x;
                float scaleX = worldWidth > 0.001f ? length / worldWidth : length;
                if (dist < length)
                    scaleX = worldWidth > 0.001f ? dist / worldWidth : dist;
                aimArrowSprite.transform.position = transform.position;
                aimArrowSprite.transform.right = direction;
                aimArrowSprite.transform.localScale = new Vector3(scaleX, 1f, 1f);
                // Цвет: зелёный -> красный по мере зарядки.
                var sr = aimArrowSprite.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = Color.Lerp(chargeReadyColor, chargeFiringColor, t);
            }
        }

        private void HideChargeVisual()
        {
            if (aimArrowSprite != null) aimArrowSprite.gameObject.SetActive(false);
        }

        private void UpdateChargeRing()
        {
            // Управляет видимостью/цветом/fillAmount chargeRingFill (UnityEngine.UI.Image).
            // 3 состояния:
            //   -- isCharging:   fillAmount = chargeProgress (0..1), color = firing (red)
            //   -- !isCharging && reload.IsReady: fillAmount = 1, color = ready (green)
            //   -- !isCharging && !reload.IsReady (cooldown): hide ring entirely
            if (chargeRingFill != null)
            {
                if (isCharging)
                {
                    float t = Mathf.Clamp01((Time.time - chargeStartTime) / maxChargeTime);
                    chargeRingFill.gameObject.SetActive(true);
                    chargeRingFill.fillAmount = t;
                    Color c = chargeFiringColor;
                    c.a = 1f;
                    chargeRingFill.color = c;
                }
                else if (reload != null && reload.IsReady)
                {
                    chargeRingFill.gameObject.SetActive(true);
                    chargeRingFill.fillAmount = 1f;
                    Color c = chargeReadyColor;
                    c.a = 0.85f;
                    chargeRingFill.color = c;
                }
                else
                {
                    chargeRingFill.gameObject.SetActive(false);
                }
            }
            if (chargeRingBackground != null)
            {
                bool show = isCharging || (reload != null && reload.IsReady);
                if (chargeRingBackground.gameObject.activeSelf != show)
                    chargeRingBackground.gameObject.SetActive(show);
            }
        }

        private void ShowChargeVisualIfReady()
        {
            UpdateChargeRing();
        }

        // ─── Input ─────────────────────────────────────────────────

        private Vector2? GetPointerWorldPosition()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null) return null;
            Vector2 screen = pointer.position.ReadValue();
            return mainCam.ScreenToWorldPoint(screen);
        }

        private bool WasPointerPressedThisFrame()
        {
            if (Mouse.current != null) return Mouse.current.leftButton.wasPressedThisFrame;
            if (Pointer.current is Touchscreen ts) return ts.primaryTouch.press.wasPressedThisFrame;
            return false;
        }

        private bool WasPointerReleasedThisFrame()
        {
            if (Mouse.current != null) return Mouse.current.leftButton.wasReleasedThisFrame;
            if (Pointer.current is Touchscreen ts) return ts.primaryTouch.press.wasReleasedThisFrame;
            return false;
        }
    }
}
