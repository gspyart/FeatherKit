using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Этажи Live Builder: высота этажа, где первый этаж и на каком этаже работаешь сейчас (людям — с единицы, <see cref="CurrentStoreyNumber"/>).
    /// Рисование в пустоте идёт от пола текущего этажа. Разрез прячет в окне сцены всё выше плоскости над полом этажа —
    /// видно этаж сверху, как план, и можно работать внутри дома. Разрез — ключевое слово и высота для шейдера проекта
    /// (имена — в настройках проекта, <see cref="LiveBuilderProjectSettings"/>), только для камер окна сцены: игра не меняется.
    /// Этажи и разрез свои у каждого человека, хранятся в настройках редактора.
    /// Читаются оттуда один раз и дальше держатся в памяти: их спрашивают на каждый кадр камеры и каждую коробку при отрисовке.
    /// </summary>
    [InitializeOnLoad]
    public static class LiveBuilderStoreys
    {
        private const string PreferencePrefix = "FeatherKit.LiveBuilder.";

        // Самый низкий и самый высокий этаж — подвалы и высотки.
        private const int LowestStorey = -5;
        private const int HighestStorey = 60;

        // Настройки в памяти — копия настроек редактора, пишутся туда же при каждой смене.
        private static float storeyHeight;
        private static float groundLevel;
        private static int currentStorey;
        private static bool isSectionEnabled;
        private static float sectionHeightAboveFloor;

        // Слово и высота разреза для шейдера по именам из настроек проекта. Нужны на каждый кадр камеры — держим, пока имя то же.
        private static string sectionKeywordName;
        private static GlobalKeyword sectionKeyword;
        private static string sectionHeightPropertyName;
        private static int sectionHeightId;


        static LiveBuilderStoreys()
        {
            storeyHeight = EditorPrefs.GetFloat(PreferencePrefix + "StoreyHeight", 4f);
            groundLevel = EditorPrefs.GetFloat(PreferencePrefix + "GroundLevel", 0f);
            currentStorey = EditorPrefs.GetInt(PreferencePrefix + "CurrentStorey", 0);
            isSectionEnabled = EditorPrefs.GetBool(PreferencePrefix + "IsSectionEnabled", false);
            sectionHeightAboveFloor = EditorPrefs.GetFloat(PreferencePrefix + "SectionHeightAboveFloor", 3f);

            RenderPipelineManager.beginCameraRendering += ApplyToCamera;
        }


        // Поменялись этаж, высота или разрез — окно и сцена перерисовываются.
        public static event Action Changed;


        // ── Настройки ────────────────────────────────────────────────────

        public static float StoreyHeight
        {
            get => storeyHeight;
            set
            {
                storeyHeight = Mathf.Max(0.5f, value);
                SetFloat("StoreyHeight", storeyHeight);
            }
        }

        // Высота пола первого этажа в мире, м.
        public static float GroundLevel
        {
            get => groundLevel;
            set
            {
                groundLevel = value;
                SetFloat("GroundLevel", groundLevel);
            }
        }

        public static int CurrentStorey
        {
            get => currentStorey;
            set
            {
                currentStorey = Mathf.Clamp(value, LowestStorey, HighestStorey);
                EditorPrefs.SetInt(PreferencePrefix + "CurrentStorey", currentStorey);
                NotifyChanged();
            }
        }

        // Номер текущего этажа для людей — как в жизни и у зданий: первый этаж — 1, подвалы — −1, −2. Сам этаж хранится с нуля.
        public static int CurrentStoreyNumber
        {
            get => currentStorey >= 0 ? currentStorey + 1 : currentStorey;
            set => CurrentStorey = value > 0 ? value - 1 : value;
        }

        public static bool IsSectionEnabled
        {
            get => isSectionEnabled;
            set
            {
                isSectionEnabled = value;
                EditorPrefs.SetBool(PreferencePrefix + "IsSectionEnabled", value);

                if (!value)
                    Shader.SetKeyword(SectionKeyword, false);

                NotifyChanged();
            }
        }

        // На сколько плоскость разреза выше пола этажа, м. Ниже потолка — видно комнаты сверху.
        public static float SectionHeightAboveFloor
        {
            get => sectionHeightAboveFloor;
            set
            {
                sectionHeightAboveFloor = Mathf.Max(0.1f, value);
                SetFloat("SectionHeightAboveFloor", sectionHeightAboveFloor);
            }
        }

        // Высота пола текущего этажа в мире, м.
        public static float CurrentFloorHeight => GroundLevel + CurrentStorey * StoreyHeight;

        // Высота плоскости разреза в мире, м.
        public static float SectionPlaneHeight => CurrentFloorHeight + SectionHeightAboveFloor;


        // Слово разреза; имя в настройках сменили — старое слово выключается, чтобы не осталось висеть.
        private static GlobalKeyword SectionKeyword
        {
            get
            {
                var name = LiveBuilderProjectSettings.instance.SectionKeyword;

                if (name == sectionKeywordName)
                    return sectionKeyword;

                if (!string.IsNullOrEmpty(sectionKeywordName))
                    Shader.SetKeyword(sectionKeyword, false);

                sectionKeywordName = name;
                sectionKeyword = GlobalKeyword.Create(string.IsNullOrEmpty(name) ? "LIVE_BUILDER_SECTION_VIEW" : name);
                return sectionKeyword;
            }
        }

        private static int SectionHeightId
        {
            get
            {
                var name = LiveBuilderProjectSettings.instance.SectionHeightProperty;

                if (name != sectionHeightPropertyName)
                {
                    sectionHeightPropertyName = name;
                    sectionHeightId = Shader.PropertyToID(string.IsNullOrEmpty(name) ? "_LiveBuilderSectionHeight" : name);
                }

                return sectionHeightId;
            }
        }


        // ── Разрез ───────────────────────────────────────────────────────

        // Точка спрятана разрезом — её не выбирают кликом и на ней не рисуют.
        public static bool IsHiddenBySection(Vector3 worldPoint)
        {
            return IsSectionEnabled && worldPoint.y > SectionPlaneHeight + 0.001f;
        }


        // Перед кадром камеры: разрез — только окну сцены
        private static void ApplyToCamera(ScriptableRenderContext context, Camera camera)
        {
            var isSectioned = IsSectionEnabled && camera.cameraType == CameraType.SceneView;
            Shader.SetKeyword(SectionKeyword, isSectioned);

            if (isSectioned)
                Shader.SetGlobalFloat(SectionHeightId, SectionPlaneHeight);
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static void SetFloat(string name, float value)
        {
            EditorPrefs.SetFloat(PreferencePrefix + name, value);
            NotifyChanged();
        }

        private static void NotifyChanged()
        {
            Changed?.Invoke();
            SceneView.RepaintAll();
        }
    }
}
