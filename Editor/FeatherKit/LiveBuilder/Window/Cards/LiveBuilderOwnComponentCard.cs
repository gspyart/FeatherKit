using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка вкладки «Выделено» со своими полями выделенной постройки: проём, комната, лестница, стена по линии, здание или объект из коробок —
    /// их инспектор прямо в окне. Комната любого вида называется «Комната» и одной строкой говорит, во что превратилась (<see cref="LiveBuilderRooms"/>).
    /// У объекта из коробок — ещё «В ProBuilder». Точки стены и здания — в <see cref="LiveBuilderPathPointsCard"/>.
    /// </summary>
    public sealed class LiveBuilderOwnComponentCard : LiveBuilderSelectionCard
    {
        private Editor componentEditor;


        public override int Order => 40;


        public override bool Draw(GameObject selected)
        {
            var component = OwnComponentOf(selected);

            if (component == null)
                return false;

            var roomKind = LiveBuilderRooms.KindOf(component);

            using (LiveBuilderWindowControls.Card(roomKind != null ? "Комната «" + selected.name + "»" : selected.name))
            {
                if (roomKind != null)
                    LiveBuilderWindowControls.Note(roomKind);

                DrawCachedInspector(component, null, ref componentEditor);

                if (component is BoxModel && BoxModelActions.CanFreezeToProBuilder && GUILayout.Button(new GUIContent("В ProBuilder", "Копия объекта обычным мешем для ручной правки; сам объект выключается")))
                    BoxModelActions.FreezeSelectedModel();
            }

            return true;
        }


        // IDisposable: отпустить готовый инспектор.
        public override void Dispose()
        {
            DestroyInspector(ref componentEditor);
        }
    }
}
