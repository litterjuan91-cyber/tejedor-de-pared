using UnityEngine;

namespace TejedorDePared
{
    /// <summary>
    /// Arranque automático: al darle Play en cualquier escena (incluso vacía) crea el juego.
    /// Si la escena ya tiene un objeto con el componente Game, no hace nada.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Object.FindFirstObjectByType<Game>() != null) return;
            new GameObject("Tejedor de Pared").AddComponent<Game>();
        }
    }
}
