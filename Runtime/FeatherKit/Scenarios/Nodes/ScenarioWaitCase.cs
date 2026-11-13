using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Один исход ноды Wait For: событие или условие, ровно что-то одно, и свой выход.
    /// - id исхода постоянный: по нему провод находит выход «case.id»
    /// - пустая подпись — имя блока
    /// </summary>
    [Serializable]
    public class ScenarioWaitCase
    {
        public const string OutputIdPrefix = "case.";

        [SerializeField] private string id;

        [Tooltip("The output's caption on the node. Empty — the block's name")]
        [SerializeField] private string label;

        [SerializeReference] private ScenarioEvent awaitedEvent;
        [SerializeReference] private ScenarioCondition condition;


        public ScenarioWaitCase(ScenarioEvent awaitedEvent, string label = null)
        {
            id = CreateId();
            this.label = label;
            this.awaitedEvent = awaitedEvent;
        }

        public ScenarioWaitCase(ScenarioCondition condition, string label = null)
        {
            id = CreateId();
            this.label = label;
            this.condition = condition;
        }


        public string Id => id;

        /// <summary>Подпись выхода: своя или имя блока.</summary>
        public string Label
        {
            get
            {
                if (!string.IsNullOrEmpty(label))
                    return label;

                var block = (object)awaitedEvent ?? condition;

                return block != null ? ScenarioBlockAttribute.TitleOf(block.GetType()) : "Empty";
            }
        }

        public ScenarioEvent AwaitedEvent => awaitedEvent;

        public ScenarioCondition Condition => condition;

        /// <summary>Исход ждёт событие; иначе — проверяет условие.</summary>
        public bool IsEvent => awaitedEvent != null;

        /// <summary>Выход исхода. Не хранится: собирается из id, равенство портов — по id.</summary>
        public ScenarioOutput Output => new ScenarioOutput(OutputIdPrefix + id, Label);


        /// <summary>Короткий id нового исхода — один формат и для редактора, и для сборки кодом.</summary>
        public static string CreateId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8);
        }


        // Только для ноды: провода старого id после этого выход не найдут.
        internal void AssignNewId()
        {
            id = CreateId();
        }
    }
}
