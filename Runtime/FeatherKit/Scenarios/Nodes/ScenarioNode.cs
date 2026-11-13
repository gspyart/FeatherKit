using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// База ноды графа сценария.
    /// - постоянный id в графе: по нему провода, подсветка и сохранение
    /// - место на холсте и своя подпись поверх названия
    /// - порты: постоянные — статические поля типа, переменные — в DescribePorts
    /// </summary>
    [Serializable]
    public abstract class ScenarioNode
    {
        [SerializeField] private int id;
        [SerializeField] private Vector2 position;

        [Tooltip("The node's caption on the canvas. Empty — the block's name")]
        [SerializeField] private string customTitle;


        /// <summary>Id в своём графе: выдаёт граф, не повторяется даже после удаления ноды. 0 — нода ещё не в графе.</summary>
        public int Id
        {
            get => id;
            internal set => id = value;
        }

        /// <summary>Левый верхний угол ноды на холсте.</summary>
        public Vector2 Position
        {
            get => position;
            set => position = value;
        }

        public string CustomTitle
        {
            get => customTitle;
            set => customTitle = value;
        }

        /// <summary>Что написано в шапке ноды: своя подпись или название по умолчанию.</summary>
        public string Title => string.IsNullOrEmpty(customTitle) ? DefaultTitle : customTitle;

        /// <summary>Название по [ScenarioBlock]; без атрибута — имя класса.</summary>
        public virtual string DefaultTitle => ScenarioBlockAttribute.TitleOf(GetType());

        /// <summary>Короткая подпись настроек для ноды на холсте. Null — без неё.</summary>
        public virtual string Summary => null;


        // ── Порты ────────────────────────────────────────────────────────

        /// <summary>Свежий список портов ноды: у Wait For он меняется вместе с исходами, поэтому не кешируется.</summary>
        public ScenarioPortList CollectPorts()
        {
            var ports = new ScenarioPortList();
            DescribePorts(ports);

            return ports;
        }


        /// <summary>Переменное число портов — переопределить и дописать свои к постоянным.</summary>
        public virtual void DescribePorts(ScenarioPortList ports)
        {
            ScenarioNodePorts.AddStaticPorts(GetType(), ports);
        }
    }
}
