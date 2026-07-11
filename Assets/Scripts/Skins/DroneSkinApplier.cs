using UnityEngine;
using UnityEngine.UI;

namespace CurlingRoyale.Skins
{
    /// <summary>
    /// Применяет DroneSkin к дрону: раскрашивает Frame / Blades / Core / AimArrow / ChargeRing.
    /// Ссылается на SpriteRenderer'ы дочерних объектов + UI Image кольца.
    ///
    /// Использование:
    ///   1. Повесить на root дрона (там же где PlayerController / BotController).
    ///   2. Перетащить в inspector ссылки на Frame, Blades_Up, Blades_Bot, Core, AimArrowSprite, ChargeRingFill.
    ///   3. Вызвать ApplySkin(skin) — скин применится.
    ///
    /// Если ссылки не заданы — applier сам найдёт дочерние объекты по имени.
    /// </summary>
    [DisallowMultipleComponent]
    public class DroneSkinApplier : MonoBehaviour
    {
        [Header("Дети (найдутся автоматически если пусто)")]
        [SerializeField] private SpriteRenderer frameRenderer;
        [SerializeField] private SpriteRenderer bladesUpRenderer;
        [SerializeField] private SpriteRenderer bladesBotRenderer;
        [SerializeField] private SpriteRenderer coreRenderer;
        [SerializeField] private SpriteRenderer aimArrowRenderer;
        [SerializeField] private Image chargeRingFill;

        [Header("Текущий скин (read-only)")]
        [SerializeField] private DroneSkin currentSkin;

        public DroneSkin CurrentSkin => currentSkin;

        void Awake()
        {
            AutoFind();
        }

        void Start()
        {
            // По умолчанию НЕ применяем скин автоматически. Дроны остаются с prefab-цветами.
            // Чтобы включить скины -- нужно явно вызвать ApplySkin() (например, из MainMenu).
            // Это сделано чтобы при добавлении скин-системы не перекрашивать все дроны в игре.
        }

        /// <summary>
        /// Подписаться на смену скина через SkinSelector. Вызывать из MainMenu/UI shop'а.
        /// </summary>
        public void EnableAutoSkin()
        {
            if (SkinSelector.Instance != null)
            {
                SkinSelector.Instance.onSkinChanged -= ApplySkin;
                SkinSelector.Instance.onSkinChanged += ApplySkin;
                if (SkinSelector.Instance.Current != null)
                    ApplySkin(SkinSelector.Instance.Current);
            }
        }

        public void DisableAutoSkin()
        {
            if (SkinSelector.Instance != null)
                SkinSelector.Instance.onSkinChanged -= ApplySkin;
        }

        /// <summary>
        /// Найти все SpriteRenderer'ы и UI Image автоматически по имени.
        /// </summary>
        public void AutoFind()
        {
            if (frameRenderer == null)      frameRenderer      = FindRenderer("Frame");
            if (bladesUpRenderer == null)   bladesUpRenderer   = FindRenderer("Blades_Up");
            if (bladesBotRenderer == null)  bladesBotRenderer  = FindRenderer("Blades_Bot");
            if (coreRenderer == null)       coreRenderer       = FindRenderer("Core");
            if (aimArrowRenderer == null)   aimArrowRenderer   = FindRenderer("AimArrowSprite");
            if (chargeRingFill == null)     chargeRingFill     = FindImage("ChargeRingFill");
        }

        private SpriteRenderer FindRenderer(string name)
        {
            var t = transform.Find(name);
            if (t == null) return null;
            return t.GetComponent<SpriteRenderer>();
        }

        private Image FindImage(string name)
        {
            var t = transform.Find(name);
            if (t == null) return null;
            return t.GetComponent<Image>();
        }

        /// <summary>
        /// Применить скин ко всем частям. Если skin == null — сбрасывает цвета на дефолтные.
        /// </summary>
        public void ApplySkin(DroneSkin skin)
        {
            currentSkin = skin;
            if (skin == null) return;

            if (frameRenderer != null)     frameRenderer.color     = skin.frameColor;
            if (bladesUpRenderer != null)  bladesUpRenderer.color  = skin.bladesColor;
            if (bladesBotRenderer != null) bladesBotRenderer.color = skin.bladesColor;
            if (coreRenderer != null)      coreRenderer.color      = skin.coreColor;
            if (aimArrowRenderer != null)  aimArrowRenderer.color  = skin.aimArrowColor;
            // chargeRingFill.color меняется в UpdateChargeRing из контроллера каждый кадр,
            // поэтому здесь ставим только текущий "ready" цвет как базу.
            if (chargeRingFill != null)    chargeRingFill.color    = skin.ringReadyColor;
        }

        /// <summary>
        /// Получить цвета кольца для контроллера. Контроллер должен звать это и подставлять
        /// вместо своих chargeReadyColor / chargeFiringColor.
        /// </summary>
        public Color GetRingReadyColor()
        {
            return currentSkin != null ? currentSkin.ringReadyColor : new Color(0.2f, 1f, 0.3f, 0.85f);
        }

        public Color GetRingFiringColor()
        {
            return currentSkin != null ? currentSkin.ringFiringColor : new Color(1f, 0.25f, 0.25f, 1f);
        }
    }
}
