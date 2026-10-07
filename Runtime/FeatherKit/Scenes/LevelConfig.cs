using UnityEngine;

namespace FeatherKit.Scenes
{
    /// <summary>
    /// Уровень как данные: какую сцену грузить и всё, что о ней нужно знать заранее —
    /// до того, как она загрузится.
    ///
    /// Базовый класс намеренно почти пустой: свои поля игра дописывает наследником —
    /// номер главы, награда, музыка, набор врагов. Загрузчик про них не знает и знать
    /// не должен.
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "FeatherKit/Level")]
    public class LevelConfig : ScriptableObject
    {
        [Tooltip("Имя сцены как в Build Settings, без пути и расширения. " +
                 "Сцены нет в списке сборки — загрузка не пройдёт, и загрузчик скажет об этом")]
        [SerializeField] private string sceneName;

        [Tooltip("Название для игрока — на экране загрузки или в меню выбора")]
        [SerializeField] private string displayName;


        public string SceneName => sceneName;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? sceneName : displayName;
    }
}
