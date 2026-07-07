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
        [Tooltip("ScriptableObject с диапазонами урона. Создать через Create -> Curling Royale -> Damage Config.")]
        [SerializeField] private DamageConfig damageConfig;

        [Header("Визуал (поворот в сторону движения)")]
        [Tooltip("Если true -- камень визуально поворачивается (Z-rotation) в сторону velocity.")]
        public bool rotateTowardsVelocity = true;

        [Tooltip("Смещение между направлением спрайта 'вперёд' и velocity=+X.")]
        public float spriteForwardOffsetDegrees = -90f;

        [Tooltip("Порог скорости ниже которого rotation не обновляется (стоящий камень не дрожит).")]
        [Min(0f)] public float rotationVelocityThreshold = 0.2f;

        [Header("События (для UI и VFX)")]
        public UnityEvent<int, int> onHealthChanged = new UnityEvent<int, int>(); // (current, max)
        public UnityEvent onDamageTaken = new UnityEvent();
        public UnityEvent onDeath = new UnityEvent();

        // Публичное состояние
        public int CurrentHP { get; private set; }
        public int MaxHP => damageConfig != null ? damageConfig.maxHealth : 100;
        public bool IsDead => CurrentHP <= 0;

        // Velocity ДО столкновения. FixedUpdate кэширует её ДО physics step.
        // OnCollisionEnter2D срабатывает ВНУТРИ physics step, и в этот момент
        // rb.linearVelocity уже включает collision impulse (отражение) ->
        // Значение "после" обычно обнулено или развёрнуто. Кэш хранит "до".
        public Vector2 PreCollisionVelocity => cachedLinearVelocity;
        private Vector2 cachedLinearVelocity;

        private Rigidbody2D rb;
        private float lastDamageTime = -999f;

        // Кэш исходной позиции/поворота для ResetToOriginal() при Restart.
        private Vector3 originalPosition;
        private Quaternion originalRotation;

        [Header("Звук столкновения")]
        [Tooltip("Минимальный нанесённый урон, чтобы проиграть звук (касательные не пикают).")]
        [Min(0)] [SerializeField] private int collisionHitMinDamage = 1;
        [Tooltip("Базовый множитель громкости звука столкновения от 0 до 1.")]
        [Range(0f, 1f)] [SerializeField] private float collisionHitIntensity = 0.7f;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.linearDamping = 2.5f;

            // Запоминаем стартовые координаты -- по ним Restart-кнопка вернёт камень.
            originalPosition = transform.position;
            originalRotation = transform.rotation;

            if (damageConfig == null)
            {
                damageConfig = ScriptableObject.CreateInstance<DamageConfig>();
                damageConfig.name = "DamageConfig_RuntimeDefault";
                Debug.LogWarning($"[Combat] {name}: DamageConfig НЕ назначен -- создан runtime-дефолт. " +
                                 "Назначь Assets/Configs/DamageConfig.asset в инспекторе для production values.", this);
            }

            CurrentHP = MaxHP;
        }

        void Start()
        {
            onHealthChanged?.Invoke(CurrentHP, MaxHP);

            // Жёстко залочиваем bodyType = Dynamic. Никто и нигде (включая сторонние
            // пакеты, плагины) не должен менять Rigidbody камня на Kinematic или Static.
            if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;
        }

        void FixedUpdate()
        {
            // Первый приоритет: bodyType должен быть Dynamic. Если кто-то поменял -- верни.
            EnforceDynamicBodyType();

            // Кэшируем velocity ДО физического шага.
            cachedLinearVelocity = rb.linearVelocity;

            // Визуальный поворот (если включён).
            if (!rotateTowardsVelocity) return;
            if (cachedLinearVelocity.sqrMagnitude < rotationVelocityThreshold * rotationVelocityThreshold) return;
            float angle = Mathf.Atan2(cachedLinearVelocity.y, cachedLinearVelocity.x) * Mathf.Rad2Deg + spriteForwardOffsetDegrees;
            rb.MoveRotation(angle);
        }

        // Получение урона
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

        // Обработка столкновений (closing-speed based)
        void OnCollisionEnter2D(Collision2D collision)
        {
            if (damageConfig == null) return;

            var other = collision.collider.GetComponent<StoneCombat>();
            if (other == null) return;

            // Скорость ДО столкновения -- из кэша (rb уже отражён impulse-ом).
            Vector2 myVel = cachedLinearVelocity;
            if (myVel.sqrMagnitude < damageConfig.minAttackSpeed * damageConfig.minAttackSpeed) {
                return;
            }

            Vector2 hitDir = ((Vector2)other.transform.position - (Vector2)transform.position).normalized;

            // Урон считаем от моего СОБСТВЕННОГО движения:
            //   -- камень быстро движется -> он агрессор (curling-like)
            //   -- камень стоит и получил удар -> он агрессором не считается
            //   -- спина/бок/лоб определяются по тому, в какую часть жертвы я попал
            //      относительно её собственного направления движения.
            Vector2 otherVel = other.PreCollisionVelocity;
            Vector2 victimFacing = otherVel.sqrMagnitude > 0.01f
                ? otherVel.normalized
                : -hitDir;
            float angle = Vector2.Angle(victimFacing, -hitDir);

            float speedFactor = Mathf.Clamp(
                myVel.magnitude / Mathf.Max(0.01f, damageConfig.referenceAttackSpeed),
                0f, damageConfig.maxSpeedMultiplier);
            int damage = Mathf.RoundToInt(
                damageConfig.CalculateDamage(angle) * speedFactor);

            // Звук столкновения через центральный SoundManager (один источник, не дублируется).
            // Звук играется ОДИН раз -- оба камня вызывают SoundManager.Instance.PlayCollision,
            // но он использует PlayOneShot и не прерывает уже играющие звуки.
            if (damage >= collisionHitMinDamage && CurlingRoyale.Audio.SoundManager.Instance != null)
                CurlingRoyale.Audio.SoundManager.Instance.PlayCollision(transform.position, collisionHitIntensity);

            other.TakeDamage(damage, gameObject);
        }

        /// <summary>
        /// Сброс камня на произвольную позицию. Вызывается Restart-кнопкой.
        /// </summary>
        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            CurrentHP = MaxHP;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            transform.SetPositionAndRotation(position, rotation);

            onHealthChanged?.Invoke(CurrentHP, MaxHP);
        }

        /// <summary>
        /// Сброс на исходную позицию, зафиксированную в Awake. Используется по умолчанию
        /// Restart-кнопкой, когда отдельные Spawn Points не заданы.
        /// </summary>
        public void ResetToOriginal() => ResetTo(originalPosition, originalRotation);

        // Сторож: каждый fixedUpdate возвращаем Dynamic, если что-то его изменило.
        // По репорту: после смерти на 1 кадр bodyType меняется на Kinematic -- это
        // может быть пакет или плагин. Мы переопределяем обратно в Dynamic и логируем кто виноват.
        void EnforceDynamicBodyType()
        {
            if (rb != null && rb.bodyType != RigidbodyType2D.Dynamic)
            {
                Debug.LogWarning($"[Combat] {name}: bodyType != Dynamic (было {rb.bodyType}) -- форсирую Dynamic.");
                rb.bodyType = RigidbodyType2D.Dynamic;
            }
        }

        // Смерть
        private void Die()
        {
            Debug.Log($"[Combat] {name} уничтожен.");
            onDeath?.Invoke();

            // bodyType НЕ трогаем. Только обнуляем скорость, чтобы камень не разлетался
            // от инерции в момент смерти.
            rb.linearVelocity = Vector2.zero;
            EnforceDynamicBodyType();
        }
    }
}
