using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HD2DRPG
{
    /// <summary>
    /// Creates URP materials at runtime. Template materials in Resources/HD2D/Materials (created by
    /// the editor setup) are preferred so the correct shader variants survive player builds; if they
    /// are missing we fall back to Shader.Find and set keywords by hand.
    /// </summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public static bool IsURP => GraphicsSettings.defaultRenderPipeline != null || QualitySettings.renderPipeline != null;

        static Shader Find(params string[] names)
        {
            foreach (var n in names)
            {
                var s = Shader.Find(n);
                if (s != null) return s;
            }
            return Shader.Find("Hidden/InternalErrorShader");
        }

        static Material FromTemplate(string template, params string[] shaderNames)
        {
            var t = Resources.Load<Material>("HD2D/Materials/" + template);
            if (t != null) return new Material(t);
            return new Material(Find(shaderNames));
        }

        static void SetTex(Material m, Texture tex)
        {
            if (tex == null) return;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        }

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        /// <summary>Opaque lit surface (floors, walls) with world-tiled texture.</summary>
        public static Material Lit(string key, Texture tex, Color tint, float smoothness = 0.15f)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = FromTemplate("HD2D_Lit", "Universal Render Pipeline/Lit", "Standard");
            m.name = key;
            SetTex(m, tex);
            SetColor(m, tint);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            cache[key] = m;
            return m;
        }

        /// <summary>
        /// Alpha-clipped, double-sided lit material for pixel sprites and cut-out decals.
        /// A small emission keeps sprites readable in the dark castle (the classic HD-2D look).
        /// </summary>
        public static Material Sprite(Texture tex, float selfLight = 0.28f, Color? tint = null)
        {
            var m = FromTemplate("HD2D_SpriteLit", "Universal Render Pipeline/Lit", "Standard");
            m.name = "Sprite_" + (tex != null ? tex.name : "null");
            SetTex(m, tex);
            SetColor(m, tint ?? Color.white);
            ConfigureCutout(m);
            SetEmission(m, tex, Color.white * selfLight);
            return m;
        }

        public static void ConfigureCutout(Material m)
        {
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (m.HasProperty("_Mode")) // Built-in Standard fallback: cutout mode
            {
                m.SetFloat("_Mode", 1f);
                m.SetOverrideTag("RenderType", "TransparentCutout");
            }
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.doubleSidedGI = true;
        }

        public static void SetEmission(Material m, Texture map, Color c)
        {
            if (!m.HasProperty("_EmissionColor")) return;
            m.SetColor("_EmissionColor", c);
            if (map != null && m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", map);
            if (c.maxColorComponent > 0.001f)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else m.DisableKeyword("_EMISSION");
        }

        /// <summary>Additive unlit material for particles, glows, light shafts and spell effects.</summary>
        public static Material Additive(string key, Texture tex)
        {
            if (cache.TryGetValue("add_" + key, out var m) && m != null) return m;
            m = FromTemplate("HD2D_Additive", "Universal Render Pipeline/Particles/Unlit", "Legacy Shaders/Particles/Additive", "Sprites/Default");
            m.name = "Additive_" + key;
            SetTex(m, tex);
            SetColor(m, Color.white);
            ConfigureAdditive(m);
            cache["add_" + key] = m;
            return m;
        }

        /// <summary>Fresh (uncached) additive material, for effects that animate their color.</summary>
        public static Material AdditiveInstance(Texture tex, Color color)
        {
            var m = new Material(Additive(tex != null ? tex.name : "none", tex));
            SetColor(m, color);
            return m;
        }

        public static void ConfigureAdditive(Material m)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f); // Additive
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.One);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>Unlit, alpha-clipped material for glowing pixel sprites (flames, crystals).</summary>
        public static Material Glow(Texture tex, float intensity = 2.5f)
        {
            var m = FromTemplate("HD2D_UnlitCutout", "Universal Render Pipeline/Unlit", "Unlit/Transparent Cutout");
            m.name = "Glow_" + (tex != null ? tex.name : "null");
            SetTex(m, tex);
            SetColor(m, Color.white * intensity);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            return m;
        }

        public static void SetMainColor(Material m, Color c) => SetColor(m, c);
    }
}
