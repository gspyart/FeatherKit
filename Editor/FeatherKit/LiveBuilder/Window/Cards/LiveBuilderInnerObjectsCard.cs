using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка «Внутри объекта» зоны «Выделено»: выделен объект из коробок, а в нём здания и стены по линии — кнопки, чтобы перейти к ним:
    /// их числа — у них самих.
    /// </summary>
    public sealed class LiveBuilderInnerObjectsCard : LiveBuilderSelectionCard
    {
        public override int Order => 80;


        public override bool Draw(GameObject selected)
        {
            if (selected == null || !selected.TryGetComponent<BoxModel>(out var model))
                return false;

            var inner = new List<Component>();
            inner.AddRange(model.GetComponentsInChildren<SplineBuilding>(false));
            inner.AddRange(model.GetComponentsInChildren<SplineWall>(false));

            if (inner.Count == 0)
                return false;

            using (LiveBuilderWindowControls.Card("Внутри объекта"))
            {
                LiveBuilderWindowControls.Note("Выдели, чтобы менять:");

                foreach (var owner in inner)
                {
                    var kind = owner is SplineBuilding ? "Здание" : "Стена";

                    if (GUILayout.Button(kind + " «" + owner.name + "»"))
                        Selection.activeGameObject = owner.gameObject;
                }
            }

            return true;
        }
    }
}
