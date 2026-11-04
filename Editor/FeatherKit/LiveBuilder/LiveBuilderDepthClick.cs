using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Повторный клик в то же место — следующее по глубине под мышью, как в Unity: первый клик выделяет ближайшее
    /// (<see cref="LiveBuilderClickSelection"/>), каждый следующий — то, что за ним, после самого дальнего — снова ближайшее.
    /// Под мышью — всё, сквозь что проходит луч, и закрытое тоже: грани объектов из коробок (в режиме «Грани» — куски граней,
    /// иначе — коробки, что их создали), стены по линии и здания, резчик дыры. Только то, что правит выбранный инструмент.
    /// «То же место» — мышь сдвинулась не больше чем на несколько пикселей, камера стоит, выделено то, что выбрал прошлый клик.
    /// </summary>
    public static class LiveBuilderDepthClick
    {
        // Мышь дальше этого от прошлого клика — клик уже в другом месте, пикс.
        private const float SamePlacePixels = 4f;

        // Камера сдвинулась или повернулась больше этого — вид другой, м и градусы.
        private const float SameCameraMeters = 0.001f;
        private const float SameCameraDegrees = 0.01f;


        private static bool hasLastClick;
        private static Vector2 lastMousePosition;
        private static Vector3 lastCameraPosition;
        private static Quaternion lastCameraRotation;
        private static LiveBuilderTool lastTool;
        private static DepthClickTarget lastTarget;
        private static int lastTargetNumber;
        private static int lastTargetCount;


        // ── Повторный клик ───────────────────────────────────────────────

        // Клик в то же место, что и прошлый, — выделить следующее по глубине. Не повтор или выбирать не из чего — false: клик обычный.
        public static bool TrySelectDeeper(Vector2 mousePosition, LiveBuilderTool tool)
        {
            var camera = Camera.current;
            return camera != null && TrySelectDeeper(mousePosition, HandleUtility.GUIPointToWorldRay(mousePosition), camera.transform, tool);
        }

        // То же с лучом и камерой напрямую — без окна сцены (проверки).
        public static bool TrySelectDeeper(Vector2 mousePosition, Ray worldRay, Transform cameraTransform, LiveBuilderTool tool)
        {
            if (!IsRepeatAt(mousePosition, cameraTransform, tool))
                return false;

            var targets = CollectTargets(worldRay, tool);

            if (targets.Count < 2)
                return false;

            var index = targets.FindIndex(target => target.IsSameAs(lastTarget));
            var next = targets[(index + 1) % targets.Count];
            next.Select();
            Remember(mousePosition, cameraTransform, tool, next, targets);
            return true;
        }

        private static bool IsRepeatAt(Vector2 mousePosition, Transform cameraTransform, LiveBuilderTool tool)
        {
            return hasLastClick && tool == lastTool
                   && (mousePosition - lastMousePosition).magnitude <= SamePlacePixels
                   && Vector3.Distance(cameraTransform.position, lastCameraPosition) <= SameCameraMeters
                   && Quaternion.Angle(cameraTransform.rotation, lastCameraRotation) <= SameCameraDegrees
                   && lastTarget.IsSelectedAlone();
        }


        // После обычного клика: запомнить, что он выделил, — следующий клик сюда пойдёт вглубь. Выделено не одно — помнить нечего.
        public static void RememberClick(Vector2 mousePosition, LiveBuilderTool tool)
        {
            hasLastClick = false;
            var camera = Camera.current;

            if (camera != null)
                RememberClick(mousePosition, HandleUtility.GUIPointToWorldRay(mousePosition), camera.transform, tool);
        }

        // То же с лучом и камерой напрямую — без окна сцены (проверки).
        public static void RememberClick(Vector2 mousePosition, Ray worldRay, Transform cameraTransform, LiveBuilderTool tool)
        {
            hasLastClick = false;

            if (DepthClickTarget.TryFromSelection(IsSelectingFacesWith(tool), out var selected))
                Remember(mousePosition, cameraTransform, tool, selected, CollectTargets(worldRay, tool));
        }

        // Shift-клик, рисование и прочее — прошлый клик забыт.
        public static void Forget()
        {
            hasLastClick = false;
        }

        private static void Remember(Vector2 mousePosition, Transform cameraTransform, LiveBuilderTool tool, DepthClickTarget target, List<DepthClickTarget> targets)
        {
            hasLastClick = true;
            lastMousePosition = mousePosition;
            lastCameraPosition = cameraTransform.position;
            lastCameraRotation = cameraTransform.rotation;
            lastTool = tool;
            lastTarget = target;
            lastTargetCount = targets.Count;
            lastTargetNumber = targets.FindIndex(candidate => candidate.IsSameAs(target)) + 1;
        }


        // ── Подсказка ────────────────────────────────────────────────────

        // «Клик ещё раз сюда — глубже (2 из 4)», пока мышь там, где кликнули, и под ней есть что-то ещё; иначе пусто.
        public static string HintAt(Vector2 mousePosition, LiveBuilderTool tool)
        {
            if (!hasLastClick || tool != lastTool || lastTargetCount < 2 || (mousePosition - lastMousePosition).magnitude > SamePlacePixels
                || !lastTarget.IsSelectedAlone())
                return "";

            var number = lastTargetNumber > 0 ? lastTargetNumber + " из " + lastTargetCount : "из " + lastTargetCount;
            return " · Клик ещё раз сюда — глубже (" + number + ")";
        }


        // ── Что под мышью по глубине ─────────────────────────────────────

        // Всё под лучом, что правит инструмент, от ближнего к дальнему, без повторов: у грани — кусок или его коробка, у коллайдера — стена или здание.
        private static List<DepthClickTarget> CollectTargets(Ray worldRay, LiveBuilderTool tool)
        {
            var targets = new List<DepthClickTarget>();
            var isSelectingFaces = IsSelectingFacesWith(tool);

            foreach (var surfaceHit in BoxModelPicker.PickAllSurfaces(worldRay))
            {
                if (isSelectingFaces && BoxModelPartFace.TryFindUnder(surfaceHit, out var face))
                {
                    targets.Add(DepthClickTarget.ForFace(face, surfaceHit.Distance));
                    continue;
                }

                var owner = BoxModelPicker.SourceBoxOf(surfaceHit)?.Owner;
                AddObject(targets, tool, owner != null ? owner.gameObject : surfaceHit.Model.gameObject, surfaceHit.Distance);
            }

            foreach (var physicsHit in Physics.RaycastAll(worldRay, float.MaxValue, ~0, QueryTriggerInteraction.Ignore))
            {
                var pathOwner = LiveBuilderPathOwners.OwnerOf(physicsHit.collider);

                if (pathOwner != null && !LiveBuilderStoreys.IsHiddenBySection(physicsHit.point))
                    AddObject(targets, tool, pathOwner.gameObject, physicsHit.distance);
            }

            if (GeometryCutterPicker.TryPick(worldRay, out var cutterHit))
                AddObject(targets, tool, cutterHit.Cutter.gameObject, cutterHit.EntryDistance);

            targets.Sort((first, second) => first.Distance.CompareTo(second.Distance));
            RemoveRepeats(targets);
            return targets;
        }

        private static void AddObject(List<DepthClickTarget> targets, LiveBuilderTool tool, GameObject target, float distance)
        {
            if (tool.CanEdit(target))
                targets.Add(DepthClickTarget.ForObject(target, distance));
        }

        // Режим «Грани» и инструмент правит грани — клик выделяет куски граней.
        private static bool IsSelectingFacesWith(LiveBuilderTool tool)
        {
            return BoxModelSelectionModeSetting.IsSelectingFaces && tool.CanEditFaces;
        }

        // Одна коробка или стена под лучом дважды (вход и выход, несколько граней) — остаётся ближняя.
        private static void RemoveRepeats(List<DepthClickTarget> targets)
        {
            for (var index = targets.Count - 1; index > 0; index--)
                for (var earlier = 0; earlier < index; earlier++)
                    if (targets[earlier].IsSameAs(targets[index]))
                    {
                        targets.RemoveAt(index);
                        break;
                    }
        }


        // ── Цель клика ───────────────────────────────────────────────────

        /// <summary>Что выделит клик вглубь: кусок грани (в режиме «Грани») или объект — и как далеко он по лучу, м.</summary>
        private readonly struct DepthClickTarget
        {
            private DepthClickTarget(GameObject targetObject, BoxModelPartFace face, bool isFace, float distance)
            {
                TargetObject = targetObject;
                Face = face;
                IsFace = isFace;
                Distance = distance;
            }


            public GameObject TargetObject { get; }
            public BoxModelPartFace Face { get; }
            public bool IsFace { get; }
            public float Distance { get; }


            public static DepthClickTarget ForFace(BoxModelPartFace face, float distance) => new(null, face, true, distance);

            public static DepthClickTarget ForObject(GameObject targetObject, float distance) => new(targetObject, default, false, distance);


            // Выделено ровно одно — кусок грани (в режиме «Грани») или объект: его и запомнить.
            public static bool TryFromSelection(bool isSelectingFaces, out DepthClickTarget selected)
            {
                var faces = BoxModelFaceSelection.Faces;

                if (isSelectingFaces && faces.Count == 1 && faces[0].IsPiece)
                {
                    selected = ForFace(faces[0], 0f);
                    return true;
                }

                if ((!isSelectingFaces || faces.Count == 0) && Selection.objects.Length == 1 && Selection.activeGameObject != null)
                {
                    selected = ForObject(Selection.activeGameObject, 0f);
                    return true;
                }

                selected = default;
                return false;
            }


            public bool IsSameAs(DepthClickTarget other)
            {
                return IsFace == other.IsFace && (IsFace ? Face.IsSameAs(other.Face) : TargetObject == other.TargetObject);
            }


            // Выделено только оно: значит, после прошлого клика выделение никто не менял.
            public bool IsSelectedAlone()
            {
                if (IsFace)
                    return BoxModelFaceSelection.Faces.Count == 1 && BoxModelFaceSelection.Faces[0].IsSameAs(Face);

                return TargetObject != null && Selection.objects.Length == 1 && Selection.activeGameObject == TargetObject
                       && (!BoxModelSelectionModeSetting.IsSelectingFaces || BoxModelFaceSelection.Faces.Count == 0);
            }


            // Выделить только его, как обычный клик без Shift.
            public void Select()
            {
                if (IsFace)
                {
                    BoxModelFaceSelection.Select(Face, false);
                    return;
                }

                BoxModelFaceSelection.Clear();
                Selection.activeGameObject = TargetObject;
            }
        }
    }
}
