using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TejedorDePared.EditorTools
{
    /// <summary>Menú "Tejedor" en la barra de Unity.</summary>
    public static class TejedorMenu
    {
        const string ScenePath = "Assets/TejedorDePared/TejedorDePared.unity";

        [MenuItem("Tejedor/1. Crear escena del juego", priority = 1)]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Tejedor de Pared").AddComponent<Game>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.Refresh();
            Debug.Log("[Tejedor] Escena creada en " + ScenePath + " y agregada a Build Profiles. Dale Play.");
        }

        [MenuItem("Tejedor/2. Configurar para proyector (1920×1080, pantalla completa)", priority = 2)]
        public static void ConfigurePlayer()
        {
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;
            PlayerSettings.productName = "Tejedor de Pared";
            Debug.Log("[Tejedor] Player configurado: 1920×1080, pantalla completa, sigue corriendo en segundo plano.");
        }

        [MenuItem("Tejedor/Abrir carpeta de la disposición guardada", priority = 20)]
        public static void OpenDataFolder()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }

        [MenuItem("Tejedor/Borrar calibración guardada", priority = 21)]
        public static void ClearSaved()
        {
            if (!EditorUtility.DisplayDialog("Tejedor", "¿Borrar la disposición y el corner-pin guardados? Se vuelve a la pared de ejemplo.", "Borrar", "Cancelar")) return;
            var f = System.IO.Path.Combine(Application.persistentDataPath, "pared_layout.json");
            if (System.IO.File.Exists(f)) System.IO.File.Delete(f);
            PlayerPrefs.DeleteKey("tejedor.pin");
            PlayerPrefs.DeleteKey("tejedor.realwall");
            PlayerPrefs.Save();
            Debug.Log("[Tejedor] Calibración borrada.");
        }
    }
}
