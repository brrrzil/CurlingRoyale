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
        [Tooltip("UI Image (UnityEngine.UI) с Type=Filled, FillMethod=Radial360, Clockwise=true. " +
                 "Использует Drone_ChargeFill.png. fillAmount = t при зарядке (0..1).")]
        [SerializeField] private UnityEngine.UI.Image chargeRingFill;
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
        private bool visualsAutoCreated;
        // Базовый localScale стрелки (из префаба). Используем как основу,
        // к которой применяем charge multiplier в UpdateChargeVisual.
        private Vector3 aimArrowBaseScale = Vector3.one;

        // Кэшированные runtime-спрайты (загружаются из Resources, если есть).
        private static Sprite cachedArrowSprite;

        // ─── Init ──────────────────────────────────────────────────

        void Awake()
        {
            physicsBody = GetComponent<CustomPhysicsBody>();
            reload = GetComponent<ReloadController>();
            combat = GetComponent<CurlingRoyale.Combat.StoneCombat>();
            mainCam = Camera.main;
            if (chargeAudioSource == null) chargeAudioSource = GetComponent<AudioSource>();
            EnsureVisuals();
            // Запоминаем базовый размер стрелки, чтобы не перетирать то, что юзер поставил в префабе.
            if (aimArrowSprite != null) aimArrowBaseScale = aimArrowSprite.transform.localScale;
            HideChargeVisual();
        }

        /// <summary>
        /// Auto-find стрелки прицеливания. chargeRingFill (UI Image) берётся только из префаба.
        /// </summary>
        private void EnsureVisuals()
        {
            // Стрелка прицеливания (auto-create fallback, если не привязана в префабе).
            if (aimArrowSprite == null)
            {
                var t = transform.Find("AimArrow");
                if (t != null) aimArrowSprite = t.GetComponent<SpriteRenderer>();
            }
            if (aimArrowSprite == null)
            {
                var go = new GameObject("AimArrow", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one;
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = LoadSprite("Drone/Drone_Arrow", ref cachedArrowSprite);
                sr.sortingOrder = 30;
                sr.color = Color.white;
                aimArrowSprite = sr;
                go.SetActive(false);
                visualsAutoCreated = true;
            }
        }

        /// <summary>
        /// Загрузить спрайт из Resources. Если не нашли — fallback на 1x1 white (будет виден как квадрат).
        /// </summary>
        private static Sprite LoadSprite(string resourcePath, ref Sprite cache)
        {
            if (cache != null) return cache;
            cache = Resources.Load<Sprite>(resourcePath);
            if (cache != null) return cache;
            // Fallback: 1x1 white square.
            var tex = Texture2D.whiteTexture;
            cache = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
            cache.name = resourcePath + "_AutoFallback";
            Debug.LogWarning($"[PlayerController] Sprite '{resourcePath}' не найден в Resources/ -- используется белый квадрат. " +
                             $"Скопируй Drone_Arrow.png и Drone_ChargeWedge.png в Assets/Resources/{resourcePath.Replace("Drone/", "Drone/")}.");
            return cache;
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

            // ─── DEBUG: детальная диагностика charge ring (временный код) ──────────
            if (Time.frameCount % 30 == 0) // раз в ~0.5 сек
            {
                Debug.Log($"[PlayerDiag] isCharging={isCharging} reload.IsReady={reload?.IsReady} pointer={pointerWorld?.ToString() ?? "NULL"} ringFill={(chargeRingFill != null ? "OK" : "NULL")} dist={(pointerWorld.HasValue ? Vector2.Distance(pointerWorld.Value, transform.position).ToString("F2") : "-")}");
            }
            // ────────────────────────────────────────────────────────────────────────

            if (!pointerWorld.HasValue) { UpdateChargeRing(); return; }

            // Старт зарядки — только если ready.
            if (!isCharging && reload.IsReady && WasPointerPressedThisFrame())
            {
                if (Vector2.Distance(pointerWorld.Value, transform.position) < 1f)
                {
                    Debug.Log("[PlayerDiag] StartCharge() — клик в радиусе 1 ед.");
                    StartCharge();
                }
                else
                {
                    Debug.Log($"[PlayerDiag] Клик СЛИШКОМ ДАЛЕКО ({Vector2.Distance(pointerWorld.Value, transform.position):F2} > 1)");
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
            if (pullDir.sqrMagnitude < 0.0001f) return;
            direction = -pullDir.normalized;
            float chargeTime = Mathf.Min(Time.time - chargeStartTime, maxChargeTime);
            float t = chargeTime / maxChargeTime;

            // Arrow: SpriteRenderer с Drone_Arrow.png (стилизованный > chevron).
            // Sprite квадратный, pivot по центру. Размещается на ФИКСИРОВАННОМ расстоянии от дрона
            // (не следует за курсором), повернут в сторону удара (direction = -pullDir).
            if (aimArrowSprite != null && aimArrowSprite.sprite != null)
            {
                // Фиксированная дистанция от дрона до стрелки (немного за ring).
                float fixedDist = chargeRingRadius + 0.25f;
                // Если задана aimArrowMaxLength > 0 -- используем её (для дальнобойных стрелок).
                if (aimArrowMaxLength > 0f) fixedDist = aimArrowMaxLength;
                // Позиция: drone center + direction * fixedDist.
                Vector3 pos = transform.position + (Vector3)(direction * fixedDist);
                aimArrowSprite.transform.position = pos;
                // Chevron смотрит в direction (transform.right = direction → наконечник в направлении удара).
                aimArrowSprite.transform.right = direction;
                // Размер: уважаем localScale из префаба, применяем мультипликатор 1.0→1.3 по зарядке.
                float chargeMul = Mathf.Lerp(1f, 1.3f, t);
                aimArrowSprite.transform.localScale = aimArrowBaseScale * chargeMul;
                // Цвет: не тинтируем (спрайт уже cyan) — только меняем alpha по зарядке (1.0→0.7),
                // чтобы было видно прогресс без потери яркости.
                var sr = aimArrowSprite.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Color baseCol = sr.color;
                    baseCol.a = Mathf.Lerp(1f, 0.7f, t);
                    sr.color = baseCol;
                }
            }
        }

        private void HideChargeVisual()
        {
            if (aimArrowSprite != null) aimArrowSprite.gameObject.SetActive(false);
        }

        private void UpdateChargeRing()
        {
            // Кольцо зарядки (chargeRingFill UI Image, Radial360 Clockwise=true).
            //   -- Ready:      full (1.0), зелёный -- игрок может ударить
            //   -- Charging:   опустошается по часовой 1→0, цвет green→red
            //   -- Cooldown:   скрыто
            // При t=0 зарядки fillAmount=1 (кольцо ВИДНО), не 0. Иначе оно
            // «исчезает» в первом кадре, что сбивает с толку.
            if (chargeRingFill == null) return;

            if (isCharging)
            {
                float t = Mathf.Clamp01((Time.time - chargeStartTime) / maxChargeTime);
                if (!chargeRingFill.gameObject.activeSelf)
                    chargeRingFill.gameObject.SetActive(true);
                chargeRingFill.fillAmount = 1f - t;
                Color c = chargeFiringColor;
                c.a = 1f;
                chargeRingFill.color = c;
            }
            else if (reload != null && reload.IsReady)
            {
                if (!chargeRingFill.gameObject.activeSelf)
                    chargeRingFill.gameObject.SetActive(true);
                chargeRingFill.fillAmount = 1f;
                Color c = chargeReadyColor;
                c.a = 0.85f;
                chargeRingFill.color = c;
            }
            else
            {
                if (chargeRingFill.gameObject.activeSelf)
                    chargeRingFill.gameObject.SetActive(false);
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
