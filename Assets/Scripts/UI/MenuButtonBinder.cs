using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CurlingRoyale.UI
{
    /// <summary>
    /// Универсальный биндер кнопок в MainMenu.
    ///
    /// Логика для кнопки с именем "X":
    ///   1. Ищет панель (GameObject c Image, не Button) с именем X. Если нашёл → toggle.
    ///   2. Если не нашёл — ищет XPanel.
    ///   3. Если не нашёл — ищет "X" без суффикса "Button" (SettingsButton → Settings).
    ///   4. Если и это не нашёл — ищет метод "OnX" или "OnY" (где Y = X без "Button").
    ///   5. Если ничего нет — Debug.LogWarning.
    ///
    /// Поведение панели:
    ///   - При открытии ищет child Button с именем "Close", "CloseButton" или "X" → вешает закрытие.
    ///   - При открытии создаёт ПОЗАДИ панели полупрозрачный backdrop Image на весь экран.
    ///     Клик по backdrop → закрывает панель. При закрытии backdrop уничтожается.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuButtonBinder : MonoBehaviour
    {
        [Header("Куда отправлять кнопки без панели")]
        [Tooltip("MonoBehaviour со скриптом, у которого ищутся методы OnX. Если null -- ищется на этом объекте.")]
        [SerializeField] private MonoBehaviour fallbackTarget;

        private void Start()
        {
            // Скрываем ВСЕ панели при старте -- юзер увидит только корневой UI с кнопками.
            foreach (var panel in FindAllPanels())
            {
                if (panel != null) panel.SetActive(false);
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
            GameObject panel = FindPanelForButton(buttonName);
            if (panel != null)
            {
                TogglePanel(panel);
                return;
            }
            InvokeButtonMethod(buttonName);
        }

        /// <summary>
        /// Ищет панель по кнопке. Пробует варианты: name, name + "Panel", name без "Button".
        /// </summary>
        GameObject FindPanelForButton(string buttonName)
        {
            // Сначала точное совпадение
            var go = GameObject.Find(buttonName);
            if (go != null && IsPanel(go)) return go;

            // name + "Panel"
            go = GameObject.Find(buttonName + "Panel");
            if (go != null && IsPanel(go)) return go;

            // Имя без суффикса "Button"
            string stripped = buttonName;
            if (stripped.EndsWith("Button")) stripped = stripped.Substring(0, stripped.Length - 6);
            if (stripped != buttonName)
            {
                go = GameObject.Find(stripped);
                if (go != null && IsPanel(go)) return go;
                go = GameObject.Find(stripped + "Panel");
                if (go != null && IsPanel(go)) return go;
            }
            return null;
        }

        bool IsPanel(GameObject go)
        {
            // Панелью считаем объект с Image, но не кнопку.
            if (go.GetComponent<Button>() != null) return false;
            return go.GetComponent<Image>() != null;
        }

        void TogglePanel(GameObject panel)
        {
            bool willBeActive = !panel.activeSelf;
            if (willBeActive)
            {
                // Скрываем все остальные панели + их backdrop'ы
                foreach (var p in FindAllPanels())
                {
                    if (p == null || p == panel) continue;
                    CloseBackdropFor(p);
                    p.SetActive(false);
                }
                panel.SetActive(true);
                WireCloseButton(panel);
                CreateBackdrop(panel);
            }
            else
            {
                CloseBackdropFor(panel);
                panel.SetActive(false);
            }
        }

        void WireCloseButton(GameObject panel)
        {
            // Ищем child Button с именем "Close" / "CloseButton" / "X" / "Back"
            var buttons = panel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                string n = btn.gameObject.name.ToLower();
                if (n == "close" || n == "closebutton" || n == "x" || n == "back" || n.Contains("close"))
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => {
                        CloseBackdropFor(panel);
                        panel.SetActive(false);
                    });
                }
            }
        }

        // ─── Backdrop (click-outside) ───
        // Привязываем backdrop → panel чтобы при закрытии знать, что удалять.
        private readonly System.Collections.Generic.Dictionary<GameObject, GameObject> backdrops = new System.Collections.Generic.Dictionary<GameObject, GameObject>();

        void CreateBackdrop(GameObject panel)
        {
            if (backdrops.ContainsKey(panel)) return;
            var canvas = panel.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var go = new GameObject("Backdrop_" + panel.name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsFirstSibling();  // под панелью (но в том же Canvas)

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0.4f);

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => {
                CloseBackdropFor(panel);
                panel.SetActive(false);
            });

            backdrops[panel] = go;
        }

        void CloseBackdropFor(GameObject panel)
        {
            if (backdrops.TryGetValue(panel, out var bg))
            {
                if (bg != null) Destroy(bg);
                backdrops.Remove(panel);
            }
        }

        // ─── Метод по кнопке ───
        void InvokeButtonMethod(string buttonName)
        {
            // Пробуем варианты имени
            string[] candidates = { buttonName };
            if (buttonName.EndsWith("Button"))
                candidates = new[] { buttonName, buttonName.Substring(0, buttonName.Length - 6) };
            else
                candidates = new[] { buttonName, buttonName + "Button" };

            foreach (var name in candidates)
            {
                if (TryInvokeMethod($"On{name}")) return;
            }
            Debug.LogWarning($"[MenuButtonBinder] Кнопка '{buttonName}': нет панели и нет метода On{buttonName} (или On{StripButtonSuffix(buttonName)}).");
        }

        bool TryInvokeMethod(string methodName)
        {
            MethodInfo m = null;
            if (fallbackTarget != null)
                m = fallbackTarget.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m == null)
                m = GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m == null) return false;
            try { m.Invoke(fallbackTarget != null ? (object)fallbackTarget : this, null); return true; }
            catch (Exception e) { Debug.LogError($"[MenuButtonBinder] {methodName} failed: {e.Message}"); return false; }
        }

        string StripButtonSuffix(string s)
        {
            return s.EndsWith("Button") ? s.Substring(0, s.Length - 6) : s;
        }

        System.Collections.Generic.IEnumerable<GameObject> FindAllPanels()
        {
            // Панель -- любой GameObject на Canvas с Image, но НЕ Button.
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                var images = canvas.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    var go = img.gameObject;
                    if (go.GetComponent<Button>() != null) continue;
                    yield return go;
                }
            }
        }

        // ─── Встроенные методы для базовых кнопок ───
        public void OnPlay()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
        }

        public void OnPlayButton() => OnPlay();

        public void OnFullScreen()
        {
            // Переключение fullscreen. В WebGL это работает только если билд собран с
            // fullscreen-template, но вызов безопасен в любой среде.
            Screen.fullScreen = !Screen.fullScreen;
        }

        public void OnFullScreenButton() => OnFullScreen();

        public void OnExit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        public void OnExitButton() => OnExit();
    }
}
