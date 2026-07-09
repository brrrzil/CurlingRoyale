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
        private bool visualsAutoCreated;
        // Базовый localScale стрелки (из префаба). Используем как основу,
        // к которой применяем charge multiplier в UpdateChargeVisual.
        private Vector3 aimArrowBaseScale = Vector3.one;

        // Кэшированные runtime-спрайты (загружаются из Resources, если есть).
        private static Sprite cachedArrowSprite;
        private static Sprite cachedRingFillSprite;
        private static Sprite cachedRingBgSprite;

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
        /// Auto-find или auto-create визуалов: стрелка прицеливания + ring зарядки.
        /// Правило проекта: юзер делает wiring вручную, но мы страхуемся fallback'ом.
        /// Поиск по имени (transform.Find("AimArrow"), и т.д.) — не трогает чужие GO.
        /// </summary>
        private void EnsureVisuals()
        {
            // 1. Стрелка прицеливания.
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

            // 2. Ring: Canvas > Fill (UI Image) + Background (SpriteRenderer).
            if (chargeRingFill == null || chargeRingBackground == null)
            {
                var canvasT = transform.Find("ChargeRingCanvas");
                if (canvasT != null)
                {
                    if (chargeRingFill == null)
                    {
                        var fillT = canvasT.Find("ChargeRingFill");
                        if (fillT != null) chargeRingFill = fillT.GetComponent<UnityEngine.UI.Image>();
                    }
                    if (chargeRingBackground == null)
                    {
                        var bgT = canvasT.Find("ChargeRingBackground");
                        if (bgT != null) chargeRingBackground = bgT.GetComponent<SpriteRenderer>();
                    }
                }

                if (chargeRingFill == null || chargeRingBackground == null)
                {
                    // Создаём Canvas + RectTransform.
                    var canvasGo = new GameObject("ChargeRingCanvas",
                        typeof(RectTransform), typeof(Canvas));
                    canvasGo.transform.SetParent(transform, false);
                    canvasGo.transform.localPosition = Vector3.zero;
                    // Canvas в world space, scale подбираем так, чтобы 1 unit Canvas == ~100px.
                    // Для ring диаметром ~0.9 world units, RectTransform size = 90 px.
                    canvasGo.transform.localScale = Vector3.one * 0.01f;
                    var canvas = canvasGo.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvas.sortingOrder = 50;
                    var canvasRt = canvasGo.GetComponent<RectTransform>();
                    canvasRt.sizeDelta = new Vector2(chargeRingRadius * 200f, chargeRingRadius * 200f);

                    // Background: тусклое серое кольцо (SpriteRenderer под fill).
                    if (chargeRingBackground == null)
                    {
                        var bgGo = new GameObject("ChargeRingBackground", typeof(SpriteRenderer));
                        bgGo.transform.SetParent(canvasGo.transform, false);
                        bgGo.transform.localPosition = Vector3.zero;
                        bgGo.transform.localScale = Vector3.one * (chargeRingRadius * 100f);
                        var bgSr = bgGo.GetComponent<SpriteRenderer>();
                        bgSr.sprite = LoadSprite("Drone/Drone_ChargeRing_BG", ref cachedRingBgSprite);
                        bgSr.color = new Color(1f, 1f, 1f, 0.25f);
                        bgSr.sortingOrder = 51;
                        chargeRingBackground = bgSr;
                    }

                    // Fill: UI Image с radial 360.
                    if (chargeRingFill == null)
                    {
                        var fillGo = new GameObject("ChargeRingFill",
                            typeof(RectTransform), typeof(UnityEngine.UI.Image));
                        fillGo.transform.SetParent(canvasGo.transform, false);
                        var fillRt = fillGo.GetComponent<RectTransform>();
                        fillRt.anchorMin = Vector2.zero;
                        fillRt.anchorMax = Vector2.one;
                        fillRt.offsetMin = Vector2.zero;
                        fillRt.offsetMax = Vector2.zero;
                        var img = fillGo.GetComponent<UnityEngine.UI.Image>();
                        img.sprite = LoadSprite("Drone/Drone_ChargeFill", ref cachedRingFillSprite);
                        img.type = UnityEngine.UI.Image.Type.Filled;
                        img.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
                        img.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
                        img.fillClockwise = true;
                        img.fillAmount = 1f;
                        img.color = chargeReadyColor;
                        img.raycastTarget = false;
                        chargeRingFill = img;
                    }

                    visualsAutoCreated = true;
                }
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
