using FeatherKit.Health;
using UnityEngine;
using HealthComponent = FeatherKit.Health.Health;

namespace FeatherKit.Feedback
{
    /// <summary>
    /// Заливает модель цветом на доли секунды в момент получения урона. Подписывается на
    /// Health.Damaged, а не вызывается из того, кто наносит удар: бой не должен знать про
    /// то, как он выглядит.
    ///
    /// Шейдеру нужны свойства _HitFlash и _HitFlashColor (имена настраиваются). Готовый
    /// такой шейдер лежит в пакете — FeatherKit/Toon Lit.
    ///
    /// Пишем в собственные копии материалов — почему именно так, написано
    /// в <see cref="MaterialCopies"/>. Копии живут столько же, сколько объект в пуле.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitFlash : MonoBehaviour
    {
        [Tooltip("Сколько держится вспышка, сек")]
        [SerializeField] private float duration = 0.12f;

        [Tooltip("Цвет вспышки")]
        [SerializeField] private Color flashColor = Color.white;

        [Tooltip("Сила на пике: 1 — модель полностью заливается цветом")]
        [SerializeField, Range(0f, 1f)] private float strength = 1f;

        [Tooltip("Имя float-свойства вспышки в шейдере")]
        [SerializeField] private string flashProperty = "_HitFlash";

        [Tooltip("Имя color-свойства вспышки в шейдере")]
        [SerializeField] private string flashColorProperty = "_HitFlashColor";

        private static readonly int FallbackFlashId = Shader.PropertyToID("_HitFlash");

        private MaterialCopies materials;
        private HealthComponent health;
        private int flashId;
        private int flashColorId;
        private float timeLeft;


        private void Awake()
        {
            flashId = string.IsNullOrEmpty(flashProperty) ? FallbackFlashId : Shader.PropertyToID(flashProperty);
            flashColorId = Shader.PropertyToID(flashColorProperty);

            // Ссылку на источник не выносим в инспектор: Unity не сериализует интерфейсы,
            // а конкретный Health ищем сами по иерархии: вспышка живёт на той же модели.
            health = GetComponentInParent<HealthComponent>();

            materials = new MaterialCopies(gameObject, flashId);
        }

        private void OnDestroy()
        {
            materials?.Dispose();
        }

        private void OnEnable()
        {
            if (health != null)
                health.Damaged += OnDamaged;

            // Инстанс мог прийти из пула с недогоревшей вспышкой от прошлой жизни.
            timeLeft = 0f;
            Apply(0f);
        }

        private void OnDisable()
        {
            if (health != null)
                health.Damaged -= OnDamaged;

            timeLeft = 0f;
            Apply(0f);
        }

        private void Update()
        {
            if (timeLeft <= 0f)
                return;

            // Реальное время: вспышка — это фидбек, она доигрывает и на паузе. Иначе
            // пауза в момент попадания оставляет модель залитой белым.
            timeLeft -= Time.unscaledDeltaTime;

            if (timeLeft <= 0f)
            {
                timeLeft = 0f;
                Apply(0f);
                return;
            }

            // Затухание от пика к нулю: резкий вход и мягкий выход читаются как удар,
            // ровная полка — как смена цвета.
            Apply(timeLeft / duration * strength);
        }


        private void OnDamaged(int amount)
        {
            if (duration <= 0f)
                return;

            // Повторное попадание не копится, а перезапускает вспышку с пика — иначе
            // при плотной стрельбе враг просто залипает белым.
            timeLeft = duration;
            Apply(strength);
        }

        private void Apply(float value)
        {
            materials?.SetFloat(flashId, value);
            materials?.SetColor(flashColorId, flashColor);
        }
    }
}
