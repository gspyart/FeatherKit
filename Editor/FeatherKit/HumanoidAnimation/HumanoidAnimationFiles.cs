using System.IO;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Файл анимации: снаружи — готовый Humanoid-клип для игры, внутри спрятан исходник с ручками
    /// рига. Один файл на анимацию: в Project нечего путать, при переносе и переименовании
    /// исходник едет вместе с клипом, ссылки игры на клип не рвутся.
    ///
    /// Здесь — создать такой файл, найти исходник по любому клипу анимации и перевести старую
    /// пару «Клип» + «Клип_Humanoid» в один файл.
    /// </summary>
    internal static class HumanoidAnimationFiles
    {
        public const string FileExtension = ".anim";
        public const string SourceClipSuffix = " (ручки)";

        private const float NewClipFrameRate = 30f;


        // ── Создать ──────────────────────────────

        /// <summary>
        /// Создать файл анимации. Вернёт спрятанный в нём исходник — его и анимируют.
        /// Игровой клип пустой, пока исходник не запекут.
        /// </summary>
        public static AnimationClip Create(string path, Avatar avatar)
        {
            var gameClip = new AnimationClip
            {
                name = Path.GetFileNameWithoutExtension(path),
                frameRate = NewClipFrameRate,
            };

            SetLooped(gameClip, true);
            AssetDatabase.CreateAsset(gameClip, path);

            var source = new AnimationClip
            {
                name = gameClip.name + SourceClipSuffix,
                frameRate = NewClipFrameRate,
                hideFlags = HideFlags.HideInHierarchy,
            };

            SetLooped(source, true);
            AssetDatabase.AddObjectToAsset(source, gameClip);

            // Сначала на диск, потом метка: метка перечитывает файл.
            AssetDatabase.SaveAssetIfDirty(gameClip);
            HumanoidClipBaker.MarkNewFile(path, source, avatar);

            return source;
        }


        // ── Найти исходник и игровой клип ──────────────────────────────

        /// <summary>Спрятан ли исходник внутри файла анимации.</summary>
        public static bool IsEmbeddedSource(AnimationClip clip)
        {
            if (!HumanoidSourceClips.IsNestedClip(clip))
                return false;

            var path = AssetDatabase.GetAssetPath(clip);

            return path.EndsWith(FileExtension)
                && AssetDatabase.LoadMainAssetAtPath(path) is AnimationClip
                && HumanoidClipBaker.IsOwnFile(path);
        }


        /// <summary>
        /// Исходник с ручками для любого клипа анимации: сам исходник, игровой клип со спрятанным
        /// исходником или старый «_Humanoid»-клип. Не наш клип — вернёт его самого, разбираться
        /// будет тот, кто открывает.
        /// </summary>
        public static AnimationClip FindSourceClip(AnimationClip clip)
        {
            if (clip == null || !AssetDatabase.IsMainAsset(clip))
                return clip;

            var path = AssetDatabase.GetAssetPath(clip);

            if (!HumanoidClipBaker.IsOwnFile(path))
                return clip;

            var embedded = FindEmbeddedSource(path);

            if (embedded != null)
                return embedded;

            var markedSource = HumanoidClipBaker.LoadMarkedSource(path);
            return markedSource != null ? markedSource : clip;
        }

        private static AnimationClip FindEmbeddedSource(string path)
        {
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && asset != mainAsset)
                    return clip;
            }

            return null;
        }


        /// <summary>Игровой клип исходника: сам файл анимации или старый «_Humanoid» рядом. Ещё не пекли — null.</summary>
        public static AnimationClip FindGameClip(AnimationClip source) => HumanoidSourceClips.LoadHumanoidClip(source);


        /// <summary>Как называть анимацию человеку: по игровому клипу, а не по спрятанному исходнику.</summary>
        public static string DisplayName(AnimationClip source)
        {
            if (!IsEmbeddedSource(source))
                return source.name;

            return Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(source));
        }


        // ── Зацикливание ──────────────────────────────

        public static bool IsLooped(AnimationClip clip) => AnimationUtility.GetAnimationClipSettings(clip).loopTime;


        /// <summary>Зациклить клип или снять зацикливание. С Undo.</summary>
        public static void SetLooped(AnimationClip clip, bool isLooped, bool withUndo)
        {
            if (withUndo)
                Undo.RecordObject(clip, isLooped ? "Зациклить анимацию" : "Не зацикливать анимацию");

            SetLooped(clip, isLooped);
            EditorUtility.SetDirty(clip);
        }

        private static void SetLooped(AnimationClip clip, bool isLooped)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = isLooped;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }


        // ── Старая пара в один файл ──────────────────────────────

        /// <summary>Исходник лежит отдельным файлом, а рядом — запечённый из него «_Humanoid».</summary>
        public static bool IsLegacyPair(AnimationClip source)
        {
            if (source == null || HumanoidSourceClips.IsNestedClip(source))
                return false;

            var gamePath = HumanoidSourceClips.GetHumanoidClipPath(source);
            return AssetDatabase.LoadMainAssetAtPath(gamePath) is AnimationClip && HumanoidClipBaker.IsBakedFrom(gamePath, source);
        }


        /// <summary>
        /// Собрать старую пару в один файл: исходник переезжает внутрь «_Humanoid»-клипа, файл
        /// получает имя исходника, старый файл исходника уходит в корзину. Файл игрового клипа
        /// тот же — ссылки игры на него не рвутся. Вернёт новый, спрятанный исходник.
        /// </summary>
        public static AnimationClip MergeLegacyPair(HumanoidAnimationRig rig, AnimationClip source)
        {
            var sourcePath = AssetDatabase.GetAssetPath(source);
            var gamePath = HumanoidSourceClips.GetHumanoidClipPath(source);
            var gameClip = (AnimationClip)AssetDatabase.LoadMainAssetAtPath(gamePath);
            var animationName = Path.GetFileNameWithoutExtension(sourcePath);

            var embedded = Object.Instantiate(source);
            embedded.name = animationName + SourceClipSuffix;
            embedded.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(embedded, gameClip);

            HumanoidRigClipLibrary.ReplaceClip(rig, source, embedded);

            // Сначала на диск, потом метка: метка перечитывает файл.
            AssetDatabase.SaveAssets();
            HumanoidClipBaker.MoveMarkToSource(gamePath, rig, embedded);

            AssetDatabase.MoveAssetToTrash(sourcePath);

            var renamedPath = Path.GetDirectoryName(gamePath)?.Replace('\\', '/') + "/" + animationName + FileExtension;
            var renameError = AssetDatabase.MoveAsset(gamePath, renamedPath);

            if (!string.IsNullOrEmpty(renameError))
                Debug.LogWarning($"«{gameClip.name}» собран в один файл, но не переименован: {renameError}", gameClip);

            return embedded;
        }
    }
}
