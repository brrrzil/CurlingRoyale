using UnityEngine;
using TMPro;

namespace CurlingRoyale.Player
{
    /// <summary>
    /// Показывает 'You were knocked out' (или своё сообщение) когда PlayerStone умирает.
    /// Появляется через configurableDelay (по умолчанию 1 сек) после смерти.
    /// Скрывается когда игрок снова жив (например, после Restart) и isAliveReshow = false.
    ///
    /// Как подключить:
    /// 1. Canvas с TMP_Text (например, на весь экран по центру).
    /// 2. Скрипт вешается на этот Canvas или рядом.
    /// 3. В инспекторе: toastText = твой TMP_Text, playerCombat = ссылка на
    ///    StoneCombat игрока (можно через FindFirstObjectByType).
    /// </summary>
    public class DefeatedToast : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("TextMeshPro для 'Defeated'. Если null -- авто-поиск в сцене.")]
        [SerializeField] private TMP_Text toastText;

        [Tooltip("StoneCombat игрока. Если null -- авто-поиск первого PlayerStone.")]
        [SerializeField] private CurlingRoyale.Combat.StoneCombat playerCombat;

        [Header("Поведение")]
        [Tooltip("Задержка перед появлением уведомления после смерти камня.")]
        [Min(0f)] [SerializeField] private float delaySeconds = 1f;

        [Tooltip("Текст уведомления.")]
        [SerializeField] private string message = "You were knocked out";

        [Tooltip("Скрывать автоматически когда игрок снова жив (например, после Restart).")]
        [SerializeField] private bool hideOnRevive = true;

        private bool subscribed = false;
        private bool isShowing = false;

        void Awake()
        {
            if (toastText == null)
                toastText = FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include);
            if (playerCombat == null)
            {
                // PlayerStone -- тот, что с PlayerController.
                var pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
                if (pc != null)
                    playerCombat = pc.GetComponentInParent<CurlingRoyale.Combat.StoneCombat>();
            }

            if (toastText != null)
                toastText.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!subscribed && playerCombat != null)
            {
                playerCombat.onDeath.RemoveListener(OnPlayerDied);
                playerCombat.onDeath.AddListener(OnPlayerDied);
                subscribed = true;
            }

            if (hideOnRevive && isShowing && playerCombat != null && !playerCombat.IsDead)
            {
                Hide();
            }
        }

        void OnDestroy()
        {
            if (playerCombat != null)
                playerCombat.onDeath.RemoveListener(OnPlayerDied);
        }

        void OnPlayerDied()
        {
            CancelInvoke(nameof(Show));
            Invoke(nameof(Show), delaySeconds);
        }

        void Show()
        {
            if (toastText == null) return;
            toastText.text = message;
            if (!toastText.gameObject.activeSelf)
                toastText.gameObject.SetActive(true);
            isShowing = true;
        }

        void Hide()
        {
            if (toastText == null) return;
            if (toastText.gameObject.activeSelf)
                toastText.gameObject.SetActive(false);
            isShowing = false;
        }
    }
}
