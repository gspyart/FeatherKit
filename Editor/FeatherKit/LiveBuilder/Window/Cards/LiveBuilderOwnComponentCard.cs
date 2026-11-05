using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка зоны «Выделено» со своими полями выделенной постройки: проём, комната, лестница, стена по линии, здание или объект из коробок —
    /// их инспектор прямо в окне. У объекта из коробок — ещё «В ProBuilder». Точки стены и здания — в <see cref="LiveBuilderPathPointsCard"/>.
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

            using (LiveBuilderWindowControls.Card(selected.name))
            {
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
