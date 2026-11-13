using UnityEngine;

namespace TejedorDePared
{
    public partial class Game
    {
        void DefeatEnemy(Enemy e) { e.dead = true; e.deadT = 0; Sfx("pop"); Burst(e.x, e.y, PixelArt.C("#7bff8a"), 18, 300); }

        void Explode(Bomb b)
        {
            b.life = 0; Sfx("boom");
            Burst(b.x, b.y, PixelArt.C("#ff8c1a"), 18, 320); Burst(b.x, b.y, PixelArt.C("#ffd23f"), 10, 200);
            if (Vector2.Distance(new Vector2(P.x, P.y), new Vector2(b.x, b.y)) < 85) HurtPlayer(1, b.x);
        }

        /// <summary>Fichas que aparecen al azar (como en el video): máx. 2 a la vez, duran 12 s.</summary>
        void StepTokens(float dt)
        {
            tokenTimer -= dt;
            foreach (var t in tokens) t.life -= dt;
            tokens.RemoveAll(t => t.got || t.life <= 0);
            if (won || tokenTimer > 0 || tokens.Count >= 2 || spawnPts.Count == 0) return;
            var free = spawnPts.FindAll(p => Vector2.Distance(new Vector2(p.x, p.y), new Vector2(P.x, P.y)) > 260 && !tokens.Exists(t => t.pt == p));
            var list = free.Count > 0 ? free : spawnPts;
            var pick = list[Random.Range(0, list.Count)];
            tokens.Add(new Token { x = pick.x, y = pick.y, pt = pick, life = 12, born = now });
            Burst(pick.x, pick.y, PixelArt.C("#ffd23f"), 8, 120); Sfx("appear");
            tokenTimer = 2 + Rnd() * 3;
        }

        void StepEnemies(float dt)
        {
            foreach (var e in enemies)
            {
                e.t += dt; e.flash = Mathf.Max(0, e.flash - dt); e.spawnT = Mathf.Max(0, e.spawnT - dt);
                if (e.dead)
                {
                    // reaparecen para que la pared nunca quede vacía
                    e.deadT += dt;
                    if (e.deadT > 7 && !won)
                    {
                        e.dead = false; e.hits = 0; e.x = e.sx; e.y = e.sy; e.spawnT = .9f;
                        Burst(e.x, e.y, Color.white, 10, 140);
                    }
                    continue;
                }
                float px = e.x, py = e.y; bool near = !P.defeated;
                if (e.type == "patrol" || e.type == "chaser")
                {
                    var p = e.plat;
                    float speed = 80;
                    if (e.type == "chaser")
                    {
                        // persigue al jugador por su plataforma
                        float dx = P.x - e.x; bool sameLevel = Mathf.Abs(P.y - e.y) < 320;
                        if (near && sameLevel && Mathf.Abs(dx) < 1100)
                        {
                            if (dx != 0) e.dir = dx > 0 ? 1 : -1;
                            speed = Mathf.Abs(dx) > 20 ? 135 : 0;
                        }
                        else speed = 60;
                    }
                    e.x += e.dir * speed * dt;
                    float minX = p != null ? p.x + e.w / 2 : e.w / 2, maxX = p != null ? p.x + p.w - e.w / 2 : W - e.w / 2;
                    if (e.x < minX) { e.x = minX; if (e.type == "patrol") e.dir = 1; }
                    if (e.x > maxX) { e.x = maxX; if (e.type == "patrol") e.dir = -1; }
                    if (p != null) e.y = p.y - e.h / 2;
                }
                else if (e.type == "bomber")
                {
                    // vuela arriba y tira bombas
                    float tx = (near ? P.x : e.sx) + Mathf.Sin(e.t * .6f) * 220;
                    e.x += Mathf.Clamp((tx - e.x) * 1.5f * dt, -170 * dt, 170 * dt);
                    e.y = e.sy + Mathf.Sin(e.t * 2) * 22; e.dir = tx < e.x ? -1 : 1;
                    e.cool -= dt;
                    if (e.cool <= 0 && near && Mathf.Abs(P.x - e.x) < 320 && P.y > e.y)
                    {
                        e.cool = 2.2f + Rnd();
                        bombs.Add(new Bomb { x = e.x, y = e.y + 16, vx = Mathf.Clamp((P.x - e.x) * .7f, -260, 260), vy = -120, life = 5 });
                        Sfx("drop");
                    }
                }
                else if (e.type == "flyer")
                {
                    float dx = P.x - e.x, dy = P.y - 20 - e.y, d = Mathf.Max(1, Mathf.Sqrt(dx * dx + dy * dy));
                    if (d < 750 && near) { e.x += dx / d * 120 * dt; e.y += dy / d * 120 * dt; }
                    else { e.x += (e.sx - e.x) * dt * .8f; e.y += (e.sy - e.y) * dt * .8f; }
                    e.y += Mathf.Sin(e.t * 4) * 40 * dt; e.dir = dx < 0 ? -1 : 1;
                }
                else if (e.type == "shooter")
                {
                    e.dir = P.x < e.x ? -1 : 1;
                    e.cool -= dt;
                    float d = Vector2.Distance(new Vector2(P.x, P.y), new Vector2(e.x, e.y));
                    if (e.cool <= 0 && d < 900 && near)
                    {
                        e.cool = 2.1f;
                        var n = Norm(P.x - e.x, P.y - e.y);
                        bolts.Add(new Shot { x = e.x + n.x * 24, y = e.y + n.y * 24, vx = n.x * 420, vy = n.y * 420, life = 3 });
                        Sfx("bolt");
                    }
                }
                e.vx = (e.x - px) / dt; e.vy = (e.y - py) / dt;
            }

            // disparos del jugador
            foreach (var s in shots)
            {
                s.x += s.vx * dt; s.y += s.vy * dt; s.life -= dt;
                foreach (var e in enemies)
                {
                    if (e.dead || e.spawnT > 0 || s.life <= 0) continue;
                    if (Mathf.Abs(s.x - e.x) < e.w / 2 + 6 && Mathf.Abs(s.y - e.y) < e.h / 2 + 6)
                    {
                        s.life = 0; e.hits++; e.flash = .12f; Sfx("hit"); Burst(s.x, s.y, PixelArt.C("#cfe9ff"), 6, 150);
                        if (e.hits >= 3) DefeatEnemy(e);
                    }
                }
                foreach (var b in bombs)   // las bombas se pueden reventar en el aire
                    if (b.life > 0 && s.life > 0 && Vector2.Distance(new Vector2(s.x, s.y), new Vector2(b.x, b.y)) < 20) { s.life = 0; Explode(b); }
                foreach (var q in surfaces)
                    if (s.life > 0 && IsSolid(q) && s.x > q.x && s.x < q.x + q.w && s.y > q.y && s.y < q.y + q.h) { s.life = 0; Burst(s.x, s.y, PixelArt.C("#cfe9ff"), 4, 90); }
                if (s.x < -50 || s.x > W + 50 || s.y < -300 || s.y > H + 50) s.life = 0;
            }
            shots.RemoveAll(s => s.life <= 0);

            // proyectiles de torretas
            foreach (var b in bolts)
            {
                b.x += b.vx * dt; b.y += b.vy * dt; b.life -= dt;
                if (!P.defeated && Mathf.Abs(b.x - P.x) < P.w / 2 + 6 && Mathf.Abs(b.y - P.y) < P.h / 2 + 6) { b.life = 0; HurtPlayer(1, b.x); }
                foreach (var q in surfaces)
                    if (IsSolid(q) && b.x > q.x && b.x < q.x + q.w && b.y > q.y && b.y < q.y + q.h) b.life = 0;
            }
            bolts.RemoveAll(b => b.life <= 0 || b.x < -50 || b.x > W + 50 || b.y < -300 || b.y > H + 50);

            // bombas: caen con gravedad y explotan al tocar una superficie o al jugador
            foreach (var b in bombs)
            {
                if (b.life <= 0) continue;
                float prevY = b.y;
                b.vy += 1400 * dt; b.x += b.vx * dt; b.y += b.vy * dt; b.life -= dt; b.spin += dt * 8;
                if (!P.defeated && Mathf.Abs(b.x - P.x) < P.w / 2 + 8 && Mathf.Abs(b.y - P.y) < P.h / 2 + 8) { Explode(b); continue; }
                bool landed = false;
                foreach (var q in surfaces)
                {
                    if (b.x < q.x || b.x > q.x + q.w) continue;
                    if (IsSolid(q)) landed = prevY <= q.y && b.y + 8 >= q.y;
                    else foreach (float ty in LedgesOf(q)) if (prevY <= ty && b.y + 8 >= ty) { landed = true; break; }
                    if (landed) break;
                }
                if (landed) { Explode(b); continue; }
                if (b.y > H + 40) { b.life = 0; continue; }
                if (b.life <= 0) Explode(b);   // se acabó la mecha
            }
            bombs.RemoveAll(b => b.life <= 0);

            foreach (var p in particles) { p.x += p.vx * dt; p.y += p.vy * dt; p.vy += 600 * dt; p.life -= dt; }
            particles.RemoveAll(p => p.life <= 0);
        }
    }
}
