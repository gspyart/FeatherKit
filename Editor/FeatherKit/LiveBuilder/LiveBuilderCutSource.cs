using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Вырез глазами правила (<see cref="LiveBuilderCutRules"/>): чей он, где режет, что режет и какой это кусок объекта.
    /// Собирается один раз на вырез — проём или вырезающую коробку солвера, — дальше правило только сравнивает.
    /// </summary>
    public readonly struct LiveBuilderCutSource
    {
        public LiveBuilderCutSource(Component owner, Transform ownConstruction, BoxModel ownModel, GeometryCutterScope scope,
            LiveBuilderCutTargets targets, int pieceKey)
        {
            Owner = owner;
            OwnConstruction = ownConstruction;
            OwnModel = ownModel;
            Scope = scope;
            Targets = targets;
            PieceKey = pieceKey;
        }


        // Кто дал вырез: проём, вырезающая коробка, комната, лестница. Null — коробка предпросмотра рисования.
        public Component Owner { get; }

        // Ближайшая постройка над вырезом: «Own object» режет её и то, что в ней. Null — постройки над вырезом нет.
        public Transform OwnConstruction { get; }

        // Ближайший объект из коробок над вырезом: во вложенные объекты «Own object» не заходит.
        public BoxModel OwnModel { get; }

        // Без старого «Своя группа»: оно уже прочитано как «Own object».
        public GeometryCutterScope Scope { get; }

        public LiveBuilderCutTargets Targets { get; }

        // Кусок объекта, которому вырез принадлежит (<see cref="BoxModelSolverBox.PieceKey"/>): свой кусок он не режет. 0 — ничей.
        public int PieceKey { get; }

        public bool CutsWalls => Targets != LiveBuilderCutTargets.SlabsOnly;

        public bool CutsSlabs => Targets != LiveBuilderCutTargets.WallsOnly;
    }
}
