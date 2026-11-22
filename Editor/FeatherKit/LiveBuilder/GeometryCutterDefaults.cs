using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Где режет» у нового выреза — по месту, куда его поставили: без постройки над ним (корень уровня, префаб двери) —
    /// «Everything it touches», внутри постройки — «Own object». Ставят инструменты (<see cref="BoxModelFactory"/>) и Add Component.
    /// </summary>
    [InitializeOnLoad]
    public static class GeometryCutterDefaults
    {
        static GeometryCutterDefaults()
        {
            ObjectFactory.componentWasAdded += OnComponentAdded;
        }


        public static void Apply(GeometryCutter cutter)
        {
            var hasConstructionAbove = LiveBuilderCutRules.OwnConstructionOf(cutter.transform) != null;
            cutter.Scope = hasConstructionAbove ? GeometryCutterScope.OwnObject : GeometryCutterScope.EverythingItTouches;
        }


        private static void OnComponentAdded(Component component)
        {
            if (component is GeometryCutter cutter)
                Apply(cutter);
        }
    }
}
