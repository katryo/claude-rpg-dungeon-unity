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
            profile.name = "HD2D Profile";
            volume.sharedProfile = profile;

            dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(12f);
            dof.focalLength.Override(150f);
            dof.aperture.Override(2.4f);
            dof.bladeCount.Override(6);
            dof.bladeCurvature.Override(1f);

            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(1.35f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.86f, 0.72f));
            bloom.highQualityFiltering.Override(true);

            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.42f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.05f, 0.02f, 0.08f));

            color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.35f);
            color.contrast.Override(14f);
            color.saturation.Override(12f);
            color.colorFilter.Override(new Color(1f, 0.97f, 0.95f));

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            var grain = profile.Add<FilmGrain>(true);
            grain.intensity.Override(0.12f);

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
            dof.aperture.Override(DofLevel == 2 ? 2.2f : 4.5f);
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
            color.saturation.Override(boss ? 4f : battle ? 16f : 12f);
            color.colorFilter.Override(boss ? new Color(1f, 0.9f, 0.95f) : new Color(1f, 0.97f, 0.95f));
            vignette.intensity.Override(boss ? 0.5f : 0.42f);
        }

        void LateUpdate()
        {
            focus = Mathf.Lerp(focus, focusTarget, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 6f));
            if (dof != null && !menuBlur) dof.focusDistance.Override(focus);
            flashExposure = Mathf.MoveTowards(flashExposure, 0f, Time.unscaledDeltaTime * 4f);
            if (color != null) color.postExposure.Override(0.35f + flashExposure);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (profile != null) Destroy(profile);
        }
    }
}
