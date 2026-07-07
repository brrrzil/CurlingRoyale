using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CurlingRoyale.Combat;

namespace CurlingRoyale.Game
{
    /// <summary>
    /// End-game экран: показывает победителя (TextMeshPro) и кнопку Restart.
    ///
    /// Как использовать:
    /// 1. Создай Canvas (ScreenSpace-Overlay).
    /// 2. Внутри Panel (полупрозрачный фон) с TextMeshPro -- winnerText, и Button -- restartButton.
    /// 3. Перетащи Canvas/panel в это поле, TextMeshPro/Button -- в соответствующие поля.
    ///
    /// Победитель определяется как ЕДИНСТВЕННЫЙ живой StoneCombat на момент конца матча.
    /// Если не осталось живых -- "Ничья (все погибли)".
    /// </summary>
    public class MatchEndUI : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Panel с TextMeshPro + Button, скрывается в начале матча.")]
        [SerializeField] private GameObject endScreenPanel;

        [Tooltip("TextMeshPro -- 'Победил: ...' / 'Ничья'")]
        [SerializeField] private TMP_Text winnerText;

        [Tooltip("Button с подписью 'Restart'. Скрипт сам подписывается на клик.")]
        [SerializeField] private Button restartButton;

        void Start()
        {
            if (endScreenPanel != null)
                endScreenPanel.SetActive(false);

            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestartPressed);

            StartCoroutine(SubscribeWhenReady());
        }

        System.Collections.IEnumerator SubscribeWhenReady()
        {
            float deadline = Time.time + 2f;
            while (GameManager.Instance == null && Time.time < deadline)
                yield return null;
            if (GameManager.Instance != null)
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

            // Сбросить все камни на исходные позиции (зафиксированные в StoneCombat.Awake).
            StoneCombat[] all = FindObjectsByType<StoneCombat>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s == null) continue;
                s.ResetToOriginal();
            }

            endScreenPanel?.SetActive(false);
            GameManager.Instance.StartMatch();
        }
    }
}
