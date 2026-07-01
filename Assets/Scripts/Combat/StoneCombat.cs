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
            // Continuous collision detection — без этого быстрые камни тоннелируют
            // сквозь друг друга и сквозь границу арены.
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            // Усиленное торможение, чтобы камни останавливались за ~2-3 секунды.
            rb.linearDamping = 2.5f;
            // Замораживаем вращение — иначе HP-бар (child) крутится вместе с камнем.
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            CurrentHP = MaxHP;
        }

        void Start()
        {
            // Оповестить подписчиков о начальном HP (HP-бар рисуется сразу).
            onHealthChanged?.Invoke(CurrentHP, MaxHP);
        }

        // ─── Получение урона ─────────────────────────────────────────

        /// <summary>
        /// Применить урон. Учитывает кулдаун, чтобы один контакт
        /// не списывал HP несколько раз.
        /// </summary>
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

        // ─── Обработка столкновений ─────────────────────────────────

        void OnCollisionEnter2D(Collision2D collision)
        {
            // Игнорируем коллизии с не-камнями (колонны обрабатываются отдельно).
            var other = collision.collider.GetComponent<StoneCombat>();
            if (other == null || damageConfig == null) return;

            // Оба камня получают OnCollisionEnter2D. Чтобы не удваивать урон,
            // считаем атакующим того, у кого скорость выше (или тот, кто двигался).
            float mySpeed = rb.linearVelocity.magnitude;
            var otherRb = other.GetComponent<Rigidbody2D>();
            float otherSpeed = otherRb != null ? otherRb.linearVelocity.magnitude : 0f;
            if (mySpeed < otherSpeed) return; // уступаем право ударить тому, кто быстрее

            // Скорость атакующего должна быть достаточной.
            if (mySpeed < damageConfig.minAttackSpeed) return;

            // Рассчитать угол.
            // Направление удара: от атакующего к жертве.
            Vector2 hitDir = ((Vector2)other.transform.position - (Vector2)transform.position).normalized;
            // Направление "спины" жертвы: куда жертва двигалась (или hitDir если стоит).
            Vector2 victimFacing = otherRb.linearVelocity.sqrMagnitude > 0.01f
                ? otherRb.linearVelocity.normalized
                : hitDir;
            // Угол между направлением "спины" жертвы и направлением, откуда прилетел удар.
            float angle = Vector2.Angle(victimFacing, -hitDir);

            // Damage пропорционален скорости атакующего + углу.
            float speedFactor = Mathf.Clamp(
                mySpeed / Mathf.Max(0.01f, damageConfig.referenceAttackSpeed),
                0f, damageConfig.maxSpeedMultiplier);
            int damage = Mathf.RoundToInt(
                damageConfig.CalculateDamage(angle) * speedFactor);

            // Отладка: вывести угол и направления.
            Debug.Log($"[Combat] {name} → {other.name}: угол={angle:F1}°, скорость={mySpeed:F1} (×{speedFactor:F2}), урон={damage}");

            other.TakeDamage(damage, gameObject);
        }

        // ─── Смерть ─────────────────────────────────────────────────

        private void Die()
        {
            Debug.Log($"[Combat] {name} уничтожен.");
            onDeath?.Invoke();

            // Отключаем физику и коллизии, чтобы камень-призрак не мешал.
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            // Само удаление объекта — снаружи (через ObjectPool или Destroy).
            // Здесь только событие; GameManager решает, что делать.
        }
    }
}