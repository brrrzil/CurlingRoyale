using UnityEngine;
using UnityEngine.InputSystem;

namespace CurlingRoyale.Player
{
    /// <summary>
    /// Управление камнем игрока: зажатие ЛКМ/тача — зарядка, отпускание — разгон.
    /// Использует New Input System (com.unity.inputsystem 1.18.0).
    /// Поддерживает мышь (десктоп) и тач (мобилки) из коробки через Pointer.current.
    /// </summary>
    [RequireComponent(typeof(CustomPhysicsBody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Параметры удара")]
        public float minForce = 3f;
        public float maxForce = 16f;
        public float maxChargeTime = 2f;

        [Header("Визуал")]
        public LineRenderer lineRenderer;
        public Transform chargeCircle;

        private CustomPhysicsBody physicsBody;
        private Vector2 direction;
        private float chargeStartTime;
        private bool isCharging;
        private Camera mainCam;

        void Awake()
        {
            physicsBody = GetComponent<CustomPhysicsBody>();
            mainCam = Camera.main;
            HideChargeVisual();
        }

        void OnEnable()
        {
            if (lineRenderer != null) lineRenderer.enabled = false;
            if (chargeCircle != null) chargeCircle.gameObject.SetActive(false);
        }

        void Update()
        {
            // Получаем позицию указателя (мышь или тач) в мировых координатах
            Vector2? pointerWorld = GetPointerWorldPosition();
            if (!pointerWorld.HasValue) return;

            // Старт зарядки при нажатии
            if (!isCharging && WasPointerPressedThisFrame())
            {
                if (Vector2.Distance(pointerWorld.Value, transform.position) < 1f)
                {
                    StartCharge();
                }
            }

            // Пока заряжаем — обновляем визуал каждый кадр
            if (isCharging)
            {
                UpdateChargeVisual(pointerWorld.Value);
            }

            // Релиз — выстрел
            if (isCharging && WasPointerReleasedThisFrame())
            {
                ReleaseCharge(pointerWorld.Value);
            }
        }

        // ─── Input System helpers ────────────────────────────────────

        /// <summary>
        /// Позиция указателя (mouse/touch) в мировых координатах через камеру.
        /// Null если Pointer.current недоступен.
        /// </summary>
        private Vector2? GetPointerWorldPosition()
        {
            if (mainCam == null) return null;
            Pointer pointer = Pointer.current;
            if (pointer == null) return null;
            Vector2 screen = pointer.position.ReadValue();
            return mainCam.ScreenToWorldPoint(screen);
        }

        private bool WasPointerPressedThisFrame()
        {
            // Для мыши: левая кнопка. Для тача: первый палец.
            if (Mouse.current != null)
                return Mouse.current.leftButton.wasPressedThisFrame;
            if (Pointer.current is Touchscreen ts)
                return ts.primaryTouch.press.wasPressedThisFrame;
            return false;
        }

        private bool WasPointerReleasedThisFrame()
        {
            if (Mouse.current != null)
                return Mouse.current.leftButton.wasReleasedThisFrame;
            if (Pointer.current is Touchscreen ts)
                return ts.primaryTouch.press.wasReleasedThisFrame;
            return false;
        }

        // ─── Зарядка / выстрел ──────────────────────────────────────

        private void StartCharge()
        {
            isCharging = true;
            chargeStartTime = Time.time;

            if (lineRenderer != null) lineRenderer.enabled = true;
            if (chargeCircle != null) chargeCircle.gameObject.SetActive(true);
        }

        private void ReleaseCharge(Vector2 pointerWorld)
        {
            isCharging = false;
            HideChargeVisual();

            float chargeTime = Mathf.Min(Time.time - chargeStartTime, maxChargeTime);
            float force = Mathf.Lerp(minForce, maxForce, chargeTime / maxChargeTime);

            // Финальное направление (могло не обновиться последним кадром)
            Vector2 pullDir = pointerWorld - (Vector2)transform.position;
            Vector2 finalDir = -pullDir.normalized;

            physicsBody.ApplyForce(finalDir, force);
        }

        private void UpdateChargeVisual(Vector2 pointerWorld)
        {
            Vector2 pullDir = pointerWorld - (Vector2)transform.position;
            direction = -pullDir.normalized;

            float chargeTime = Mathf.Min(Time.time - chargeStartTime, maxChargeTime);
            float t = chargeTime / maxChargeTime;
            float lineLength = Mathf.Lerp(0.5f, 3f, t);

            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, transform.position);
                lineRenderer.SetPosition(1, transform.position + (Vector3)direction * lineLength);
            }

            if (chargeCircle != null)
            {
                float scale = Mathf.Lerp(0.5f, 1.5f, t);
                chargeCircle.localScale = Vector3.one * scale;

                SpriteRenderer sr = chargeCircle.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.color = Color.Lerp(Color.green, Color.red, t);
            }
        }

        private void HideChargeVisual()
        {
            if (lineRenderer != null) lineRenderer.enabled = false;
            if (chargeCircle != null) chargeCircle.gameObject.SetActive(false);
        }
    }
}
