using UnityEngine;
using CurlingRoyale.Configs;

namespace CurlingRoyale.Arena
{
    /// <summary>
    /// Визуальное масштабирование арены. Следит за полем ArenaConfig.currentRadius
    /// или за сигналом от GameManager. Scale спрайта арены = currentRadius.
    ///
    /// Если на сцене отдельный Sprite визуала арены (отличный от EdgeCollider),
    /// вешается на тот GameObject и масштабирует свой Transform.
    /// </summary>
    [ExecuteAlways]
    public class ArenaScaler : MonoBehaviour
    {
        [Tooltip("ScriptableObject с параметрами. currentRadius перезаписывается им.")]
        [SerializeField] private ArenaConfig config;

        [Tooltip("Ссылка на объект, масштаб которого менять. Если null — менять свой transform.")]
        [SerializeField] private Transform target;

        [Tooltip("Базовый радиус спрайта при scale = 1 (для пересчёта). Обычно 5 для радиуса 5.125 арены.")]
        [Min(0.1f)] [SerializeField] private float baseRadius = 5f;

        void LateUpdate()
        {
            if (config == null) return;
            var t = target != null ? target : transform;
            // scale = currentRadius / baseRadius. Если baseRadius == initialRadius, scale = 1 при старте.
            float scale = config.currentRadius / baseRadius;
            t.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
