using System.Globalization;
using UnityEngine;

namespace TejedorDePared
{
    /// <summary>
    /// Modo calibración (F1): editor de la pared sobre la proyección real, corner-pin (P)
    /// y toda la interfaz de texto (OnGUI + GL, en píxeles de pantalla).
    /// </summary>
    public partial class Game
    {
        class Sel { public string type; public int i; public int corner = -1; }

        Sel sel;
        bool dragging, pinMode;
        Vector2 dragO, dragP;
        Rect dragR;
        float saveCountdown;
        int pinDrag = -1;
        float[] pin;   // 4 esquinas (sup-izq, sup-der, inf-der, inf-izq) como fracción de la pantalla; null = encajar
        double[] Hm = { 1, 0, 0, 0, 1, 0, 0, 0, 1 }, Hinv = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };

        // =====================================================================
        // Editor
        // =====================================================================
        Sel HitTest(float x, float y)
        {
            for (int i = hooks.Count - 1; i >= 0; i--) if (Vector2.Distance(new Vector2(hooks[i].x, hooks[i].y), new Vector2(x, y)) < 20) return new Sel { type = "hook", i = i };
            for (int i = spawnPts.Count - 1; i >= 0; i--) if (Vector2.Distance(new Vector2(spawnPts[i].x, spawnPts[i].y), new Vector2(x, y)) < 22) return new Sel { type = "token", i = i };
            for (int i = enemies.Count - 1; i >= 0; i--) if (Mathf.Abs(enemies[i].x - x) < 30 && Mathf.Abs(enemies[i].y - y) < 30) return new Sel { type = "enemy", i = i };
            for (int i = surfaces.Count - 1; i >= 0; i--)
            {
                var s = surfaces[i];
                Vector2[] c = { new Vector2(s.x, s.y), new Vector2(s.x + s.w, s.y), new Vector2(s.x + s.w, s.y + s.h), new Vector2(s.x, s.y + s.h) };
                for (int k = 0; k < 4; k++) if (Mathf.Abs(c[k].x - x) < 14 && Mathf.Abs(c[k].y - y) < 14) return new Sel { type = "surface", i = i, corner = k };
            }
            for (int i = surfaces.Count - 1; i >= 0; i--)
            {
                var s = surfaces[i];
                if (x >= s.x - 4 && x <= s.x + s.w + 4 && y >= s.y - 4 && y <= s.y + s.h + 4) return new Sel { type = "surface", i = i };
            }
            return null;
        }

        Vector2 ObjPos(Sel s)
        {
            if (s.type == "token") return new Vector2(spawnPts[s.i].x, spawnPts[s.i].y);
            if (s.type == "hook") return new Vector2(hooks[s.i].x, hooks[s.i].y);
            return new Vector2(enemies[s.i].x, enemies[s.i].y);
        }

        void SetObjPos(Sel s, float x, float y)
        {
            if (s.type == "token")
            {
                var o = spawnPts[s.i]; o.x = x; o.y = y; var q = PxToPt(x, y); o.src.x = q.x; o.src.y = q.y;
            }
            else if (s.type == "hook")
            {
                var o = hooks[s.i]; var q = PxToPt(x, y); o.src.x = q.x; o.src.y = q.y;
                RebuildWorld(false);
            }
            else
            {
                var e = enemies[s.i]; e.x = x; e.y = y;
                float feet = (e.type == "flyer" || e.type == "bomber") ? y : y + e.h / 2;
                e.src.pos = PxToPt(x, feet);
            }
        }

        void EditorInput()
        {
            if (pinMode) { PinInput(); return; }
            float x = pointer.x, y = pointer.y;
            float snap = Controls.AnyHeld("LeftAlt", "RightAlt") ? 1 : 5;
            System.Func<float, float> sn = v => Mathf.Round(v / snap) * snap;

            if (Controls.MousePressed(0))
            {
                sel = HitTest(x, y);
                dragging = sel != null;
                dragO = pointer;
                if (sel != null)
                {
                    if (sel.type == "surface") { var s = surfaces[sel.i]; dragR = new Rect(s.x, s.y, s.w, s.h); }
                    else dragP = ObjPos(sel);
                }
            }
            if (dragging && sel != null && Controls.MouseHeld(0))
            {
                float dx = x - dragO.x, dy = y - dragO.y;
                if (sel.type == "surface")
                {
                    var s = surfaces[sel.i]; var r = dragR;
                    float nx = r.x, ny = r.y, nw = r.width, nh = r.height;
                    if (sel.corner == -1) { nx = sn(r.x + dx); ny = sn(r.y + dy); }
                    else
                    {
                        if (sel.corner == 0 || sel.corner == 3) { nx = sn(r.x + dx); nw = r.x + r.width - nx; }
                        else nw = sn(r.x + r.width + dx) - r.x;
                        if (sel.corner == 0 || sel.corner == 1) { ny = sn(r.y + dy); nh = r.y + r.height - ny; }
                        else nh = sn(r.y + r.height + dy) - r.y;
                    }
                    if (nw >= 10 && nh >= 6 && (nx != s.x || ny != s.y || nw != s.w || nh != s.h))
                    {
                        PxToRect(s.src, nx, ny, nw, nh);
                        RebuildWorld(false);
                    }
                }
                else SetObjPos(sel, sn(dragP.x + dx), sn(dragP.y + dy));
            }
            if (!Controls.MouseHeld(0)) dragging = false;

            // ---- atajos ----
            if (Controls.Pressed("N"))
            {
                string id = "sup_" + Random.Range(1000, 9999);
                var s = new SurfaceData(id, "Building", 0, 0, 0, 0, true, true, true);
                PxToRect(s, x - 120, y - 80, 240, 160);
                layout.surfaces.Add(s); RebuildWorld(false);
                sel = new Sel { type = "surface", i = surfaces.Count - 1 };
                Say("Estructura nueva: " + id);
            }
            if (Controls.Pressed("M")) { layout.tokens.Add(PxToPt(x, y)); BuildEntities(); Say("Punto de aparición de ficha agregado"); }
            if (Controls.Pressed("K")) { layout.hooks.Add(PxToPt(x, y)); RebuildWorld(false); Say("Gancho agregado"); }
            string etype = Controls.Pressed("Digit1") ? "chaser" : Controls.Pressed("Digit2") ? "bomber" : Controls.Pressed("Digit3") ? "patrol" : Controls.Pressed("Digit4") ? "shooter" : null;
            if (etype != null) { var q = PxToPt(x, y); layout.enemies.Add(new EnemyData(etype, q.x, q.y)); BuildEntities(); Say("Enemigo: " + etype); }

            if (sel != null && Controls.AnyPressed("Delete", "Backspace"))
            {
                if (sel.type == "surface") layout.surfaces.RemoveAt(sel.i);
                else if (sel.type == "token") layout.tokens.RemoveAt(sel.i);
                else if (sel.type == "hook") layout.hooks.RemoveAt(sel.i);
                else if (sel.type == "enemy") layout.enemies.RemoveAt(sel.i);
                sel = null; dragging = false; RebuildWorld(false); BuildEntities(); Say("Eliminado");
            }
            if (sel != null && sel.type == "surface" && sel.i < layout.surfaces.Count)
            {
                var s = layout.surfaces[sel.i]; bool changed = true;
                if (Controls.Pressed("O")) { s.oneWay = !s.oneWay; Say("Una vía (atravesable desde abajo): " + (s.oneWay ? "sí" : "no")); }
                else if (Controls.Pressed("T")) { s.web = !s.web; Say("Acepta red: " + (s.web ? "sí" : "no")); }
                else if (Controls.Pressed("L")) { s.landable = !s.landable; Say("Se puede pisar: " + (s.landable ? "sí" : "no")); }
                else if (Controls.Pressed("V")) { int k = System.Array.IndexOf(Kinds, s.kind); s.kind = Kinds[(k + 1) % Kinds.Length]; Say("Tipo: " + s.kind); }
                else if (Controls.Pressed("Y")) { s.hidden = !s.hidden; Say(s.hidden ? "Esta estructura ahora es invisible" : "Esta estructura ahora se dibuja"); }
                else changed = false;
                if (changed) RebuildWorld(false);
            }
            if (Controls.Pressed("Q")) { showGrid = !showGrid; Say(showGrid ? "Cuadrícula visible" : "Cuadrícula oculta: alineá los contornos con tus objetos"); }
            if (Controls.Pressed("S") && saveCountdown <= 0) { saveCountdown = 3; Say("Guardando en 3 s — Esc cancela", 3); }
            if (saveCountdown > 0 && Controls.Pressed("Escape")) { saveCountdown = 0; Say("Guardado cancelado"); }
            if (Controls.Pressed("X"))
            {
                if (layout.SaveTo(WallLayout.ExportPath)) { Say("Exportado: pared_layout_exportado.json"); Application.OpenURL("file://" + WallLayout.Folder); }
                else Say("No se pudo exportar");
            }
            if (Controls.Pressed("I"))
            {
                WallLayout l = null;
                try { if (System.IO.File.Exists(WallLayout.ImportPath)) l = WallLayout.FromJson(System.IO.File.ReadAllText(WallLayout.ImportPath)); }
                catch (System.Exception e) { Debug.LogWarning(e.Message); }
                if (l != null) { layout = l; sel = null; RebuildWorld(false); BuildEntities(); Say("Disposición importada"); }
                else { Say("Poné pared_layout_importar.json en la carpeta de datos", 4); Application.OpenURL("file://" + WallLayout.Folder); }
            }
            if (Controls.Pressed("Z") && Controls.AnyHeld("LeftShift", "RightShift"))
            {
                layout = WallLayout.CreateDefault(); sel = null; RebuildWorld(false); BuildEntities(); Say("Pared de ejemplo restaurada (sin guardar)");
            }
        }

        void PinInput()
        {
            Vector2 ms = Controls.MouseScreen; ms.y = Screen.height - ms.y;
            var corners = CurrentCorners();
            if (Controls.MousePressed(0))
            {
                pinDrag = -1; float best = 60;
                for (int i = 0; i < 4; i++) { float d = Vector2.Distance(ms, corners[i]); if (d < best) { best = d; pinDrag = i; } }
                if (pinDrag >= 0 && pin == null)
                {
                    pin = new float[8];
                    for (int i = 0; i < 4; i++) { pin[i * 2] = corners[i].x / Screen.width; pin[i * 2 + 1] = corners[i].y / Screen.height; }
                }
            }
            if (pinDrag >= 0 && Controls.MouseHeld(0))
            {
                pin[pinDrag * 2] = ms.x / Screen.width; pin[pinDrag * 2 + 1] = ms.y / Screen.height;
                displayDirty = true;
            }
            if (pinDrag >= 0 && !Controls.MouseHeld(0)) { pinDrag = -1; SavePin(); }
        }

        // =====================================================================
        // Corner-pin (homografía)
        // =====================================================================
        void LoadPin()
        {
            pin = null;
            string s = PlayerPrefs.GetString("tejedor.pin", "");
            if (string.IsNullOrEmpty(s)) return;
            var parts = s.Split(';');
            if (parts.Length != 8) return;
            var p = new float[8];
            for (int i = 0; i < 8; i++) if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out p[i])) return;
            pin = p;
        }

        void SavePin()
        {
            if (pin == null) PlayerPrefs.DeleteKey("tejedor.pin");
            else
            {
                var parts = new string[8];
                for (int i = 0; i < 8; i++) parts[i] = pin[i].ToString("R", CultureInfo.InvariantCulture);
                PlayerPrefs.SetString("tejedor.pin", string.Join(";", parts));
            }
            PlayerPrefs.Save();
            displayDirty = true;
        }

        Vector2[] FitCorners()
        {
            float sw = Screen.width, sh = Screen.height, s = Mathf.Min(sw / W, sh / H);
            float ox = (sw - W * s) / 2, oy = (sh - H * s) / 2;
            return new[] { new Vector2(ox, oy), new Vector2(ox + W * s, oy), new Vector2(ox + W * s, oy + H * s), new Vector2(ox, oy + H * s) };
        }

        Vector2[] CurrentCorners()
        {
            if (pin == null) return FitCorners();
            var c = new Vector2[4];
            for (int i = 0; i < 4; i++) c[i] = new Vector2(pin[i * 2] * Screen.width, pin[i * 2 + 1] * Screen.height);
            return c;
        }

        void UpdateHomography()
        {
            Vector2[] src = { new Vector2(0, 0), new Vector2(W, 0), new Vector2(W, H), new Vector2(0, H) };
            Hm = Homography(src, CurrentCorners());
            Hinv = Inv3(Hm);
        }

        static double[] Homography(Vector2[] src, Vector2[] dst)
        {
            var A = new double[8, 8]; var b = new double[8];
            for (int i = 0; i < 4; i++)
            {
                double x = src[i].x, y = src[i].y, u = dst[i].x, v = dst[i].y;
                int r = i * 2;
                A[r, 0] = x; A[r, 1] = y; A[r, 2] = 1; A[r, 6] = -u * x; A[r, 7] = -u * y; b[r] = u;
                A[r + 1, 3] = x; A[r + 1, 4] = y; A[r + 1, 5] = 1; A[r + 1, 6] = -v * x; A[r + 1, 7] = -v * y; b[r + 1] = v;
            }
            var h = Solve(A, b);
            return new[] { h[0], h[1], h[2], h[3], h[4], h[5], h[6], h[7], 1.0 };
        }

        static double[] Solve(double[,] A, double[] b)
        {
            int n = b.Length;
            for (int i = 0; i < n; i++)
            {
                int m = i;
                for (int r = i + 1; r < n; r++) if (System.Math.Abs(A[r, i]) > System.Math.Abs(A[m, i])) m = r;
                if (m != i)
                {
                    for (int c = 0; c < n; c++) { double t = A[i, c]; A[i, c] = A[m, c]; A[m, c] = t; }
                    double tb = b[i]; b[i] = b[m]; b[m] = tb;
                }
                double piv = A[i, i];
                if (System.Math.Abs(piv) < 1e-12) piv = 1e-12;
                for (int r = i + 1; r < n; r++)
                {
                    double f = A[r, i] / piv;
                    for (int c = i; c < n; c++) A[r, c] -= f * A[i, c];
                    b[r] -= f * b[i];
                }
            }
            var x = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                double s = b[i];
                for (int c = i + 1; c < n; c++) s -= A[i, c] * x[c];
                double piv = System.Math.Abs(A[i, i]) < 1e-12 ? 1e-12 : A[i, i];
                x[i] = s / piv;
            }
            return x;
        }

        static double[] Inv3(double[] m)
        {
            double a = m[0], b = m[1], c = m[2], d = m[3], e = m[4], f = m[5], g = m[6], h = m[7], i = m[8];
            double A = e * i - f * h, B = -(d * i - f * g), C = d * h - e * g;
            double det = a * A + b * B + c * C;
            if (System.Math.Abs(det) < 1e-12) det = 1e-12;
            return new[] { A / det, -(b * i - c * h) / det, (b * f - c * e) / det,
                           B / det, (a * i - c * g) / det, -(a * f - c * d) / det,
                           C / det, -(a * h - b * g) / det, (a * e - b * d) / det };
        }

        static Vector2 Apply(double[] m, Vector2 p)
        {
            double w = m[6] * p.x + m[7] * p.y + m[8];
            if (System.Math.Abs(w) < 1e-12) w = 1e-12;
            return new Vector2((float)((m[0] * p.x + m[1] * p.y + m[2]) / w), (float)((m[3] * p.x + m[4] * p.y + m[5]) / w));
        }

        /// <summary>Pared (1920×1080) → pantalla (píxeles, origen arriba-izquierda).</summary>
        Vector2 CanvasToScreen(Vector2 p) { return Apply(Hm, p); }
        /// <summary>Pantalla (origen arriba-izquierda) → pared.</summary>
        Vector2 ScreenToCanvas(Vector2 s) { return Apply(Hinv, s); }

        // =====================================================================
        // Interfaz (OnGUI + GL)
        // =====================================================================
        GUIStyle guiStyle;
        Material glMat;

        void EnsureGui()
        {
            if (guiStyle == null)
            {
                guiStyle = new GUIStyle(GUI.skin.label) { richText = false, wordWrap = false, clipping = TextClipping.Overflow };
                guiStyle.fontStyle = FontStyle.Bold;
            }
            if (glMat == null)
            {
                var sh = Shader.Find("Hidden/Internal-Colored");
                glMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                glMat.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                glMat.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                glMat.SetFloat("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                glMat.SetFloat("_ZWrite", 0);
                glMat.SetFloat("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
        }

        float GuiScale { get { return Vector2.Distance(CanvasToScreen(Vector2.zero), CanvasToScreen(new Vector2(W, 0))) / W; } }

        /// <summary>Texto en coordenadas de pared. y = borde superior del texto.</summary>
        void Label(float x, float y, string text, int size, Color c, TextAnchor anchor = TextAnchor.UpperLeft, float alpha = 1)
        {
            Vector2 s = CanvasToScreen(new Vector2(x, y));
            float sc = GuiScale, h = size * sc * 1.6f;
            guiStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * sc));
            guiStyle.alignment = anchor;
            guiStyle.normal.textColor = c;
            Rect r;
            if (anchor == TextAnchor.UpperRight) r = new Rect(s.x - 2000, s.y, 2000, h);
            else if (anchor == TextAnchor.UpperCenter) r = new Rect(s.x - 1000, s.y, 2000, h);
            else r = new Rect(s.x, s.y, 2000, h);
            var old = GUI.color; GUI.color = new Color(1, 1, 1, alpha);
            GUI.Label(r, text, guiStyle);
            GUI.color = old;
        }

        // ---- primitivas GL (se llaman entre GLBegin y GLEnd, solo en Repaint) ----
        void GLBegin()
        {
            glMat.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
            GL.Begin(GL.QUADS);
        }
        void GLEnd() { GL.End(); GL.PopMatrix(); }

        void QuadS(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
        {
            GL.Color(col);
            GL.Vertex3(a.x, a.y, 0); GL.Vertex3(b.x, b.y, 0); GL.Vertex3(c.x, c.y, 0); GL.Vertex3(d.x, d.y, 0);
        }
        void LineS(Vector2 a, Vector2 b, float width, Color col)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * width * .5f;
            QuadS(a + n, b + n, b - n, a - n, col);
        }
        void Line(float x0, float y0, float x1, float y1, float width, Color col)
        {
            LineS(CanvasToScreen(new Vector2(x0, y0)), CanvasToScreen(new Vector2(x1, y1)), width, col);
        }
        void FillRect(float x, float y, float w, float h, Color col)
        {
            QuadS(CanvasToScreen(new Vector2(x, y)), CanvasToScreen(new Vector2(x + w, y)), CanvasToScreen(new Vector2(x + w, y + h)), CanvasToScreen(new Vector2(x, y + h)), col);
        }
        void StrokeRect(float x, float y, float w, float h, float width, Color col)
        {
            Line(x, y, x + w, y, width, col); Line(x + w, y, x + w, y + h, width, col);
            Line(x + w, y + h, x, y + h, width, col); Line(x, y + h, x, y, width, col);
        }
        void Circle(float cx, float cy, float r, float width, Color col, bool dashed = false)
        {
            const int seg = 28;
            for (int i = 0; i < seg; i++)
            {
                if (dashed && i % 2 == 1) continue;
                float a0 = i / (float)seg * Mathf.PI * 2, a1 = (i + 1) / (float)seg * Mathf.PI * 2;
                Line(cx + Mathf.Cos(a0) * r, cy + Mathf.Sin(a0) * r, cx + Mathf.Cos(a1) * r, cy + Mathf.Sin(a1) * r, width, col);
            }
        }
        void DiscS(Vector2 c, float r, Color col)
        {
            const int seg = 24;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2, a1 = (i + 1) / (float)seg * Mathf.PI * 2;
                Vector2 p0 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, p1 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r;
                QuadS(c, p0, p1, p1, col);
            }
        }
        void Disc(float cx, float cy, float r, Color col)
        {
            const int seg = 20;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2, a1 = (i + 1) / (float)seg * Mathf.PI * 2;
                Vector2 c = CanvasToScreen(new Vector2(cx, cy));
                Vector2 p0 = CanvasToScreen(new Vector2(cx + Mathf.Cos(a0) * r, cy + Mathf.Sin(a0) * r));
                Vector2 p1 = CanvasToScreen(new Vector2(cx + Mathf.Cos(a1) * r, cy + Mathf.Sin(a1) * r));
                QuadS(c, p0, p1, p1, col);
            }
        }

        void OnGUI()
        {
            EnsureGui();
            if (blackout) return;
            if (calib) DrawCalibrationGUI();
            else DrawHudGUI();
        }

        void BannerGL() { FillRect(0, H / 2 - 90, W, 180, new Color(0, 0, 0, .6f)); }
        void BannerText(string a, string b)
        {
            Label(W / 2, H / 2 - 62, a, 72, Hex("#ffd23f"), TextAnchor.UpperCenter);
            Label(W / 2, H / 2 + 30, b, 26, new Color(.8f, .8f, .8f), TextAnchor.UpperCenter);
        }

        void DrawHudGUI()
        {
            bool repaint = Event.current.type == EventType.Repaint;
            float top = H * .05f + 6, left = W * .05f + 6;
            if (repaint && (paused || won)) { GLBegin(); BannerGL(); GLEnd(); }
            Label(W - left, top - 6, collected + "/" + layout.goal, 34, Hex("#ffd23f"), TextAnchor.UpperRight);
            if (showHelp && helpT > 0)
            {
                string[] lines =
                {
                    "A/D mover · Espacio saltar · Mantener clic/F columpiar (ganchos y cuadros) · W/S acortar/alargar",
                    "Clic der./K disparar red · C rodar · S+Espacio bajar de un borde · Pared: empujar hacia ella + W/S",
                    "F1/º calibrar · F2/B apagón · F3/G estructuras invisibles · Esc pausa · R (mantener) reiniciar · U sonido · H ayuda"
                };
                for (int i = 0; i < lines.Length; i++) Label(left, H - H * .05f - 90 + i * 26, lines[i], 20, new Color(.6f, .67f, .67f), TextAnchor.UpperLeft, Mathf.Min(1, helpT));
            }
            if (toastT > 0) Label(W / 2, top, toast, 28, Color.white, TextAnchor.UpperCenter, Mathf.Min(1, toastT * 2));
            if (paused) BannerText("PAUSA", "Esc para seguir");
            if (won) BannerText("¡PARED COMPLETADA!", "R para jugar de nuevo");
        }

        void DrawCalibrationGUI()
        {
            bool repaint = Event.current.type == EventType.Repaint;
            Color gridMinor = Hex("#1f3b4d"), gridMajor = Hex("#3e7ea6"), yellow = Hex("#ffd23f"), red = Hex("#ff3b6b");
            Color turq = Hex("#14f1d9"), orange = Hex("#ff8c1a"), pink = Hex("#ff4f6d"), sky = Hex("#7fd6ff"), purple = Hex("#7b5cff");
            float lw = showGrid ? 3 : 5;
            bool panel = showHelp || pinMode;
            Vector2 sp = SpawnPoint();

            if (repaint)
            {
                GLBegin();
                if (showGrid)
                {
                    for (float x = 0; x <= W; x += 60) Line(x, 0, x, H, 1, gridMinor);
                    for (float y = 0; y <= H; y += 60) Line(0, y, W, y, 1, gridMinor);
                    for (float x = 0; x <= W; x += 240) Line(x, 0, x, H, 2, gridMajor);
                    for (float y = 0; y <= H; y += 240) Line(0, y, W, y, 2, gridMajor);
                    float sx0 = W * .05f, sy0 = H * .05f, sx1 = W * .95f, sy1 = H * .95f;
                    for (float x = sx0; x < sx1; x += 26) { Line(x, sy0, Mathf.Min(x + 16, sx1), sy0, 3, yellow); Line(x, sy1, Mathf.Min(x + 16, sx1), sy1, 3, yellow); }
                    for (float y = sy0; y < sy1; y += 26) { Line(sx0, y, sx0, Mathf.Min(y + 16, sy1), 3, yellow); Line(sx1, y, sx1, Mathf.Min(y + 16, sy1), 3, yellow); }
                    StrokeRect(2, 2, W - 4, H - 4, 4, red);
                    Line(W / 2 - 40, H / 2, W / 2 + 40, H / 2, 4, red); Line(W / 2, H / 2 - 40, W / 2, H / 2 + 40, 4, red);
                    Circle(W / 2, H / 2, 200, 4, red);
                    Disc(0, 0, 36, red); Disc(W, 0, 36, red); Disc(0, H, 36, red); Disc(W, H, 36, red);
                }
                for (int i = 0; i < surfaces.Count; i++)
                {
                    var s = surfaces[i];
                    bool isSel = sel != null && sel.type == "surface" && sel.i == i;
                    Color baseC = s.src.oneWay ? turq : orange;
                    if (showGrid) FillRect(s.x, s.y, s.w, s.h, s.src.oneWay ? new Color(.08f, .95f, .85f, .12f) : new Color(1, .55f, .1f, .18f));
                    StrokeRect(s.x, s.y, s.w, s.h, lw, isSel ? Color.white : baseC);
                    if (s.src.oneWay) Line(s.x, s.y, s.x + s.w, s.y, lw * 2, turq);
                    Color hc = isSel ? Color.white : Hex("#99ffdd");
                    FillRect(s.x - 7, s.y - 7, 14, 14, hc); FillRect(s.x + s.w - 7, s.y - 7, 14, 14, hc);
                    FillRect(s.x + s.w - 7, s.y + s.h - 7, 14, 14, hc); FillRect(s.x - 7, s.y + s.h - 7, 14, 14, hc);
                }
                foreach (var a in anchors)
                {
                    if (a.hook) continue;
                    Circle(a.x, a.y, 9, 2, Color.white);
                    Line(a.x - 13, a.y, a.x + 13, a.y, 2, Color.white); Line(a.x, a.y - 13, a.x, a.y + 13, 2, Color.white);
                }
                for (int i = 0; i < spawnPts.Count; i++)
                    Circle(spawnPts[i].x, spawnPts[i].y, 16, 3, sel != null && sel.type == "token" && sel.i == i ? Color.white : yellow, true);
                for (int i = 0; i < hooks.Count; i++)
                    Disc(hooks[i].x, hooks[i].y, 8, sel != null && sel.type == "hook" && sel.i == i ? Color.white : sky);
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    StrokeRect(e.x - e.w / 2, e.y - e.h / 2, e.w, e.h, 3, sel != null && sel.type == "enemy" && sel.i == i ? Color.white : pink);
                }
                StrokeRect(sp.x - P.w / 2, sp.y - P.h / 2, P.w, P.h, 3, purple);
                if (panel) FillRect(W * .05f, H * .05f, 940, pinMode ? 120 : 330, new Color(0, 0, 0, .75f));
                if (saveCountdown > 0) BannerGL();
                GLEnd();

                if (pinMode)
                {
                    GL.PushMatrix(); glMat.SetPass(0); GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0); GL.Begin(GL.QUADS);
                    var c = CurrentCorners();
                    for (int i = 0; i < 4; i++) { DiscS(c[i], 18, yellow); DiscS(c[i], 12, new Color(0, 0, 0, .8f)); }
                    GL.End(); GL.PopMatrix();
                }
            }

            // ---- textos ----
            foreach (var s in surfaces)
            {
                string t = s.src.id + " · " + s.src.kind + (s.src.oneWay ? " · 1-vía" : " · sólido") + (s.src.web ? " · red" : "")
                         + (s.src.landable ? "" : " · no pisable") + (s.src.hidden || realWall ? " · invisible" : "");
                Label(s.x + 4, s.y - 26, t, 16, Color.white);
            }
            foreach (var e in enemies) Label(e.x - e.w / 2, e.y - e.h / 2 - 24, e.type, 16, pink);
            Label(sp.x - 22, sp.y - P.h / 2 - 24, "inicio", 16, purple);

            if (pinMode)
            {
                var c = CurrentCorners();
                for (int i = 0; i < 4; i++)
                {
                    guiStyle.fontSize = 14; guiStyle.alignment = TextAnchor.MiddleCenter; guiStyle.normal.textColor = yellow;
                    GUI.Label(new Rect(c[i].x - 20, c[i].y - 20, 40, 40), (i + 1).ToString(), guiStyle);
                }
            }

            if (panel)
            {
                float px = W * .05f + 16, py = H * .05f + 14;
                Label(px, py, pinMode ? "ESQUINAS (corner-pin)" : "CALIBRACIÓN DE PARED", 24, yellow);
                string[] help = pinMode
                    ? new[] { "Arrastrá los 4 círculos a las esquinas reales de la proyección.", "P volver · 0 restablecer esquinas" }
                    : new[]
                    {
                        "Arrastrar: mover · arrastrar esquina: redimensionar (Alt = sin imán)",
                        "N cuadro nuevo · K gancho · M aparición de ficha · 1 perseguidor 2 bombardero 3 patrulla 4 torreta",
                        "Con estructura elegida: O una vía · T acepta red · L pisable · V tipo · Y invisible",
                        "Supr borrar · S guardar (cuenta regresiva) · X exportar · I importar JSON",
                        "P esquinas (corner-pin) · Shift+Z restaurar ejemplo · H ocultar panel · F1 salir",
                        "Q cuadrícula on/off · G modo invisible (todas) · naranja = sólido, turquesa = una vía",
                        "Cruces blancas = anclas de red; puntos celestes = ganchos reales de la pared.",
                        "Mouse: " + Mathf.RoundToInt(pointer.x) + ", " + Mathf.RoundToInt(pointer.y) + " px"
                    };
                for (int i = 0; i < help.Length; i++) Label(px, py + 40 + i * 26, help[i], 17, new Color(.87f, .87f, .87f));
            }
            if (saveCountdown > 0) BannerText("Guardando en " + Mathf.CeilToInt(saveCountdown) + "…", "Esc cancela");
            if (toastT > 0) Label(W / 2, H - H * .05f - 44, toast, 26, Color.white, TextAnchor.UpperCenter);
        }
    }
}
