using System;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Выделение коробки кликом в обычных инструментах — Move и прочих, как вход внутрь префаба:
    /// первый клик выделяет объект из коробок, следующий по нему же — коробку или лестницу, которая создала грань под мышью.
    /// Так выделенную коробку двигают штатным Move. В инструменте «Live Builder» выделение своё, этот проход молчит.
    /// </summary>
    [InitializeOnLoad]
    public static class BoxModelPartClickSelection
    {
        static BoxModelPartClickSelection()
        {
            HandleUtility.pickGameObjectCustomPasses += PickPartOfSelectedModel;
        }


        // Коробка под мышью, если её объект уже выделен (сам или любая его коробка); иначе — обычный выбор Unity.
        private static GameObject PickPartOfSelectedModel(Camera camera, int layers, Vector2 position, GameObject[] ignore,
            GameObject[] filter, out int materialIndex)
        {
            materialIndex = -1;

            if (BoxModelActions.IsToolActive || BoxModelActions.SelectedModel() == null || Camera.current == null)
                return null;

            if (!BoxModelPicker.TryPickSurface(HandleUtility.GUIPointToWorldRay(position), out var hit))
                return null;

            if (!IsModelOrItsPartSelected(hit.Model.gameObject))
                return null;

            var owner = BoxModelPicker.SourceBoxOf(hit)?.Owner;

            if (owner == null || (filter != null && Array.IndexOf(filter, owner.gameObject) < 0))
                return null;

            return owner.gameObject;
        }

        private static bool IsModelOrItsPartSelected(GameObject model)
        {
            foreach (var selected in Selection.gameObjects)
            {
                var selectedModel = selected.GetComponentInParent<BoxModel>();

                if (selectedModel != null && selectedModel.gameObject == model)
                    return true;
            }

            return false;
        }
    }
}
