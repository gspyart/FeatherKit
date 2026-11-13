using System;
using System.Collections.Generic;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Все иконки редактора графа в одном месте: свои (When, Flow, Wait For, Guard, Repeat), по типу Unity и встроенные по имени.
    /// - не нашлась — запасная, без ошибки в консоль
    /// - свои иконки белые; в светлой теме редактора отдаются перекрашенными в тёмный
    /// </summary>
    internal static class ScenarioIcons
    {
        public const string OwnIconsFolder = "Packages/com.gspy.featherkit/Editor/FeatherKit/Scenarios/Graph/Icons/";

        private const string LockOffName = "LockIcon";
        private const string LockOnName = "LockIcon-On";
        private const string SaveName = "SaveAs";
        private const string PrefabName = "Prefab Icon";
        private const string WarningName = "console.warnicon.sml";
        private const string ErrorName = "console.erroricon.sml";
        private const string InfoName = "console.infoicon.sml";
        private const string HelpName = "_Help";
        private const string FrameName = "ViewToolZoom";
        private const string SelectName = "UnityEditor.SceneHierarchyWindow";
        private const string CallerName = "Linked";
        private const string PlayerName = "AvatarSelector";

        private const string WhenName = "ScenarioWhen";
        private const string FlowName = "ScenarioFlow";
        private const string WaitForName = "ScenarioWaitFor";
        private const string GuardName = "ScenarioGuard";
        private const string RepeatName = "ScenarioRepeat";
        private const string RepeatTitle = "Repeat";

        /// <summary>Встроенные иконки по имени — все должны находиться, это проверяет тест.</summary>
        public static readonly string[] BuiltInNames =
        {
            LockOffName, LockOnName, SaveName, PrefabName, WarningName, ErrorName, HelpName, FrameName, SelectName,
            CallerName, PlayerName, InfoName
        };

        public static readonly string[] OwnNames = { WhenName, FlowName, WaitForName, GuardName, RepeatName };

        private static readonly Color LightSkinTint = new Color(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Dictionary<string, Texture2D> OwnIconsByName = new Dictionary<string, Texture2D>();


        // ── Своя графика ─────────────────────────────────────────────────

        public static Texture2D When => Own(WhenName);

        public static Texture2D Flow => Own(FlowName);

        public static Texture2D WaitFor => Own(WaitForName);

        public static Texture2D Guard => Own(GuardName);

        public static Texture2D Repeat => Own(RepeatName);


        // ── Встроенные по имени ──────────────────────────────────────────

        public static Texture Save => BuiltIn(SaveName);

        public static Texture Prefab => BuiltIn(PrefabName);

        public static Texture Warning => BuiltIn(WarningName);

        public static Texture Error => BuiltIn(ErrorName);

        public static Texture Info => BuiltIn(InfoName);

        public static Texture Help => BuiltIn(HelpName);

        public static Texture Frame => BuiltIn(FrameName);

        public static Texture Select => BuiltIn(SelectName);

        public static Texture Caller => BuiltIn(CallerName);

        public static Texture Player => BuiltIn(PlayerName);

        public static Texture Scene => TypeIcon(typeof(SceneAsset));

        public static Texture GraphFile => TypeIcon(typeof(ScenarioGraphAsset));

        /// <summary>Do: стопка шагов сверху вниз — как вертикальная раскладка.</summary>
        public static Texture Steps => TypeIcon(typeof(UnityEngine.UI.VerticalLayoutGroup));

        public static Texture Neutral => TypeIcon(typeof(Object));


        public static Texture Lock(bool isLocked)
        {
            return BuiltIn(isLocked ? LockOnName : LockOffName);
        }


        // ── По смыслу ────────────────────────────────────────────────────

        /// <summary>
        /// Иконка ноды или блока: своя у When, Do, Wait For, Guard, Repeat и нод потока, у блока — его категории.
        /// Неизвестное — нейтральная иконка, чтобы не путать с потоком.
        /// </summary>
        public static Texture IconOf(Type nodeOrBlockType)
        {
            if (nodeOrBlockType == null)
                return Neutral;

            if (nodeOrBlockType == typeof(ScenarioWhenNode))
                return When;

            if (nodeOrBlockType == typeof(ScenarioStepsNode))
                return Steps;

            if (nodeOrBlockType == typeof(ScenarioWaitForNode))
                return WaitFor;

            if (nodeOrBlockType == typeof(ScenarioGuardNode))
                return Guard;

            // По названию, а не по классу: нода Repeat придёт с рантаймом, блок Repeat из старых списков уйдёт на стыковке.
            if (ScenarioBlockAttribute.TitleOf(nodeOrBlockType) == RepeatTitle)
                return Repeat;

            if (typeof(ScenarioFlowNode).IsAssignableFrom(nodeOrBlockType) || ScenarioBlockStyle.IsFlowCategory(nodeOrBlockType))
                return Flow;

            var iconType = ScenarioBlockStyle.IconTypeOf(nodeOrBlockType);

            if (iconType != null)
                return TypeIcon(iconType);

            return typeof(ScenarioEvent).IsAssignableFrom(nodeOrBlockType) ? When : Neutral;
        }

        public static Texture IconOf(ScenarioVariableDefinition variable)
        {
            return variable.Kind switch
            {
                ScenarioVariableKind.Flag => TypeIcon(typeof(UnityEngine.UI.Toggle)),
                ScenarioVariableKind.Counter => TypeIcon(typeof(UnityEngine.UI.InputField)),
                ScenarioVariableKind.Number => TypeIcon(typeof(UnityEngine.UI.Slider)),
                _ => TypeIcon(variable.ObjectType)
            };
        }

        public static Texture TypeIcon(Type type)
        {
            return type != null ? EditorGUIUtility.ObjectContent(null, type).image : null;
        }


        // ── Загрузка ─────────────────────────────────────────────────────

        // FindTexture, а не IconContent: тот пишет в консоль «Unable to load», если иконки нет.
        private static Texture BuiltIn(string name)
        {
            var darkName = "d_" + name;

            return EditorGUIUtility.isProSkin && EditorGUIUtility.FindTexture(darkName) != null
                ? EditorGUIUtility.FindTexture(darkName)
                : EditorGUIUtility.FindTexture(name) ?? EditorGUIUtility.FindTexture(InfoName);
        }

        private static Texture2D Own(string name)
        {
            var key = name + (EditorGUIUtility.isProSkin ? "" : ".light");

            if (OwnIconsByName.TryGetValue(key, out var icon) && icon != null)
                return icon;

            var source = AssetDatabase.LoadAssetAtPath<Texture2D>($"{OwnIconsFolder}{name}@2x.png")
                         ?? AssetDatabase.LoadAssetAtPath<Texture2D>($"{OwnIconsFolder}{name}.png");

            icon = EditorGUIUtility.isProSkin || source == null ? source : Tinted(source, LightSkinTint);
            OwnIconsByName[key] = icon;

            return icon;
        }

        // UI Toolkit умеет красить картинку стилем, а IMGUI (поиск нод) — нет: в светлой теме нужна тёмная копия.
        private static Texture2D Tinted(Texture2D source, Color tint)
        {
            var pixels = source.GetPixels();

            for (var index = 0; index < pixels.Length; index++)
                pixels[index] = new Color(tint.r, tint.g, tint.b, pixels[index].a * tint.a);

            var tinted = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };

            tinted.SetPixels(pixels);
            tinted.Apply();

            return tinted;
        }
    }
}
