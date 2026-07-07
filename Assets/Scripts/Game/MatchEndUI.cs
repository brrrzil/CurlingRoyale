using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CurlingRoyale.Combat;

namespace CurlingRoyale.Game
{
    /// <summary>
    /// End-game экран: показывает победителя (TextMeshPro) и кнопку Restart.
    ///
    /// Подключение:
    /// 1. Повесь скрипт на любой GameObject (часто -- сама Panel).
    /// 2. Если Inspector-поля НЕ заданы, скрипт попробует найти:
    ///    -- endScreenPanel: первый ребёнок Canvas.
    ///    -- winnerText: первая TMP_Text в сцене.
    ///    -- restartButton: первая Button в сцене.
    /// 3. Можно не задавать -- скрипт работает с минимальной диагностикой (Warning в Console).
    /// </summary>
    public class MatchEndUI : MonoBehaviour
    {
        [Header("UI (опционально, авто-фоллбэк)")]
        [Tooltip("Panel с TextMeshPro + Button. Если null -- берём первый Canvas на сцене.")]
        [SerializeField] private GameObject endScreenPanel;

        [Tooltip("TextMeshPro 'Победил: ...' / 'Ничья'. Если null -- первая TMP_Text на сцене.")]
        [SerializeField] private TMP_Text winnerText;

        [Tooltip("Button для Restart. Если null -- первая Button на сцене.")]
        [SerializeField] private Button restartButton;

        private bool subscribed = false;

        void Awake()
        {
            // Авто-фоллбэк если пользователь не задал поля в инспекторе.
            if (endScreenPanel == null)
            {
                var canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null) { endScreenPanel = canvas.gameObject; Debug.LogWarning($"[MatchEndUI] endScreenPanel не задан -- использую первую Canvas ({canvas.name})."); }
            }
            if (winnerText == null)
            {
                var txt = FindFirstObjectByType<TMP_Text>();
                if (txt != null) { winnerText = txt; Debug.LogWarning($"[MatchEndUI] winnerText не задан -- использую первый TMP_Text ({txt.name})."); }
            }
            if (restartButton == null)
            {
                var btn = FindFirstObjectByType<Button>();
                if (btn != null) { restartButton = btn; Debug.LogWarning($"[MatchEndUI] restartButton не задан -- использую первую Button ({btn.name})."); }
            }
        }

        void Start()
        {
            // Спрятать панель в начале матча. Если панель == this.gameObject -- пропускаем
            // (иначе деактивируем свой же MonoBehaviour).
            if (endScreenPanel != null && endScreenPanel != gameObject && endScreenPanel.activeSelf)
                endScreenPanel.SetActive(false);

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartPressed);
                restartButton.onClick.AddListener(OnRestartPressed);
            }
            else
            {
                Debug.LogWarning("[MatchEndUI] restartButton всё ещё null -- OnRestartPressed не подключён.");
            }
        }

        void Update()
        {
            if (!subscribed) TrySubscribe();
        }

        void TrySubscribe()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.onStateChanged -= OnStateChanged;
            GameManager.Instance.onStateChanged += OnStateChanged;
            subscribed = true;
        }

        void OnEnable()
        {
            // Если панель была скрыта в Start, а теперь включена -- пробуем подписаться ещё раз.
            if (!subscribed) TrySubscribe();
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.onStateChanged -= OnStateChanged;
        }

        void OnStateChanged(GameManager.MatchState newState)
        {
            Debug.Log($"[MatchEndUI] state -> {newState}, panel={(endScreenPanel != null ? endScreenPanel.name : "null")}, subscribed={subscribed}");

            if (endScreenPanel == null) return;

            if (newState == GameManager.MatchState.MatchEnd)
            {
                ShowEndScreen();
            }
            else if (newState == GameManager.MatchState.InProgress)
            {
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
            else
            {
                Debug.LogWarning("[MatchEndUI] winnerText == null -- невозможно показать имя победителя.");
            }

            // Активируем ПОСЛЕ изменения текста -- иначе порядок рендера/расчёта layout'а может не успеть.
            if (!endScreenPanel.activeSelf)
            {
                endScreenPanel.SetActive(true);
                Debug.Log($"[MatchEndUI] panel activated: name={endScreenPanel.name}, active={endScreenPanel.activeInHierarchy}");
            }
        }

        void OnRestartPressed()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[MatchEndUI] GameManager.Instance == null -- restart отменён.");
                return;
            }

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
