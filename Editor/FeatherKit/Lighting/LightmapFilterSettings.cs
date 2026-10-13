using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Настройки фильтра карт света: включён ли он и какой фильтр ставить.
    /// Общие на проект: лежат в ProjectSettings — попадают в git.
    /// Сами карты не трогает, этим занят <see cref="LightmapFilterApplier"/>.
    /// </summary>
    [FilePath("ProjectSettings/FeatherKit/LightmapFilter.asset", FilePathAttribute.Location.ProjectFolder)]
    public class LightmapFilterSettings : ScriptableSingleton<LightmapFilterSettings>
    {
        [SerializeField] private bool isEnabled;
        [SerializeField] private FilterMode filterMode = FilterMode.Point;


        /// <summary>Ставить ли фильтр сам после каждого запекания.</summary>
        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (isEnabled == value)
                    return;

                isEnabled = value;
                Save(true);
            }
        }

        /// <summary>Какой фильтр ставить картам света: Point — квадратами, Bilinear — плавно.</summary>
        public FilterMode FilterMode
        {
            get => filterMode;
            set
            {
                if (filterMode == value)
                    return;

                filterMode = value;
                Save(true);
            }
        }
    }
}
