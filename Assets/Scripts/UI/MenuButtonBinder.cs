using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace CurlingRoyale.UI
{
    /// <summary>
    /// Универсальный биндер кнопок в MainMenu:
    ///   - Кнопка с именем "X" ищет в сцене панель с именем "X" и переключает её (вкл/выкл).
    ///   - Если панели нет -- ищет в этом компоненте метод "OnX" и вызывает его.
    ///   - Панель можно пометить тегом "MenuPanel" (опционально), чтобы не скрывать чужие объекты.
    ///
    /// Использование:
    ///   1. Повесить на любой GameObject в MainMenu.
    ///   2. В Hierarchy создать кнопки: "Play", "Settings", "Skins", "Exit"...
    ///   3. Если нужна панель -- создать GameObject с тем же именем ("Settings", "Skins"), он автоматически
    ///      скрывается/показывается по клику.
    ///   4. Для кнопок без панели добавить методы OnPlay / OnExit в этот класс (или [SerializeField] target).
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuButtonBinder : MonoBehaviour
    {
        [Header("Куда отправлять кнопки без панели")]
        [Tooltip("GameObject со скриптом, у которого ищутся методы OnX. Если null -- ищется на этом объекте.")]
        [SerializeField] private MonoBehaviour fallbackTarget;

        [Header("Поведение панелей")]
        [Tooltip("Скрывать ли все панели при старте (кроме тех, что marked as 'defaultPanel').")]
        [SerializeField] private bool hideAllPanelsOnStart = true;

        [Tooltip("Имя панели, которая показывается сразу при старте (например 'Main').")]
        [SerializeField] private string defaultPanel = "";

        private void Start()
        {
            // Скрываем все панели (опционально)
            if (hideAllPanelsOnStart)
            {
                foreach (var panel in FindAllPanels())
                {
                    if (panel == null) continue;
                    if (panel.name == defaultPanel) continue;
                    panel.SetActive(false);
                }
            }
            if (!string.IsNullOrEmpty(defaultPanel))
            {
                var def = GameObject.Find(defaultPanel);
                if (def != null) def.SetActive(true);
            }

            // Привязываем все кнопки в сцене
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                string name = btn.gameObject.name;
                btn.onClick.AddListener(() => OnButtonClicked(btn, name));
            }
        }

        void OnButtonClicked(Button btn, string buttonName)
        {
            // Ищем панель с тем же именем
            var panel = GameObject.Find(buttonName);
            if (panel != null)
            {
                TogglePanel(panel);
                return;
            }

            // Панели нет -- вызываем метод OnX
            InvokeMethod($"On{buttonName}");
        }

        void TogglePanel(GameObject panel)
        {
            // Если панель была включена -- выключаем её, иначе включаем
            // Но если это единственная активная панель -- оставляем открытой (анти-залипание).
            bool willBeActive = !panel.activeSelf;
            if (willBeActive)
            {
                // Скрываем все остальные панели
                foreach (var p in FindAllPanels())
                {
                    if (p == null || p == panel) continue;
                    p.SetActive(false);
                }
            }
            panel.SetActive(willBeActive);
        }

        void InvokeMethod(string methodName)
        {
            // Сначала на fallbackTarget, потом на этом объекте
            MethodInfo m = null;
            if (fallbackTarget != null)
                m = fallbackTarget.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m == null)
                m = GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m != null)
            {
                try { m.Invoke(fallbackTarget != null ? (object)fallbackTarget : this, null); }
                catch (Exception e) { Debug.LogError($"[MenuButtonBinder] {methodName} failed: {e.Message}"); }
            }
            else
            {
                Debug.LogWarning($"[MenuButtonBinder] Button '{methodName.Substring(2)}' has no panel and no method '{methodName}'.");
            }
        }

        System.Collections.Generic.IEnumerable<GameObject> FindAllPanels()
        {
            // "Панелью" считаем любой GameObject, у которого есть Image и он НЕ является кнопкой.
            // Чтобы не задеть лишнего, ищем только среди детей Canvas.
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                var panels = canvas.GetComponentsInChildren<RectTransform>(true);
                foreach (var rt in panels)
                {
                    var go = rt.gameObject;
                    if (go.GetComponent<Button>() != null) continue;  // пропускаем кнопки
                    if (go.GetComponent<Image>() == null) continue; // пропускаем объекты без Image
                    if (go.transform.childCount > 0) continue;      // пропускаем контейнеры с детьми (это группы)
                    yield return go;
                }
            }
        }

        // ─── Методы по умолчанию (если ни одна панель не подошла) ───
        public void OnPlay()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
        }

        public void OnExit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
