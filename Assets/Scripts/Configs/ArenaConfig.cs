using UnityEngine;

namespace CurlingRoyale.Configs
{
    /// <summary>
    /// Конфигурация арены: размеры, shrink-параметры.
    /// Создавать через меню: Assets → Create → Curling Royale → Arena Config.
    /// Привязывать к ArenaBorder и GameManager через Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "ArenaConfig", menuName = "Curling Royale/Arena Config", order = 0)]
    public class ArenaConfig : ScriptableObject
    {
        [Header("Радиус")]
        [Tooltip("Начальный радиус арены (если не задан, берётся из ArenaBorder).")]
        [Min(0.1f)] public float initialRadius = 5.125f;

        [Tooltip("Финальный радиус арены после сжатия (в режиме shrinking).")]
        [Min(0.1f)] public float finalRadius = 2.5f;

        [Header("Shrinking (финальная фаза)")]
        [Tooltip("Когда начинать shrinking: по числу оставшихся в живых камней. " +
                 "Например, 3 = когда осталось 3 камня, начать сжатие.")]
        [Min(2)] public int shrinkStartAliveCount = 3;

        [Tooltip("Сколько секунд длится сжатие от initialRadius до finalRadius.")]
        [Min(0.5f)] public float shrinkDuration = 25f;

        [Header("Границы коллайдера")]
        [Tooltip("Сколько точек в Edge Collider 2D (больше = глаже круг).")]
        [Range(8, 256)] public int edgeColliderPoints = 64;

        [Header("Физика")]
        [Tooltip("Bounciness материала границ (если Material2D не назначен). " +
                 "Создаст PhysicsMaterial2D автоматически с этими параметрами.")]
        [Range(0f, 1.5f)] public float bounciness = 0.9f;

        [Tooltip("Friction материала границ.")]
        [Range(0f, 1f)] public float friction = 0f;

        // ─── Утилиты ───────────────────────────────────────────────────

        /// <summary>
        /// Текущий радиус (для визуализации арены).
        /// По умолчанию = initialRadius. GameManager перезаписывает при shrinking.
        /// </summary>
        [System.NonSerialized] public float currentRadius;

        void OnEnable()
        {
            currentRadius = initialRadius;
        }
    }
}
