// Procedural "old rusty iron door" sounds for ROOM 13.
// Pure C# (no Unity dependency): returns mono float samples at 32000 Hz, or null for unknown clip names.
using System;

namespace Room13Mod
{
    public static class IronDoor
    {
        public const int SR = 32000;

        public static float[] Generate(string name)
        {
            switch (name)
            {
                case "door_creak_slow": return Finish(Creak(3.4f, 1.0f, 11), 3.4f);
                case "door_open": return Finish(Open(), 2.4f);
                case "door_close": return Finish(Close(), 1.5f);
                case "door_slam": return Finish(Slam(), 2.0f);
                case "locked": return Finish(Locked(), 1.0f);
                case "slide_scrape": return Finish(SlideScrape(), 0.9f);
            }
            return null;
        }

        // ---------------------------------------------------------------- helpers
        sealed class Rng
        {
            uint s;
            public Rng(uint seed) { s = seed * 2654435761u + 1013904223u; if (s == 0) s = 1; }
            public float Next()            // 0..1
            {
                s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                return (s & 0xFFFFFF) / (float)0x1000000;
            }
            public float Signed() { return Next() * 2f - 1f; }
            public float Gauss() { return (Next() + Next() + Next() + Next() - 2f) * 1.7f; }
        }

        // smooth random contour in [-1,1], one value per sample
        static float[] Contour(Rng r, int n, float stepSec)
        {
            int step = Math.Max(1, (int)(stepSec * SR));
            int pts = n / step + 3;
            float[] p = new float[pts];
            for (int i = 0; i < pts; i++) p[i] = r.Signed();
            float[] c = new float[n];
            for (int i = 0; i < n; i++)
            {
                int k = i / step; float f = (i % step) / (float)step;
                f = (1f - (float)Math.Cos(f * Math.PI)) * 0.5f;
                c[i] = p[k] * (1f - f) + p[k + 1] * f;
            }
            return c;
        }

        static float Formant(float f)
        {
            float a = (f - 950f) / 380f, b = (f - 2400f) / 700f;
            return (float)(Math.Exp(-a * a) + 0.6 * Math.Exp(-b * b));
        }

        // Rusty hinge: stick-slip pulsed harmonic squeal + thin high whine + a little grit.
        static float[] Creak(float dur, float intensity, uint seed)
        {
            Rng r = new Rng(seed);
            int n = (int)(dur * SR);
            float[] o = new float[n];
            float[] pc = Contour(r, n, 0.14f), rc = Contour(r, n, 0.2f), ac = Contour(r, n, 0.3f), wc = Contour(r, n, 0.1f);
            double ph = 0, slip = 0, ph2 = 0;
            float f0b = 150f + (seed % 5) * 14f;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR, u = t / dur;
                float env = Smooth(t / 0.3f) * Smooth((dur - t) / 0.6f);
                env *= 0.55f + 0.45f * (0.5f + 0.5f * ac[i]);
                float f0 = f0b * (1f + 0.5f * pc[i] + 0.3f * u);
                slip += (20f + 14f * rc[i]) / SR;
                float g = (float)(slip - Math.Floor(slip));
                float gate = g < 0.72f ? (float)Math.Sin(Math.PI * g / 0.72f) : 0f;
                gate = (float)Math.Pow(gate, 0.75);
                ph += f0 / SR;
                float s = 0f;
                for (int h = 1; h <= 11; h++)
                {
                    float fh = h * f0;
                    if (fh > 7000f) break;
                    float a = (float)(1.0 / Math.Pow(h, 0.85)) * (0.35f + 2.2f * Formant(fh));
                    s += a * (float)Math.Sin(2 * Math.PI * h * (ph - Math.Floor(ph)));
                }
                s *= 0.16f * gate;
                float fw = 1050f * (1f + 0.35f * wc[i] + 0.2f * u);
                ph2 += fw / SR;
                float whine = (float)Math.Sin(2 * Math.PI * (ph2 - Math.Floor(ph2)))
                              * (float)Math.Pow(Math.Max(0f, wc[i] * 0.5f + 0.5f * ac[i] * 0.5f + 0.15f), 2.0) * 0.22f * (gate * 0.6f + 0.4f);
                float grit = r.Gauss() * 0.035f * (0.3f + gate);
                float x = (s + whine + grit) * env * intensity;
                lp += 0.55f * (x - lp);          // soften the top end a little
                o[i] = lp;
            }
            return o;
        }

        static float Smooth(float x) { if (x <= 0f) return 0f; if (x >= 1f) return 1f; return x * x * (3f - 2f * x); }

        // Metal impact (latch / iron slab). baseHz sets the size of the object, decay its ring time.
        static void Clang(float[] o, float at, float strength, float baseHz, float decay, uint seed)
        {
            Rng r = new Rng(seed);
            int s0 = (int)(at * SR);
            float[] ratio = { 1f, 2.32f, 3.87f, 5.61f, 7.9f, 10.4f };
            float[] amp = { 1f, 0.8f, 0.6f, 0.45f, 0.3f, 0.2f };
            float[] tau = { 0.55f, 0.4f, 0.3f, 0.22f, 0.15f, 0.1f };
            int len = (int)(decay * 1.6f * SR);
            for (int k = 0; k < ratio.Length; k++)
            {
                double f1 = baseHz * ratio[k], f2 = f1 * 1.0045;
                float tk = tau[k] * decay;
                for (int i = 0; i < len; i++)
                {
                    int idx = s0 + i; if (idx >= o.Length) break;
                    float t = i / (float)SR;
                    float e = (float)Math.Exp(-t / tk);
                    if (e < 0.001f) break;
                    o[idx] += strength * 0.22f * amp[k] * e *
                              ((float)Math.Sin(2 * Math.PI * f1 * t) + 0.7f * (float)Math.Sin(2 * Math.PI * f2 * t + k));
                }
            }
            // initial noise transient (a few ms) + low thump
            int tl = (int)(0.012f * SR); float prev = 0f;
            for (int i = 0; i < tl && s0 + i < o.Length; i++)
            {
                float n = r.Gauss(); float hp = n - prev; prev = n;
                o[s0 + i] += strength * 0.55f * hp * (float)Math.Exp(-i / (0.0035f * SR));
            }
            if (baseHz < 400f)
            {
                int th = (int)(0.35f * SR);
                for (int i = 0; i < th && s0 + i < o.Length; i++)
                {
                    float t = i / (float)SR;
                    o[s0 + i] += strength * 0.55f * (float)(Math.Sin(2 * Math.PI * (48f + 40f * Math.Exp(-t * 18)) * t) * Math.Exp(-t / 0.11));
                }
            }
        }

        static void Mix(float[] dst, float[] src, float at, float gain)
        {
            int s0 = (int)(at * SR);
            for (int i = 0; i < src.Length && s0 + i < dst.Length; i++) dst[s0 + i] += src[i] * gain;
        }

        static void Echo(float[] o, float delay, float fb, float mix)
        {
            int d = (int)(delay * SR);
            float[] c = (float[])o.Clone();
            for (int i = d; i < o.Length; i++) { c[i] += c[i - d] * fb; }
            for (int i = 0; i < o.Length; i++) o[i] += c[i] * mix;
        }

        // ---------------------------------------------------------------- the sounds
        static float[] Open()
        {
            float[] o = new float[(int)(2.4f * SR)];
            Clang(o, 0.00f, 0.55f, 1350f, 0.16f, 3);     // latch lifts
            Clang(o, 0.07f, 0.40f, 1500f, 0.12f, 4);
            Mix(o, Creak(1.95f, 1.0f, 21), 0.14f, 2.4f);   // long rusty hinge squeal
            Clang(o, 2.02f, 0.30f, 210f, 0.45f, 5);      // door bumps its stop
            Echo(o, 0.061f, 0.30f, 0.22f);
            return o;
        }

        static float[] Close()
        {
            float[] o = new float[(int)(1.5f * SR)];
            Mix(o, Creak(0.75f, 0.85f, 31), 0.0f, 2.4f);
            Clang(o, 0.74f, 1.00f, 170f, 0.90f, 6);      // heavy iron meets frame
            Clang(o, 0.79f, 0.45f, 1400f, 0.13f, 7);     // latch drops
            Echo(o, 0.053f, 0.32f, 0.25f);
            return o;
        }

        static float[] Slam()
        {
            float[] o = new float[(int)(2.0f * SR)];
            Clang(o, 0.00f, 1.50f, 135f, 1.50f, 8);
            Clang(o, 0.00f, 0.50f, 640f, 0.60f, 9);
            Clang(o, 0.15f, 0.55f, 150f, 0.60f, 10);     // rebound
            Clang(o, 0.27f, 0.28f, 160f, 0.40f, 11);
            Clang(o, 0.05f, 0.40f, 1400f, 0.12f, 12);
            Echo(o, 0.047f, 0.38f, 0.30f);
            Echo(o, 0.083f, 0.30f, 0.20f);
            return o;
        }

        static float[] Locked()
        {
            float[] o = new float[(int)(1.0f * SR)];
            Clang(o, 0.00f, 0.80f, 900f, 0.22f, 13);     // handle rattles against a locked iron door
            Clang(o, 0.11f, 0.65f, 940f, 0.20f, 14);
            Clang(o, 0.20f, 0.45f, 880f, 0.18f, 15);
            Clang(o, 0.05f, 0.25f, 2400f, 0.10f, 16);
            Clang(o, 0.00f, 0.35f, 190f, 0.25f, 17);
            Echo(o, 0.041f, 0.28f, 0.18f);
            return o;
        }

        // body sliding over a concrete floor: a bandpass-swept noise swish with a gritty scrape and a soft thud
        static float[] SlideScrape()
        {
            Rng r = new Rng(41);
            int n = (int)(0.9f * SR);
            float[] o = new float[n];
            float lpA = 0f, lpB = 0f, grit = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float x = r.Gauss();
                float a = 0.30f - 0.20f * Smooth(t / 0.8f);      // sweeps the band downward
                lpA += a * (x - lpA);
                lpB += (a * 0.35f) * (x - lpB);
                float band = lpA - lpB;
                grit += 0.5f * (r.Gauss() - grit);
                float am = 0.75f + 0.25f * (float)Math.Sin(2 * Math.PI * (26f + 8f * Math.Sin(t * 5f)) * t);
                float env = Smooth(t / 0.03f) * (float)Math.Exp(-t / 0.42f);
                o[i] = (band * 3.2f + grit * 0.22f) * am * env;
                o[i] += (float)(Math.Sin(2 * Math.PI * (70f - 20f * t) * t) * Math.Exp(-t / 0.09)) * 0.35f;
            }
            return o;
        }

        static float[] Finish(float[] o, float dur)
        {
            int n = o.Length;
            float peak = 0f;
            for (int i = 0; i < n; i++)
            {
                if (float.IsNaN(o[i]) || float.IsInfinity(o[i])) o[i] = 0f;
                o[i] = (float)Math.Tanh(1.6 * o[i]);          // soft limiter keeps impacts from swallowing the creak
                float a = Math.Abs(o[i]); if (a > peak) peak = a;
            }
            float g = peak > 0.0001f ? 0.9f / peak : 1f;
            int fade = (int)(0.03f * SR);
            for (int i = 0; i < n; i++)
            {
                o[i] *= g;
                if (i > n - fade) o[i] *= (n - i) / (float)fade;
            }
            return o;
        }
    }
}


