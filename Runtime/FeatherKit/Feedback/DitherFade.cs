using System.Collections.Generic;
using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.Feedback
{
    /// <summary>
    /// Растворяет модель решетом, пока хоть кто-то об этом просит. Само ничего не решает:
    /// «когда растворяться» знает тот, кто просит, — например система, гасящая препятствие
    /// между камерой и персонажем.
    ///
    /// Заявка именная: <see cref="Fade"/> и <see cref="Release"/> берут просящего.
    /// Общий флаг здесь неправильный ответ — за один и тот же объект однажды возьмутся
    /// двое (заслон камере, катсцена, растворение при появлении), и тот, кто отпустил
    /// первым, погасил бы чужое растворение. Пока жива хоть одна заявка, объект растворён.
    ///
    /// Насколько растворять — свойство самого объекта, а не просящего: толстая стена
    /// и редкий забор гаснут по-разному, и знает об этом тот, кто их ставил.
    ///
    /// Шейдеру нужно свойство _DitherFade и ВКЛЮЧЁННОЕ решето. Готовый такой шейдер
    /// лежит в пакете — FeatherKit/Toon Lit.
    /// </summary>
    [DisallowMultipleComponent]
    [FDescription("Растворяет объект решетом, пока кто-то об этом просит. Сам не решает когда — это дело просящего.")]
    public class DitherFade : MonoBehaviour
    {
        private const string DitherKeyword = "_DITHER_ON";

        private static readonly int FallbackFadeId = Shader.PropertyToID("_DitherFade");

        [Tooltip("Насколько растворять, когда просят: 1 — объект пропадает совсем")]
        [SerializeField, Range(0f, 1f)] private float amount = 0.7f;

        [Tooltip("За сколько секунд растворяется и возвращается обратно, сек")]
        [SerializeField] private float duration = 0.15f;

        [Tooltip("Имя float-свойства растворения в шейдере")]
        [SerializeField] private string fadeProperty = "_DitherFade";

        private readonly HashSet<object> requesters = new HashSet<object>();

        private MaterialCopies materials;
        private int fadeId;
        private float current;


        /// <summary>Просит ли кто-нибудь растворения прямо сейчас.</summary>
        public bool IsFading => requesters.Count > 0;

        /// <summary>Насколько объект растворён сейчас: 0 — плотный, 1 — невидимый.</summary>
        public float Current => current;

        /// <summary>Насколько растворять по заявке.</summary>
        public float Amount
        {
            get => amount;
            set => amount = Mathf.Clamp01(value);
        }


        // ── Заявки ────────────────────────────────────────────────────────────

        /// <summary>Попросить растворения от своего имени. Повторная просьба ничего не меняет.</summary>
        public void Fade(object owner)
        {
            if (owner != null)
                requesters.Add(owner);
        }

        /// <summary>Снять свою заявку. Не просил — ничего и не случится.</summary>
        public void Release(object owner)
        {
            if (owner != null)
                requesters.Remove(owner);
        }

        /// <summary>Снять все заявки разом. Для выгрузки сцены и возврата в пул.</summary>
        public void ReleaseAll()
        {
            requesters.Clear();
        }


        // ── Внутреннее ────────────────────────────────────────────────────────

        private void Apply(float value)
        {
            current = value;
            materials?.SetFloat(fadeId, value);
        }

        // Ругаемся один раз при старте, а не молчим: без ключевого слова свойство
        // не читается вовсе, и объект просто никогда не растворится. Найти это по виду
        // почти невозможно — в материале всё выглядит настроенным.
        private void WarnIfNotReady()
        {
            if (materials.Count == 0)
            {
                Debug.LogWarning($"[DitherFade] {name}: ни у одного материала нет свойства {fadeProperty} — растворять нечего.", this);
                return;
            }

            if (!materials.HasKeyword(DitherKeyword))
                Debug.LogWarning($"[DitherFade] {name}: у материала выключено решето (галка Dithering) — растворение работать не будет.", this);
        }


        private void Awake()
        {
            fadeId = string.IsNullOrEmpty(fadeProperty) ? FallbackFadeId : Shader.PropertyToID(fadeProperty);
            materials = new MaterialCopies(gameObject, fadeId);

            WarnIfNotReady();
        }

        private void OnEnable()
        {
            // Объект мог прийти из пула недорастворённым с прошлой жизни.
            requesters.Clear();
            Apply(0f);
        }

        private void OnDisable()
        {
            requesters.Clear();
            Apply(0f);
        }

        private void OnDestroy()
        {
            materials?.Dispose();
        }

        private void Update()
        {
            var target = IsFading ? amount : 0f;

            // Пока значение на месте — не трогаем материалы вовсе. Растворяемых объектов
            // на уровне сотни, и почти все стоят плотными весь забег.
            if (Mathf.Approximately(current, target))
                return;

            var step = duration > 0f ? Time.unscaledDeltaTime / duration : 1f;

            Apply(Mathf.MoveTowards(current, target, step));
        }
    }
}
