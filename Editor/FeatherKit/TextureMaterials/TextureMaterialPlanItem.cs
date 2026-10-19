namespace FeatherKit.EditorTools.TextureMaterials
{
    /// <summary>Что план решил про одну текстуру.</summary>
    public enum TextureMaterialDecision
    {
        /// <summary>Материала нет — будет создан.</summary>
        Create,

        /// <summary>У текстуры уже есть материал — дубликат не делаем.</summary>
        AlreadyHasMaterial,

        /// <summary>На месте нового материала лежит другой с тем же именем — не перезаписываем.</summary>
        NameTaken,

        /// <summary>Текстура импортирована как карта рельефа — это не цвет поверхности.</summary>
        NormalMap,

        /// <summary>Служебная карта по окончанию имени: _rough, _ao, _height…</summary>
        ServiceMapByName
    }


    /// <summary>
    /// Одна строка плана создания материалов: какая текстура, что с ней решено и где материал.
    /// Для «создать» путь — куда ляжет новый, для «уже есть» — где лежит найденный.
    /// </summary>
    public class TextureMaterialPlanItem
    {
        public TextureMaterialPlanItem(string texturePath, TextureMaterialDecision decision, string materialPath)
        {
            TexturePath = texturePath;
            Decision = decision;
            MaterialPath = materialPath;
        }


        public string TexturePath { get; }

        public TextureMaterialDecision Decision { get; }

        public string MaterialPath { get; }

        /// <summary>Текстура — цвет поверхности: материал у неё есть или будет. Служебные карты — нет.</summary>
        public bool IsSurfaceTexture => Decision == TextureMaterialDecision.Create
                                        || Decision == TextureMaterialDecision.AlreadyHasMaterial
                                        || Decision == TextureMaterialDecision.NameTaken;
    }
}
