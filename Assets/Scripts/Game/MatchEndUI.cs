using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CurlingRoyale.Combat;

namespace CurlingRoyale.Game
{
    /// <summary>
    /// End-game экран: показывает победителя (TextMeshPro) и кнопку Restart.
    ///
    /// Как подключить:
    /// 1. Любой GameObject (часто сама Panel). Укажи endScreenPanel и winnerText(TMP).
    /// 2. Назначь restartButton -- его клик вызовет Reset.
    /// 3. GameManager.onStateChanged -> MatchEnd -- показ панели.
    /// </summary>
    public class MatchEndUI : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Panel с TextMeshPro + Button. Если ссылка == this.gameObject, " +
                 "сам скрипт НЕ деактивирует его -- это не требуется, т.к. он уже inactive в инспекторе.")]
        [SerializeField] private GameObject endScreenPanel;

        [Tooltip("TextMeshPro -- 'Победил: ...' / 'Ничья'")]
        [SerializeField] private TMP_Text winnerText;

        [Tooltip("Button с подписью 'Restart'.")]
        [SerializeField] private Button restartButton;

        [Header("Поведение")]
        [Tooltip("Задержка перед показом экрана победы/поражения после MatchEnd.")]
        [Min(0f)] [SerializeField] private float showDelaySeconds = 1f;

        private bool subscribed = false;

        void Start()
        {
            // Спрятать панель в начале матча. Если панель == this.gameObject, не трогать -- иначе
            // деактивируем свой же MonoBehaviour, и coroutines не запускаются.
            if (endScreenPanel != null && endScreenPanel != gameObject && endScreenPanel.activeSelf)
                endScreenPanel.SetActive(false);

            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestartPressed);
        }

        void Update()
        {
            // Lazy-подписка на GameManager до победного.
            if (!subscribed) TrySubscribe();
        }

        void TrySubscribe()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.onStateChanged -= OnStateChanged;
            GameManager.Instance.onStateChanged += OnStateChanged;
            subscribed = true;
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.onStateChanged -= OnStateChanged;
        }

        void OnStateChanged(GameManager.MatchState newState)
        {
            if (endScreenPanel == null) return;

            if (newState == GameManager.MatchState.MatchEnd)
            {
                // Задержка перед показом экрана -- чтобы игрок увидел момент
                // гибели/победы без рывка в UI.
                CancelInvoke(nameof(ShowEndScreen));
                Invoke(nameof(ShowEndScreen), showDelaySeconds);
            }
            else if (newState == GameManager.MatchState.InProgress)
            {
                CancelInvoke(nameof(ShowEndScreen));
                if (endScreenPanel != gameObject && endScreenPanel.activeSelf)
                    endScreenPanel.SetActive(false);
            }
        }

        void ShowEndScreen()
        {
            StoneCombat[] all = FindObjectsByType<StoneCombat>(FindObjectsSortMode.None);
            StoneCombat alive = null;
            int aliveCount = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && !all[i].IsDead)
                {
                    alive = all[i];
                    aliveCount++;
                }
            }

            if (winnerText != null)
            {
                if (aliveCount == 1)
                    winnerText.text = $"Победил: {alive.name}";
                else if (aliveCount == 0)
                    winnerText.text = "Ничья (все погибли)";
                else
                    winnerText.text = $"Матч окончен ({aliveCount} живых)";
            }

            // Активируем ПОСЛЕ изменения текста -- иначе порядок рендера/расчёта layout'а может не успеть.
            if (!endScreenPanel.activeSelf)
                endScreenPanel.SetActive(true);
        }

        void OnRestartPressed()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[MatchEndUI] GameManager.Instance == null -- restart отменён.");
                return;
            }

            // Сбросить все камни на исходные позиции (зафиксированные в StoneCombat.Awake).
            StoneCombat[] all = FindObjectsByType<StoneCombat>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s == null) continue;
                s.ResetToOriginal();
            }

            if (endScreenPanel != null && endScreenPanel != gameObject)
                endScreenPanel.SetActive(false);

            GameManager.Instance.StartMatch();
        }
    }
}
