using System.Collections.Generic;
using UnityEngine;

namespace TejedorDePared
{
    /// <summary>Efectos de sonido sintetizados al iniciar (no hacen falta archivos de audio).</summary>
    public class Synth : MonoBehaviour
    {
        const int SR = 44100;
        AudioSource source;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly System.Random rnd = new System.Random(7);
        public bool muted;

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            Build();
        }

        public void Play(string name)
        {
            AudioClip c;
            if (!muted && clips.TryGetValue(name, out c)) source.PlayOneShot(c, 0.9f);
        }

        // ---------- síntesis ----------
        class Buf
        {
            public float[] d;
            public Buf(float seconds) { d = new float[Mathf.CeilToInt(seconds * SR) + 1]; }
        }

        void Tone(Buf b, float f0, float f1, float dur, string type, float vol, float delay = 0f)
        {
            int start = (int)(delay * SR), n = (int)(dur * SR);
            double phase = 0;
            for (int i = 0; i < n && start + i < b.d.Length; i++)
            {
                float t = i / (float)SR, k = t / dur;
                float f = f0 * Mathf.Pow(Mathf.Max(20f, f1) / f0, k);
                phase += f / SR;
                float p = (float)(phase - System.Math.Floor(phase));
                float s;
                if (type == "square") s = p < .5f ? 1f : -1f;
                else if (type == "triangle") s = 1f - 4f * Mathf.Abs(p - .5f);
                else if (type == "sawtooth") s = 2f * p - 1f;
                else s = Mathf.Sin(p * 2f * Mathf.PI);
                float g = vol * Mathf.Pow(.001f / vol, k);
                b.d[start + i] += s * g;
            }
        }

        void Noise(Buf b, float dur, float freq, float vol, float q = 1f, float sweep = 1f, float delay = 0f)
        {
            int start = (int)(delay * SR), n = (int)(dur * SR);
            float x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < n && start + i < b.d.Length; i++)
            {
                float k = i / (float)n;
                float f = Mathf.Min(SR * .45f, freq * Mathf.Pow(sweep, k));
                float w0 = 2f * Mathf.PI * f / SR, alpha = Mathf.Sin(w0) / (2f * q), cs = Mathf.Cos(w0);
                float a0 = 1 + alpha, b0 = alpha / a0, b2 = -alpha / a0, a1 = -2 * cs / a0, a2 = (1 - alpha) / a0;
                float x = (float)(rnd.NextDouble() * 2 - 1);
                float y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                b.d[start + i] += y * vol * Mathf.Pow(.001f / vol, k) * 2.5f;
            }
        }

        void Make(string name, float seconds, System.Action<Buf> fill)
        {
            var b = new Buf(seconds + .03f);
            fill(b);
            for (int i = 0; i < b.d.Length; i++) b.d[i] = Mathf.Clamp(b.d[i], -1f, 1f);
            var clip = AudioClip.Create(name, b.d.Length, 1, SR, false);
            clip.SetData(b.d, 0);
            clips[name] = clip;
        }

        void Build()
        {
            Make("jump", .12f, b => Tone(b, 320, 640, .12f, "square", .12f));
            Make("land", .07f, b => Noise(b, .07f, 700, .18f, 1.2f));
            Make("landHard", .14f, b => Noise(b, .14f, 400, .35f, 1.2f));
            Make("web", .2f, b => { Noise(b, .16f, 5000, .3f, 2, .4f); Tone(b, 1800, 900, .08f, "triangle", .06f); Noise(b, .06f, 2600, .25f, 3, 1, .06f); });
            Make("release", .08f, b => Tone(b, 500, 900, .08f, "triangle", .1f));
            Make("shot", .09f, b => Noise(b, .09f, 3500, .22f, 2, .5f));
            Make("hit", .08f, b => Tone(b, 900, 300, .08f, "square", .12f));
            Make("pop", .18f, b => { Tone(b, 600, 1400, .1f, "square", .12f); Tone(b, 900, 1800, .12f, "triangle", .1f, .06f); });
            Make("hurt", .25f, b => { Tone(b, 220, 90, .25f, "sawtooth", .2f); Noise(b, .15f, 300, .2f); });
            Make("token", .25f, b => { float[] f = { 880, 1109, 1319, 1760 }; for (int i = 0; i < 4; i++) Tone(b, f[i], f[i], .09f, "square", .08f, i * .05f); });
            Make("roll", .2f, b => Noise(b, .2f, 500, .18f, .8f, .5f));
            Make("win", .75f, b => { float[] f = { 523, 659, 784, 1047, 784, 1047 }; for (int i = 0; i < f.Length; i++) Tone(b, f[i], f[i], .16f, "square", .1f, i * .11f); });
            Make("ui", .04f, b => Tone(b, 1200, 1200, .04f, "square", .06f));
            Make("bolt", .15f, b => Tone(b, 700, 200, .15f, "sawtooth", .07f));
            Make("boom", .4f, b => { Noise(b, .4f, 220, .45f, .7f, .4f); Tone(b, 120, 40, .3f, "sawtooth", .15f); });
            Make("drop", .25f, b => Tone(b, 1200, 400, .25f, "triangle", .06f));
            Make("appear", .08f, b => Tone(b, 1500, 2200, .08f, "triangle", .05f));
        }
    }
}
