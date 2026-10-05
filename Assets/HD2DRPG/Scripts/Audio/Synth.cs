using System.Collections.Generic;
using UnityEngine;

namespace HD2DRPG
{
    /// <summary>
    /// Tiny offline synthesizer: renders all music loops and sound effects into AudioClips at
    /// startup-on-demand, so the project needs no audio assets.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 24000;

        /// <summary>When true, notes that run past the end wrap to the start (seamless music loops).</summary>
        static bool loopRender;

        enum Wave { Sine, Triangle, Square, Saw, Noise, Pulse25 }

        static float Osc(Wave w, float phase, ref uint rng)
        {
            phase -= Mathf.Floor(phase);
            switch (w)
            {
                case Wave.Sine: return Mathf.Sin(phase * 2f * Mathf.PI);
                case Wave.Triangle: return 1f - 4f * Mathf.Abs(phase - 0.5f);
                case Wave.Square: return phase < 0.5f ? 0.7f : -0.7f;
                case Wave.Pulse25: return phase < 0.25f ? 0.7f : -0.7f;
                case Wave.Saw: return 2f * phase - 1f;
                default:
                    rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                    return (rng & 0xFFFF) / 32768f - 1f;
            }
        }

        static float Midi(float note) => 440f * Mathf.Pow(2f, (note - 69f) / 12f);

        static int NoteNum(string s)
        {
            // e.g. "C4", "F#3", "Bb2"
            int[] baseN = { 9, 11, 0, 2, 4, 5, 7 }; // A B C D E F G
            char c = char.ToUpperInvariant(s[0]);
            int n = baseN[c - 'A'];
            int i = 1;
            if (i < s.Length && s[i] == '#') { n++; i++; }
            else if (i < s.Length && s[i] == 'b') { n--; i++; }
            int oct = int.Parse(s.Substring(i));
            return n + (oct + 1) * 12;
        }

        /// <summary>Renders one voice into the buffer.</summary>
        static void Note(float[] buf, float start, float dur, float note, Wave w, float vol,
                         float attack = 0.01f, float decay = 0.1f, float sustain = 0.7f, float release = 0.1f,
                         float vibrato = 0f, float detune = 0f, float slide = 0f)
        {
            int s0 = Mathf.Max(0, (int)(start * Rate));
            int len = (int)((dur + release) * Rate);
            float f = Midi(note + detune);
            float phase = 0;
            uint rng = (uint)(note * 7919 + start * 104729) | 1u;
            for (int i = 0; i < len; i++)
            {
                int idx = s0 + i;
                if (idx >= buf.Length) { if (!loopRender) break; idx -= buf.Length; } // wrap for seamless loops
                if (idx < 0 || idx >= buf.Length) continue;
                float t = i / (float)Rate;
                float env;
                if (t < attack) env = t / attack;
                else if (t < attack + decay) env = Mathf.Lerp(1f, sustain, (t - attack) / decay);
                else if (t < dur) env = sustain;
                else env = sustain * Mathf.Max(0f, 1f - (t - dur) / release);
                float freq = f * (1f + (vibrato > 0 ? Mathf.Sin(t * 5.5f * 2 * Mathf.PI) * vibrato * Mathf.Clamp01(t * 2f) : 0f));
                if (slide != 0) freq *= Mathf.Pow(2f, slide * t / 12f);
                phase += freq / Rate;
                buf[idx] += Osc(w, phase, ref rng) * env * vol;
            }
        }

        static void Drum(float[] buf, float start, string kind, float vol)
        {
            int s0 = (int)(start * Rate);
            uint rng = (uint)(s0 * 2654435761u) | 1u;
            float len = kind == "kick" ? 0.25f : kind == "snare" ? 0.18f : kind == "crash" ? 0.9f : 0.05f;
            int n = (int)(len * Rate);
            float phase = 0;
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= buf.Length) { if (!loopRender) break; idx -= buf.Length; }
                if (idx < 0 || idx >= buf.Length) continue;
                float t = i / (float)Rate;
                float v;
                if (kind == "kick")
                {
                    float f = 120f * Mathf.Exp(-t * 18f) + 42f;
                    phase += f / Rate;
                    v = Mathf.Sin(phase * 2 * Mathf.PI) * Mathf.Exp(-t * 9f) * 1.2f;
                }
                else if (kind == "snare")
                {
                    float noise = Osc(Wave.Noise, 0, ref rng);
                    phase += 190f / Rate;
                    v = (noise * 0.8f + Mathf.Sin(phase * 2 * Mathf.PI) * 0.4f) * Mathf.Exp(-t * 18f);
                }
                else if (kind == "crash")
                {
                    float noise = Osc(Wave.Noise, 0, ref rng);
                    lp += (noise - lp) * 0.6f;
                    v = (noise - lp) * Mathf.Exp(-t * 4f) * 0.6f;
                }
                else
                {
                    float noise = Osc(Wave.Noise, 0, ref rng);
                    lp += (noise - lp) * 0.5f;
                    v = (noise - lp) * Mathf.Exp(-t * 60f);
                }
                buf[idx] += v * vol;
            }
        }

        static void Echo(float[] buf, float delay, float feedback, bool wrap)
        {
            int d = (int)(delay * Rate);
            int passes = wrap ? 2 : 1;
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < buf.Length; i++)
                {
                    int j = i - d;
                    if (j < 0) { if (!wrap) continue; j += buf.Length; }
                    buf[i] += buf[j] * feedback * (p == 0 ? 1f : 0.5f);
                }
        }

        static void LowPass(float[] buf, float k)
        {
            float y = 0;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < buf.Length; i++) { y += (buf[i] - y) * k; buf[i] = y; }
        }

        static AudioClip Finish(string name, float[] buf, float gain)
        {
            float peak = 0.0001f;
            for (int i = 0; i < buf.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(buf[i]));
            float g = gain / peak;
            for (int i = 0; i < buf.Length; i++)
            {
                float v = buf[i] * g;
                buf[i] = v / (1f + Mathf.Abs(v) * 0.25f) * 1.2f; // soft clip
            }
            var clip = AudioClip.Create(name, buf.Length, 1, Rate, false);
            clip.SetData(buf, 0);
            return clip;
        }

        // ================================================================== music

        struct Chord
        {
            public int[] Notes;
            public Chord(params string[] n)
            {
                Notes = new int[n.Length];
                for (int i = 0; i < n.Length; i++) Notes[i] = NoteNum(n[i]);
            }
        }

        static void Melody(float[] buf, float stepSec, string seq, Wave w, float vol, float transpose = 0,
                           float vibrato = 0.006f, float attack = 0.02f, float release = 0.15f, float startStep = 0)
        {
            var tokens = seq.Split(new[] { ' ', '\n', '\t', '|' }, System.StringSplitOptions.RemoveEmptyEntries);
            int i = 0;
            while (i < tokens.Length)
            {
                string tk = tokens[i];
                if (tk == "-" || tk == "_") { i++; continue; }
                int len = 1;
                while (i + len < tokens.Length && tokens[i + len] == "_") len++;
                Note(buf, (startStep + i) * stepSec, len * stepSec * 0.95f, NoteNum(tk) + transpose, w, vol,
                     attack, 0.12f, 0.65f, release, vibrato);
                i += len;
            }
        }

        static AudioClip Castle()
        {
            float bpm = 72f;
            float step = 60f / bpm / 2f; // eighth notes
            int steps = 64;
            var buf = new float[(int)(steps * step * Rate)];
            var chords = new[]
            {
                new Chord("D3", "F3", "A3", "D4"), new Chord("A#2", "D3", "F3", "A#3"),
                new Chord("G2", "A#2", "D3", "G3"), new Chord("A2", "C#3", "E3", "A3"),
                new Chord("D3", "F3", "A3", "D4"), new Chord("C3", "E3", "G3", "C4"),
                new Chord("A#2", "D3", "F3", "A#3"), new Chord("A2", "C#3", "E3", "A3"),
            };
            for (int c = 0; c < chords.Length; c++)
            {
                float t0 = c * 8 * step;
                var ch = chords[c];
                // pad
                foreach (var n in ch.Notes)
                {
                    Note(buf, t0, 8 * step, n + 12, Wave.Saw, 0.035f, 0.6f, 0.5f, 0.8f, 0.8f, 0.004f, 0.06f);
                    Note(buf, t0, 8 * step, n + 12, Wave.Saw, 0.035f, 0.6f, 0.5f, 0.8f, 0.8f, 0.004f, -0.06f);
                }
                // bass
                Note(buf, t0, 4 * step, ch.Notes[0] - 12, Wave.Triangle, 0.22f, 0.02f, 0.3f, 0.6f, 0.2f);
                Note(buf, t0 + 4 * step, 4 * step, ch.Notes[0] - 12, Wave.Triangle, 0.18f, 0.02f, 0.3f, 0.6f, 0.2f);
                // harp arpeggio
                int[] pattern = { 0, 1, 2, 3, 2, 1, 2, 3 };
                for (int k = 0; k < 8; k++)
                    Note(buf, t0 + k * step, step * 1.5f, ch.Notes[pattern[k]] + 12, Wave.Triangle, 0.085f, 0.004f, 0.25f, 0.15f, 0.3f);
            }
            string mel =
                "- - - - A4 _ _ D5 | F5 _ E5 _ D5 _ _ _ | - - D5 _ F5 _ A5 _ | G5 _ _ F5 E5 _ _ _ |" +
                "- - - - A4 _ _ D5 | E5 _ F5 _ G5 _ _ _ | F5 _ E5 _ D5 _ C#5 _ | D5 _ _ _ _ _ _ _";
            var mb = new float[buf.Length];
            Melody(mb, step, mel, Wave.Pulse25, 0.06f, 0, 0.008f, 0.03f, 0.3f);
            Melody(mb, step, mel, Wave.Sine, 0.08f, -12, 0.008f, 0.03f, 0.3f);
            for (int i = 0; i < buf.Length; i++) buf[i] += mb[i];
            LowPass(buf, 0.45f);
            Echo(buf, step * 3, 0.32f, true);
            return Finish("bgm_castle", buf, 0.7f);
        }

        static AudioClip Battle()
        {
            float bpm = 152f;
            float step = 60f / bpm / 4f; // sixteenths
            int steps = 128;
            var buf = new float[(int)(steps * step * Rate)];
            var chords = new[]
            {
                new Chord("E2", "G3", "B3", "E4"), new Chord("C2", "G3", "C4", "E4"),
                new Chord("D2", "F#3", "A3", "D4"), new Chord("B1", "F#3", "B3", "D#4"),
            };
            for (int c = 0; c < 8; c++)
            {
                var ch = chords[c % 4];
                float t0 = c * 16 * step;
                // driving bass (eighths, octave jumps)
                for (int k = 0; k < 8; k++)
                    Note(buf, t0 + k * 2 * step, step * 1.6f, ch.Notes[0] + (k % 2 == 1 ? 12 : 0) + 12, Wave.Saw, 0.12f, 0.005f, 0.08f, 0.5f, 0.05f);
                // stabs
                foreach (int s in new[] { 0, 3, 6, 10, 12 })
                    foreach (var n in ch.Notes)
                        if (n > 40) Note(buf, t0 + s * step, step * 1.2f, n + 12, Wave.Square, 0.03f, 0.003f, 0.06f, 0.3f, 0.05f);
                // drums
                for (int k = 0; k < 16; k++)
                {
                    if (k % 8 == 0 || k == 6 || k == 11) Drum(buf, t0 + k * step, "kick", 0.55f);
                    if (k % 8 == 4) Drum(buf, t0 + k * step, "snare", 0.4f);
                    if (k % 2 == 0) Drum(buf, t0 + k * step, "hat", 0.18f);
                }
                if (c == 0) Drum(buf, t0, "crash", 0.3f);
            }
            string mel =
                "E5 _ _ _ B4 _ E5 _ F#5 _ G5 _ F#5 _ E5 _ | E5 _ _ _ G5 _ _ _ C6 _ B5 _ G5 _ E5 _ |" +
                "D5 _ _ _ F#5 _ A5 _ D6 _ _ _ C6 _ A5 _ | B5 _ _ _ _ _ _ _ D#5 _ F#5 _ B5 _ A5 _ |" +
                "G5 _ _ _ F#5 _ E5 _ B4 _ _ _ E5 _ G5 _ | C6 _ _ _ B5 _ A5 _ G5 _ _ _ E5 _ _ _ |" +
                "F#5 _ G5 _ A5 _ _ _ D6 _ C6 _ A5 _ F#5 _ | G5 _ F#5 _ D#5 _ _ _ B4 _ _ _ - - - -";
            Melody(buf, step, mel, Wave.Square, 0.075f, 0, 0.01f, 0.01f, 0.08f);
            Melody(buf, step, mel, Wave.Pulse25, 0.04f, -12, 0.01f, 0.01f, 0.08f);
            Echo(buf, step * 3, 0.18f, true);
            return Finish("bgm_battle", buf, 0.72f);
        }

        static AudioClip Boss()
        {
            float bpm = 138f;
            float step = 60f / bpm / 4f;
            int steps = 128;
            var buf = new float[(int)(steps * step * Rate)];
            var chords = new[]
            {
                new Chord("C2", "G3", "C4", "D#4"), new Chord("G#1", "G#3", "C4", "D#4"),
                new Chord("F1", "F3", "G#3", "C4"), new Chord("G1", "G3", "B3", "D4"),
            };
            for (int c = 0; c < 8; c++)
            {
                var ch = chords[c % 4];
                float t0 = c * 16 * step;
                // organ pad
                foreach (var n in ch.Notes)
                {
                    Note(buf, t0, 16 * step, n, Wave.Square, 0.03f, 0.08f, 0.2f, 0.8f, 0.2f, 0.003f);
                    Note(buf, t0, 16 * step, n + 12, Wave.Sine, 0.04f, 0.08f, 0.2f, 0.8f, 0.2f, 0.003f);
                }
                // choir
                Note(buf, t0, 16 * step, ch.Notes[2] + 12, Wave.Sine, 0.05f, 0.4f, 0.3f, 0.8f, 0.3f, 0.01f);
                // galloping bass
                for (int k = 0; k < 16; k++)
                    if (k % 4 != 1) Note(buf, t0 + k * step, step * 0.9f, ch.Notes[0] + 12, Wave.Saw, 0.13f, 0.003f, 0.05f, 0.4f, 0.03f);
                for (int k = 0; k < 16; k++)
                {
                    if (k % 4 == 0 || k % 4 == 3) Drum(buf, t0 + k * step, "kick", 0.5f);
                    if (k % 8 == 4) Drum(buf, t0 + k * step, "snare", 0.45f);
                    Drum(buf, t0 + k * step, "hat", k % 2 == 0 ? 0.16f : 0.08f);
                }
                if (c % 4 == 0) Drum(buf, t0, "crash", 0.35f);
            }
            string mel =
                "C5 _ _ _ _ _ D#5 _ G5 _ _ _ F5 _ D#5 _ | D#5 _ _ _ _ _ D5 _ C5 _ _ _ G4 _ _ _ |" +
                "G#4 _ _ _ C5 _ F5 _ G#5 _ _ _ G5 _ F5 _ | G5 _ _ _ _ _ _ _ B4 _ D5 _ F5 _ G5 _ |" +
                "C6 _ _ _ B5 _ G5 _ D#5 _ _ _ C5 _ D#5 _ | D#5 _ D5 _ C5 _ G#4 _ C5 _ _ _ _ _ _ _ |" +
                "F5 _ G#5 _ C6 _ _ _ D6 _ C6 _ G#5 _ F5 _ | G5 _ _ _ F5 _ _ _ D5 _ _ _ B4 _ _ _";
            Melody(buf, step, mel, Wave.Saw, 0.06f, 0, 0.012f, 0.01f, 0.1f);
            Melody(buf, step, mel, Wave.Square, 0.04f, 12, 0.012f, 0.01f, 0.1f);
            LowPass(buf, 0.6f);
            Echo(buf, step * 3, 0.22f, true);
            return Finish("bgm_boss", buf, 0.75f);
        }

        static AudioClip Ending()
        {
            float bpm = 80f;
            float step = 60f / bpm / 2f;
            int steps = 64;
            var buf = new float[(int)(steps * step * Rate)];
            var chords = new[]
            {
                new Chord("F2", "A3", "C4", "F4"), new Chord("C2", "G3", "C4", "E4"),
                new Chord("D2", "A3", "D4", "F4"), new Chord("A#1", "A#3", "D4", "F4"),
                new Chord("F2", "A3", "C4", "F4"), new Chord("C2", "G3", "C4", "E4"),
                new Chord("A#1", "A#3", "D4", "F4"), new Chord("C2", "G3", "C4", "E4"),
            };
            for (int c = 0; c < chords.Length; c++)
            {
                float t0 = c * 8 * step;
                var ch = chords[c];
                foreach (var n in ch.Notes)
                    Note(buf, t0, 8 * step, n, Wave.Sine, 0.06f, 0.3f, 0.4f, 0.8f, 0.6f, 0.003f);
                Note(buf, t0, 8 * step, ch.Notes[0], Wave.Triangle, 0.2f, 0.02f, 0.5f, 0.5f, 0.4f);
                int[] pattern = { 1, 2, 3, 2, 1, 2, 3, 2 };
                for (int k = 0; k < 8; k++)
                    Note(buf, t0 + k * step, step * 1.6f, ch.Notes[pattern[k]] + 12, Wave.Triangle, 0.07f, 0.004f, 0.2f, 0.2f, 0.4f);
            }
            string mel =
                "C5 _ _ _ A4 _ C5 _ | D5 _ _ _ E5 _ C5 _ | D5 _ _ _ F5 _ A5 _ | G5 _ _ _ _ _ F5 _ |" +
                "A5 _ _ _ G5 _ F5 _ | E5 _ _ _ D5 _ C5 _ | D5 _ _ _ F5 _ E5 _ | F5 _ _ _ _ _ _ _";
            Melody(buf, step, mel, Wave.Sine, 0.12f, 0, 0.01f, 0.05f, 0.4f);
            Melody(buf, step, mel, Wave.Triangle, 0.05f, 12, 0.01f, 0.05f, 0.4f);
            Echo(buf, step * 3, 0.35f, true);
            return Finish("bgm_ending", buf, 0.65f);
        }

        static AudioClip Victory()
        {
            float step = 0.11f;
            var buf = new float[(int)(4.2f * Rate)];
            string mel = "C5 E5 G5 C6 _ _ G5 _ A5 _ B5 _ C6 _ _ _ _ _ _ _";
            Melody(buf, step, mel, Wave.Square, 0.1f, 0, 0f, 0.005f, 0.2f);
            Melody(buf, step, mel, Wave.Triangle, 0.12f, -12, 0f, 0.005f, 0.2f);
            foreach (var n in new[] { "C4", "E4", "G4", "C3" })
                Note(buf, 12 * step, 1.6f, NoteNum(n), Wave.Saw, 0.05f, 0.02f, 0.3f, 0.6f, 0.8f);
            Drum(buf, 0, "crash", 0.3f);
            Drum(buf, 12 * step, "crash", 0.4f);
            Echo(buf, 0.18f, 0.25f, false);
            return Finish("bgm_victory", buf, 0.7f);
        }

        // ================================================================== sfx

        static AudioClip Sfx(string id)
        {
            float[] b;
            switch (id)
            {
                case "cursor":
                    b = new float[(int)(0.06f * Rate)];
                    Note(b, 0, 0.03f, 84, Wave.Square, 0.4f, 0.001f, 0.02f, 0.4f, 0.02f);
                    break;
                case "confirm":
                    b = new float[(int)(0.18f * Rate)];
                    Note(b, 0, 0.05f, 79, Wave.Square, 0.35f, 0.001f, 0.03f, 0.5f, 0.02f);
                    Note(b, 0.06f, 0.08f, 86, Wave.Square, 0.35f, 0.001f, 0.03f, 0.5f, 0.04f);
                    break;
                case "cancel":
                    b = new float[(int)(0.16f * Rate)];
                    Note(b, 0, 0.05f, 76, Wave.Square, 0.3f, 0.001f, 0.03f, 0.5f, 0.02f);
                    Note(b, 0.06f, 0.06f, 69, Wave.Square, 0.3f, 0.001f, 0.03f, 0.5f, 0.03f);
                    break;
                case "error":
                    b = new float[(int)(0.2f * Rate)];
                    Note(b, 0, 0.15f, 45, Wave.Square, 0.4f, 0.001f, 0.05f, 0.6f, 0.03f);
                    break;
                case "slash":
                    b = new float[(int)(0.25f * Rate)];
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = i / (float)Rate;
                        uint r = (uint)(i * 2654435761u) | 1u;
                        float n = Osc(Wave.Noise, 0, ref r);
                        b[i] = n * Mathf.Exp(-t * 18f) * Mathf.Clamp01(t * 200f) * 0.6f;
                    }
                    LowPass(b, 0.35f);
                    Note(b, 0, 0.08f, 90, Wave.Saw, 0.12f, 0.001f, 0.05f, 0.2f, 0.05f, 0, 0, -30f);
                    break;
                case "hit":
                    b = new float[(int)(0.25f * Rate)];
                    Drum(b, 0, "kick", 0.8f);
                    Drum(b, 0, "snare", 0.5f);
                    break;
                case "heavy":
                    b = new float[(int)(0.5f * Rate)];
                    Drum(b, 0, "kick", 1f);
                    Drum(b, 0.01f, "snare", 0.6f);
                    Note(b, 0, 0.2f, 36, Wave.Saw, 0.3f, 0.001f, 0.1f, 0.3f, 0.2f, 0, 0, -12f);
                    break;
                case "magic":
                    b = new float[(int)(0.6f * Rate)];
                    for (int k = 0; k < 6; k++) Note(b, k * 0.05f, 0.08f, 72 + k * 4, Wave.Sine, 0.25f, 0.002f, 0.05f, 0.5f, 0.15f);
                    Echo(b, 0.09f, 0.3f, false);
                    break;
                case "fire":
                    b = new float[(int)(0.7f * Rate)];
                    {
                        uint r = 12345u; float lp = 0;
                        for (int i = 0; i < b.Length; i++)
                        {
                            float t = i / (float)Rate;
                            float n = Osc(Wave.Noise, 0, ref r);
                            lp += (n - lp) * (0.08f + 0.1f * Mathf.Sin(t * 30f));
                            b[i] = lp * Mathf.Exp(-t * 4f) * Mathf.Clamp01(t * 40f) * 2.2f;
                        }
                    }
                    break;
                case "ice":
                    b = new float[(int)(0.6f * Rate)];
                    for (int k = 0; k < 8; k++) Note(b, k * 0.035f, 0.05f, 96 - (k % 3) * 5, Wave.Triangle, 0.2f, 0.001f, 0.03f, 0.3f, 0.2f);
                    Echo(b, 0.07f, 0.35f, false);
                    break;
                case "thunder":
                    b = new float[(int)(0.9f * Rate)];
                    {
                        uint r = 999u;
                        for (int i = 0; i < b.Length; i++)
                        {
                            float t = i / (float)Rate;
                            float n = Osc(Wave.Noise, 0, ref r);
                            float crackle = (Mathf.Sin(t * 90f) > 0.6f ? 1f : 0.4f);
                            b[i] = n * crackle * Mathf.Exp(-t * 5f) * 0.8f;
                        }
                        Drum(b, 0, "kick", 0.9f);
                    }
                    break;
                case "light":
                    b = new float[(int)(0.9f * Rate)];
                    foreach (var n in new[] { 84, 88, 91, 96 })
                        Note(b, (n - 84) * 0.012f, 0.4f, n, Wave.Sine, 0.18f, 0.01f, 0.1f, 0.6f, 0.4f, 0.01f);
                    Echo(b, 0.11f, 0.35f, false);
                    break;
                case "dark":
                    b = new float[(int)(0.8f * Rate)];
                    Note(b, 0, 0.5f, 40, Wave.Saw, 0.3f, 0.05f, 0.2f, 0.6f, 0.2f, 0.02f, 0, -10f);
                    Note(b, 0, 0.5f, 47, Wave.Saw, 0.2f, 0.05f, 0.2f, 0.6f, 0.2f, 0.02f, 0, -10f);
                    LowPass(b, 0.25f);
                    break;
                case "heal":
                    b = new float[(int)(0.9f * Rate)];
                    foreach (var (n, t) in new[] { (79, 0f), (84, 0.08f), (88, 0.16f), (91, 0.24f) })
                        Note(b, t, 0.3f, n, Wave.Sine, 0.22f, 0.005f, 0.1f, 0.5f, 0.3f);
                    Echo(b, 0.12f, 0.3f, false);
                    break;
                case "buff":
                    b = new float[(int)(0.6f * Rate)];
                    Note(b, 0, 0.35f, 67, Wave.Square, 0.2f, 0.01f, 0.05f, 0.6f, 0.1f, 0, 0, 24f);
                    break;
                case "break":
                    b = new float[(int)(0.9f * Rate)];
                    {
                        uint r = 4242u;
                        for (int i = 0; i < b.Length; i++)
                        {
                            float t = i / (float)Rate;
                            b[i] = Osc(Wave.Noise, 0, ref r) * Mathf.Exp(-t * 7f) * 0.5f;
                        }
                        Note(b, 0, 0.06f, 100, Wave.Square, 0.25f, 0.001f, 0.03f, 0.5f, 0.2f);
                        Note(b, 0.05f, 0.06f, 95, Wave.Square, 0.25f, 0.001f, 0.03f, 0.5f, 0.2f);
                        Drum(b, 0, "kick", 1f);
                        Drum(b, 0, "crash", 0.6f);
                    }
                    break;
                case "boost":
                    b = new float[(int)(0.25f * Rate)];
                    Note(b, 0, 0.15f, 72, Wave.Square, 0.25f, 0.001f, 0.05f, 0.5f, 0.05f, 0, 0, 24f);
                    break;
                case "levelup":
                    b = new float[(int)(1.2f * Rate)];
                    Melody(b, 0.08f, "C5 E5 G5 C6 _ _ _ _", Wave.Square, 0.15f, 0, 0f, 0.002f, 0.3f);
                    Echo(b, 0.12f, 0.3f, false);
                    break;
                case "chest":
                    b = new float[(int)(1.0f * Rate)];
                    Melody(b, 0.07f, "G4 C5 E5 G5 C6 _ _ _", Wave.Triangle, 0.25f, 0, 0f, 0.002f, 0.3f);
                    Echo(b, 0.1f, 0.3f, false);
                    break;
                case "encounter":
                    b = new float[(int)(0.9f * Rate)];
                    for (int k = 0; k < 10; k++) Note(b, k * 0.04f, 0.05f, 60 + (k % 2 == 0 ? k * 2 : k * 2 + 7), Wave.Square, 0.18f, 0.001f, 0.02f, 0.5f, 0.05f);
                    Drum(b, 0.42f, "crash", 0.6f);
                    break;
                case "save":
                    b = new float[(int)(1.4f * Rate)];
                    Melody(b, 0.1f, "E5 G#5 B5 E6 _ _ _ _ _ _", Wave.Sine, 0.2f, 0, 0.01f, 0.01f, 0.5f);
                    Echo(b, 0.15f, 0.4f, false);
                    break;
                case "death":
                    b = new float[(int)(0.8f * Rate)];
                    Note(b, 0, 0.5f, 60, Wave.Square, 0.25f, 0.001f, 0.1f, 0.5f, 0.2f, 0, 0, -24f);
                    break;
                case "roar":
                    b = new float[(int)(1.6f * Rate)];
                    {
                        uint r = 777u; float lp = 0;
                        for (int i = 0; i < b.Length; i++)
                        {
                            float t = i / (float)Rate;
                            float n = Osc(Wave.Noise, 0, ref r);
                            lp += (n - lp) * 0.05f;
                            b[i] = lp * Mathf.Sin(Mathf.Clamp01(t / 1.6f) * Mathf.PI) * 4f;
                        }
                        Note(b, 0, 1.3f, 31, Wave.Saw, 0.4f, 0.2f, 0.4f, 0.8f, 0.3f, 0.03f);
                        LowPass(b, 0.3f);
                    }
                    break;
                case "step":
                    b = new float[(int)(0.05f * Rate)];
                    Drum(b, 0, "hat", 0.2f);
                    break;
                default:
                    b = new float[(int)(0.1f * Rate)];
                    Note(b, 0, 0.05f, 72, Wave.Sine, 0.3f);
                    break;
            }
            return Finish("sfx_" + id, b, 0.8f);
        }

        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        public static AudioClip Get(string id)
        {
            if (clips.TryGetValue(id, out var c) && c != null) return c;
            loopRender = id.StartsWith("bgm_") && id != "bgm_victory";
            switch (id)
            {
                case "bgm_castle": c = Castle(); break;
                case "bgm_battle": c = Battle(); break;
                case "bgm_boss": c = Boss(); break;
                case "bgm_ending": c = Ending(); break;
                case "bgm_victory": c = Victory(); break;
                default: c = Sfx(id); break;
            }
            loopRender = false;
            clips[id] = c;
            return c;
        }
    }

}
