using UnityEngine;
using UnityEngine.UI;
using CurlingRoyale.Combat;

namespace CurlingRoyale.Game
{
    /// <summary>
    /// End-game экран: показывает победителя и кнопку Restart.
    ///
    /// Как использовать:
    /// 1. Создай Canvas (ScreenSpace-Overlay).
    /// 2. Внутри -- Panel (полупрозрачный фон) с Text (winnerText) и Button (restartButton).
    /// 3. Перетащи Canvas/panel в это поле, Text/Button -- в соответствующие поля.
    /// 4. GameManager автоматически триггерит событие onStateChanged -> MatchEndState.
    ///
    /// Победитель определяется как ЕДИНСТВЕННЫЙ живой StoneCombat на момент конца матча.
    /// Если не осталось живых -- "Ничья" (или "Все погибли").
    /// </summary>
    public class MatchEndUI : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Panel с текстом + кнопкой, скрывается в начале матча.")]
        [SerializeField] private GameObject endScreenPanel;

        [Tooltip("Text -- 'Победил: ...' / 'Ничья'")]
        [SerializeField] private Text winnerText;

        [Tooltip("Button с подписью 'Restart'.")]
        [SerializeField] private Button restartButton;

        [Header("Логика")]
        [Tooltip("Камни, которые будут сброшены на исходные позиции при Restart. " +
                 "Если пусто -- перебирает все StoneCombat в сцене.")]
        [SerializeField] private Transform[] respawnPoints;

        [Tooltip("Кнопка скрытия (опционально) -- крестик.")]
        [SerializeField] private Button hideButton;

        void Start()
        {
            if (endScreenPanel != null)
                endScreenPanel.SetActive(false);

            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestartPressed);

            if (hideButton != null)
                hideButton.onClick.AddListener(() => endScreenPanel?.SetActive(false));

            // Подпишемся на GameManager (lazy -- на случай если Instance появился позже).
            Subscribe();

            // Подпишемся через корутину чтобы пережить ситуацию когда GameManager.Awake ещё не отработал.
            StartCoroutine(SubscribeWhenReady());
        }

        System.Collections.IEnumerator SubscribeWhenReady()
        {
            // Даём GameManager.StartMatch шанс отработать.
            float deadline = Time.time + 2f;
            while (GameManager.Instance == null && Time.time < deadline)
                yield return null;
            Subscribe();
        }

        void Subscribe()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.onStateChanged -= OnStateChanged;
            GameManager.Instance.onStateChanged += OnStateChanged;
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
                ShowEndScreen();
            }
            else
            {
                endScreenPanel.SetActive(false);
            }
        }

        void ShowEndScreen()
        {
            // Find alive stones.
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

            endScreenPanel.SetActive(true);
        }

        void OnRestartPressed()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[MatchEndUI] GameManager.Instance == null -- restart отменён.");
                return;
            }

            // Сбросить всех живых/мёртвых камней на исходные позиции.
            StoneCombat[] all = FindObjectsByType<StoneCombat>(FindObjectsSortMode.None);
            int pointIdx = 0;

            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s == null) continue;
                Transform sp = null;
                if (respawnPoints != null && respawnPoints.Length > 0)
                {
                    sp = respawnPoints[pointIdx % respawnPoints.Length];
                    pointIdx++;
                }
                Vector3 pos = sp != null ? sp.position : s.transform.position;
                Quaternion rot = sp != null ? sp.rotation : s.transform.rotation;
                s.ResetTo(pos, rot);
            }

            // Спрятать экран и стартануть новый матч.
            endScreenPanel?.SetActive(false);
            GameManager.Instance.StartMatch();
        }
    }
}
