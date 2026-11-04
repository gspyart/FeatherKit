using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Предпросмотр рисования: временный объект, которого нет в сцене и иерархии, показывает объект уже с новой коробкой,
    /// а настоящий объект на это время выключен. Обычный объект рисуется в каждом кадре — без мерцания.
    /// После перезагрузки скриптов посреди рисования временный объект убирается, а настоящий включается обратно.
    /// Меш у предпросмотра один на всё рисование и только перезаливается: удалять его на ходу нельзя — GPU Resident Drawer
    /// ещё держит его в кадре и пишет ошибку «invalid Batch, Mesh, or Material ID».
    /// </summary>
    public sealed class BoxModelPreview
    {
        private const string PreviewObjectName = "Box Model Preview";
        private const string HiddenModelSessionKey = "FeatherKit.LiveBuilder.HiddenModel";

        private GameObject previewObject;
        private Mesh previewMesh;
        private MeshRenderer hiddenRenderer;


        // ── Показ ────────────────────────────────────────────────────────

        // Перезаливает меш предпросмотра через fill и показывает его на месте объекта (или в осях мира, если объекта нет), а сам объект прячет.
        // fill кладёт геометрию в меш и отдаёт материалы его частей.
        public void Show(BoxModel model, System.Func<Mesh, Material[]> fill)
        {
            EnsurePreviewObject(model);
            previewObject.GetComponent<MeshRenderer>().sharedMaterials = fill(previewMesh);

            var frame = model != null ? model.transform : null;
            previewObject.transform.SetPositionAndRotation(frame != null ? frame.position : Vector3.zero, frame != null ? frame.rotation : Quaternion.identity);
            previewObject.transform.localScale = frame != null ? frame.lossyScale : Vector3.one;

            HideModel(model);
        }


        public void Clear()
        {
            if (previewObject != null)
                Object.DestroyImmediate(previewObject);

            if (previewMesh != null)
                Object.DestroyImmediate(previewMesh);

            previewObject = null;
            previewMesh = null;
            ShowHiddenModel();
        }


        // ── Временный объект ─────────────────────────────────────────────

        private void EnsurePreviewObject(BoxModel model)
        {
            if (previewObject != null)
                return;

            previewObject = new GameObject(PreviewObjectName, typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.HideAndDontSave };

            // Меш меняется почти каждый кадр — MarkDynamic подсказывает движку держать его под частую перезаливку.
            if (previewMesh == null)
            {
                previewMesh = new Mesh { name = PreviewObjectName, hideFlags = HideFlags.HideAndDontSave };
                previewMesh.MarkDynamic();
            }

            previewObject.GetComponent<MeshFilter>().sharedMesh = previewMesh;

            // Сцена объекта, а для нового — открытый префаб: иначе предпросмотр окажется там, где его не видно.
            if (model != null)
                SceneManager.MoveGameObjectToScene(previewObject, model.gameObject.scene);
            else
                StageUtility.PlaceGameObjectInCurrentStage(previewObject);
        }


        // ── Настоящий объект ─────────────────────────────────────────────

        // Выключает рендерер настоящего объекта: иначе он мерцает, наложившись на предпросмотр.
        private void HideModel(BoxModel model)
        {
            var modelRenderer = model != null ? model.GetComponent<MeshRenderer>() : null;

            if (modelRenderer == hiddenRenderer)
                return;

            ShowHiddenModel();

            if (modelRenderer == null || !modelRenderer.enabled)
                return;

            modelRenderer.enabled = false;
            hiddenRenderer = modelRenderer;
            SessionState.SetString(HiddenModelSessionKey, GlobalObjectId.GetGlobalObjectIdSlow(modelRenderer).ToString());
        }

        private void ShowHiddenModel()
        {
            if (hiddenRenderer != null)
                hiddenRenderer.enabled = true;

            hiddenRenderer = null;
            SessionState.EraseString(HiddenModelSessionKey);
        }


        // ── После перезагрузки скриптов ──────────────────────────────────

        [InitializeOnLoadMethod]
        private static void CleanUpAfterReload()
        {
            foreach (var leftover in Resources.FindObjectsOfTypeAll<GameObject>())
                if (leftover.name == PreviewObjectName && leftover.hideFlags == HideFlags.HideAndDontSave)
                    Object.DestroyImmediate(leftover);

            foreach (var leftoverMesh in Resources.FindObjectsOfTypeAll<Mesh>())
                if (leftoverMesh.name == PreviewObjectName && leftoverMesh.hideFlags == HideFlags.HideAndDontSave)
                    Object.DestroyImmediate(leftoverMesh);

            var hiddenId = SessionState.GetString(HiddenModelSessionKey, "");

            if (hiddenId.Length > 0 && GlobalObjectId.TryParse(hiddenId, out var globalId)
                && GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) is MeshRenderer modelRenderer)
                modelRenderer.enabled = true;

            SessionState.EraseString(HiddenModelSessionKey);
        }
    }
}
