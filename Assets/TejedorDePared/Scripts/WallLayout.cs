using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TejedorDePared
{
    /// <summary>Punto normalizado (0..1), origen abajo-izquierda.</summary>
    [System.Serializable]
    public class Pt
    {
        public float x, y;
        public Pt() { }
        public Pt(float x, float y) { this.x = x; this.y = y; }
    }

    /// <summary>
    /// Una estructura de la pared (cuadro, estante, respaldo...). Su rectángulo está
    /// normalizado respecto de la salida del proyector: x, y = esquina inferior izquierda.
    /// </summary>
    [System.Serializable]
    public class SurfaceData
    {
        public string id = "sup";
        public string kind = "Building";
        public float x, y, w, h;
        public bool landable = true;   // se puede pisar
        public bool oneWay = true;     // se atraviesa desde abajo (si es false es sólido y se trepa por los lados)
        public bool web = true;        // acepta la red en sus esquinas
        public bool hidden = false;    // invisible: solo existe su colisionador

        public SurfaceData() { }
        public SurfaceData(string id, string kind, float x, float y, float w, float h, bool landable, bool oneWay, bool web)
        {
            this.id = id; this.kind = kind; this.x = x; this.y = y; this.w = w; this.h = h;
            this.landable = landable; this.oneWay = oneWay; this.web = web;
        }
    }

    [System.Serializable]
    public class EnemyData
    {
        public string type = "chaser";   // chaser, bomber, patrol, shooter, flyer
        public Pt pos = new Pt();
        public EnemyData() { }
        public EnemyData(string type, float x, float y) { this.type = type; pos = new Pt(x, y); }
    }

    /// <summary>Disposición calibrada de una pared real. Se guarda como JSON.</summary>
    [System.Serializable]
    public class WallLayout
    {
        public string schema = "tejedor.layout.unity.v1";
        public string name = "Pared de ejemplo - reemplazar con medidas reales";
        public int goal = 10;
        public List<SurfaceData> surfaces = new List<SurfaceData>();
        public List<Pt> hooks = new List<Pt>();
        public List<EnemyData> enemies = new List<EnemyData>();
        public List<Pt> tokens = new List<Pt>();

        public static string Folder => Application.persistentDataPath;
        public static string FilePath => Path.Combine(Folder, "pared_layout.json");
        public static string ExportPath => Path.Combine(Folder, "pared_layout_exportado.json");
        public static string ImportPath => Path.Combine(Folder, "pared_layout_importar.json");

        /// <summary>Pared del video: respaldo de cama, dos cuadros proyectados y ganchos reales.</summary>
        public static WallLayout CreateDefault()
        {
            var l = new WallLayout();
            l.surfaces.Add(new SurfaceData("respaldo", "Ground", 0.00f, 0.240f, 1.00f, 0.040f, true, false, false));
            l.surfaces.Add(new SurfaceData("edificio", "Building", 0.27f, 0.400f, 0.10f, 0.190f, true, true, true));
            l.surfaces.Add(new SurfaceData("ventana_noche", "NightWindow", 0.49f, 0.480f, 0.10f, 0.190f, true, true, true));
            l.surfaces.Add(new SurfaceData("edificio_der", "Building", 0.75f, 0.430f, 0.08f, 0.160f, true, true, true));

            float[,] hooks = { { .06f, .55f }, { .13f, .72f }, { .22f, .85f }, { .36f, .77f }, { .45f, .90f },
                               { .60f, .80f }, { .68f, .93f }, { .80f, .76f }, { .92f, .87f } };
            for (int i = 0; i < hooks.GetLength(0); i++) l.hooks.Add(new Pt(hooks[i, 0], hooks[i, 1]));

            l.enemies.Add(new EnemyData("chaser", 0.70f, 0.280f));
            l.enemies.Add(new EnemyData("bomber", 0.50f, 0.900f));
            l.enemies.Add(new EnemyData("patrol", 0.79f, 0.590f));

            float[,] tokens = { { .15f, .40f }, { .32f, .66f }, { .32f, .47f }, { .54f, .74f }, { .54f, .55f }, { .79f, .66f },
                                { .45f, .36f }, { .90f, .40f }, { .20f, .95f }, { .62f, .96f }, { .95f, .66f }, { .05f, .36f } };
            for (int i = 0; i < tokens.GetLength(0); i++) l.tokens.Add(new Pt(tokens[i, 0], tokens[i, 1]));
            return l;
        }

        public static WallLayout Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var l = FromJson(File.ReadAllText(FilePath));
                    if (l != null) return l;
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Tejedor] No se pudo leer la disposición: " + e.Message); }
            return CreateDefault();
        }

        public static WallLayout FromJson(string json)
        {
            var l = JsonUtility.FromJson<WallLayout>(json);
            if (l == null || l.surfaces == null || l.surfaces.Count == 0) return null;
            if (l.hooks == null) l.hooks = new List<Pt>();
            if (l.enemies == null) l.enemies = new List<EnemyData>();
            if (l.tokens == null) l.tokens = new List<Pt>();
            if (l.goal <= 0) l.goal = 10;
            return l;
        }

        public bool SaveTo(string path)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(this, true)); return true; }
            catch (System.Exception e) { Debug.LogWarning("[Tejedor] No se pudo guardar: " + e.Message); return false; }
        }

        public bool Save() { return SaveTo(FilePath); }
    }
}
