using UnityEngine;

namespace CurlingRoyale.Combat
{
    /// <summary>
    /// Конфигурация урона при столкновении камней.
    /// Создавать через меню: Assets → Create → Curling Royale → Damage Config.
    /// Вешать на камень через инспектор (поле StoneCombat).
    /// </summary>
    [CreateAssetMenu(fileName = "DamageConfig", menuName = "Curling Royale/Damage Config", order = 0)]
    public class DamageConfig : ScriptableObject
    {
        [Header("Базовые значения HP")]
        [Tooltip("Максимальное здоровье камня.")]
        [Min(1)] public int maxHealth = 100;

        [Header("Урон по углу атаки (градусы от 0 до 180)")]
        [Tooltip("Урон при ударе 'в спину' (170°–180°).")]
        [Min(0)] public int backDamage = 40;

        [Tooltip("Урон при ударе 'в бок' (60°–120°).")]
        [Min(0)] public int sideDamage = 20;

        [Tooltip("Урон при ударе 'в лоб' (0°–30°). Обычно 0 — камень отскакивает без урона.")]
        [Min(0)] public int frontDamage = 0;

        [Header("Пороги углов")]
        [Tooltip("Граница 'лоб → бок'. Ниже этого угла считается лоб (минимум 0 урона).")]
        [Range(0f, 90f)] public float frontAngleThreshold = 30f;

        [Tooltip("Граница 'бок → спина'. Выше этого угла считается спина (максимум урона).")]
        [Range(90f, 180f)] public float backAngleThreshold = 170f;

        [Header("Физика столкновения")]
        [Tooltip("Минимальная скорость атакующего, чтобы урон вообще считался. Ниже — просто отскок.")]
        [Min(0f)] public float minAttackSpeed = 3f;

        [Tooltip("Скорость-референс: при ней damage-формула даёт базовый урон (speedFactor = 1.0). " +
                 "Быстрая атака выше referenceAttackSpeed увеличивает урон пропорционально.")]
        [Min(0.1f)] public float referenceAttackSpeed = 10f;

        [Tooltip("Множитель максимального усиления урона от скорости. speedFactor clamp [0..maxSpeedMultiplier]. " +
                 "1.5 = лёгкое усиление, 2.0 = сильно быстрее = больнее.")]
        [Min(1f)] public float maxSpeedMultiplier = 1.5f;

        [Tooltip("Кулдаун между получениями урона в секундах. Защита от двойного урона при одном контакте.")]
        [Min(0f)] public float damageCooldown = 0.4f;

        /// <summary>
        /// Рассчитать урон по углу между направлением движения жертвы и направлением удара.
        /// Угол 0° = удар в лоб, 180° = удар в спину.
        /// </summary>
        public int CalculateDamage(float angleDegrees)
        {
            // Лоб
            if (angleDegrees <= frontAngleThreshold)
                return frontDamage;

            // Спина
            if (angleDegrees >= backAngleThreshold)
                return backDamage;

            // Бок — плоский урон в диапазоне [frontAngle, 90°] ∪ [90°, backAngle]
            // Внутри бокового диапазона возвращаем sideDamage.
            // Между frontAngle и первым боковым — линейная интерполяция.
            // Между последним боковым и backAngle — линейная интерполяция.
            float sideLow = frontAngleThreshold;
            float sideHigh = backAngleThreshold;

            if (angleDegrees >= sideLow && angleDegrees <= sideHigh)
            {
                // Плавная интерполяция в переходных зонах,
                // плоский sideDamage в середине.
                if (angleDegrees <= sideLow + 30f)
                    return Mathf.RoundToInt(Mathf.Lerp(frontDamage, sideDamage, (angleDegrees - sideLow) / 30f));
                if (angleDegrees >= sideHigh - 50f)
                    return Mathf.RoundToInt(Mathf.Lerp(sideDamage, backDamage, (angleDegrees - (sideHigh - 50f)) / 50f));
                return sideDamage;
            }

            return 0;
        }
    }
}