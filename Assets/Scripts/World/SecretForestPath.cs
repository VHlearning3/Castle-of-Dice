using UnityEngine.SceneManagement;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Zone 2.5: the short hidden forest trail behind the Rogue's lockpick gate on the Forest Path. It counts
    /// as the Forest for music and the map, and ends at a secret side door into the Library.
    /// </summary>
    public static class SecretForestPath
    {
        /// <summary>Scene file name (Assets/Scenes/Zone_2.5_SecretForestPath.unity).</summary>
        public const string SceneName = "Zone_2.5_SecretForestPath";

        /// <summary>Name on the zone banner and on the signs that lead here.</summary>
        public const string DisplayName = "Hidden Trail";

        /// <summary>Zone banner subtitle.</summary>
        public const string Subtitle = "A Secret Way to the Grand Archives";

        /// <summary>Landing spot when coming through the Forest Path's secret gate.</summary>
        public const string SpawnFromForest = "Spawn_From_ForestPath";

        /// <summary>Landing spot when coming back out of the Library's secret side door.</summary>
        public const string SpawnFromLibrary = "Spawn_From_Library";

        /// <summary>Forest Path landing spot just inside the secret gate, coming back from the trail.</summary>
        public const string ForestSpawnFromTrail = "Spawn_From_SecretPath";

        /// <summary>Library landing spot in front of the secret side door.</summary>
        public const string LibrarySpawnFromTrail = "Spawn_From_SecretForestPath";

        /// <summary>True for this zone's scene name.</summary>
        public static bool IsScene(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName) && sceneName.Contains("SecretForestPath");
        }

        /// <summary>True while the hero is on the hidden trail.</summary>
        public static bool IsActive => IsScene(SceneManager.GetActiveScene().name);
    }
}
