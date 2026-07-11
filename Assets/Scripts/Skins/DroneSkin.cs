using UnityEngine;

namespace CurlingRoyale.Skins
{
    /// <summary>
    /// Скин дрона: набор цветов для Frame / Blades / Core / AimArrow / ChargeRing.
    /// Никакой логики — просто данные. Применяется через DroneSkinApplier.
    ///
    /// Создание: Assets → Create → Curling Royale → Drone Skin.
    /// </summary>
    [CreateAssetMenu(menuName = "Curling Royale/Drone Skin", fileName = "DroneSkin_New")]
    public class DroneSkin : ScriptableObject
    {
        [Header("Идентификация")]
        [Tooltip("Имя скина, показывается в UI магазина.")]
        public string skinName = "Default";

        [Tooltip("ID для сохранения выбора (PlayerPrefs / cloud).")]
        public string skinId = "default";

        [Header("Цвета дрона")]
        public Color frameColor    = new Color(0.4f, 0.7f, 0.95f, 1f); // металлический синий
        public Color bladesColor   = new Color(0.2f, 0.9f, 0.4f, 1f);  // неоновый зелёный
        public Color coreColor     = new Color(1f, 0.9f, 0.3f, 1f);    // тёплый жёлтый
        public Color aimArrowColor = new Color(1f, 1f, 1f, 1f);

        [Header("Кольцо зарядки")]
        public Color ringReadyColor  = new Color(0.2f, 1f, 0.3f, 0.85f);   // green
        public Color ringFiringColor = new Color(1f, 0.25f, 0.25f, 1f);    // red

        [Header("Эффекты (опционально, не используется пока)")]
        public Color trailColor = new Color(1f, 1f, 1f, 0.4f);
    }
}
