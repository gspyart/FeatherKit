using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Что-то ударилось об объект сценария или отлипло от него. Нужен коллайдер и Rigidbody хотя бы у одного из двух.
    /// «Caller» — объект с Rigidbody, а если его нет — с коллайдером; Visitor Tag пустой — любой.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Physics/Collision", Description = "Something hits the scenario object or stops touching it")]
    public class CollisionEvent : ScenarioEvent
    {
        [ScenarioLabel("When")]
        [SerializeField] private ScenarioCollisionMoment moment;

        [Tooltip("The visitor's tag. Empty — any object")]
        [ScenarioLabel("Visitor Tag")]
        [SerializeField] private string visitorTag;


        public ScenarioCollisionMoment Moment
        {
            get => moment;
            set => moment = value;
        }

        public string VisitorTag
        {
            get => visitorTag;
            set => visitorTag = value;
        }

        public override string Summary => (moment == ScenarioCollisionMoment.Enter ? "enter" : "exit")
                                          + (string.IsNullOrEmpty(visitorTag) ? " · anyone" : " · tag " + visitorTag);


        protected override void OnAttached()
        {
            if (moment == ScenarioCollisionMoment.Enter)
                Scenario.CollisionEntered += HandleCollision;
            else
                Scenario.CollisionExited += HandleCollision;
        }

        protected override void OnReleased()
        {
            Scenario.CollisionEntered -= HandleCollision;
            Scenario.CollisionExited -= HandleCollision;
        }

        private void HandleCollision(Collision collision)
        {
            var visitor = collision.rigidbody != null ? collision.rigidbody.gameObject : collision.gameObject;

            if (string.IsNullOrEmpty(visitorTag) || visitor.CompareTag(visitorTag))
                Fire(visitor);
        }
    }


    /// <summary>Какой момент столкновения ловить. Новые — только в конец: в данных хранится число.</summary>
    public enum ScenarioCollisionMoment
    {
        [InspectorName("Enter")] Enter,
        [InspectorName("Exit")] Exit
    }
}
