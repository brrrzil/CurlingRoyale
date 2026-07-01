using UnityEngine;
using UnityEngine.Events;

namespace CurlingRoyale.Combat
{
    /// <summary>
    /// Здоровье и обработка столкновений для камня (игрока или бота).
    /// Вешать на префаб Stone рядом с Rigidbody2D и CircleCollider2D.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class StoneCombat : MonoBehaviour
    {
        [Header("Конфигурация")]
        [Tooltip("ScriptableObject с диапазонами урона. Создать через Create → Curling Royale → Damage Config.")]
        [SerializeField] private DamageConfig damageConfig;

        [Header("Визуал (поворот в сторону движения)")]
        [Tooltip("Если true — камень визуально поворачивается (Z-rotation) в сторону velocity.")]
        public bool rotateTowardsVelocity = true;

        [Tooltip("Смещение между направлением спрайта 'вперёд' и velocity=+X. " +
                 "Если спрайт нарисован лицом вверх — поставь -90 (тогда velocity=+X даёт rotation 0).")]
        public float spriteForwardOffsetDegrees = -90f;

        [Tooltip("Порог скорости ниже которого rotation не обновляется (стоящий камень не дрожит).")]
        [Min(0f)] public float rotationVelocityThreshold = 0.2f;

        [Header("События (для UI и VFX)")]
        public UnityEvent<int, int> onHealthChanged = new UnityEvent<int, int>(); // (current, max)
        public UnityEvent onDamageTaken = new UnityEvent();
        public UnityEvent onDeath = new UnityEvent();

        // ─── Публичное состояние ─────────────────────────────────────
        public int CurrentHP { get; private set; }
        public int MaxHP => damageConfig != null ? damageConfig.maxHealth : 100;
        public bool IsDead => CurrentHP <= 0;

        private Rigidbody2D rb;
        private float lastDamageTime = -999f;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.linearDamping = 2.5f;
            CurrentHP = MaxHP;
        }

        void Start()
        {
            onHealthChanged?.Invoke(CurrentHP, MaxHP);
        }

        void FixedUpdate()
        {
            // Визуальный поворот в сторону движения (если включён).
            if (!rotateTowardsVelocity) return;
            Vector2 v = rb.linearVelocity;
            if (v.sqrMagnitude < rotationVelocityThreshold * rotationVelocityThreshold) return;
            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg + spriteForwardOffsetDegrees;
            rb.MoveRotation(angle);
        }

        // ─── Получение урона ─────────────────────────────────────────

        public void TakeDamage(int amount, GameObject attacker = null)
        {
            if (IsDead || amount <= 0) return;
            if (damageConfig != null && Time.time - lastDamageTime < damageConfig.damageCooldown) return;

            lastDamageTime = Time.time;
            CurrentHP = Mathf.Max(0, CurrentHP - amount);

            Debug.Log($"[Combat] {name} получил {amount} урона от {(attacker != null ? attacker.name : "неизв.")}. " +
                      $"HP: {CurrentHP}/{MaxHP}");

            onDamageTaken?.Invoke();
            onHealthChanged?.Invoke(CurrentHP, MaxHP);

            if (CurrentHP <= 0)
                Die();
        }

        // ─── Обработка столкновений (closing-speed based) ──────────

        void OnCollisionEnter2D(Collision2D collision)
        {
            var other = collision.collider.GetComponent<StoneCombat>();
            if (other == null || damageConfig == null) return;

            var otherRb = other.GetComponent<Rigidbody2D>();

            // Направление удара: от меня к жертве.
            Vector2 hitDir = ((Vector2)other.transform.position - (Vector2)transform.position).normalized;

            // МОЯ скорость сближения = моё движение в направлении жертвы.
            // Стоящий камень при ударе = myApproach ≈ 0 → не наношу урон (только получаю).
            // Касательный удар при быстром движении = myApproach маленькая → минимум урона.
            // Лоб-в-лоб = каждый атакующий наносит (с кулдауном).
            float myApproach = Vector2.Dot(rb.linearVelocity, hitDir);
            if (myApproach < damageConfig.minAttackSpeed) return;

            // Направление "спины" жертвы: куда жертва двигалась (или её «лицо» если стоит).
            Vector2 victimFacing = (otherRb != null && otherRb.linearVelocity.sqrMagnitude > 0.01f)
                ? otherRb.linearVelocity.normalized
                : -hitDir;
            // Угол между "спиной" жертвы и направлением, откуда прилетел удар.
            // Угол 0° = прямо в лицо (лоб), 180° = в спину.
            float angle = Vector2.Angle(victimFacing, -hitDir);

            // Damage = baseDamage(angle) × speedFactor(approach).
            float speedFactor = Mathf.Clamp(
                myApproach / Mathf.Max(0.01f, damageConfig.referenceAttackSpeed),
                0f, damageConfig.maxSpeedMultiplier);
            int damage = Mathf.RoundToInt(
                damageConfig.CalculateDamage(angle) * speedFactor);

            // Отладка.
            Debug.Log($"[Combat] {name} → {other.name}: угол={angle:F1}°, approach={myApproach:F1} (×{speedFactor:F2}), урон={damage}");

            other.TakeDamage(damage, gameObject);
        }

        // ─── Смерть ─────────────────────────────────────────────────

        private void Die()
        {
            Debug.Log($"[Combat] {name} уничтожен.");
            onDeath?.Invoke();

            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            // GameManager дёрнет наш счётчик alive при следующем Update.
        }
    }
}
