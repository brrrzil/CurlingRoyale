using UnityEngine;

namespace CurlingRoyale.Arena
{
    /// <summary>
    /// Камера слежения за целевым объектом (обычно PlayerStone).
    /// Если target == null, ищем в сцене PlayerController (точнее -- его GameObject).
    /// Если ничего не нашли -- камера неподвижна.
    ///
    /// Поддерживает dead-time fallback: если target уничтожен (IsDead),
    /// камера переходит на центр арены (плавно).
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Цель")]
        [Tooltip("Конкретный Transform для слежения. Если null -- ищем PlayerController в сцене.")]
        [SerializeField] private Transform target;

        [Header("Сглаживание")]
        [Tooltip("Чем больше -- тем медленнее камера. 0 -- жёсткое следование.")]
        [Min(0f)] [SerializeField] private float smoothTime = 0.18f;

        [Header("Кадрирование")]
        [Tooltip("Смещение от цели. Z = -10 для ортографической камеры top-down.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

        [Header("Dead-target fallback")]
        [Tooltip("Если цель мертва -- камера смотрит на эту позицию (центр арены).")]
        [SerializeField] private Vector2 deadTargetCenter = Vector2.zero;

        [Tooltip("Задержка переключения на dead-центр после смерти цели (сек).")]
        [Min(0f)] [SerializeField] private float deadTargetDelay = 1.0f;

        private Camera cam;
        private Vector3 velocity;
        private float deadTargetTime = -1f;

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
        }

        void Start()
        {
            if (target == null)
            {
                var pc = FindFirstObjectByType<CurlingRoyale.Player.PlayerController>(FindObjectsInactive.Include);
                if (pc != null) target = pc.transform;
            }
        }

        void LateUpdate()
        {
            // Куда реально лететь камере.
            Vector3 goal;
            if (target != null)
            {
                // Проверим жив ли таргет (StoneCombat есть, IsDead есть).
                var combat = target.GetComponent<CurlingRoyale.Combat.StoneCombat>();
                bool isDead = combat != null && combat.IsDead;
                if (isDead)
                {
                    if (deadTargetTime < 0f) deadTargetTime = Time.time;
                    if (Time.time - deadTargetTime >= deadTargetDelay)
                    {
                        goal = new Vector3(deadTargetCenter.x, deadTargetCenter.y, offset.z);
                    }
                    else
                    {
                        // Ещё идёт delay -- продолжаем кадрировать около target (который стоит).
                        goal = target.position + offset;
                    }
                }
                else
                {
                    deadTargetTime = -1f;
                    goal = target.position + offset;
                }
            }
            else
            {
                goal = new Vector3(deadTargetCenter.x, deadTargetCenter.y, offset.z);
            }

            transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
        }
    }
}
