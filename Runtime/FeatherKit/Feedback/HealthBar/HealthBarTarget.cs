using UnityEngine;
using HealthComponent = FeatherKit.Health.Health;

namespace FeatherKit.Feedback
{
    /// <summary>
    /// Отметка «у этого объекта показывать полосу здоровья». Сам ничего не рисует —
    /// только заявляет о себе контроллеру и говорит, на какой высоте у него голова.
    ///
    /// Привязан к Health, а не к юниту, поэтому годится и врагу, и бочке, и любому другому
    /// разрушаемому объекту.
    ///
    /// Регистрация статическая, а не через контекст сцены: компонент живёт на префабе,
    /// который спавнится из пула, а FeatherKit про игровой контекст знать не должен.
    /// Принцип тот же, что у акторов — сущность сама заявляет о себе системе.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthBarTarget : MonoBehaviour
    {
        [Tooltip("Чьё здоровье показываем; если пусто — ищется выше по иерархии")]
        [SerializeField] private HealthComponent health;

        [Tooltip("К чему привязать полосу: кость головы, макушка модели, крышка бочки. " +
                 "Если пусто — сам объект")]
        [SerializeField] private Transform anchor;

        [Tooltip("Смещение от привязки в ЛОКАЛЬНЫХ координатах, м — поднять полосу над головой. " +
                 "Смысловой центр объекта (FEntityBase.Center) здесь не используется намеренно: " +
                 "он для расчёта расстояний, а полосе нужна своя высота")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.2f, 0f);


        public HealthComponent Health => health;

        public Vector3 WorldPosition => anchor != null
            ? anchor.TransformPoint(offset)
            : transform.TransformPoint(offset);


        private void Awake()
        {
            if (health == null)
                health = GetComponentInParent<HealthComponent>();
        }

        private void OnEnable()
        {
            if (health != null)
                HealthBarController.Register(this);
        }

        private void OnDisable()
        {
            HealthBarController.Unregister(this);
        }
    }
}
