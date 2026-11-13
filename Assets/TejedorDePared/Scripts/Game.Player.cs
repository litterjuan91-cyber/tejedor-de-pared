using UnityEngine;

namespace TejedorDePared
{
    public partial class Game
    {
        static bool IsSolid(Surf s) { return !s.src.oneWay && s.src.landable; }
        bool OverlapX(Surf s) { return P.x + P.w / 2 > s.x && P.x - P.w / 2 < s.x + s.w; }
        bool OverlapY(Surf s) { return P.y + P.h / 2 > s.y && P.y - P.h / 2 < s.y + s.h; }

        void MoveAndCollide(float dt, bool dropThrough)
        {
            float prevBottom = P.y + P.h / 2, prevTop = P.y - P.h / 2;
            P.grounded = false; P.wallDir = 0; P.wallSrc = null;

            // eje X
            P.x += P.vx * dt;
            foreach (var s in surfaces)
            {
                if (!IsSolid(s) || !OverlapY(s) || !OverlapX(s)) continue;
                if (P.vx > 0) P.x = s.x - P.w / 2;
                else if (P.vx < 0) P.x = s.x + s.w + P.w / 2;
                else P.x = P.x < s.x + s.w / 2 ? s.x - P.w / 2 : s.x + s.w + P.w / 2;
                P.vx = 0;
            }
            if (P.x - P.w / 2 < 0) { P.x = P.w / 2; P.vx = Mathf.Max(0, P.vx); }
            if (P.x + P.w / 2 > W) { P.x = W - P.w / 2; P.vx = Mathf.Min(0, P.vx); }

            // eje Y
            P.y += P.vy * dt;
            foreach (var s in surfaces)
            {
                if (!OverlapX(s)) continue;
                float bottom = P.y + P.h / 2, top = P.y - P.h / 2;
                if (IsSolid(s) && OverlapY(s))
                {
                    if (P.vy >= 0 && prevBottom <= s.y + 1) { P.y = s.y - P.h / 2; P.vy = 0; P.grounded = true; P.groundSrc = s; }
                    else if (P.vy < 0 && prevTop >= s.y + s.h - 1) { P.y = s.y + s.h + P.h / 2; P.vy = 0; }
                    else
                    {
                        float up = bottom - s.y, down = s.y + s.h - top;
                        if (up < down) { P.y -= up; P.vy = Mathf.Min(0, P.vy); P.grounded = true; P.groundSrc = s; }
                        else { P.y += down; P.vy = Mathf.Max(0, P.vy); }
                    }
                    continue;
                }
                if (!dropThrough && P.vy >= 0)
                {
                    foreach (float ly in LedgesOf(s))
                    {
                        if (IsSolid(s) && ly == s.y) continue;
                        if (prevBottom <= ly + 1 && P.y + P.h / 2 >= ly) { P.y = ly - P.h / 2; P.vy = 0; P.grounded = true; P.groundSrc = s; break; }
                    }
                }
            }
            if (P.y - P.h / 2 < -260) { P.y = -260 + P.h / 2; P.vy = Mathf.Max(0, P.vy); }

            // sensores de pared: bordes de la proyección y lados de estructuras sólidas
            if (!P.grounded)
            {
                if (P.x - P.w / 2 <= 1) P.wallDir = -1;
                else if (P.x + P.w / 2 >= W - 1) P.wallDir = 1;
                else
                {
                    foreach (var s in surfaces)
                    {
                        if (!IsSolid(s) || !OverlapY(s)) continue;
                        if (Mathf.Abs(P.x + P.w / 2 - s.x) < 3) { P.wallDir = 1; P.wallSrc = s; break; }
                        if (Mathf.Abs(P.x - P.w / 2 - (s.x + s.w)) < 3) { P.wallDir = -1; P.wallSrc = s; break; }
                    }
                }
            }
        }

        /// <summary>Segmento contra rectángulo (Liang–Barsky).</summary>
        static bool SegRect(float x1, float y1, float x2, float y2, float rx, float ry, float rw, float rh)
        {
            float t0 = 0, t1 = 1, dx = x2 - x1, dy = y2 - y1;
            float[] p = { -dx, dx, -dy, dy };
            float[] q = { x1 - rx, rx + rw - x1, y1 - ry, ry + rh - y1 };
            for (int i = 0; i < 4; i++)
            {
                if (p[i] == 0) { if (q[i] < 0) return false; }
                else
                {
                    float t = q[i] / p[i];
                    if (p[i] < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
                    else { if (t < t0) return false; if (t < t1) t1 = t; }
                }
            }
            return true;
        }

        bool LineOfSight(float x1, float y1, Anchor a)
        {
            foreach (var s in surfaces)
            {
                if (!IsSolid(s) || s == a.owner) continue;
                if (SegRect(x1, y1, a.x, a.y, s.x + 2, s.y + 2, s.w - 4, s.h - 4)) return false;
            }
            return true;
        }

        static Vector2 Norm(float x, float y) { float l = Mathf.Sqrt(x * x + y * y); if (l < 1e-6f) l = 1; return new Vector2(x / l, y / l); }

        /// <summary>Dirección de apuntado (y hacia arriba): joystick derecho, mouse, velocidad o frente.</summary>
        Vector2 ResolveAim()
        {
            if (padAim) return Norm(ax, ay);
            if (now - pointerLastMove < 2.5f) return Norm(pointer.x - P.x, -(pointer.y - P.y));
            if (P.vx * P.vx + P.vy * P.vy > U * U) return Norm(P.vx, -P.vy);
            return Norm(P.facing, .75f);
        }

        /// <summary>Puntaje de anclas igual a WebTargetResolver2D: alineación .45, altura .25, distancia .20, frente .10.</summary>
        Anchor ResolveAnchor(Vector2 aim)
        {
            Anchor best = null; float bestScore = float.NegativeInfinity, bestD = float.PositiveInfinity;
            float ox = P.x, oy = P.y - 10;
            foreach (var a in anchors)
            {
                float dx = a.x - ox, dy = -(a.y - oy), d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < 5 || d > SwingRadius) continue;
                float ux = dx / d, uy = dy / d, align = aim.x * ux + aim.y * uy;
                if (dy < -.25f * U && align < .75f) continue;
                if (!LineOfSight(ox, oy, a)) continue;
                float sc = Mathf.Clamp01((align + 1) / 2) * .45f
                         + Mathf.Clamp01((dy / SwingRadius + 1) / 2) * .25f
                         + (1 - Mathf.Min(1, d / SwingRadius)) * .20f
                         + Mathf.Clamp01((ux * P.facing + 1) / 2) * .10f;
                if (sc > bestScore || (Mathf.Abs(sc - bestScore) < 1e-6f && d < bestD)) { best = a; bestScore = sc; bestD = d; }
            }
            return best;
        }

        void HurtPlayer(int n, float fromX)
        {
            if (P.defeated || P.invul > 0 || won) return;
            P.health = Mathf.Max(0, P.health - n);
            Burst(P.x, P.y, PixelArt.C("#ff4f6d"), 14);
            P.anchor = null;
            if (P.health <= 0) { P.defeated = true; P.defeatT = RespawnDelay; P.vx = P.vy = 0; P.state = "Defeat"; Sfx("hurt"); return; }
            P.invul = HurtInvul; P.state = "Hurt"; Sfx("hurt");
            P.vx = (P.x < fromX ? -1 : 1) * 4 * U; P.vy = -5 * U;
        }

        void Shoot(Vector2 aim)
        {
            if (now < P.nextShot || P.defeated) return;
            P.nextShot = now + ShotCooldown;
            float ox = P.x + P.facing * 20, oy = P.y - 8;
            Vector2 dir;
            Enemy target = null; float td = ShotRange;
            foreach (var e in enemies)
            {
                if (e.dead) continue;
                float d = Vector2.Distance(new Vector2(e.x, e.y), new Vector2(ox, oy));
                if (d < td) { td = d; target = e; }
            }
            bool aimed = padAim || now - pointerLastMove < 2.5f;
            if (target != null && !aimed)
            {
                // auto-apuntado con predicción, como PlayerBrain.Shoot del original
                float tx = target.x, ty = target.y;
                for (int i = 0; i < 2; i++)
                {
                    float ft = Vector2.Distance(new Vector2(tx, ty), new Vector2(ox, oy)) / ShotSpeed;
                    tx = target.x + target.vx * ft; ty = target.y + target.vy * ft;
                }
                dir = Norm(tx - ox, ty - oy);
            }
            else if (aimed) dir = new Vector2(aim.x, -aim.y);
            else dir = new Vector2(P.facing, 0);
            shots.Add(new Shot { x = ox, y = oy, vx = dir.x * ShotSpeed, vy = dir.y * ShotSpeed, life = 1.1f });
            Sfx("shot");
        }

        void Burst(float x, float y, Color color, int n = 10, float speed = 260)
        {
            for (int i = 0; i < n; i++)
            {
                float a = Rnd() * Mathf.PI * 2, s = speed * (.3f + Rnd());
                particles.Add(new Particle { x = x, y = y, vx = Mathf.Cos(a) * s, vy = Mathf.Sin(a) * s, life = .35f + Rnd() * .35f, max = .7f, color = color, r = 2 + Rnd() * 3 });
            }
        }

        void StepPlayer(float dt)
        {
            if (P.defeated)
            {
                P.defeatT -= dt;
                if (P.defeatT <= 0) { P.health = MaxHealth; ResetPlayer(); P.invul = RespawnInvul; }
                return;
            }
            P.invul = Mathf.Max(0, P.invul - dt);
            Vector2 aim = ResolveAim();
            candidate = ResolveAnchor(aim);

            if (inRoll && P.grounded && P.anchor == null)
            {
                P.rollDir = Mathf.Abs(mx) > .15f ? (int)Mathf.Sign(mx) : P.facing; P.roll = RollTime; Sfx("roll");
            }
            if (inShoot) Shoot(aim);
            if (inSwing && candidate != null && P.anchor == null)
            {
                P.anchor = candidate;
                P.rope = Mathf.Clamp(Vector2.Distance(new Vector2(P.x, P.y), new Vector2(candidate.x, candidate.y)), RopeMin, RopeMax);
                P.roll = 0; Sfx("web");
                Burst(candidate.x, candidate.y, Color.white, 8, 160);
            }
            if (P.anchor != null && !swingHeld)
            {
                P.vx *= ReleaseBoost; P.vy *= ReleaseBoost; P.anchor = null; P.state = "SwingRelease"; P.releaseT = .18f; Sfx("release");
            }

            string state = P.state;
            bool drop = my < -.5f && now - jumpAt < JumpBuffer && P.grounded && P.groundSrc != null && P.groundSrc.src.oneWay;
            if (drop) { P.dropT = .22f; jumpAt = -99; P.y += 4; }
            P.dropT = Mathf.Max(0, P.dropT - dt);

            if (P.anchor != null)
            {
                // ---- Columpio: cuerda tipo DistanceJoint2D (solo distancia máxima) ----
                var a = P.anchor;
                float rx = P.x - a.x, ry = P.y - a.y, rl = Mathf.Sqrt(rx * rx + ry * ry);
                if (rl < 1e-3f) rl = 1;
                float tx = -ry / rl, ty = rx / rl;
                if (tx < 0) { tx = -tx; ty = -ty; }
                P.vx += tx * mx * Pump * dt; P.vy += ty * mx * Pump * dt;           // bombeo
                P.rope = Mathf.Clamp(P.rope - my * Reel * dt, RopeMin, RopeMax);    // acortar / alargar
                P.vy = Mathf.Min(P.vy + G * Gravity * dt, MaxFall);
                MoveAndCollide(dt, true);
                rx = P.x - a.x; ry = P.y - a.y;
                float d = Mathf.Sqrt(rx * rx + ry * ry);
                if (d > P.rope && d > 1e-3f)
                {
                    float nx = rx / d, ny = ry / d;
                    P.x = a.x + nx * P.rope; P.y = a.y + ny * P.rope;
                    float vr = P.vx * nx + P.vy * ny;
                    if (vr > 0) { P.vx -= vr * nx; P.vy -= vr * ny; }
                }
                if (Mathf.Abs(mx) > .1f) P.facing = (int)Mathf.Sign(mx);
                state = "SwingLoop";
            }
            else
            {
                bool onWall = false;
                if (!P.grounded && P.wallDir != 0 && Mathf.Abs(mx) > .12f && (int)Mathf.Sign(mx) == P.wallDir && P.roll <= 0)
                {
                    // ---- Pared (WallTraversal2D) ----
                    onWall = true;
                    if (now - jumpAt < JumpBuffer)
                    {
                        jumpAt = -99; P.vx = -P.wallDir * WallJumpX; P.vy = -JumpSpeed; P.facing = -P.wallDir;
                        Sfx("jump"); onWall = false; state = "JumpRise";
                    }
                    else
                    {
                        bool ledge = P.wallSrc != null && P.y - P.h / 2 < P.wallSrc.y + 6;
                        if (ledge && my > .2f)
                        {
                            var src = P.wallSrc; int dirW = P.wallDir;
                            P.vy = -WallCrawl * 1.6f; P.vx = dirW * 1.5f * U; state = "LedgeClimb";
                            P.x += dirW * 3; MoveAndCollide(dt, false);
                            if (P.y + P.h / 2 < src.y + 2) P.vy = -2 * U;
                            P.facing = dirW;
                        }
                        else
                        {
                            float vert = Mathf.Abs(my) > .12f ? my * WallCrawl : 0;
                            P.vx = P.wallDir * 30; P.vy = Mathf.Min(-vert, WallSlide);
                            if (vert == 0) P.vy = Mathf.Min(P.vy + G * Gravity * dt, WallSlide * .25f);
                            int keepDir = P.wallDir; var keepSrc = P.wallSrc;
                            MoveAndCollide(dt, false);
                            if (!P.grounded && P.wallDir != 0) { if (P.wallSrc == null) P.wallSrc = keepSrc; }
                            else if (!P.grounded && P.wallDir == 0) { /* perdió el contacto: cae */ }
                            state = Mathf.Abs(vert) > .1f ? "WallCrawl" : "WallCling";
                            P.facing = P.wallDir != 0 ? P.wallDir : keepDir;
                        }
                    }
                }
                if (!onWall) state = MotorTick(dt);
            }

            if (P.releaseT > 0) { P.releaseT -= dt; if (!P.grounded && state != "SwingLoop") state = "SwingRelease"; }
            if (P.invul > HurtInvul - .2f && P.state == "Hurt") state = "Hurt";
            if (P.y - P.h / 2 > H + 300) { ResetPlayer(); P.invul = RespawnInvul; }
            P.state = state;

            // contacto con enemigos (pisarlos los derrota)
            foreach (var e in enemies)
            {
                if (e.dead || e.spawnT > 0) continue;
                if (Mathf.Abs(P.x - e.x) < (P.w + e.w) / 2 - 4 && Mathf.Abs(P.y - e.y) < (P.h + e.h) / 2 - 4)
                {
                    if (P.vy > 2 * U && P.y < e.y - e.h * .2f) { DefeatEnemy(e); P.vy = -JumpSpeed * .75f; Sfx("jump"); }
                    else HurtPlayer(1, e.x);
                }
            }
            // fichas
            foreach (var t in tokens)
            {
                if (t.got) continue;
                if (Vector2.Distance(new Vector2(P.x, P.y), new Vector2(t.x, t.y)) < 44)
                {
                    t.got = true; collected++; Sfx("token"); Burst(t.x, t.y, PixelArt.C("#ffd23f"), 16, 220);
                    if (collected >= layout.goal && !won) { won = true; winT = 0; Sfx("win"); }
                }
            }
        }

        string MotorTick(float dt)
        {
            if (P.grounded) P.coyote = Coyote; else P.coyote = Mathf.Max(0, P.coyote - dt);
            if (P.roll > 0)
            {
                P.roll = Mathf.Max(0, P.roll - dt);
                P.vx = P.rollDir * RollSpeed; P.vy = Mathf.Min(P.vy + G * Gravity * dt, MaxFall);
                P.prevVy = P.vy; MoveAndCollide(dt, P.dropT > 0); LandCheck(); return "Roll";
            }
            float move = Mathf.Abs(mx) < .12f ? 0 : mx;
            if (move != 0) P.facing = move > 0 ? 1 : -1;
            float target = move * RunSpeed;
            float accel = P.grounded ? (Mathf.Abs(target) > .01f ? GroundAccel : GroundDecel) : AirAccel;
            bool skidding = P.grounded && move != 0 && Mathf.Sign(move) != Mathf.Sign(P.vx) && Mathf.Abs(P.vx) >= Skid;
            P.vx = Mathf.MoveTowards(P.vx, target, accel * dt);
            bool downOnOneWay = my < -.5f && P.groundSrc != null && P.groundSrc.src.oneWay;
            if (now - jumpAt < JumpBuffer && (P.grounded || P.coyote > 0) && !downOnOneWay)
            {
                P.vy = -JumpSpeed; P.coyote = 0; jumpAt = -99; P.jumpStart = .05f; Sfx("jump"); P.hardLand = false;
            }
            float gs = (!jumpHeld && P.vy < 0) ? JumpCutGravity : Gravity;
            P.vy = Mathf.Min(P.vy + G * gs * dt, MaxFall);
            P.prevVy = P.vy;
            MoveAndCollide(dt, P.dropT > 0);
            LandCheck();
            if (P.jumpStart > 0) { P.jumpStart -= dt; return "JumpStart"; }
            if (P.land > 0) { P.land -= dt; return "Land"; }
            if (P.grounded) return skidding ? "Skid" : (Mathf.Abs(P.vx) > .2f * U ? "Run" : "Idle");
            if (-P.vy > Apex) return "JumpRise";
            if (Mathf.Abs(P.vy) <= Apex) return "Apex";
            return "Fall";
        }

        void LandCheck()
        {
            if (!P.wasGrounded && P.grounded)
            {
                P.hardLand = Mathf.Abs(P.lastAirVy) >= HardLanding;
                P.land = P.hardLand ? .16f : .09f;
                Sfx(P.hardLand ? "landHard" : "land");
                int n = P.hardLand ? 10 : 5;
                for (int i = 0; i < n; i++)
                    particles.Add(new Particle { x = P.x + (Rnd() - .5f) * 30, y = P.y + P.h / 2, vx = (Rnd() - .5f) * 300, vy = -Rnd() * 120, life = .3f, max = .3f, color = new Color(.73f, .73f, .73f), r = 3 });
            }
            if (!P.grounded) P.lastAirVy = P.vy;
            P.wasGrounded = P.grounded;
        }
    }
}
