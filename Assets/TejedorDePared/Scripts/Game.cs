using System.Collections.Generic;
using UnityEngine;

namespace TejedorDePared
{
    /// <summary>
    /// TEJEDOR DE PARED — juego de plataformas para proyectar sobre una pared real.
    ///
    /// Todo se calcula en "píxeles de salida" (1920×1080, origen arriba-izquierda, y hacia abajo),
    /// igual que la versión web, y se dibuja en un mundo ortográfico de 19,2×10,8 unidades
    /// (1 unidad = 100 px, cámara ortográfica de tamaño 5,4). Los valores de ajuste vienen de
    /// gameplay_spec.json del proyecto original spider-projection.
    /// </summary>
    public partial class Game : MonoBehaviour
    {
        // ---------------- Constantes ----------------
        public const float W = 1920f, H = 1080f, U = 100f, G = 9.81f * U;
        const float RunSpeed = 7 * U, GroundAccel = 52 * U, GroundDecel = 64 * U, AirAccel = 30 * U;
        const float JumpSpeed = 11 * U, Coyote = .12f, JumpBuffer = .12f;
        const float Gravity = 3.5f, JumpCutGravity = 5.5f, MaxFall = 18 * U, Apex = .6f * U, HardLanding = 13 * U;
        const float RollSpeed = 9 * U, RollTime = .43f, Skid = 5.5f * U;
        const float SwingRadius = 8 * U, RopeMin = 1.4f * U, RopeMax = 8.5f * U, Pump = 18 * U, Reel = 2.4f * U, ReleaseBoost = 1.25f;
        const float WallCrawl = 3 * U, WallSlide = 2.5f * U, WallJumpX = 7.5f * U;
        const int MaxHealth = 5;
        const float HurtInvul = .5f, RespawnDelay = .6f, RespawnInvul = .75f;
        const float ShotSpeed = 14 * U, ShotCooldown = .3f, ShotRange = 14 * U;
        const float FrameBorder = 9f;   // marco de los cuadros (3 px de pixel art)
        const float STEP = 1f / 60f;
        static readonly string[] Kinds = { "Building", "NightWindow", "PictureFrame", "Shelf", "Clock", "Cabinet", "Ground", "Wall" };

        // ---------------- Tipos internos ----------------
        class Surf { public SurfaceData src; public float x, y, w, h; }
        class Anchor { public float x, y; public Surf owner; public bool hook; }
        class PointRef { public Pt src; public float x, y; }
        class Enemy
        {
            public string type; public EnemyData src;
            public float x, y, sx, sy, vx, vy, w = 42, h = 30, deadT, flash, t, cool, spawnT;
            public int hits, dir = 1; public bool dead; public Surf plat;
        }
        class Shot { public float x, y, vx, vy, life; }
        class Bomb { public float x, y, vx, vy, life, spin; }
        class Token { public float x, y, life, born; public bool got; public PointRef pt; }
        class Particle { public float x, y, vx, vy, life, max, r; public Color color; }
        class Player
        {
            public float x, y, vx, vy, w = 36, h = 54;
            public int facing = 1, rollDir = 1, wallDir, health = MaxHealth;
            public string state = "Idle";
            public bool grounded, wasGrounded, hardLand, defeated;
            public float coyote, roll, land, prevVy, lastAirVy, jumpStart, releaseT, invul, defeatT, rope, dropT, nextShot, anim;
            public Anchor anchor; public Surf wallSrc, groundSrc;
            public Vector2[] scarf = new Vector2[5];
        }

        // ---------------- Estado ----------------
        WallLayout layout;
        readonly List<Surf> surfaces = new List<Surf>();
        readonly List<Anchor> anchors = new List<Anchor>();
        readonly List<PointRef> hooks = new List<PointRef>();
        readonly List<PointRef> spawnPts = new List<PointRef>();
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<Shot> bolts = new List<Shot>();
        readonly List<Bomb> bombs = new List<Bomb>();
        readonly List<Token> tokens = new List<Token>();
        readonly List<Particle> particles = new List<Particle>();
        readonly Player P = new Player();

        int collected; bool won; float winT, tokenTimer = 1f;
        float now, acc;
        bool paused, blackout, realWall, calib, showHelp = true, showGrid = true;
        float helpT = 12f;
        Anchor candidate;
        string toast = ""; float toastT;
        Synth synth;

        // ---------------- Entrada ----------------
        float mx, my, ax, ay, jumpAt = -99f;
        bool jumpHeld, swingHeld, padAim, inSwing, inShoot, inRoll, inPause;
        Vector2 pointer = new Vector2(W / 2, H / 2);
        Vector2 lastMouseScreen;
        float pointerLastMove = -99f;
        float selectHold, comboHold, resetHold;
        bool comboFired, selFired;

        void Awake()
        {
            Application.runInBackground = true;
            synth = gameObject.AddComponent<Synth>();
            realWall = PlayerPrefs.GetInt("tejedor.realwall", 0) == 1;
            layout = WallLayout.Load();
            LoadPin();
            SetupRendering();
            RebuildWorld();
            ResetPlayer();
            BuildEntities();
            lastMouseScreen = Controls.MouseScreen;
        }

        void Update()
        {
            float dt = Mathf.Min(.1f, Time.unscaledDeltaTime);
            ReadInput(dt);
            GlobalKeys(dt);
            toastT = Mathf.Max(0, toastT - dt);
            if (showHelp) helpT -= dt;

            if (calib)
            {
                inSwing = inShoot = inRoll = false;
                EditorInput();
                if (saveCountdown > 0)
                {
                    saveCountdown -= dt;
                    if (saveCountdown <= 0)
                    {
                        saveCountdown = 0;
                        Say(layout.Save() ? "Disposición guardada" : "No se pudo guardar el archivo");
                    }
                }
            }
            else if (paused || blackout)
            {
                inSwing = inShoot = inRoll = false;
            }
            else
            {
                acc += dt;
                while (acc >= STEP)
                {
                    now += STEP;
                    StepPlayer(STEP);
                    StepEnemies(STEP);
                    StepTokens(STEP);
                    inSwing = inShoot = inRoll = false;
                    acc -= STEP;
                }
                P.anim += dt;
                // bufanda con retardo
                P.scarf[0] = new Vector2(P.x - P.facing * 9, P.y - 6);
                for (int i = 1; i < P.scarf.Length; i++)
                {
                    Vector2 q = P.scarf[i - 1], p = P.scarf[i];
                    p.x += (q.x - P.facing * 4 - p.x) * .6f;
                    p.y += (q.y + 2 + Mathf.Sin(now * 10 + i) * 1.5f - p.y) * .45f;
                    P.scarf[i] = p;
                }
                if (won) winT += dt;
            }
            RenderWorld();
        }

        void Say(string msg, float t = 2.2f) { toast = msg; toastT = t; }
        void Sfx(string name) { synth.Play(name); }
        static float Rnd() { return UnityEngine.Random.value; }

        // ---------------- Coordenadas ----------------
        static Rect RectToPx(SurfaceData s) { return new Rect(s.x * W, H - (s.y + s.h) * H, s.w * W, s.h * H); }
        static Vector2 PtToPx(Pt p) { return new Vector2(p.x * W, H - p.y * H); }
        static float R4(float v) { return Mathf.Round(v * 10000f) / 10000f; }
        static Pt PxToPt(float x, float y) { return new Pt(R4(x / W), R4((H - y) / H)); }
        static void PxToRect(SurfaceData s, float x, float y, float w, float h)
        {
            s.x = R4(x / W); s.y = R4((H - y - h) / H); s.w = R4(w / W); s.h = R4(h / H);
        }

        // ---------------- Mundo ----------------
        void RebuildWorld(bool props = true)
        {
            surfaces.Clear(); anchors.Clear(); hooks.Clear();
            foreach (var s in layout.surfaces)
            {
                var r = RectToPx(s);
                surfaces.Add(new Surf { src = s, x = r.x, y = r.y, w = r.width, h = r.height });
            }
            foreach (var h in layout.hooks) { var p = PtToPx(h); hooks.Add(new PointRef { src = h, x = p.x, y = p.y }); }
            foreach (var s in surfaces)
            {
                if (!s.src.web) continue;
                if (s.src.kind == "Shelf")
                {
                    anchors.Add(new Anchor { x = s.x + 6, y = s.y, owner = s });
                    anchors.Add(new Anchor { x = s.x + s.w / 2, y = s.y, owner = s });
                    anchors.Add(new Anchor { x = s.x + s.w - 6, y = s.y, owner = s });
                }
                else
                {
                    anchors.Add(new Anchor { x = s.x, y = s.y, owner = s });
                    anchors.Add(new Anchor { x = s.x + s.w / 2, y = s.y, owner = s });
                    anchors.Add(new Anchor { x = s.x + s.w, y = s.y, owner = s });
                    anchors.Add(new Anchor { x = s.x, y = s.y + s.h, owner = s });
                    anchors.Add(new Anchor { x = s.x + s.w, y = s.y + s.h, owner = s });
                }
            }
            foreach (var h in hooks) anchors.Add(new Anchor { x = h.x, y = h.y, hook = true });
            if (props) RebuildProps();
        }

        /// <summary>Bordes "una vía": el borde superior y, en los cuadros, el borde interior inferior (para meterse adentro).</summary>
        readonly List<float> ledgeTmp = new List<float>(2);
        List<float> LedgesOf(Surf s)
        {
            ledgeTmp.Clear();
            if (!s.src.landable) return ledgeTmp;
            if (s.src.oneWay) ledgeTmp.Add(s.y);
            if (PixelArt.IsFrame(s.src.kind) && s.h > FrameBorder * 4) ledgeTmp.Add(s.y + s.h - FrameBorder);
            return ledgeTmp;
        }

        Vector2 SpawnPoint()
        {
            Surf floor = surfaces.Find(s => s.src.kind == "Ground");
            float fy = floor != null ? floor.y : H - 40;
            return new Vector2(Mathf.Max(160, W * .18f), fy - P.h / 2 - 1);
        }

        void ResetPlayer()
        {
            var sp = SpawnPoint();
            P.x = sp.x; P.y = sp.y; P.vx = P.vy = 0; P.anchor = null; P.roll = 0; P.land = 0; P.coyote = 0;
            P.defeated = false; P.state = "Idle"; P.wallDir = 0;
            for (int i = 0; i < P.scarf.Length; i++) P.scarf[i] = new Vector2(P.x, P.y);
        }

        Surf SurfaceBelow(float x, float y)
        {
            Surf best = null; float bd = 1e9f;
            foreach (var s in surfaces)
                if (x >= s.x && x <= s.x + s.w && s.y >= y - 2 && s.y - y < bd) { bd = s.y - y; best = s; }
            return best;
        }

        void BuildEntities()
        {
            enemies.Clear();
            foreach (var e in layout.enemies)
            {
                var p = PtToPx(e.pos);
                var en = new Enemy { type = e.type, src = e, x = p.x, y = p.y, sx = p.x, sy = p.y, t = Rnd() * 5, cool = 1.5f + Rnd(), dir = Rnd() < .5f ? -1 : 1 };
                if (e.type == "patrol" || e.type == "chaser" || e.type == "shooter")
                {
                    if (e.type == "patrol") { en.w = 42; en.h = 24; }
                    else if (e.type == "chaser") { en.w = 48; en.h = 36; }
                    else { en.w = 36; en.h = 36; }
                    en.y = p.y - en.h / 2; en.sy = en.y; en.plat = SurfaceBelow(p.x, p.y - 4);
                }
                else if (e.type == "bomber") { en.w = 54; en.h = 27; }
                else { en.w = 40; en.h = 30; }
                enemies.Add(en);
            }
            spawnPts.Clear();
            foreach (var t in layout.tokens) { var p = PtToPx(t); spawnPts.Add(new PointRef { src = t, x = p.x, y = p.y }); }
            tokens.Clear(); shots.Clear(); bolts.Clear(); bombs.Clear();
            collected = 0; won = false; tokenTimer = 1f;
        }

        void RestartGame()
        {
            P.health = MaxHealth; P.invul = 0; ResetPlayer(); BuildEntities(); paused = false; Say("Reiniciado");
        }

        // ---------------- Entrada ----------------
        static float Dz(float v, float d = .2f) { return Mathf.Abs(v) < d ? 0 : v; }

        void ReadInput(float dt)
        {
            float x = (Controls.AnyHeld("D", "RightArrow") ? 1 : 0) - (Controls.AnyHeld("A", "LeftArrow") ? 1 : 0);
            float y = (Controls.AnyHeld("W", "UpArrow") ? 1 : 0) - (Controls.AnyHeld("S", "DownArrow") ? 1 : 0);   // +y = arriba
            bool jH = Controls.Held("Space"), jP = Controls.Pressed("Space");
            bool sH = Controls.AnyHeld("F", "J") || Controls.MouseHeld(0);
            bool sP = Controls.AnyPressed("F", "J") || Controls.MousePressed(0);
            bool shP = Controls.AnyPressed("K", "E") || Controls.MousePressed(1);
            bool rP = Controls.AnyPressed("C", "LeftShift", "LeftCtrl");
            bool pP = Controls.Pressed("Escape");
            float aX = 0, aY = 0; bool pAim = false;

            // mouse → coordenadas de la pared (inverso del corner-pin)
            Vector2 ms = Controls.MouseScreen;
            if ((ms - lastMouseScreen).sqrMagnitude > .25f) { pointerLastMove = now; lastMouseScreen = ms; }
            pointer = ScreenToCanvas(new Vector2(ms.x, Screen.height - ms.y));

            if (Controls.HasPad)
            {
                Vector2 l = Controls.LeftStick, r = Controls.RightStick;
                if (Dz(l.x) != 0 || Dz(l.y) != 0) { x = Dz(l.x); y = Dz(l.y); }
                if (Controls.PadHeld("left")) x = -1; if (Controls.PadHeld("right")) x = 1;
                if (Controls.PadHeld("up")) y = 1; if (Controls.PadHeld("down")) y = -1;
                if (Dz(r.x, .3f) != 0 || Dz(r.y, .3f) != 0) { aX = Dz(r.x, .3f); aY = Dz(r.y, .3f); pAim = true; }
                jH |= Controls.PadHeld("south"); jP |= Controls.PadPressed("south");
                rP |= Controls.PadPressed("east"); shP |= Controls.PadPressed("west") || Controls.PadPressed("north");
                sH |= Controls.PadHeld("rt") || Controls.PadHeld("rb"); sP |= Controls.PadPressed("rt") || Controls.PadPressed("rb");
                bool sel = Controls.PadHeld("select"), st = Controls.PadHeld("start");
                pP |= Controls.PadPressed("start") && !sel;
                // Select+Start sostenido = calibración · Select sostenido = reiniciar
                if (sel && st) { comboHold += dt; if (comboHold > .75f && !comboFired) { comboFired = true; ToggleCalibration(); } }
                else { comboHold = 0; comboFired = false; }
                if (sel && !st) { selectHold += dt; if (selectHold > .5f && !selFired) { selFired = true; RestartGame(); } }
                else { selectHold = 0; selFired = false; }
            }
            float len = Mathf.Sqrt(x * x + y * y);
            if (len > 1) { x /= len; y /= len; }
            mx = x; my = y; ax = aX; ay = aY; padAim = pAim;
            jumpHeld = jH; swingHeld = sH;
            if (jP) jumpAt = now;
            inSwing |= sP; inShoot |= shP; inRoll |= rP; inPause = pP;
        }

        void GlobalKeys(float dt)
        {
            if (Controls.AnyPressed("F1", "Backquote")) ToggleCalibration();
            if (Controls.AnyPressed("F2", "B")) { blackout = !blackout; if (!blackout) Say("Apagón desactivado"); }
            if (Controls.AnyPressed("F3", "G"))
            {
                realWall = !realWall;
                PlayerPrefs.SetInt("tejedor.realwall", realWall ? 1 : 0); PlayerPrefs.Save();
                Say(realWall ? "Modo invisible: solo se ven personaje, red, enemigos y fichas" : "Estructuras visibles (decorados de prueba)");
            }
            if (Controls.Pressed("H")) { showHelp = !showHelp; helpT = showHelp ? 12 : 0; }
            if (Controls.Pressed("U")) { synth.muted = !synth.muted; Say(synth.muted ? "Sonido apagado" : "Sonido encendido"); }
            if (Controls.Pressed("F11")) Screen.fullScreen = !Screen.fullScreen;
            if (calib)
            {
                if (Controls.Pressed("P")) { pinMode = !pinMode; }
                if (pinMode && Controls.Pressed("Digit0")) { pin = null; SavePin(); Say("Esquinas restablecidas"); }
                return;
            }
            if (inPause) { paused = !paused; Sfx("ui"); }
            if (Controls.Held("R")) { resetHold += dt; if (resetHold > .5f) { resetHold = -99; RestartGame(); } }
            else resetHold = 0;
            if (won && Controls.Pressed("R")) RestartGame();
        }

        void ToggleCalibration()
        {
            calib = !calib; sel = null; dragging = false; saveCountdown = 0; P.anchor = null; pinMode = false;
            Say(calib ? "Modo calibración" : "Modo juego"); Sfx("ui");
            if (!calib) { RebuildWorld(); RestartGame(); }
        }
    }
}
