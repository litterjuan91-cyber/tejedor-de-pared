using System.Collections.Generic;
using UnityEngine;

namespace TejedorDePared
{
    /// <summary>Buffer de píxeles con origen arriba-izquierda (como un canvas) y recorte opcional.</summary>
    public sealed class PixBuf
    {
        public readonly int w, h;
        public readonly Color32[] px;
        int cx0, cy0, cx1, cy1;

        public PixBuf(int w, int h)
        {
            this.w = Mathf.Max(1, w); this.h = Mathf.Max(1, h);
            px = new Color32[this.w * this.h];
            NoClip();
        }

        public void Clip(int x, int y, int cw, int ch) { cx0 = x; cy0 = y; cx1 = x + cw; cy1 = y + ch; }
        public void NoClip() { cx0 = 0; cy0 = 0; cx1 = w; cy1 = h; }

        public void Set(int x, int y, Color32 c)
        {
            if (x < cx0 || y < cy0 || x >= cx1 || y >= cy1 || x < 0 || y < 0 || x >= w || y >= h) return;
            px[(h - 1 - y) * w + x] = c;
        }

        public void Fill(int x, int y, int fw, int fh, Color32 c)
        {
            for (int yy = y; yy < y + fh; yy++)
                for (int xx = x; xx < x + fw; xx++) Set(xx, yy, c);
        }

        public Sprite ToSprite(Vector2 pivot)
        {
            var t = PixelArt.NewTex(w, h);
            t.SetPixels32(px);
            t.Apply(false, false);
            return Sprite.Create(t, new Rect(0, 0, w, h), pivot, PixelArt.PPU, 0, SpriteMeshType.FullRect);
        }
    }

    /// <summary>Generador determinístico (igual al de la versión web) para que los cuadros se vean siempre iguales.</summary>
    public sealed class Rng
    {
        uint seed;
        public Rng(uint seed) { this.seed = seed; }
        public float Next() { unchecked { seed = seed * 1664525u + 1013904223u; } return (float)(seed / 4294967296.0); }
    }

    /// <summary>Todos los gráficos del juego, generados por código a partir de mapas de caracteres.</summary>
    public static class PixelArt
    {
        /// <summary>1 píxel de sprite = 3 píxeles de una salida de 1920×1080 = 0,03 unidades de mundo.</summary>
        public const float PPU = 100f / 3f;

        static readonly Dictionary<char, Color32> Pal = new Dictionary<char, Color32>
        {
            { 'P', C("#8a63ff") }, { 'D', C("#4b2fb0") }, { 'V', C("#3ff5e0") }, { 'W', C("#ffffff") }, { 'O', C("#ff8c1a") },
            { 'Y', C("#ffd23f") }, { 'y', C("#c99a12") }, { 'w', C("#fff6c4") }, { 'G', C("#36d06b") }, { 'g', C("#1f8a43") },
            { 'K', C("#111111") }, { 'R', C("#ff3b3b") }, { 'M', C("#6f7f8f") }, { 'm', C("#4a5866") }, { 'C', C("#f2a541") },
            { 'A', C("#b8c4cf") }, { 'S', C("#7fd6ff") }, { 'B', C("#2e86c1") }, { 'L', C("#e8f4ff") }, { 'l', C("#6a7a8a") },
            { 'H', C("#ff7eb6") }, { 'h', C("#ffd1e6") }, { 'E', C("#55658f") }, { 'e', C("#7a8ab8") }, { 'F', C("#ff8c1a") },
            { 'f', C("#ffe066") }, { 'o', C("#b85c00") }, { 'T', C("#ff9f1c") }, { 't', C("#7a4a00") }, { 'n', C("#6b6b6b") },
            { 'N', C("#bdbdbd") }
        };

        static readonly string[] Head = { "....PPPP....", "...PPPPPP...", "..PPPPPPPP..", "..PPPPVVVVV.", "..PPPPVWVWV.", "..PPPPPPPP..", "...PPPPPP...", "..OOOOOOOO.." };
        static readonly string[] Body = { ".PPPPPPPPPP.", ".PPPPYYPPPP.", ".PPPYYYYPPP.", ".PP.PYYP.PP.", ".PP.PPPP.PP." };
        static readonly string[] BodyUp = { ".PPPPPPPPPPP", ".PPPPYYPPP..", ".PPPYYYYPP..", ".PP.PYYPP...", "....PPPP...." };

        static string[] Cat(params string[][] parts)
        {
            var l = new List<string>();
            foreach (var p in parts) l.AddRange(p);
            return l.ToArray();
        }

        static readonly Dictionary<string, string[]> Src = new Dictionary<string, string[]>
        {
            // Héroe original "Tejedor": visor turquesa y bufanda naranja
            { "idle", Cat(Head, Body, new[] { "....DD.DD...", "....DD.DD...", "....DD.DD...", "....DD.DD...", "...DDD.DDD.." }) },
            { "run1", Cat(Head, Body, new[] { "....DDDD....", "...DD..DD...", "..DD....DD..", ".DD......DD.", ".D........DD" }) },
            { "run2", Cat(Head, Body, new[] { "....DDD.....", "....DDD.....", "....DD......", "...DDD......", "...DD......." }) },
            { "jump", Cat(Head, BodyUp, new[] { "...DDDDDD...", "...DD..DD...", "..DD....DD..", "............", "............" }) },
            { "ball", new[] { "....PPPP....", "..PPPPPPPP..", ".PPPPPPPPPP.", ".PPPVVPPPPP.", "PPPPVWPPPPPP", "OOOOOOOOOOOO", "PPPPPPPPPPPP", "PPPPYYPPPPPP", ".PPPPPPPPPP.", ".PPPPPPPPPP.", "..PPPPPPPP..", "....PPPP...." } },
            // Robot cangrejo que persigue por el piso
            { "chaser1", new[] { "......AAAA......", ".....AAAAAA.....", "....AARRAAAA....", "....AAAAAAAA....", ".CC.MMMMMMMM.CC.", "C..CMMMMMMMMC..C", "C...MMMmmMMM...C", ".CC.MMMMMMMM.CC.", ".....m....m.....", "....mm....mm....", "...mm......mm...", "..mm........mm.." } },
            { "chaser2", new[] { "......AAAA......", ".....AAAAAA.....", "....AARRAAAA....", "....AAAAAAAA....", "CC..MMMMMMMM..CC", "..C.MMMMMMMM.C..", "...CMMMmmMMMC...", "..C.MMMMMMMM.C..", "....m......m....", "...mm......mm...", "...m........m...", "..mm........mm.." } },
            // Dron que tira bombas
            { "bomber1", new[] { "..LLLL......LLLL..", "....ll......ll....", ".....SSSSSSSS.....", "...SSBBBBBBBBSS...", "..SSBBBRRRRBBBSS..", "..SSBBBBBBBBBBSS..", "...SSSSSSSSSSSS...", ".....l......l.....", "....ll......ll...." } },
            { "bomber2", new[] { ".LLLLLL....LLLLLL.", "....ll......ll....", ".....SSSSSSSS.....", "...SSBBBBBBBBSS...", "..SSBBBRRRRBBBSS..", "..SSBBBBBBBBBBSS..", "...SSSSSSSSSSSS...", ".....l......l.....", "....ll......ll...." } },
            { "patrol1", new[] { "....GGGGGG....", "..GGGGGGGGGG..", ".GGGGGGGGGWKG.", "GGGGGGGGGGWWGG", "GGGGGGGGGGGGGG", ".gggggggggggg.", ".g.g.g.g.g.g..", ".............." } },
            { "patrol2", new[] { "....GGGGGG....", "..GGGGGGGGGG..", ".GGGGGGGGGWKG.", "GGGGGGGGGGWWGG", "GGGGGGGGGGGGGG", ".gggggggggggg.", "..g.g.g.g.g.g.", ".............." } },
            { "flyer", new[] { "..hhh....hhh..", ".hhhhh..hhhhh.", "..HHHHHHHHHH..", ".HHHHHHHHWKHH.", ".HHHHHHHHWWHH.", "..HHHHHHHHHH..", "...HHHHHHHH..." } },
            { "shooter", new[] { "..TTTTTTTT..", ".TTTTTTTTTT.", "TTTWWTTTTTTT", "TTTWRTTTTttt", "TTTTTTTTTttt", "TTTTTTTTTTTT", ".TTTTTTTTTT.", "..tttttttt..", "..t......t..", ".tt......tt.", ".t........t.", "tt........tt" } },
            { "coin", new[] { "...YYY...", ".YYYYYYY.", ".YwYYYYY.", "YwYYyYYYY", "YYYyYyYYY", "YYYYyYYYY", ".YYYYYYy.", ".YYYYYyy.", "...yyy..." } },
            { "heart", new[] { ".HH...HH.", "HHHH.HHHH", "HhHHHHHHH", "HHHHHHHHH", ".HHHHHHH.", "..HHHHH..", "...HHH...", "....H...." } },
            { "heart0", new[] { ".EE...EE.", "EEEE.EEEE", "EeEEEEEEE", "EEEEEEEEE", ".EEEEEEE.", "..EEEEE..", "...EEE...", "....E...." } },
            { "bomb", new[] { "..ff..", "..oo..", ".FFFF.", "FFwFFF", "FFFFFF", "FFFFFF", ".FFFF." } },
            { "hook", new[] { "Nnn", "nnn", "nnn" } },
            { "cross", new[] { ".W.", "WWW", ".W." } }
        };

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Sprite whiteTL;

        /// <summary>Limpia la caché al entrar en Play (necesario si "Reload Domain" está desactivado).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            cache.Clear();
            whiteTL = null;
        }

        public static Color32 C(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }

        public static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        /// <summary>Sprite por nombre. El pivote cae en el mismo píxel que usa la versión web (floor(w/2), floor(h/2)).</summary>
        public static Sprite Get(string name)
        {
            Sprite s;
            // Unity 6 puede no recargar el código entre un Play y otro: los sprites guardados
            // de la sesión anterior ya fueron destruidos, así que se verifica y se regeneran.
            if (cache.TryGetValue(name, out s) && s != null && s.texture != null) return s;
            string[] rows;
            if (!Src.TryGetValue(name, out rows)) rows = Src["coin"];
            int w = 0;
            foreach (var r in rows) w = Mathf.Max(w, r.Length);
            int h = rows.Length;
            var b = new PixBuf(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    Color32 c;
                    if (Pal.TryGetValue(rows[y][x], out c)) b.Set(x, y, c);
                }
            s = b.ToSprite(new Vector2((w / 2) / (float)w, (h - h / 2) / (float)h));
            s.name = name;
            cache[name] = s;
            return s;
        }

        /// <summary>Sprite blanco de 1 píxel con pivote arriba-izquierda: sirve para dibujar rectángulos escalándolo.</summary>
        public static Sprite WhiteTL
        {
            get
            {
                if (whiteTL == null || whiteTL.texture == null)
                {
                    var b = new PixBuf(1, 1);
                    b.Set(0, 0, new Color32(255, 255, 255, 255));
                    whiteTL = b.ToSprite(new Vector2(0f, 1f));
                }
                return whiteTL;
            }
        }

        public static uint Hash(string str)
        {
            uint h = 2166136261u;
            unchecked
            {
                foreach (char c in str ?? "") { h ^= c; h *= 16777619u; }
            }
            return h;
        }

        public static bool IsFrame(string kind) { return kind == "Building" || kind == "NightWindow" || kind == "PictureFrame"; }

        /// <summary>
        /// Dibuja la decoración de prueba de una estructura (w×h en píxeles bajos).
        /// En una pared real se ocultan y solo quedan los colisionadores.
        /// </summary>
        public static Sprite Prop(SurfaceData s, int w, int h)
        {
            w = Mathf.Max(2, w); h = Mathf.Max(2, h);
            var b = new PixBuf(w, h);
            var r = new Rng(Hash(s.id));
            string k = s.kind;
            const int bd = 3;

            if (IsFrame(k))
            {
                b.Fill(0, 0, w, h, C("#1c1c1c"));
                b.Fill(1, 1, w - 2, h - 2, C("#2c2c2c"));
                int ix = bd, iy = bd, iw = w - 2 * bd, ih = h - 2 * bd;
                if (iw >= 4 && ih >= 4)
                {
                    b.Clip(ix, iy, iw, ih);
                    if (k == "Building") Building(b, r, ix, iy, iw, ih);
                    else if (k == "NightWindow") NightWindow(b, r, ix, iy, iw, ih);
                    else Painting(b, r, s.id, ix, iy, iw, ih);
                    b.NoClip();
                }
            }
            else if (k == "Shelf")
            {
                b.Fill(0, 0, w, h, C("#7a4a26"));
                b.Fill(0, 0, w, 1, C("#a8693a"));
            }
            else if (k == "Clock")
            {
                float cx = w / 2f, cy = h / 2f, rad = Mathf.Min(w, h) / 2f;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float d = Mathf.Sqrt((x + .5f - cx) * (x + .5f - cx) + (y + .5f - cy) * (y + .5f - cy));
                        if (d <= rad) b.Set(x, y, d > rad - 2 ? C("#444444") : C("#f4f1de"));
                    }
                b.Fill(Mathf.RoundToInt(cx), Mathf.RoundToInt(cy - rad * .6f), 1, Mathf.RoundToInt(rad * .6f), C("#222222"));
                b.Fill(Mathf.RoundToInt(cx), Mathf.RoundToInt(cy), Mathf.RoundToInt(rad * .45f), 1, C("#222222"));
            }
            else if (k == "Cabinet")
            {
                b.Fill(0, 0, w, h, C("#5c3d2e"));
                b.Fill(2, 2, w - 4, h / 2 - 3, C("#6e4a37"));
                b.Fill(2, h / 2 + 1, w - 4, h / 2 - 3, C("#6e4a37"));
            }
            else if (k == "Ground")
            {
                b.Fill(0, 0, w, 1, C("#4a4a4a"));
            }
            else
            {
                b.Fill(0, 0, w, h, C("#444444"));
            }
            return b.ToSprite(new Vector2(0f, 1f));
        }

        static void Building(PixBuf b, Rng r, int ix, int iy, int iw, int ih)
        {
            Color32 brick = C("#2f8f73"), mortar = C("#257360");
            b.Fill(ix, iy, iw, ih, brick);
            for (int yy = 0; yy < ih; yy += 3)
            {
                b.Fill(ix, iy + yy, iw, 1, mortar);
                for (int xx = ((yy / 3) % 2) * 3; xx < iw; xx += 6) b.Fill(ix + xx, iy + yy, 1, 3, mortar);
            }
            int cols = 2, rows = Mathf.Max(2, Mathf.RoundToInt(ih / 18f)), gx = Mathf.Max(2, Mathf.RoundToInt(iw * .14f)), gy = 3;
            int ww = Mathf.FloorToInt((iw - gx * (cols + 1)) / (float)cols);
            int wh = Mathf.FloorToInt((ih - gy * (rows + 1)) / (float)rows * .78f);
            if (ww < 2 || wh < 2) return;
            for (int c = 0; c < cols; c++)
                for (int ri = 0; ri < rows; ri++)
                {
                    int wx = ix + gx + c * (ww + gx), wy = iy + gy + ri * Mathf.FloorToInt((ih - gy) / (float)rows);
                    bool lit = r.Next() < .7f;
                    b.Fill(wx - 1, wy - 1, ww + 2, wh + 2, C("#9fd8ef"));
                    b.Fill(wx, wy, ww, wh, lit ? C("#ffcf5a") : C("#2b4f7a"));
                    if (lit)
                    {
                        b.Fill(wx, wy, ww, 1, C("#ffe9a8"));
                        b.Fill(wx + ww / 2, wy, 1, wh, C("#c9861e"));
                        b.Fill(wx, wy + wh / 2, ww, 1, C("#c9861e"));
                    }
                    else
                    {
                        b.Fill(wx + 1, wy + 1, 2, 1, C("#6d9cc9"));
                        b.Fill(wx + ww / 2, wy, 1, wh, C("#1d3a5c"));
                    }
                    b.Fill(wx - 1, wy + wh + 1, ww + 2, 1, C("#c7ecfa"));
                }
        }

        static void NightWindow(PixBuf b, Rng r, int ix, int iy, int iw, int ih)
        {
            for (int yy = 0; yy < ih; yy++)
            {
                float t = yy / (float)ih;
                b.Fill(ix, iy + yy, iw, 1, new Color32((byte)(27 + t * 30), (byte)(42 + t * 50), (byte)(138 + t * 70), 255));
            }
            int stars = iw * ih / 40;
            for (int i = 0; i < stars; i++)
            {
                int sx = ix + Mathf.FloorToInt(r.Next() * iw), sy = iy + Mathf.FloorToInt(r.Next() * ih * .8f);
                b.Set(sx, sy, r.Next() < .3f ? C("#ffffff") : C("#9fb6ff"));
            }
            int mx = ix + Mathf.FloorToInt(iw * .62f), my = iy + Mathf.FloorToInt(ih * .28f);
            int mr = Mathf.Max(3, Mathf.FloorToInt(Mathf.Min(iw, ih) * .13f));
            for (int dy = -mr; dy <= mr; dy++)
                for (int dx = -mr; dx <= mr; dx++)
                {
                    float ox = dx - mr * .55f, oy = dy + mr * .3f;
                    if (dx * dx + dy * dy <= mr * mr && ox * ox + oy * oy > mr * mr * .75f) b.Set(mx + dx, my + dy, C("#f5f0c8"));
                }
            b.Fill(ix + iw / 2, iy, 1, ih, C("#c9d4ff"));
            b.Fill(ix, iy + Mathf.FloorToInt(ih * .45f), iw, 1, C("#c9d4ff"));
            int cw = Mathf.Max(3, Mathf.FloorToInt(iw * .22f));
            for (int yy = 0; yy < ih; yy++)
            {
                float t = yy / (float)ih;
                int ext = Mathf.FloorToInt(cw * (1 - .55f * Mathf.Sin(t * Mathf.PI * .8f)));
                b.Fill(ix, iy + yy, ext, 1, C("#b9a7f0"));
                b.Fill(ix + iw - ext, iy + yy, ext, 1, C("#b9a7f0"));
                b.Set(ix + ext / 2, iy + yy, C("#8e7bd6"));
                b.Set(ix + iw - Mathf.CeilToInt(ext / 2f), iy + yy, C("#8e7bd6"));
            }
            b.Fill(ix, iy, iw, 2, C("#8e7bd6"));
        }

        static void Painting(PixBuf b, Rng r, string id, int ix, int iy, int iw, int ih)
        {
            string[][] pals = { new[] { "#1b3b6f", "#21d4fd", "#ff5d73", "#ffd23f" }, new[] { "#2b193d", "#c2efb3", "#ff8c42", "#6c4ab6" } };
            var pal = pals[Hash(id) % 2];
            b.Fill(ix, iy, iw, ih, C(pal[0]));
            for (int i = 0; i < 6; i++)
            {
                int x = ix + Mathf.FloorToInt(r.Next() * iw * .8f), y = iy + Mathf.FloorToInt(r.Next() * ih * .8f);
                int fw = Mathf.CeilToInt(iw * (.15f + r.Next() * .3f)), fh = Mathf.CeilToInt(ih * (.1f + r.Next() * .25f));
                b.Fill(x, y, fw, fh, C(pal[1 + i % 3]));
            }
        }
    }
}
