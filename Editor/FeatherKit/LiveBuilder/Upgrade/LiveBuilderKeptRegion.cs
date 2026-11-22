using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Часть комнаты или лестницы, которую старое правило оставляло целой внутри выреза, стоявшего в списке раньше, — для «Kept From Cuts».
    /// Сама она остаётся в объекте (вырез её режет, как велит новое правило), а вырезанная часть встаёт во вложенный объект простой коробкой
    /// с теми же материалами: вместе — то же тело.
    /// </summary>
    public sealed class LiveBuilderKeptRegion
    {
        private const string NameSuffix = " Kept";


        public LiveBuilderKeptRegion(BoxModel model, Transform parent, string sourceName, BoxModelSideMaterials materials, MillimeterBox box, int addIndex)
        {
            Model = model;
            Parent = parent;
            SourceName = sourceName;
            Materials = materials;
            Box = box;
            AddIndex = addIndex;
        }


        public BoxModel Model { get; }

        // Кому во вложенный объект: родитель исходной коробки.
        public Transform Parent { get; }

        // Имя исходной коробки; у части — оно же с « Kept».
        public string SourceName { get; }

        public BoxModelSideMaterials Materials { get; }

        // Часть в осях объекта, мм; оси вложенного объекта — те же.
        public MillimeterBox Box { get; }

        // Место исходной коробки в старом списке: вырезы ниже неё резали эту часть.
        public int AddIndex { get; }

        // Вложенный объект, в который часть встала; до правки — null.
        public BoxModel Group { get; private set; }


        // Коробка в «Kept From Cuts» на месте части, без Undo: это обновление при загрузке.
        public BoxModelPart CreateIn(BoxModel group)
        {
            Group = group;
            var partObject = new GameObject(SourceName + NameSuffix) { tag = BoxModelPieceTidying.EditorOnlyTag };
            partObject.transform.SetParent(group.transform, false);
            partObject.transform.SetPositionAndRotation(Model.transform.TransformPoint(Box.MinInMeters), Model.transform.rotation);
            partObject.layer = group.gameObject.layer;

            var part = partObject.AddComponent<BoxModelPart>();
            part.Size = Box.MaxInMeters - Box.MinInMeters;
            GeometryCutterBoxConversion.CopyMaterials(Materials, part);
            return part;
        }
    }
}
