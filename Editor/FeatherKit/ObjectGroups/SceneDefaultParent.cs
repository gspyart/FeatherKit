using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Штатный Default Parent Unity — тот же, что ПКМ в Hierarchy → Set as Default Parent: новые объекты сцены
    /// создаются внутри него. Свой у каждой сцены. Ставить и снимать — открытым API; узнать, какой стоит, Unity даёт
    /// только внутренним методом, поэтому читаем его отражением — нет метода в новой версии, просто не узнаем.
    /// </summary>
    public static class SceneDefaultParent
    {
        private static readonly MethodInfo CurrentGetter =
            typeof(SceneView).GetMethod("GetDefaultParentObjectIfSet", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);


        // Default Parent этой сцены; null — не стоит или Unity не сказал. Unity отдаёт его только для активной сцены.
        public static Transform CurrentIn(Scene scene)
        {
            if (CurrentGetter == null || scene != SceneManager.GetActiveScene())
                return null;

            return CurrentGetter.Invoke(null, null) as Transform;
        }


        public static void Set(Transform parent) => EditorUtility.SetDefaultParentObject(parent.gameObject);


        public static void Clear(Scene scene) => EditorUtility.ClearDefaultParentObject(scene);
    }
}
