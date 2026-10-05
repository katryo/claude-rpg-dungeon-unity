using System.Collections;
using UnityEngine;

namespace HD2DRPG
{
    /// <summary>Spell and weapon effects keyed by <see cref="SkillDef.Fx"/>.</summary>
    public static class BattleFX
    {
        public static Color ColorOf(string fx, Element el)
        {
            switch (fx)
            {
                case "fire": return new Color(1f, 0.45f, 0.12f);
                case "ice": return new Color(0.45f, 0.85f, 1f);
                case "thunder": return new Color(1f, 0.95f, 0.4f);
                case "light": case "revive": return new Color(1f, 0.9f, 0.55f);
                case "dark": case "nova": case "meteor": return new Color(0.7f, 0.3f, 1f);
                case "heal": return new Color(0.45f, 1f, 0.6f);
                case "shield": return new Color(0.5f, 0.75f, 1f);
                case "buff": return new Color(1f, 0.6f, 0.35f);
                case "quake": return new Color(0.9f, 0.7f, 0.45f);
            }
            return el != Element.None ? Database.ElementColor(el) : Color.white;
        }

        /// <summary>Magic circle under a caster.</summary>
        public static void CastCircle(Vector3 feet, Color c)
        {
            FX.Decal(feet + Vector3.up * 0.04f, FX.Flat, ProcTex.MagicCircle, c * 1.4f, 0.6f, 2.6f, 0.9f, 120f);
            FX.Burst(feet + Vector3.up * 0.3f, c, 24, 1.2f, 0.12f, 1f, -0.4f, 0.8f, true);
            FX.Flash(feet + Vector3.up, c, 3f, 5f, 0.6f);
            AudioManager.Play("magic", 0.7f);
        }

        /// <summary>Plays the impact effect of a skill on one target.</summary>
        public static IEnumerator Impact(string fx, Element el, Vector3 center, Vector3 feet)
        {
            Color c = ColorOf(fx, el);
            var rig = CameraRig.Instance;
            switch (fx)
            {
                case "slash":
                    FX.Decal(center, FX.FaceCamera() * Quaternion.Euler(0, 0, Random.Range(-40f, 40f)), ProcTex.SlashArc, Color.white * 1.6f, 1.2f, 2.6f, 0.22f);
                    FX.Burst(center, new Color(1f, 0.95f, 0.85f), 14, 4f, 0.12f, 0.35f, 0, 0.2f, true);
                    AudioManager.Play("slash", 0.8f, Random.Range(0.9f, 1.1f));
                    break;
                case "heavy":
                    FX.Decal(center, FX.FaceCamera() * Quaternion.Euler(0, 0, 160f + Random.Range(-20f, 20f)), ProcTex.SlashArc, new Color(1f, 0.85f, 0.6f) * 1.8f, 1.6f, 3.4f, 0.28f);
                    FX.Burst(center, new Color(1f, 0.8f, 0.5f), 26, 5f, 0.16f, 0.5f, 1f, 0.2f, true);
                    rig?.Shake(0.18f, 0.25f);
                    AudioManager.Play("heavy", 0.9f);
                    break;
                case "fire":
                    FX.Burst(feet + Vector3.up * 0.3f, c, 50, 3.5f, 0.45f, 0.9f, -0.6f, 0.5f);
                    FX.Burst(center, new Color(1f, 0.85f, 0.3f), 20, 2f, 0.3f, 0.6f, -0.3f, 0.3f);
                    FX.Decal(feet + Vector3.up * 0.05f, FX.Flat, ProcTex.Ring, c * 1.5f, 0.4f, 3f, 0.5f);
                    FX.Flash(center, c, 7f, 8f, 0.5f);
                    AudioManager.Play("fire", 0.9f);
                    break;
                case "ice":
                    FX.Burst(center, c, 40, 5f, 0.18f, 0.8f, 0.8f, 0.3f, true);
                    FX.Burst(feet + Vector3.up * 0.2f, Color.white, 20, 1.5f, 0.3f, 1f, -0.1f, 0.6f);
                    FX.Decal(center, FX.FaceCamera(), ProcTex.Ring, c * 1.4f, 2.4f, 0.4f, 0.35f);
                    FX.Flash(center, c, 5f, 7f, 0.4f);
                    AudioManager.Play("ice", 0.9f);
                    break;
                case "thunder":
                    FX.Lightning(feet, c);
                    FX.Burst(center, c, 30, 6f, 0.12f, 0.4f, 0f, 0.2f, true);
                    PostFX.Instance?.Flash(0.9f);
                    rig?.Shake(0.15f, 0.3f);
                    AudioManager.Play("thunder", 0.9f);
                    break;
                case "light":
                case "revive":
                {
                    var pillar = FX.Decal(feet + Vector3.up * 2.2f, Quaternion.Euler(0, FX.FaceCamera().eulerAngles.y, 0), ProcTex.Shaft, c * 2f, 1f, 1f, 0.8f);
                    pillar.transform.localScale = new Vector3(1.4f, 4.5f, 1);
                    Object.Destroy(pillar.GetComponent<DecalAnim>());
                    pillar.AddComponent<FadeOut>().Duration = 0.8f;
                    FX.Burst(center, c, 40, 2.5f, 0.2f, 1.2f, -0.6f, 0.5f, true);
                    FX.Decal(feet + Vector3.up * 0.05f, FX.Flat, ProcTex.MagicCircle, c * 1.3f, 2.4f, 1.6f, 0.8f, 90f);
                    FX.Flash(center, c, 7f, 8f, 0.6f);
                    PostFX.Instance?.Flash(0.4f);
                    AudioManager.Play("light", 0.9f);
                    break;
                }
                case "dark":
                    FX.Decal(center, FX.FaceCamera(), ProcTex.Ring, c * 1.6f, 3f, 0.3f, 0.35f);
                    FX.Burst(center, c, 34, 3f, 0.3f, 0.7f, 0.2f, 0.6f);
                    FX.Burst(center, new Color(0.2f, 0f, 0.3f), 20, 1.5f, 0.5f, 0.8f, 0, 0.3f);
                    FX.Flash(center, c, 5f, 6f, 0.4f);
                    AudioManager.Play("dark", 0.9f);
                    break;
                case "nova":
                    FX.Decal(feet + Vector3.up * 0.05f, FX.Flat, ProcTex.Ring, c * 2f, 0.5f, 6f, 0.6f);
                    FX.Burst(center, c, 50, 5f, 0.35f, 0.9f, 0.3f, 0.5f);
                    FX.Flash(center, c, 8f, 9f, 0.5f);
                    rig?.Shake(0.22f, 0.4f);
                    AudioManager.Play("dark", 1f, 0.8f);
                    break;
                case "meteor":
                    yield return FX.Meteor(feet + Vector3.up * 0.5f, c);
                    rig?.Shake(0.3f, 0.4f);
                    AudioManager.Play("heavy", 1f, 0.7f);
                    break;
                case "quake":
                    FX.Decal(feet + Vector3.up * 0.05f, FX.Flat, ProcTex.Ring, c * 1.5f, 0.5f, 4f, 0.5f);
                    FX.Burst(feet + Vector3.up * 0.2f, new Color(0.6f, 0.5f, 0.4f), 40, 4f, 0.2f, 0.8f, 2.5f, 0.6f, true);
                    rig?.Shake(0.28f, 0.45f);
                    AudioManager.Play("heavy", 1f, 0.8f);
                    break;
                case "heal":
                    FX.Burst(feet + Vector3.up * 0.2f, c, 30, 0.6f, 0.15f, 1.4f, -0.7f, 0.5f, true);
                    FX.Decal(feet + Vector3.up * 0.05f, FX.Flat, ProcTex.Ring, c * 1.4f, 0.5f, 2f, 0.7f);
                    FX.Flash(center, c, 3f, 5f, 0.6f);
                    AudioManager.Play("heal", 0.8f);
                    break;
                case "buff":
                case "shield":
                    FX.Decal(feet + Vector3.up * 0.1f, FX.Flat, ProcTex.Ring, c * 1.5f, 2f, 0.6f, 0.6f);
                    FX.Decal(feet + Vector3.up * 0.8f, FX.Flat, ProcTex.Ring, c * 1.2f, 1.6f, 0.5f, 0.6f);
                    FX.Burst(feet + Vector3.up * 0.4f, c, 20, 0.5f, 0.12f, 1.1f, -0.8f, 0.5f, true);
                    AudioManager.Play("buff", 0.8f);
                    break;
                default:
                    FX.Burst(center, c, 20, 3f, 0.2f, 0.5f);
                    AudioManager.Play("hit", 0.8f);
                    break;
            }
            yield return null;
        }
    }

}
