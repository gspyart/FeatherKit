using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace FeatherKit.Scenarios
{
    /// <summary>Вход в триггер или выход из него для объектов с нужным тегом; без тега — для любых.</summary>
    [Serializable]
    [ScenarioBlock("Trigger/Tagged Object in Trigger")]
    [MovedFrom(false, sourceClassName: "TaggedZoneEvent")]
    public class TaggedTriggerEvent : TriggerEvent
    {
        [Tooltip("The visitor's tag. Empty — any object. Checked on the object with the Rigidbody, or on the collider itself if there is none")]
        [ScenarioLabel("Visitor Tag")]
        [SerializeField] private string visitorTag;


        public override string Summary => string.IsNullOrEmpty(visitorTag)
            ? base.Summary + " · anyone"
            : base.Summary + " · tag " + visitorTag;


        protected override bool IsVisitor(Collider other, out GameObject visitor)
        {
            visitor = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;

            return string.IsNullOrEmpty(visitorTag) || visitor.CompareTag(visitorTag);
        }
    }
}
