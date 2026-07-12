using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace CurlingRoyale.UI
{
    /// <summary>
    /// Универсальный биндер кнопок в MainMenu.
    ///
    /// Два режима работы:
    ///
    /// 1) **Ручная привязка** (рекомендуется): заполни массив `bindings` в Inspector.
    ///    Каждый binding -- кнопка + опционально панель + опционально метод.
    ///    -- panel != null: клик toggle'ит панель (остальные панели скрываются).
    ///    -- panel == null && methodName задан: вызывается метод On{methodName}
    ///       на этом объекте или на fallbackTarget.
    ///
    /// 2) **Авто-режим** (если bindings пустое): для каждой Button в сцене ищется
    ///    панель по имени (SettingsButton -> Settings/SettingsPanel/...) или
    ///    вызывается метод On{ButtonName} (OnPlay, OnExit и т.п.).
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuButtonBinder : MonoBehaviour
    {
        [Serializable]
        public class Binding
        {
            [Tooltip("Кнопка в MainMenu.")]
            public Button button;

            [Tooltip("Панель, которую открывает/закрывает кнопка. null = вызвать метод.")]
            public GameObject panel;

            [Tooltip("Имя метода БЕЗ префикса 'On'. Например: 'Play' вызовет OnPlay(). Пусто = используется имя GameObject кнопки.")]
            public string methodName;
        }

        [Header("Ручная привязка (если пусто -- авто-режим)")]
        [SerializeField] private Binding[] bindings;

        [Header("Куда отправлять кнопки без панели")]
        [Tooltip("MonoBehaviour со скриптом, у которого ищутся методы OnX. Если null -- ищется на этом объекте.")]
        [SerializeField] private MonoBehaviour fallbackTarget;

        private void Start()
        {
            // Скрываем ВСЕ панели, упомянутые в bindings, при старте
            // (в авто-режиме -- все найденные панели)
            if (bindings != null && bindings.Length > 0)
            {
                foreach (var b in bindings)
                {
                    if (b != null && b.panel != null) b.panel.SetActive(false);
                }
            }
            else
            {
                foreach (var panel in FindAllPanels())
                {
                    if (panel != null) panel.SetActive(false);
                }
            }

            // Привязываем кнопки
            if (bindings != null && bindings.Length > 0)
            {
                foreach (var b in bindings)
                {
                    if (b == null || b.button == null) continue;
                    var captured = b; // для замыкания
                    b.button.onClick.AddListener(() => OnBindingClicked(captured));
                }
            }
            else
            {
                // Авто-режим: каждая Button в сцене
                var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var btn in buttons)
                {
                    if (btn == null) continue;
                    string name = btn.gameObject.name;
                    btn.onClick.AddListener(() => OnAutoButtonClicked(btn, name));
                }
            }
        }

        void OnBindingClicked(Binding b)
        {
            if (b.panel != null)
            {
                TogglePanel(b.panel);
            }
            else
            {
                InvokeMethod(b.methodName);
            }
        }

        void OnAutoButtonClicked(Button btn, string buttonName)
        {
            var panel = FindPanelForButton(buttonName);
            if (panel != null)
            {
                TogglePanel(panel);
                return;
            }
            InvokeMethod(buttonName);
        }

        // ─── Toggle / close panel ───
        void TogglePanel(GameObject panel)
        {
            bool willBeActive = !panel.activeSelf;
            if (willBeActive)
            {
                // Скрываем все остальные панели + их backdrop'ы
                CloseAllPanelsExcept(panel);
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

        void CloseAllPanelsExcept(GameObject except)
        {
            if (bindings != null && bindings.Length > 0)
            {
                foreach (var b in bindings)
                {
                    if (b == null || b.panel == null || b.panel == except) continue;
                    CloseBackdropFor(b.panel);
                    b.panel.SetActive(false);
                }
            }
            else
            {
                foreach (var p in FindAllPanels())
                {
                    if (p == null || p == except) continue;
                    CloseBackdropFor(p);
                    p.SetActive(false);
                }
            }
        }

        void WireCloseButton(GameObject panel)
        {
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

        // ─── Backdrop ───
        private readonly System.Collections.Generic.Dictionary<GameObject, GameObject> backdrops = new System.Collections.Generic.Dictionary<GameObject, GameObject>();

        void CreateBackdrop(GameObject panel)
        {
            if (backdrops.ContainsKey(panel)) return;
            var canvas = panel.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var go = new GameObject("Backdrop_" + panel.name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsFirstSibling();

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

        // ─── Авто-поиск панели по имени кнопки ───
        GameObject FindPanelForButton(string buttonName)
        {
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            System.Collections.Generic.List<string> candidates = new System.Collections.Generic.List<string>();
            candidates.Add(buttonName);
            candidates.Add(buttonName + "Panel");
            string stripped = buttonName;
            if (stripped.EndsWith("Button")) stripped = stripped.Substring(0, stripped.Length - 6);
            if (stripped != buttonName)
            {
                candidates.Add(stripped);
                candidates.Add(stripped + "Panel");
            }
            foreach (var name in candidates)
            {
                foreach (var go in all)
                {
                    if (go == null) continue;
                    if (go.name != name) continue;
                    if (!go.scene.IsValid()) continue;
                    if (IsPanel(go)) return go;
                }
            }
            return null;
        }

        bool IsPanel(GameObject go)
        {
            if (go.GetComponent<Button>() != null) return false;
            return go.GetComponent<Image>() != null;
        }

        System.Collections.Generic.IEnumerable<GameObject> FindAllPanels()
        {
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

        // ─── Вызов метода ───
        void InvokeMethod(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (TryInvokeMethod($"On{name}")) return;
            Debug.LogWarning($"[MenuButtonBinder] Метод On{name} не найден ни на этом объекте, ни на fallbackTarget.");
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

        // ─── Встроенные методы ───
        public void OnPlay()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
        }

        public void OnFullScreen()
        {
            Screen.fullScreen = !Screen.fullScreen;
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
