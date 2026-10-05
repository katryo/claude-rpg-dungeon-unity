using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HD2DRPG
{
    /// <summary>
    /// The "HD-2D" look: tilt-shift style bokeh depth of field focused on the party, warm bloom on
    /// torches and magic, a heavy vignette, slightly lifted contrast and saturation, and fog.
    /// </summary>
    public class PostFX : MonoBehaviour
    {
        public static PostFX Instance { get; private set; }

        Volume volume;
        VolumeProfile profile;
        DepthOfField dof;
        Bloom bloom;
        Vignette vignette;
        ColorAdjustments color;

        /// <summary>0 = off, 1 = soft, 2 = strong (HD-2D default).</summary>
        public int DofLevel { get; private set; } = 2;
        float focusTarget = 12f, focus = 12f;
        float flashExposure;
        bool menuBlur;

        /// <summary>Editable profile asset (created by the scene baker) under Resources.</summary>
        public const string ProfileResourcePath = "HD2D/HD2D_PostFX";

        float baseAperture = 2.2f, baseExposure = 0.35f, baseSaturation = 12f, baseVignette = 0.42f;
        Color baseFilter = Color.white;

        /// <summary>Adds the HD-2D default overrides for any component the profile lacks.</summary>
        public static void AddDefaults(VolumeProfile p)
        {
            if (!p.Has<DepthOfField>())
            {
                var d = p.Add<DepthOfField>(true);
                d.mode.Override(DepthOfFieldMode.Bokeh);
                d.focusDistance.Override(12f);
                d.focalLength.Override(150f);
                d.aperture.Override(2.2f);
                d.bladeCount.Override(6);
                d.bladeCurvature.Override(1f);
            }
            if (!p.Has<Bloom>())
            {
                var b = p.Add<Bloom>(true);
                b.threshold.Override(0.85f);
                b.intensity.Override(1.35f);
                b.scatter.Override(0.72f);
                b.tint.Override(new Color(1f, 0.86f, 0.72f));
                b.highQualityFiltering.Override(true);
            }
            if (!p.Has<Vignette>())
            {
                var v = p.Add<Vignette>(true);
                v.intensity.Override(0.42f);
                v.smoothness.Override(0.45f);
                v.color.Override(new Color(0.05f, 0.02f, 0.08f));
            }
            if (!p.Has<ColorAdjustments>())
            {
                var c = p.Add<ColorAdjustments>(true);
                c.postExposure.Override(0.35f);
                c.contrast.Override(14f);
                c.saturation.Override(12f);
                c.colorFilter.Override(new Color(1f, 0.97f, 0.95f));
            }
            if (!p.Has<Tonemapping>()) p.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
            if (!p.Has<FilmGrain>()) p.Add<FilmGrain>(true).intensity.Override(0.12f);
        }

        public static PostFX Create(Camera cam)
        {
            var go = new GameObject("PostFX Volume");
            var fx = go.AddComponent<PostFX>();
            fx.Setup(cam);
            return fx;
        }

        void Setup(Camera cam)
        {
            Instance = this;
            var camData = cam.GetUniversalAdditionalCameraData();
            if (camData != null)
            {
                camData.renderPostProcessing = true;
                camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                camData.antialiasingQuality = AntialiasingQuality.High;
                camData.renderShadows = true;
                camData.stopNaN = true;
                camData.dithering = true;
            }
            cam.allowHDR = true;

            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "HD2D Profile (runtime)";
            // Copy the editable profile asset if it exists, so runtime tweaks never touch the asset.
            var template = Resources.Load<VolumeProfile>(ProfileResourcePath);
            if (template != null)
                foreach (var c in template.components)
                    if (c != null) profile.components.Add(Instantiate(c));
            AddDefaults(profile);
            volume.sharedProfile = profile;

            profile.TryGet(out dof);
            profile.TryGet(out bloom);
            profile.TryGet(out vignette);
            profile.TryGet(out color);
            baseAperture = dof.aperture.value;
            baseExposure = color.postExposure.value;
            baseSaturation = color.saturation.value;
            baseVignette = vignette.intensity.value;
            baseFilter = color.colorFilter.value;
            ApplyDofLevel();
        }

        public void SetFocusDistance(float d, bool snap = false)
        {
            focusTarget = d;
            if (snap) focus = d;
        }

        public void SetDofLevel(int level)
        {
            DofLevel = Mathf.Clamp(level, 0, 2);
            ApplyDofLevel();
        }

        void ApplyDofLevel()
        {
            if (dof == null) return;
            dof.active = DofLevel > 0;
            dof.aperture.Override(DofLevel == 2 ? baseAperture : Mathf.Min(32f, baseAperture * 2f));
        }

        /// <summary>Blurs the whole scene behind the menu.</summary>
        public void SetMenuBlur(bool on)
        {
            menuBlur = on;
            if (dof == null) return;
            if (on)
            {
                dof.active = true;
                dof.focusDistance.Override(0.3f);
                dof.aperture.Override(1.4f);
            }
            else ApplyDofLevel();
        }

        /// <summary>Brief exposure flash (lightning, big spells, break).</summary>
        public void Flash(float amount)
        {
            flashExposure = Mathf.Max(flashExposure, amount);
        }

        public void SetBattleMood(bool battle, bool boss)
        {
            if (color == null) return;
            color.saturation.Override(baseSaturation + (boss ? -8f : battle ? 4f : 0f));
            color.colorFilter.Override(boss ? baseFilter * new Color(1f, 0.93f, 1f) : baseFilter);
            vignette.intensity.Override(baseVignette + (boss ? 0.08f : 0f));
        }

        void LateUpdate()
        {
            focus = Mathf.Lerp(focus, focusTarget, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 6f));
            if (dof != null && !menuBlur) dof.focusDistance.Override(focus);
            flashExposure = Mathf.MoveTowards(flashExposure, 0f, Time.unscaledDeltaTime * 4f);
            if (color != null) color.postExposure.Override(baseExposure + flashExposure);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (profile != null) Destroy(profile);
        }
    }
}
