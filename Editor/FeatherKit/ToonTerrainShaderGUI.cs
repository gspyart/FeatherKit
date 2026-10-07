using System.Collections.Generic;
using FeatherKit.Helpers;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    // ═══════════════════════════════════════════════════════════════════════════════════
    // ПАМЯТКА ТОМУ, КТО БУДЕТ ЭТО ПРАВИТЬ — ЧЕЛОВЕКУ ИЛИ АГЕНТУ
    //
    // 1. Главное здесь — не список полей, а СВЯЗЬ КАНАЛА С ТЕКСТУРОЙ. Художник красит
    //    кистью красным, зелёным и синим, и должен видеть глазами, какая текстура за каким
    //    цветом закреплена. Отсюда цветная метка у каждого слоя: она не украшение, она и
    //    есть инструкция. Меняете раскладку слоёв в шейдере — меняйте метки здесь же.
    //
    // 2. Добавили свойство в шейдер — добавьте его СЮДА и подпись в таблицу Tooltips.
    //    Свойство, не упомянутое здесь, в инспекторе не появится: базовый список
    //    не рисуется, только свои секции.
    //
    // 3. Высота слоя лежит в АЛЬФЕ его текстуры. Текстура без альфы — не поломка: край
    //    по-прежнему настраивается шириной и шумом, но островков по рисунку самой
    //    текстуры не будет, и это надо сказать вслух — иначе человек будет искать их
    //    ползунками, которых нет.
    //    Проверка идёт через импортёр и кэшируется: спрашивать его на каждой перерисовке
    //    дорого. Кэш живёт до перезагрузки домена — переимпортировали текстуру и хотите
    //    увидеть это сразу, перезапустите домен любой правкой кода.
    //    Второй выход из той же беды — шум: он рвёт кромку и без карт высот, поэтому
    //    подсказка про высоту на него и ссылается.
    //
    // 4. Оформление секций — в InspectorStyle, отдельным ассетом. Цифры и цвета правьте
    //    там, а не здесь. Исключение — цвета каналов: они не оформление, они значение.
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Инспектор материала для FeatherKit/Toon Terrain: слои разложены отдельными
    /// блоками, у каждого видна цветная метка канала, которым его красят.
    /// </summary>
    public class ToonTerrainShaderGUI : ShaderGUI
    {
        // Ключевые слова «сколько слоёв читать». Их два, а не три: четыре слоя — это
        // отсутствие обоих, поэтому материалы, которые про них не знают, остаются
        // четырёхслойными.
        private const string LayersTwoKeyword = "_TERRAIN_LAYERS_TWO";
        private const string LayersThreeKeyword = "_TERRAIN_LAYERS_THREE";

        private static readonly GUIContent[] LayerCountOptions =
        {
            new GUIContent("2 — база и красный"),
            new GUIContent("3 — плюс зелёный"),
            new GUIContent("4 — плюс синий"),
        };

        // Цвета каналов. Это не оформление, а значение: ровно этими цветами художник
        // и водит кистью по мешу.
        private static readonly Color BaseChannel = new Color(0.10f, 0.10f, 0.12f);
        private static readonly Color RedChannel = new Color(0.87f, 0.30f, 0.28f);
        private static readonly Color GreenChannel = new Color(0.40f, 0.78f, 0.38f);
        private static readonly Color BlueChannel = new Color(0.38f, 0.58f, 0.95f);

        // Подписи. Отдельной таблицей, а не по месту: так их видно списком и легко
        // вычитывать целиком, не выискивая по всему файлу.
        private static readonly Dictionary<string, string> Tooltips = new Dictionary<string, string>
        {
            ["_TerrainLayerCount"] = "Сколько слоёв шейдер вообще читает. Выключенные слои " +
                                     "не стоят ничего: их текстуры не выбираются, а их доля " +
                                     "вершинного цвета уходит базовому слою. Каждый слой — " +
                                     "это выборка на каждом пикселе земли, поэтому на " +
                                     "мобильных устройствах лишние лучше снять.",

            ["_Layer0Map"] = "Текстура базового слоя. Лежит там, где вершины не закрашены. " +
                             "В альфе — высота: по ней слои спорят за границу.",
            ["_Layer1Map"] = "Текстура, которую подмешивает КРАСНЫЙ канал вершинного цвета.",
            ["_Layer2Map"] = "Текстура, которую подмешивает ЗЕЛЁНЫЙ канал вершинного цвета.",
            ["_Layer3Map"] = "Текстура, которую подмешивает СИНИЙ канал вершинного цвета.",

            ["_LayerSize"] = "Через сколько метров рисунок повторяется. UV берутся от мировых " +
                             "координат, поэтому соседние куски земли продолжают друг друга " +
                             "и разворачивать их не надо.",

            ["_LayerSaturation"] = "Насыщенность ЭТОГО слоя. Единица — как в файле, ноль — серый, " +
                                   "выше единицы — цвета сочнее. Правится сама текстура, до света: " +
                                   "цвет слоя так не умеет, он только умножает.",
            ["_LayerBrightness"] = "Яркость этого слоя, до света. Пара к насыщенности: ею слои из " +
                                   "разных наборов сводят друг к другу, чтобы трава и гравий " +
                                   "не выглядели снятыми в разную погоду.",


            ["_BlendWidth"] = "Доля от силы победителя, в пределах которой слой ещё виден. " +
                              "Маленькая — рубленый край, гравий пробивается сквозь траву " +
                              "островками. Единица — обычное смешивание текстур по весам: " +
                              "ровно то, что нарисовано кистью.\n\nДоля, а не абсолютное " +
                              "число: ползунок не зависит ни от того, есть ли у текстур карты " +
                              "высот, ни от того, насколько силён шум.",

            ["_NoiseBreakEnabled"] = "Рваная кромка у закрашенных слоёв. Выключенная не стоит " +
                                     "ничего: выборок шума не делается вовсе. Включённая берёт " +
                                     "по выборке на слой — три, даже если слот пустой.",
            ["_LayerNoiseMap"] = "Текстура шума ЭТОГО слоя. Читается только красный канал. Серый " +
                                 "цвет — «ничего не менять», поэтому пустой слот оставляет слой " +
                                 "без разрыва. У слоёв они разные намеренно: с одной текстурой " +
                                 "на всех кромка гравия и кромка песка идут по одному узору.",
            ["_LayerNoiseSize"] = "Через сколько метров узор шума повторяется. Крупный — плавные " +
                                  "заливы по краю слоя, мелкий — рваная крошка.",
            ["_LayerNoiseBreak"] = "Насколько шум РВЁТ кромку этого слоя. Множитель к его силе: " +
                                   "единица — это ±половина, и работает даже там, где у текстур " +
                                   "нет карт высот.\n\nЗа двойкой нижняя половина шума упирается " +
                                   "в ноль, и кромка идёт дырами — это уже не рваный край, " +
                                   "а растворение. Вид законный, но другой.",

            ["_ShadowTint"] = "ДОБАВКА к общему оттенку тени из рендер-фичи: они умножаются. Белый — " +
                              "значит своего оттенка нет, тень берёт общий тон сцены. Альфа — сила " +
                              "подмешивания.",
            ["_CastShadowOpacity"] = "Насколько на этой земле видна ПАДАЮЩАЯ тень — та, что ложится " +
                                     "от деревьев, стен и построек. Единица — как задано в рендер-фиче, " +
                                     "ноль — тени на земле нет вовсе, остаётся только затенение по углу " +
                                     "к солнцу. Ровный пол уходит в тень целиком, и рисунок слоёв " +
                                     "на нём перестаёт читаться — здесь это и приглушается.",
            ["_RampThreshold"] = "Где проходит граница света и тени по углу к источнику.",
            ["_RampSmoothness"] = "Мягкость этой границы. Малая — жёсткая ступень, ради которой стиль и берут.",
            ["_MidToneWidth"] = "Ширина средней ступени.",
            ["_MidToneStrength"] = "Насколько средняя ступень заметна. У плоской земли толку от неё мало: " +
                                   "угол к солнцу везде один и тот же.",

            ["_RimColor"] = "Цвет ободка по краю силуэта.",
            ["_RimAmount"] = "Сила ободка. У плоской земли держите ноль: угол взгляда одинаков " +
                             "по всей поверхности, и ободок подсветит не край, а всё разом.",
            ["_RimThreshold"] = "С какого угла ободок начинается.",
            ["_RimSmoothness"] = "Мягкость его границы.",

            ["_ToonSpecColor"] = "Цвет блика.",
            ["_SpecGloss"] = "Размер блика. Больше — мельче и резче.",
            ["_SpecIntensity"] = "Сила блика. Ноль — блик не рисуется и не считается вовсе."
        };

        // Есть ли у текстуры альфа, то есть высота. Импортёр на каждой перерисовке —
        // дорого, поэтому ответ запоминается до перезагрузки домена.
        private static readonly Dictionary<ulong, bool> HeightPresence = new Dictionary<ulong, bool>();

        private MaterialProperty[] properties;
        private MaterialEditor editor;


        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] materialProperties)
        {
            editor = materialEditor;
            properties = materialProperties;

            DrawLayers();
            DrawBlend();
            DrawNoise();
            DrawPainting();
            DrawRamp();
            DrawShadow();
            DrawRim();
            DrawSpecular();

            GUILayout.Space(InspectorStyle.Current.SectionGap);
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);

            FMaterialUtils.Sanitize(material, newShader);
        }


        // ── Секции ────────────────────────────────────────────────────────────

        private void DrawLayers()
        {
            InspectorSection.Draw("terrain.layers", "Слои", 0, () =>
            {
                DrawLayerCount();

                var count = GetLayerCount(editor.target as Material);

                DrawLayer(0, "База", "то, что не закрашено", BaseChannel);
                DrawLayer(1, "Слой 1", "красная кисть", RedChannel);

                using (new EditorGUI.DisabledScope(count < 3))
                    DrawLayer(2, "Слой 2", count < 3 ? "выключен" : "зелёная кисть", GreenChannel);

                using (new EditorGUI.DisabledScope(count < 4))
                    DrawLayer(3, "Слой 3", count < 4 ? "выключен" : "синяя кисть", BlueChannel);
            });
        }

        // Сколько слоёв читает шейдер. Руками, а не по наличию текстур: пустой слот — это
        // законный однотонный слой, который красится своим цветом, и решать за художника,
        // что он не нужен, нельзя. Каждый лишний слой — это выборка текстуры на КАЖДОМ
        // пикселе земли, а земля занимает пол-экрана.
        private void DrawLayerCount()
        {
            var material = editor.target as Material;

            if (material == null)
                return;

            EditorGUI.BeginChangeCheck();

            var picked = EditorGUILayout.Popup(Label("_TerrainLayerCount", "Сколько слоёв"),
                                               GetLayerCount(material) - 2, LayerCountOptions) + 2;

            if (!EditorGUI.EndChangeCheck())
                return;

            foreach (var target in editor.targets)
            {
                if (target is Material edited)
                    SetLayerCount(edited, picked);
            }
        }

        private static int GetLayerCount(Material material)
        {
            if (material == null)
                return 4;

            if (material.IsKeywordEnabled(LayersTwoKeyword))
                return 2;

            if (material.IsKeywordEnabled(LayersThreeKeyword))
                return 3;

            // Ни одного слова — это четыре слоя. Отсюда и обратная совместимость: материал,
            // сделанный до появления переключателя, работает как раньше.
            return 4;
        }

        private static void SetLayerCount(Material material, int count)
        {
            Undo.RecordObject(material, "Сколько слоёв земли");

            SetKeyword(material, LayersTwoKeyword, count <= 2);
            SetKeyword(material, LayersThreeKeyword, count == 3);

            EditorUtility.SetDirty(material);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        // Один слой: метка канала, текстура с цветом и размер тайла. Порядок именно такой —
        // сперва «каким цветом красить», потом «что при этом появится».
        private void DrawLayer(int index, string title, string channelHint, Color channelColor)
        {
            if (index > 0)
                EditorGUILayout.Space(8f);

            DrawChannelHeader(title, channelHint, channelColor);

            var map = Find("_Layer" + index + "Map");
            var color = Find("_Layer" + index + "Color");
            var size = Find("_Layer" + index + "Size");
            var saturation = Find("_Layer" + index + "Saturation");
            var brightness = Find("_Layer" + index + "Brightness");

            using (new EditorGUI.IndentLevelScope())
            {
                if (map != null && color != null)
                    editor.TexturePropertySingleLine(Label("_Layer" + index + "Map", "Текстура"), map, color);

                if (size != null)
                    editor.ShaderProperty(size, Label("_LayerSize", "Тайл, м"));

                if (saturation != null)
                    editor.ShaderProperty(saturation, Label("_LayerSaturation", "Насыщенность"));

                if (brightness != null)
                    editor.ShaderProperty(brightness, Label("_LayerBrightness", "Яркость"));
            }
        }

        private static void DrawChannelHeader(string title, string channelHint, Color channelColor)
        {
            var row = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                var chip = new Rect(row.x, row.y + 3f, 12f, 12f);

                // Обводка у метки обязательна: чёрная база на тёмной теме без неё
                // просто не видна.
                FeatherEditorGUIBox.Draw(chip, channelColor, InspectorStyle.Current.Title * 0.6f, 1f, 3f);
            }

            var titleRect = new Rect(row.x + 18f, row.y, 90f, row.height);
            var hintRect = new Rect(titleRect.xMax, row.y, row.width - titleRect.width - 22f, row.height);

            GUI.Label(titleRect, title, EditorStyles.boldLabel);
            GUI.Label(hintRect, channelHint, EditorStyles.miniLabel);
        }

        private void DrawBlend()
        {
            InspectorSection.Draw("terrain.blend", "Переход между слоями", 1, () =>
            {
                Property("_BlendWidth");

                var flat = FindLayersWithoutHeight();

                if (flat.Count == 0)
                    return;

                EditorGUILayout.HelpBox(
                    "Высоты нет у слоёв: " + string.Join(", ", flat) + ". " +
                    "Высота лежит в альфе текстуры, и без неё границу решают одни веса: " +
                    "ширина выше по-прежнему задаёт, насколько край резкий, но островков " +
                    "по рисунку самой текстуры не будет. Нужны островки — положите карту " +
                    "высот в альфу текстуры слоя или включите шум в разделе ниже.",
                    MessageType.Info);
            });
        }

        private void DrawNoise()
        {
            InspectorSection.Draw("terrain.noise", "Шум в переходах", 2, DrawEdgeBreak, false);
        }

        // Разрыв кромки. Метки каналов здесь те же, что в «Слоях»: человек ищет слой
        // глазами по цвету кисти, а не по номеру в подписи.
        private void DrawEdgeBreak()
        {
            Property("_NoiseBreakEnabled", "_NoiseBreakEnabled", "Рваная кромка");

            if (!IsEnabled("_NoiseBreakEnabled"))
                return;

            var count = GetLayerCount(editor.target as Material);

            DrawLayerNoise(1, "Слой 1", "красная кисть", RedChannel);

            // Выключенный слой не читается вовсе, и шум ему не берётся тоже — гасим
            // поля, чтобы человек не крутил числа, которые никуда не идут.
            using (new EditorGUI.DisabledScope(count < 3))
                DrawLayerNoise(2, "Слой 2", count < 3 ? "слой выключен" : "зелёная кисть", GreenChannel);

            using (new EditorGUI.DisabledScope(count < 4))
                DrawLayerNoise(3, "Слой 3", count < 4 ? "слой выключен" : "синяя кисть", BlueChannel);

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "База шума не получает: кромку решает РАЗНИЦА сил, и сдвинутые разом слои " +
                "остались бы там же, где были.\n\n" +
                "Пустой слот — это слой без разрыва, но выборка у него всё равно берётся. " +
                "Не нужен разрыв вовсе — снимите галку выше.",
                MessageType.None);
        }

        private void DrawLayerNoise(int index, string title, string channelHint, Color channelColor)
        {
            EditorGUILayout.Space(8f);
            DrawChannelHeader(title, channelHint, channelColor);

            var map = Find("_Layer" + index + "NoiseMap");

            using (new EditorGUI.IndentLevelScope())
            {
                if (map != null)
                    editor.TexturePropertySingleLine(Label("_LayerNoiseMap", "Текстура шума"), map);

                Property("_Layer" + index + "NoiseSize", "_LayerNoiseSize", "Размер, м");
                Property("_Layer" + index + "NoiseBreak", "_LayerNoiseBreak", "Сила разрыва");
            }
        }

        private void DrawPainting()
        {
            InspectorSection.Draw("terrain.painting", "Как это красить", 2, () =>
                EditorGUILayout.HelpBox(
                    "Слои задаются ВЕРШИННЫМ ЦВЕТОМ меша, кистью Polybrush (Tools > Polybrush).\n\n" +
                    "Непокрашенный меш приходит белым, и белизна читается как чистая база — " +
                    "такая земля выглядит правильно сразу. Но как только вы начали красить, " +
                    "залейте меш ЧЁРНЫМ: полубелая вершина базой уже не считается и даёт кашу " +
                    "из трёх слоёв разом. Дальше красным, зелёным и синим подмешиваются " +
                    "слои 1, 2 и 3.\n\n" +
                    "Перетекание живёт между вершинами, поэтому сетка нужна с шагом метр-два: " +
                    "на плоскости из двух треугольников любой мазок растянется на всю карту.\n\n" +
                    "Альфа вершины не читается — она свободна под будущее.",
                    MessageType.None), false);
        }

        private void DrawRamp()
        {
            InspectorSection.Draw("terrain.ramp", "Ступени света", 3, () =>
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
            InspectorSection.Draw("terrain.shadow", "Падающие тени", 4, () =>
            {
                Property("_CastShadowOpacity");

                EditorGUILayout.HelpBox(
                    "Порог, мягкость, глубина, жёсткость края и общий оттенок тени настраиваются " +
                    "один раз на проект — в рендер-фиче Toon Lit Settings у ассета рендерера " +
                    "Тень принадлежит сцене, а не отдельной " +
                    "поверхности. В материале остаётся своё: насколько тень видна ЗДЕСЬ — " +
                    "ползунком выше, — и добавочный оттенок в разделе «Ступени света».",
                    MessageType.None);
            });
        }

        private void DrawRim()
        {
            InspectorSection.Draw("terrain.rim", "Контровой свет", 5, () =>
            {
                Property("_RimColor");
                Property("_RimAmount");
                Property("_RimThreshold");
                Property("_RimSmoothness");
            }, false);
        }

        private void DrawSpecular()
        {
            InspectorSection.Draw("terrain.specular", "Блик", 0, () =>
            {
                Property("_ToonSpecColor");
                Property("_SpecGloss");
                Property("_SpecIntensity");
            }, false);
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

        // Подпись своя, а подсказка общая: у слоёв текст один и тот же, и класть его
        // в таблицу трижды значит однажды поправить только одну копию.
        private void Property(string name, string tooltipKey, string display)
        {
            var property = Find(name);

            if (property != null)
                editor.ShaderProperty(property, Label(tooltipKey, display));
        }

        private bool IsEnabled(string name)
        {
            var property = Find(name);

            return property != null && property.floatValue > 0.5f;
        }


        private List<string> FindLayersWithoutHeight()
        {
            var names = new[] { "База", "Слой 1", "Слой 2", "Слой 3" };
            var flat = new List<string>();

            for (var i = 0; i < names.Length; i++)
            {
                var map = Find("_Layer" + i + "Map");

                // Пустой слой не считаем: предупреждать не о чем, пока текстуры нет.
                if (map != null && map.textureValue != null && !HasHeight(map.textureValue))
                    flat.Add(names[i]);
            }

            return flat;
        }

        private static bool HasHeight(Texture texture)
        {
            var id = texture.GetStableId();

            if (HeightPresence.TryGetValue(id, out var known))
                return known;

            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            var result = importer != null && importer.DoesSourceTextureHaveAlpha();

            HeightPresence[id] = result;

            return result;
        }
    }
}
