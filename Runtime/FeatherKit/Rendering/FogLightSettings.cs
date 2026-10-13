using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Rendering
{
    /// <summary>
    /// Как один источник света — лампа или солнце — светит в тумане: насколько ярко
    /// и даёт ли в нём тень. Отвечает ТОЛЬКО за эти два числа; без компонента свет
    /// светит в тумане как обычно, с яркостью из Volume и тенью, если она у света включена.
    ///
    /// Нужен, чтобы приглушить в тумане яркую лампу, не трогая сам свет, или снять
    /// с неё дорогую тень в тумане, оставив тень на стенах.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Light))]
    [DisallowMultipleComponent]
    [AddComponentMenu("FeatherKit/Fog Light Settings")]
    public sealed class FogLightSettings : MonoBehaviour
    {
        private static readonly Dictionary<Light, FogLightSettings> SettingsByLight = new Dictionary<Light, FogLightSettings>();

        [Tooltip("Насколько ярко этот свет светит в тумане. Единица — как все, ноль — туман " +
                 "этот свет не замечает и ничего на него не тратит. Больше единицы — ореол ярче")]
        [SerializeField, Min(0f)] private float intensityInFog = 1f;

        [Tooltip("Даёт ли этот свет тень в тумане. Тень на стенах остаётся как есть. Снять — " +
                 "дешевле: тень в тумане читается на каждом шаге каждого пикселя")]
        [SerializeField] private bool castsShadowsInFog = true;

        private Light ownLight;


        // ── Настройки ─────────────────────────────────────────────────────────

        public float IntensityInFog => intensityInFog;
        public bool CastsShadowsInFog => castsShadowsInFog;


        // ── Поиск по свету ────────────────────────────────────────────────────

        /// <summary>
        /// Яркость света в тумане и есть ли у него там тень. Без компонента — единица
        /// и тень разрешена.
        /// </summary>
        public static void Read(Light light, out float intensity, out bool castsShadows)
        {
            if (light != null && SettingsByLight.TryGetValue(light, out var settings))
            {
                intensity = settings.intensityInFog;
                castsShadows = settings.castsShadowsInFog;
                return;
            }

            intensity = 1f;
            castsShadows = true;
        }


        // ── Unity-события ─────────────────────────────────────────────────────

        // Свой словарь вместо GetComponent: туман спрашивает каждый видимый свет каждый кадр.
        private void OnEnable()
        {
            ownLight = GetComponent<Light>();
            SettingsByLight[ownLight] = this;
        }

        private void OnDisable()
        {
            if (ownLight != null)
                SettingsByLight.Remove(ownLight);
        }
    }
}
