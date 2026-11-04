using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка «Повторить» зоны «Выделено»: завернуть выделенные коробки и лестницы в повтор с готовым шагом — этажами вверх, вдоль X, вдоль Z.
    /// Выделены части разных уровней списка — почему нельзя.
    /// </summary>
    public sealed class LiveBuilderRepeatWrapCard : LiveBuilderSelectionCard
    {
        public override int Order => 20;


        public override bool Draw(GameObject selected)
        {
            if (LiveBuilderRepeatActions.SelectedPieces().Count == 0)
                return false;

            using (LiveBuilderWindowControls.Card("Повторить"))
            {
                if (!LiveBuilderRepeatActions.CanWrapSelection(out var reason))
                {
                    LiveBuilderWindowControls.Note("Нельзя: " + reason);
                    return true;
                }

                LiveBuilderWindowControls.Note("Копии считаются из оригинала: правишь один — меняются все.");

                using (new EditorGUILayout.HorizontalScope())
                {
                    var bounds = LiveBuilderRepeatActions.SelectedBounds();

                    // В здании этажи повторяет привязка — блок «Этажи в здании»; второй способ того же здесь не нужен.
                    if (!IsInBuilding(selected) && GUILayout.Button(new GUIContent("Этажи ↑", "Шаг — высота этажа")))
                        LiveBuilderRepeatActions.WrapSelection(new Vector3(0f, LiveBuilderStoreys.StoreyHeight, 0f));

                    if (GUILayout.Button(new GUIContent("По X", "Шаг — ширина выделенного по X, по сетке")))
                        LiveBuilderRepeatActions.WrapSelection(new Vector3(StepAround(bounds.size.x), 0f, 0f));

                    if (GUILayout.Button(new GUIContent("По Z", "Шаг — ширина выделенного по Z, по сетке")))
                        LiveBuilderRepeatActions.WrapSelection(new Vector3(0f, 0f, StepAround(bounds.size.z)));
                }
            }

            return true;
        }

        // Выделенное — в здании по контуру: этажи там повторяет привязка к зданию.
        private static bool IsInBuilding(GameObject selected)
        {
            return selected != null && selected.GetComponentInParent<SplineBuilding>() != null;
        }

        // Шаг повтора по размеру выделенного — с запасом в клетку сетки, чтобы копии не слиплись.
        private static float StepAround(float size)
        {
            var grid = LiveBuilderGrid.CurrentStep;
            return Mathf.Max(grid, Mathf.Ceil(size / grid) * grid + grid);
        }
    }
}
