using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace FeatherKit.Scenarios
{
    /// <summary>Вход в триггер или выход из него для объектов с нужным тегом; без тега — для любых.</summary>
    [Serializable]
    [ScenarioBlock("Триггер/Объект с тегом в триггере")]
    [MovedFrom(false, sourceClassName: "TaggedZoneEvent")]
    public class TaggedTriggerEvent : TriggerEvent
    {
        [Tooltip("Тег гостя. Пусто — любой объект. Смотрится у объекта с Rigidbody, а без него — у самого коллайдера")]
        [ScenarioLabel("Тег гостя")]
        [SerializeField] private string visitorTag;


        public override string Summary => string.IsNullOrEmpty(visitorTag)
            ? base.Summary + " · кто угодно"
            : base.Summary + " · тег " + visitorTag;


        protected override bool IsVisitor(Collider other, out GameObject visitor)
        {
            visitor = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;

            return string.IsNullOrEmpty(visitorTag) || visitor.CompareTag(visitorTag);
        }
    }
}
