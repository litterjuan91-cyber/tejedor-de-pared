using System.Collections.Generic;
using UnityEngine;

namespace TejedorDePared
{
    /// <summary>
    /// Render: una cámara ortográfica dibuja el mundo en una RenderTexture de 640×360 con filtro Point
    /// (aspecto pixel art). Una segunda cámara muestra esa textura sobre una malla deformable:
    /// así funciona el corner-pin para corregir el trapecio del proyector sin tocar la física.
    /// </summary>
    public partial class Game
    {
        const int LowW = 640, LowH = 360, DisplayLayer = 31, MeshGrid = 16;

        Camera worldCam, displayCam;
        RenderTexture rt;
        Mesh displayMesh;
        Material spriteMat;
        Transform worldRoot, propsRoot;
        readonly List<SpriteRenderer> propRenderers = new List<SpriteRenderer>();
        Pool poolHooks, poolTokens, poolEnemies, poolBombs, poolScarf, poolPlayer, poolFx, poolHud;
        int lastScreenW, lastScreenH;
        bool displayDirty = true;

        /// <summary>Reutiliza SpriteRenderers frame a frame.</summary>
        class Pool
        {
            readonly List<SpriteRenderer> items = new List<SpriteRenderer>();
            readonly Transform parent; readonly Material mat; readonly int order;
            int used;
            public Pool(Transform parent, Material mat, int order, string name)
            {
                var go = new GameObject(name); go.transform.SetParent(parent, false);
                this.parent = go.transform; this.mat = mat; this.order = order;
            }
            public void Begin() { used = 0; }
            public SpriteRenderer Next()
            {
                if (used == items.Count)
                {
                    var go = new GameObject("px"); go.transform.SetParent(parent, false);
                    var sr = go.AddComponent<SpriteRenderer>(); sr.sharedMaterial = mat; sr.sortingOrder = order;
                    items.Add(sr);
                }
                var r = items[used++];
                r.enabled = true; r.flipX = false; r.color = Color.white;
                r.transform.localRotation = Quaternion.identity; r.transform.localScale = Vector3.one;
                return r;
            }
            public void End() { for (int i = used; i < items.Count; i++) items[i].enabled = false; }
        }

        void SetupRendering()
        {
            spriteMat = new Material(Shader.Find("Sprites/Default"));

            // Apaga otras cámaras de la escena (por ejemplo la "Main Camera" por defecto)
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
            if (FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 0) gameObject.AddComponent<AudioListener>();

            rt = new RenderTexture(LowW, LowH, 16, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Point; rt.wrapMode = TextureWrapMode.Clamp; rt.name = "Mundo 640x360";
            rt.Create();

            var wc = new GameObject("Cámara del mundo");
            wc.transform.SetParent(transform, false);
            wc.transform.position = new Vector3(0, 0, -10);
            worldCam = wc.AddComponent<Camera>();
            worldCam.orthographic = true; worldCam.orthographicSize = 5.4f;
            worldCam.clearFlags = CameraClearFlags.SolidColor; worldCam.backgroundColor = Color.black;
            worldCam.cullingMask = ~(1 << DisplayLayer);
            worldCam.nearClipPlane = .1f; worldCam.farClipPlane = 100f;
            worldCam.targetTexture = rt; worldCam.depth = -10;
            worldCam.allowHDR = false; worldCam.allowMSAA = false;

            worldRoot = new GameObject("Mundo").transform; worldRoot.SetParent(transform, false);
            propsRoot = new GameObject("Estructuras").transform; propsRoot.SetParent(worldRoot, false);
            poolHooks = new Pool(worldRoot, spriteMat, 1, "Ganchos");
            poolTokens = new Pool(worldRoot, spriteMat, 3, "Fichas");
            poolEnemies = new Pool(worldRoot, spriteMat, 4, "Enemigos");
            poolBombs = new Pool(worldRoot, spriteMat, 5, "Bombas");
            poolScarf = new Pool(worldRoot, spriteMat, 6, "Bufanda");
            poolPlayer = new Pool(worldRoot, spriteMat, 7, "Jugador");
            poolFx = new Pool(worldRoot, spriteMat, 8, "Efectos");
            poolHud = new Pool(worldRoot, spriteMat, 10, "HUD");

            var dc = new GameObject("Cámara de salida");
            dc.layer = DisplayLayer; dc.transform.SetParent(transform, false);
            displayCam = dc.AddComponent<Camera>();
            displayCam.orthographic = true;
            displayCam.clearFlags = CameraClearFlags.SolidColor; displayCam.backgroundColor = Color.black;
            displayCam.cullingMask = 1 << DisplayLayer;
            displayCam.nearClipPlane = .1f; displayCam.farClipPlane = 100f; displayCam.depth = 0;
            displayCam.allowHDR = false; displayCam.allowMSAA = false;

            var dq = new GameObject("Salida proyectada");
            dq.layer = DisplayLayer; dq.transform.SetParent(transform, false);
            dq.transform.position = new Vector3(0, 0, 0);
            displayMesh = new Mesh { name = "Corner-pin" };
            displayMesh.MarkDynamic();
            dq.AddComponent<MeshFilter>().sharedMesh = displayMesh;
            var mr = dq.AddComponent<MeshRenderer>();
            var m = new Material(spriteMat); m.mainTexture = rt;
            mr.sharedMaterial = m;
            UpdateDisplay();
        }

        void OnDestroy()
        {
            if (rt != null) rt.Release();
        }

        /// <summary>Recalcula la malla de salida con la homografía del corner-pin (en píxeles de pantalla).</summary>
        void UpdateDisplay()
        {
            int sw = Screen.width, sh = Screen.height;
            lastScreenW = sw; lastScreenH = sh; displayDirty = false;
            displayCam.orthographicSize = sh / 2f;
            displayCam.transform.position = new Vector3(sw / 2f, sh / 2f, -10);
            UpdateHomography();
            int n = MeshGrid + 1;
            var v = new Vector3[n * n]; var uv = new Vector2[n * n]; var tri = new int[MeshGrid * MeshGrid * 6];
            var col = new Color32[n * n];
            for (int ci = 0; ci < col.Length; ci++) col[ci] = new Color32(255, 255, 255, 255);
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float u = i / (float)MeshGrid, t = j / (float)MeshGrid;
                    Vector2 s = CanvasToScreen(new Vector2(u * W, t * H));
                    v[j * n + i] = new Vector3(s.x, sh - s.y, 0);
                    uv[j * n + i] = new Vector2(u, 1 - t);
                }
            int k = 0;
            for (int j = 0; j < MeshGrid; j++)
                for (int i = 0; i < MeshGrid; i++)
                {
                    int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                    tri[k++] = a; tri[k++] = b; tri[k++] = c;
                    tri[k++] = b; tri[k++] = d; tri[k++] = c;
                }
            displayMesh.Clear();
            displayMesh.vertices = v; displayMesh.uv = uv; displayMesh.colors32 = col; displayMesh.triangles = tri;
            displayMesh.RecalculateBounds();
        }

        void RebuildProps()
        {
            if (propsRoot == null) return;
            foreach (var sr in propRenderers)
            {
                if (sr == null) continue;
                if (sr.sprite != null) { Destroy(sr.sprite.texture); Destroy(sr.sprite); }
                Destroy(sr.gameObject);
            }
            propRenderers.Clear();
            foreach (var s in surfaces)
            {
                int lw = Mathf.Max(2, Mathf.RoundToInt(s.w / 3f)), lh = Mathf.Max(2, Mathf.RoundToInt(s.h / 3f));
                var go = new GameObject("Estructura " + s.src.id);
                go.transform.SetParent(propsRoot, false);
                go.transform.localPosition = LowPos(L(s.x), L(s.y));
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = spriteMat; sr.sortingOrder = 0;
                sr.sprite = PixelArt.Prop(s.src, lw, lh);
                propRenderers.Add(sr);
            }
        }

        // ---------------- helpers de dibujo (coordenadas de 640×360) ----------------
        static float L(float v) { return Mathf.Round(v / 3f); }
        static Vector3 LowPos(float lx, float ly) { return new Vector3(lx * .03f - 9.6f, 5.4f - ly * .03f, 0); }

        /// <summary>Dibuja un sprite en una posición de mundo (px de salida). rot en radianes, sentido horario como en un canvas.</summary>
        void Spr(Pool pool, string name, float wx, float wy, float rot = 0, bool flip = false, float sx = 1, float sy = 1, float alpha = 1)
        {
            var r = pool.Next();
            r.sprite = PixelArt.Get(name);
            r.flipX = flip;
            r.transform.localPosition = LowPos(L(wx), L(wy));
            r.transform.localRotation = Quaternion.Euler(0, 0, -rot * Mathf.Rad2Deg);
            r.transform.localScale = new Vector3(sx, sy, 1);
            r.color = new Color(1, 1, 1, alpha);
        }

        /// <summary>Rectángulo en píxeles bajos (origen arriba-izquierda).</summary>
        void Box(Pool pool, float lx, float ly, float lw, float lh, Color c)
        {
            var r = pool.Next();
            r.sprite = PixelArt.WhiteTL;
            r.transform.localPosition = LowPos(lx, ly);
            r.transform.localScale = new Vector3(lw, lh, 1);
            r.color = c;
        }

        void LowLine(Pool pool, float wx0, float wy0, float wx1, float wy1, Color c)
        {
            int x0 = (int)L(wx0), y0 = (int)L(wy0), x1 = (int)L(wx1), y1 = (int)L(wy1);
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
            for (int n = 0; n < 2000; n++)
            {
                Box(pool, x0, y0, 1, 1, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        static Color Hex(string h) { return PixelArt.C(h); }

        // ---------------- frame ----------------
        void RenderWorld()
        {
            if (displayDirty || Screen.width != lastScreenW || Screen.height != lastScreenH) UpdateDisplay();
            bool showWorld = !calib && !blackout;
            if (worldRoot.gameObject.activeSelf != showWorld) worldRoot.gameObject.SetActive(showWorld);
            if (!showWorld) return;

            for (int i = 0; i < propRenderers.Count && i < surfaces.Count; i++)
                propRenderers[i].enabled = !realWall && !surfaces[i].src.hidden;

            poolHooks.Begin(); poolTokens.Begin(); poolEnemies.Begin(); poolBombs.Begin();
            poolScarf.Begin(); poolPlayer.Begin(); poolFx.Begin(); poolHud.Begin();

            if (!realWall) foreach (var h in hooks) Spr(poolHooks, "hook", h.x, h.y);

            // ancla candidata: 4 puntos parpadeantes (útil en la pared real, donde no hay nada dibujado)
            if (candidate != null && P.anchor == null && !P.defeated && Mathf.FloorToInt(now * 4) % 2 == 1)
            {
                float cx = L(candidate.x), cy = L(candidate.y); Color c = Hex("#cfe9ff");
                Box(poolFx, cx - 4, cy, 1, 1, c); Box(poolFx, cx + 4, cy, 1, 1, c);
                Box(poolFx, cx, cy - 4, 1, 1, c); Box(poolFx, cx, cy + 4, 1, 1, c);
            }

            foreach (var t in tokens) DrawToken(t);
            foreach (var e in enemies) DrawEnemy(e);
            foreach (var b in bombs) Spr(poolBombs, "bomb", b.x, b.y, Mathf.Round(b.spin) * Mathf.PI / 2);
            DrawWeb();
            DrawPlayer();
            Color s1 = Hex("#7fd6ff"), s2 = Hex("#cfe9ff"), red = Hex("#ff3b3b");
            foreach (var s in shots) { Box(poolFx, L(s.x) - 2, L(s.y) - 1, 2, 2, s1); Box(poolFx, L(s.x) + 1, L(s.y) - 1, 2, 2, s2); }
            foreach (var b in bolts) Box(poolFx, L(b.x) - 1, L(b.y) - 1, 3, 3, red);
            foreach (var p in particles)
            {
                var c = p.color; c.a = Mathf.Clamp01(p.life / p.max);
                float sz = p.r > 3 ? 2 : 1;
                Box(poolFx, L(p.x), L(p.y), sz, sz, c);
            }

            // HUD dentro de la proyección (corazones y moneda); los números van por OnGUI
            float top = H * .05f + 6, left = W * .05f + 6;
            for (int i = 0; i < MaxHealth; i++) Spr(poolHud, i < P.health ? "heart" : "heart0", left + 14 + i * 36, top + 12);
            Spr(poolHud, "coin", W - left - 150, top + 14);

            poolHooks.End(); poolTokens.End(); poolEnemies.End(); poolBombs.End();
            poolScarf.End(); poolPlayer.End(); poolFx.End(); poolHud.End();
        }

        string PlayerSprite()
        {
            string st = P.state;
            if (st == "Run") { string[] f = { "run1", "idle", "run2", "idle" }; return f[Mathf.FloorToInt(P.anim * 12) % 4]; }
            if (st == "WallCrawl") return Mathf.FloorToInt(P.anim * 10) % 2 == 1 ? "run1" : "run2";
            if (st == "JumpRise" || st == "JumpStart" || st == "Apex" || st == "Fall" || st == "SwingLoop" ||
                st == "SwingRelease" || st == "Hurt" || st == "LedgeClimb") return "jump";
            return "idle";
        }

        void DrawPlayer()
        {
            if (P.defeated)
            {
                float t = 1 - P.defeatT / RespawnDelay;
                for (int i = 0; i < 10; i++)
                {
                    float a = i / 10f * Mathf.PI * 2;
                    Box(poolPlayer, L(P.x + Mathf.Cos(a) * t * 90), L(P.y + Mathf.Sin(a) * t * 90), 2, 2, i % 2 == 1 ? Hex("#8a63ff") : Hex("#ff8c1a"));
                }
                return;
            }
            if (P.invul > 0 && Mathf.FloorToInt(now * 20) % 2 == 1) return;
            Color orange = Hex("#ff8c1a");
            for (int i = 1; i < P.scarf.Length; i++) Box(poolScarf, L(P.scarf[i].x), L(P.scarf[i].y), 2, i < 3 ? 2 : 1, orange);

            string st = P.state;
            if (st == "Roll")
            {
                float rr = Mathf.Round((1 - P.roll / RollTime) * 8) * Mathf.PI / 4 * P.rollDir;
                Spr(poolPlayer, "ball", P.x, P.y + 9, rr);
                return;
            }
            float rot = 0, sy = 1; bool flip = P.facing < 0;
            if (st == "SwingLoop" && P.anchor != null)
                rot = Mathf.Round(Mathf.Atan2(P.anchor.x - P.x, P.y - P.anchor.y) * .9f / (Mathf.PI / 8)) * Mathf.PI / 8;
            if (st == "WallCling" || st == "WallCrawl") { rot = P.facing > 0 ? -Mathf.PI / 2 : Mathf.PI / 2; flip = false; }
            if (st == "Land") sy = .85f;
            Spr(poolPlayer, PlayerSprite(), P.x, P.y + (sy < 1 ? 4 : 0), rot, flip, 1, sy);
        }

        Vector2 Wrist()
        {
            float r = P.anchor != null ? Mathf.Atan2(P.anchor.x - P.x, P.y - P.anchor.y) * .9f : 0;
            return new Vector2(P.x + Mathf.Sin(r) * 28, P.y - Mathf.Cos(r) * 28);
        }

        void DrawWeb()
        {
            if (P.anchor == null || P.defeated) return;
            Vector2 w = Wrist(); var a = P.anchor;
            LowLine(poolFx, w.x, w.y, a.x, a.y, Hex("#cfe9ff"));
            Spr(poolFx, "cross", a.x, a.y);
        }

        void DrawEnemy(Enemy e)
        {
            float alpha = 1, dy = 0, rot = 0;
            if (e.dead)
            {
                float t = e.deadT / .6f;
                if (t > 1) return;
                dy = -Mathf.Sin(t * Mathf.PI) * 80; rot = Mathf.Round(t * 12) * Mathf.PI / 4; alpha = 1 - t;
            }
            if (e.spawnT > 0 && Mathf.FloorToInt(now * 16) % 2 == 1) return;
            bool fr = Mathf.FloorToInt(e.t * 8) % 2 == 1;
            string name;
            switch (e.type)
            {
                case "chaser": name = fr ? "chaser1" : "chaser2"; break;
                case "bomber": name = fr ? "bomber1" : "bomber2"; break;
                case "patrol": name = fr ? "patrol1" : "patrol2"; break;
                case "flyer": name = "flyer"; break;
                case "shooter": name = "shooter"; break;
                default: name = "patrol1"; break;
            }
            Spr(poolEnemies, name, e.x, e.y + dy, rot, e.dir < 0, 1, 1, alpha);
            if (e.flash > 0) Box(poolFx, L(e.x - e.w / 2), L(e.y - e.h / 2), L(e.w), L(e.h), new Color(1, 1, 1, .8f));
        }

        void DrawToken(Token t)
        {
            if (t.got) return;
            if (t.life < 2.5f && Mathf.FloorToInt(now * 10) % 2 == 1) return;   // parpadea antes de desaparecer
            float pop = Mathf.Min(1, (now - t.born) * 5);
            float sx = Mathf.Max(.25f, Mathf.Abs(Mathf.Cos(now * 3 + t.x * .01f)));
            Spr(poolTokens, "coin", t.x, t.y + Mathf.Sin(now * 3 + t.x) * 6, 0, false, sx * pop, pop);
        }
    }
}
