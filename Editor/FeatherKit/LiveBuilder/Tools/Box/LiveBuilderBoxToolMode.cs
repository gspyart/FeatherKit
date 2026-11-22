using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Режим «Коробки» (<see cref="LiveBuilderBoxTool"/>) — её часть со своим жестом рисования. Инструмент держит по части на режим
    /// и пересылает текущей мышь, клавиши, отмену, подсказки, настройки и вид — те же крючки, что у инструмента.
    /// </summary>
    public abstract class LiveBuilderBoxToolMode
    {
        // ── Окно сцены ───────────────────────────────────────────────────

        public abstract bool IsBusy { get; }

        public virtual LiveBuilderDrawingReport DrawingReport => null;

        public abstract bool DrawsSurfaceHover { get; }

        public virtual bool ShowsDepthClickHint => false;

        public abstract string Hint { get; }

        public abstract string[] KeyHints { get; }


        // ── Окно: редкие настройки ───────────────────────────────────────

        public virtual string MoreSettingsSummary => null;

        public virtual bool AreMoreSettingsChanged => false;


        // ── Окно ─────────────────────────────────────────────────────────

        public virtual void DrawSettings()
        {
        }


        public virtual void DrawMoreSettings()
        {
        }


        // ── Окно сцены: мышь ─────────────────────────────────────────────

        public virtual bool OnMouseMove(Ray worldRay, Event current)
        {
            return false;
        }


        public virtual bool OnMouseDown(Ray worldRay, Event current)
        {
            return false;
        }


        public virtual void OnMouseDrag(Ray downRay, Ray worldRay, bool canBeginGesture)
        {
        }


        public virtual bool OnMouseUp(Event current)
        {
            return false;
        }


        // ── Окно сцены: клавиши, отмена, вид ─────────────────────────────

        public virtual bool OnKeyDown(Event current)
        {
            return false;
        }


        // Бросить начатое. «Новый» у «Коробки» общий на все режимы — его снимает сам инструмент.
        public abstract void Cancel();


        public abstract void DrawPreview();
    }
}
