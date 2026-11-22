using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Пипетка текстуры: Alt + клик по грани — запомнить её материал и подгонку, Alt + правый клик — положить их на другую грань.
    /// - место под мышью — то же, что у перетаскивания материала (<see cref="LiveBuilderMaterialSlots"/>), одним шагом Ctrl+Z
    /// - клик — без протягивания: Alt + протянуть — по-прежнему камера; нажатие с Alt Unity отдаёт камере, отпускание видно всегда
    /// - взятое помнится до перезагрузки скриптов
    /// </summary>
    public static class LiveBuilderTexturePipette
    {
        // Сдвиг мыши между нажатием и отпусканием больше этого — уже протягивание камеры, px.
        private const float MaxClickTravel = 4f;

        private static Material pickedMaterial;
        private static LiveBuilderTextureFit pickedFit;
        private static bool hasPick;

        private static Vector2 pressPosition;
        private static int pressButton = -1;


        public static bool HasPick => hasPick;

        public static Material PickedMaterial => pickedMaterial;

        public static LiveBuilderTextureFit PickedFit => pickedFit;


        // ── Взять и положить ─────────────────────────────────────────────

        // Материал и подгонка грани под лучом; под ним не Live Builder — false, взятое прежнее.
        public static bool TryPick(Ray worldRay)
        {
            if (!LiveBuilderMaterialSlots.TryFindUnder(worldRay, false, out var slot) || !slot.ReadPaint(out var material, out var fit))
                return false;

            pickedMaterial = material;
            pickedFit = fit.Copy();
            hasPick = true;
            ShowNotification("Picked " + NameOf(material) + " · " + slot.Description);
            return true;
        }


        // Положить взятое на грань под лучом. Материал нельзя (у стены тип) — подгонка ложится, в подсказке — почему без материала.
        public static bool TryApply(Ray worldRay)
        {
            if (!hasPick || !LiveBuilderMaterialSlots.TryFindUnder(worldRay, false, out var slot))
                return false;

            var isMaterialAssigned = slot.TryAssign(pickedMaterial, pickedFit);
            ShowNotification(isMaterialAssigned ? "Applied " + NameOf(pickedMaterial) + " · " + slot.Description : slot.Problem);
            return isMaterialAssigned || slot.Fit.CanSet;
        }


        // Забыть взятое.
        public static void Clear()
        {
            pickedMaterial = null;
            pickedFit = null;
            hasPick = false;
        }


        // ── События окна сцены ───────────────────────────────────────────

        // Зовёт инструмент Live Builder до раздачи событий, по rawType: нажатие с Alt забирает камера. Сработала пипетка — true.
        public static bool HandleEvent(Event current)
        {
            var isClickButton = current.button == 0 || current.button == 1;

            if (current.rawType == EventType.MouseDown && current.alt && isClickButton)
            {
                pressPosition = current.mousePosition;
                pressButton = current.button;
                return false;
            }

            if (current.rawType == EventType.ContextClick && current.alt)
            {
                current.Use();
                return true;
            }

            if (current.rawType != EventType.MouseUp || current.button != pressButton)
                return false;

            var isClick = current.alt && (current.mousePosition - pressPosition).magnitude < MaxClickTravel;
            pressButton = -1;

            if (!isClick)
                return false;

            var ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);

            if (current.button == 0 ? TryPick(ray) : TryApply(ray))
                current.Use();

            return true;
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static string NameOf(Material material)
        {
            return material != null ? material.name : "no material";
        }

        private static void ShowNotification(string text)
        {
            if (!string.IsNullOrEmpty(text) && SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.ShowNotification(new GUIContent(text), 1f);
        }
    }
}
