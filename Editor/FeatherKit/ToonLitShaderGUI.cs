using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FeatherKit.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════════════
    // ПАМЯТКА ТОМУ, КТО БУДЕТ ЭТО ПРАВИТЬ — ЧЕЛОВЕКУ ИЛИ АГЕНТУ
    //
    // 1. Добавили свойство в шейдер — добавьте его СЮДА, в нужную секцию, и подпись
    //    в таблицу Tooltips. Свойство, не упомянутое здесь, в инспекторе не появится:
    //    базовый список мы не рисуем, только свои секции.
    //
    // 2. Подписи держим короткими и объясняющими ПОЧЕМУ, а не что. «Порог тени» человек
    //    и так прочитает в названии; ему нужно знать, что низкий порог съедает тонкие тени.
    //
    // 3. Инспектор не только рисует. Он доводит до конца режим поверхности: Surface Type —
    //    просто число, прозрачность включают смешивание, запись глубины и очередь ВМЕСТЕ.
    //    Это метод Apply. Трогая его, помните: он вызывается только на правку в инспекторе.
    //    Материалы, изменённые скриптом, через него не проходят — для них FMaterialUtils.Sanitize.
    //
    // 4. FMaterialUtils.Sanitize вызывается при смене шейдера на материале. Он включает все проходы
    //    и снимает чужие ключевые слова. Не убирайте: без него материал, пришедший
    //    от другого тун-шейдера, приезжает с выключенным проходом обводки, и найти
    //    это потом крайне тяжело — в инспекторе галка стоит, а контура нет. Метод общий
    //    с инспектором террейна, поэтому и живёт в EditorKit.
    //
    // 5. Оформление секций — в InspectorStyle, отдельным ассетом. Цифры и цвета правьте
    //    там, а не здесь.
    //
    // 6. Падающих теней здесь почти нет: сила, край, глубина и общий оттенок живут в рендер-фиче
    //    ToonLitSettingsFeature, общими для всех материалов. Материалу оставлено только своё —
    //    прозрачность тени на этой поверхности и добавочный оттенок, — плюс подсказка,
    //    где искать остальное.
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Инспектор материала для FeatherKit/Toon Lit: свойства разложены по секциям,
    /// у каждого подсказка, а режим поверхности доводится до рабочего состояния.
    /// </summary>
    public class ToonLitShaderGUI : ShaderGUI
    {
        // Подписи. Отдельной таблицей, а не по месту: так их видно списком и легко
        // вычитывать целиком, не выискивая по всему файлу.
        private static readonly Dictionary<string, string> Tooltips = new Dictionary<string, string>
        {
            ["_Surface"] = "Opaque — альфа считается, но выбрасывается. Transparent — смешивание с фоном, " +
                           "но объект перестаёт писать глубину и начинает спорить за порядок отрисовки.",
            ["_AlphaClip"] = "Выбрасывает пиксели ниже порога. Для листвы и решёток: дешевле прозрачности " +
                             "и не ломает порядок отрисовки.",
            ["_Cutoff"] = "Порог отсечения. Меньше — остаётся больше пикселей.",
            ["_Dither"] = "Прозрачность решёткой 4 на 4. Объект остаётся непрозрачным: пишет глубину " +
                          "и отбрасывает тень. Управляется альфой Base Color.",
            ["_DitherFade"] = "Насколько объект растворён, поверх альфы Base Color. Ноль — обычный вид. " +
                              "Это поле крутит КОД: компонент DitherFade гасит препятствие между камерой " +
                              "и персонажем. Выставленное руками значение он затрёт — здесь ползунок " +
                              "нужен, чтобы подобрать глазами, насколько сильно растворять.",

            ["_BaseMap"] = "Основная текстура и цвет. Альфа работает в режиме Transparent и в дизеринге.",
            ["_Saturation"] = "Насыщенность текстуры. Единица — как в файле, ноль — серая, выше единицы — " +
                              "цвета сочнее. Правится сама картинка, до света: Base Color так не умеет, " +
                              "он только умножает.",
            ["_Brightness"] = "Яркость текстуры, до света. Чем поднимают выцветшие покупные наборы и " +
                              "приглушают слишком светлые, не перекрашивая их Base Color.",

            ["_ShadowTint"] = "ДОБАВКА к общему оттенку тени из рендер-фичи: они умножаются. Белый — " +
                              "значит своего оттенка нет, тень берёт общий тон сцены. Альфа — сила " +
                              "подмешивания: цвет умножается на окружающий свет, поэтому яркость " +
                              "тени идёт от освещения, а отсюда только оттенок.",
            ["_CastShadowOpacity"] = "Насколько на этой поверхности видна ПАДАЮЩАЯ тень — та, что " +
                                     "ложится от других объектов. Единица — как задано в рендер-фиче, " +
                                     "ноль — тени на объекте нет вовсе, остаётся только затенение " +
                                     "по углу к источнику. Настройка материала, а не объекта: одному " +
                                     "объекту нужна своя копия материала.",
            ["_RampThreshold"] = "Где проходит граница света и тени по углу к источнику.",
            ["_RampSmoothness"] = "Мягкость этой границы. Малая — жёсткая ступень, ради которой стиль и берут.",
            ["_MidToneWidth"] = "Ширина средней ступени. Без неё силуэт разваливается на два плоских пятна.",
            ["_MidToneStrength"] = "Насколько средняя ступень заметна.",

            ["_RimColor"] = "Цвет ободка по краю силуэта.",
            ["_RimAmount"] = "Сила ободка. Отделяет персонажа от фона, когда на экране толпа.",
            ["_RimThreshold"] = "С какого угла ободок начинается.",
            ["_RimSmoothness"] = "Мягкость его границы.",

            ["_ToonSpecColor"] = "Цвет блика.",
            ["_SpecGloss"] = "Размер блика. Больше — мельче и резче.",
            ["_SpecIntensity"] = "Сила блика. Ноль — блик не рисуется и не считается вовсе.",

            ["_OutlineEnabled"] = "Контур вторым проходом, вывернутой оболочкой. Погасить его сразу " +
                                  "во всём проекте можно в рендер-фиче Toon Lit Settings.",
            ["_OutlineColor"] = "Цвет контура.",
            ["_OutlineWidth"] = "Толщина в пикселях при опорном разрешении — его задаёт рендер-фича. " +
                                "Контур не толстеет, когда камера подъезжает, и занимает одну и ту же " +
                                "долю экрана на любом разрешении. Дальше опорной дистанции из фичи " +
                                "контур начинает худеть вместе с объектом, иначе на отлёте он " +
                                "съедает усохший силуэт.",
            ["_OutlineLit"] = "Контур живёт по свету вместе с заливкой: в тени темнеет, на солнце берёт " +
                              "его оттенок. Без этого он читается как наклейка поверх картинки.",
            ["_OutlineLightInfluence"] = "Насколько сильно свет влияет на цвет контура.",
            ["_OutlineFadeStart"] = "С какой дистанции контур начинает гаснуть, м.",
            ["_OutlineFadeEnd"] = "Где гаснет полностью. Иначе мелочь на горизонте превращается в кашу.",
            ["_OutlineScale"] = "Раздувание от опоры объекта, минуя нормали. Помогает статичным пропам; " +
                                "скиннед-персонажам БЕСПОЛЕЗНО — там опора в ногах, и оболочка не растёт, а съезжает.",
            ["_OutlineDepthOffset"] = "Сдвиг контура по глубине. Спасает от мерцания там, где он совпал с моделью.",

            ["_HitFlashColor"] = "Цвет вспышки при попадании. Саму силу гонит свойство _HitFlash " +
                                 "из компонента FeatherKit.Feedback.HitFlash — оно рантаймовое, " +
                                 "в материале его не трогаем.",

            ["_Cull"] = "Какие грани отбрасывать. Общее для заливки, тени и глубины."
        };

        private MaterialProperty[] properties;
        private MaterialEditor editor;


        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] materialProperties)
        {
            editor = materialEditor;
            properties = materialProperties;

            EditorGUI.BeginChangeCheck();

            DrawSurface();
            DrawBase();
            DrawRamp();
            DrawShadow();
            DrawRim();
            DrawSpecular();
            DrawOutline();
            DrawHitFlash();
            DrawRendering();

            GUILayout.Space(InspectorStyle.Current.SectionGap);

            if (!EditorGUI.EndChangeCheck())
                return;

            foreach (var target in editor.targets)
            {
                if (target is Material material)
                    Apply(material);
            }
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);

            FMaterialUtils.Sanitize(material, newShader);
            Apply(material);
        }


        // ── Секции ────────────────────────────────────────────────────────────

        private void DrawSurface()
        {
            InspectorSection.Draw("toon.surface", "Поверхность", 0, () =>
            {
                Property("_Surface");
                Property("_AlphaClip");

                // Порог показываем, только когда вырезание включено: иначе поле стоит
                // мёртвым и сбивает с толку.
                if (IsOn("_AlphaClip"))
                    Property("_Cutoff");

                Property("_Dither");

                // Показываем только при включённом решете: без него поле ничего не делает.
                if (IsOn("_Dither"))
                    Property("_DitherFade");
            });
        }

        private void DrawBase()
        {
            InspectorSection.Draw("toon.base", "Основа", 1, () =>
            {
                var map = Find("_BaseMap");
                var color = Find("_BaseColor");

                if (map != null && color != null)
                    editor.TexturePropertySingleLine(Label("_BaseMap", "Base Map"), map, color);

                if (map != null)
                    editor.TextureScaleOffsetProperty(map);

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Правка текстуры", EditorStyles.miniBoldLabel);
                Property("_Saturation");
                Property("_Brightness");
            });
        }

        private void DrawRamp()
        {
            InspectorSection.Draw("toon.ramp", "Ступени света", 2, () =>
            {
                Property("_ShadowTint");
                Property("_RampThreshold");
                Property("_RampSmoothness");

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Средняя ступень", EditorStyles.miniBoldLabel);
                Property("_MidToneWidth");
                Property("_MidToneStrength");
            });
        }

        private void DrawShadow()
        {
            InspectorSection.Draw("toon.shadow", "Падающие тени", 3, () =>
            {
                Property("_CastShadowOpacity");

                EditorGUILayout.HelpBox(
                    "Порог, мягкость, глубина, жёсткость края и общий оттенок тени настраиваются " +
                    "один раз на проект — в рендер-фиче Toon Lit Settings у ассета рендерера " +
                    "Тень принадлежит сцене, а не отдельной модели. " +
                    "В материале остаётся своё: насколько тень видна ЗДЕСЬ — ползунком выше, — " +
                    "и добавочный оттенок в разделе «Ступени света».",
                    MessageType.None);
            });
        }

        private void DrawRim()
        {
            InspectorSection.Draw("toon.rim", "Контровой свет", 4, () =>
            {
                Property("_RimColor");
                Property("_RimAmount");
                Property("_RimThreshold");
                Property("_RimSmoothness");
            }, false);
        }

        private void DrawSpecular()
        {
            InspectorSection.Draw("toon.specular", "Блик", 5, () =>
            {
                Property("_ToonSpecColor");
                Property("_SpecGloss");
                Property("_SpecIntensity");
            }, false);
        }

        private void DrawOutline()
        {
            InspectorSection.Draw("toon.outline", "Обводка", 0, () =>
            {
                Property("_OutlineEnabled");

                if (!IsOn("_OutlineEnabled"))
                    return;

                Property("_OutlineColor");
                Property("_OutlineWidth");
                Property("_OutlineLit");

                if (IsOn("_OutlineLit"))
                    Property("_OutlineLightInfluence");

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Затухание по дальности", EditorStyles.miniBoldLabel);
                Property("_OutlineFadeStart");
                Property("_OutlineFadeEnd");

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Тонкая настройка", EditorStyles.miniBoldLabel);
                Property("_OutlineScale");
                Property("_OutlineDepthOffset");
            });
        }

        private void DrawHitFlash()
        {
            InspectorSection.Draw("toon.hitflash", "Вспышка урона", 4, () =>
            {
                Property("_HitFlashColor");

                EditorGUILayout.HelpBox(
                    "Силу вспышки гонит свойство _HitFlash — его в рантайме анимирует компонент " +
                    "FeatherKit.Feedback.HitFlash, в свою копию материала. В инспекторе его нет " +
                    "намеренно: выставленное руками значение всё равно будет затёрто.",
                    MessageType.None);
            }, false);
        }

        private void DrawRendering()
        {
            InspectorSection.Draw("toon.rendering", "Отрисовка", 5, () => Property("_Cull"), false);
        }


        // ── Мелочь ────────────────────────────────────────────────────────────

        private MaterialProperty Find(string name)
        {
            for (var i = 0; i < properties.Length; i++)
            {
                if (properties[i].name == name)
                    return properties[i];
            }

            return null;
        }

        private static GUIContent Label(string name, string display)
        {
            return new GUIContent(display, Tooltips.TryGetValue(name, out var tip) ? tip : string.Empty);
        }

        private void Property(string name)
        {
            var property = Find(name);

            if (property != null)
                editor.ShaderProperty(property, Label(name, property.displayName));
        }

        private bool IsOn(string name)
        {
            var property = Find(name);

            return property != null && property.floatValue > 0.5f;
        }


        // Доводит режим поверхности до рабочего состояния. Подробности — в памятке наверху.
        private static void Apply(Material material)
        {
            var transparent = material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f;
            var cutout = material.IsKeywordEnabled("_ALPHATEST_ON");

            if (transparent)
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);

                // Глубину не пишем: прозрачные перекрывали бы друг друга рывками,
                // и порядок зависел бы от того, кто первым попал в очередь.
                material.SetFloat("_ZWrite", 0f);
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_ZWrite", 1f);

                // С вырезанием — очередь AlphaTest: дырявое рисуется после плотного
                // и не мешает раннему отсечению по глубине.
                material.renderQueue = cutout ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            }
        }
    }
}
