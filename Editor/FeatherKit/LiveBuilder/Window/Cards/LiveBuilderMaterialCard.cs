using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Секция Material — материал и текстура выделенного по сторонам (<see cref="LiveBuilderMaterialSlots.PlacesOf"/>): у коробки All, Top, Bottom, ±X, ±Z,
    /// у здания — наружные, внутренние, пол, потолок, крыша. Правка — всем выделенным одного вида, разное — «—».
    /// - сторона, материал, подгонка текстуры (<see cref="LiveBuilderTextureFitFields"/>), World / Object у постройки
    /// - Fit — одна плитка на всю сторону коробки; Reset — подгонку «как есть», материал остаётся
    /// </summary>
    public sealed class LiveBuilderMaterialCard : LiveBuilderSelectionCard
    {
        // Какая сторона выбрана — своя у каждого вида, помнится до перезагрузки скриптов.
        private static readonly Dictionary<LiveBuilderSelectionKind, int> ChosenPlaces = new();


        public override int Order => MaterialOrder;

        public override string Title => "Material";


        public override bool ShowsFor(IReadOnlyList<GameObject> selected)
        {
            var kind = LiveBuilderSelectionKinds.KindOf(selected[0]);
            return kind is not (LiveBuilderSelectionKind.Other or LiveBuilderSelectionKind.Storey or LiveBuilderSelectionKind.BuildingPart or LiveBuilderSelectionKind.Module)
                   && LiveBuilderMaterialSlots.PlacesOf(selected[0]).Count > 0;
        }


        public override string SummaryOf(IReadOnlyList<GameObject> selected)
        {
            var places = LiveBuilderMaterialSlots.PlacesOf(selected[0]);
            return places.Count > 0 && places[0].Slot.ReadPaint(out var material, out _) && material != null ? material.name : null;
        }


        public override void Draw(IReadOnlyList<GameObject> selected)
        {
            var kind = LiveBuilderSelectionKinds.KindOf(selected[0]);
            var places = LiveBuilderMaterialSlots.PlacesOf(selected[0]);
            var chosen = Mathf.Clamp(ChosenPlaces.TryGetValue(kind, out var stored) ? stored : 0, 0, places.Count - 1);

            if (places.Count > 1)
                chosen = DrawPlacePopup(places, chosen);

            ChosenPlaces[kind] = chosen;
            var slots = SlotsAt(selected, chosen);

            DrawMaterial(slots);
            LiveBuilderTextureFitFields.Draw(slots);
            DrawAlignment(selected);
            DrawFitButtons(selected, chosen, slots);
        }


        // ── Сторона и материал ───────────────────────────────────────────

        private static int DrawPlacePopup(List<LiveBuilderMaterialPlace> places, int chosen)
        {
            var names = places.ConvertAll(place => place.Name).ToArray();
            return EditorGUILayout.Popup(new GUIContent("Side", "Какую сторону красить. All — всё целиком: свои материалы и подгонки сторон снимаются"), chosen, names);
        }


        // Место с этим номером у каждого выделенного: одного вида — места те же.
        private static List<LiveBuilderMaterialSlot> SlotsAt(IReadOnlyList<GameObject> selected, int index)
        {
            var slots = new List<LiveBuilderMaterialSlot>();

            foreach (var each in selected)
            {
                var places = LiveBuilderMaterialSlots.PlacesOf(each);

                if (index < places.Count && places[index].Slot.IsValid)
                    slots.Add(places[index].Slot);
            }

            return slots;
        }


        private static void DrawMaterial(List<LiveBuilderMaterialSlot> slots)
        {
            if (slots.Count == 0)
                return;

            var materials = slots.ConvertAll(slot => slot.ReadPaint(out var material, out _) ? material : null);
            EditorGUI.showMixedValue = materials.Exists(material => material != materials[0]);
            EditorGUI.BeginChangeCheck();
            var chosen = (Material)EditorGUILayout.ObjectField(new GUIContent("Material", "Пусто — материал того, что за этой стороной: объекта, здания"),
                materials[0], typeof(Material), false);
            EditorGUI.showMixedValue = false;

            if (EditorGUI.EndChangeCheck())
                foreach (var slot in slots)
                    slot.TryAssign(chosen);

            if (slots[0].Problem != null)
                LiveBuilderWindowControls.Note(slots[0].Problem);
        }


        // ── Выравнивание ─────────────────────────────────────────────────

        // World — рисунок по миру, без швов между постройками; Object — по осям самой постройки. Поле у постройки, у коробки — у её объекта.
        private static void DrawAlignment(IReadOnlyList<GameObject> selected)
        {
            var owners = new List<Object>();

            foreach (var each in selected)
            {
                var owner = AlignmentOwnerOf(each);

                if (owner != null && !owners.Contains(owner))
                    owners.Add(owner);
            }

            if (owners.Count == 0)
                return;

            var serializedOwners = new SerializedObject(owners.ToArray());
            var property = serializedOwners.FindProperty("textureAlignment");

            if (property == null)
                return;

            serializedOwners.Update();
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            var isWorld = LiveBuilderWindowControls.Switch(new GUIContent("Texture", "World — рисунок по миру: без швов между этажами и коробками. " +
                                                                                     "Object — по осям постройки, едет вместе с ней. У коробки — на весь её объект"),
                property.enumValueIndex == (int)LiveBuilderTextureAlignment.World, "Object", "World");
            EditorGUI.showMixedValue = false;

            var value = (int)(isWorld ? LiveBuilderTextureAlignment.World : LiveBuilderTextureAlignment.Object);

            if (value != property.enumValueIndex)
            {
                property.enumValueIndex = value;
                serializedOwners.ApplyModifiedProperties();
            }
        }

        private static Component AlignmentOwnerOf(GameObject selected)
        {
            if (selected.TryGetComponent<SplineBuilding>(out var building))
                return building;

            if (selected.TryGetComponent<SplineWall>(out var wall))
                return wall;

            if (selected.TryGetComponent<LiveBuilderColumn>(out var column))
                return column;

            return selected.GetComponentInParent<BoxModel>();
        }


        // ── Fit и Reset ──────────────────────────────────────────────────

        private static void DrawFitButtons(IReadOnlyList<GameObject> selected, int placeIndex, List<LiveBuilderMaterialSlot> slots)
        {
            var parts = new List<BoxModelPart>();

            foreach (var each in selected)
                if (each.TryGetComponent<BoxModelPart>(out var part))
                    parts.Add(part);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(parts.Count == 0))
                    if (GUILayout.Button(new GUIContent("Fit", parts.Count > 0
                            ? "Одна плитка материала — ровно на всю сторону коробки; у All — на каждую сторону"
                            : "Fit — только у сторон коробки"), EditorStyles.miniButtonLeft, GUILayout.Width(48f)))
                        FitToSides(parts, placeIndex);

                if (GUILayout.Button(new GUIContent("Reset", "Подгонка текстуры — как есть; материал остаётся"), EditorStyles.miniButtonRight, GUILayout.Width(48f)))
                    ResetFits(slots);
            }
        }


        // Reset у «All» снимает и подгонки сторон: так же, как материал всей коробки.
        public static void ResetFits(IReadOnlyList<LiveBuilderMaterialSlot> slots)
        {
            foreach (var slot in slots)
            {
                if (!slot.Fit.CanSet)
                    continue;

                Undo.RecordObject(slot.Owner, "Reset Texture Fit");
                slot.Fit.Set(null);
                EditorUtility.SetDirty(slot.Owner);
            }
        }


        // Сторона с номером placeIndex у каждой коробки; «All» (без стороны) — каждая из шести.
        private static void FitToSides(List<BoxModelPart> parts, int placeIndex)
        {
            foreach (var part in parts)
            {
                var places = LiveBuilderMaterialSlots.PlacesOf(part.gameObject);
                var side = places[placeIndex].BoxSide;

                foreach (var place in places)
                    if (place.BoxSide >= 0 && (side < 0 || place.BoxSide == side))
                        FitToSide(part, place);
            }
        }

        private static void FitToSide(BoxModelPart part, LiveBuilderMaterialPlace place)
        {
            var model = part.GetComponentInParent<BoxModel>();

            if (model == null || !place.Slot.Fit.CanSet)
                return;

            place.Slot.ReadPaint(out var material, out var current);
            Undo.RecordObject(part, "Fit Texture");
            place.Slot.Fit.Set(FitOfSide(part, model, place.BoxSide, material, current.QuarterTurns));
            EditorUtility.SetDirty(part);
        }


        // Сторона коробки в плоских метрах объекта — те же оси и сдвиг до мира, что у развёртки коробки (BoxModelMeshBuilder).
        public static LiveBuilderTextureFit FitOfSide(BoxModelPart part, BoxModel model, int side, Material material, int quarterTurns)
        {
            var axisU = BoxFaces.SurfaceAxisU(side);
            var axisV = BoxFaces.SurfaceAxisV(side);
            var min = BoxModelShapeCollector.LocalMinOf(model, part.transform);
            var max = min + part.Size;
            var space = LiveBuilderTextureSpace.Of(model.transform, model.TextureAlignment);
            var worldShift = new Vector2(space.ShiftAlongAxis(axisU), space.ShiftAlongAxis(axisV));

            return LiveBuilderTextureMapping.FitToFace(new Vector2(min[axisU], min[axisV]), new Vector2(max[axisU], max[axisV]), worldShift,
                LiveBuilderMaterialTiling.SizeOf(material), quarterTurns);
        }
    }
}
