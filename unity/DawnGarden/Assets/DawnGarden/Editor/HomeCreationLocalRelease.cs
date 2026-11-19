using System;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace DawnGarden.Editor
{
    public static class HomeCreationLocalRelease
    {
        public static string Open(string scenePath,string expectedHash)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before loading a new release.");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save current scene first.");
            if(!scenePath.StartsWith("Assets/DawnGarden/PreparedHomes/") || GardenVerification.FileHash(scenePath)!=expectedHash)
                throw new InvalidOperationException("Accepted scene no longer matches release.");
            EditorSceneManager.OpenScene(scenePath);
            return "Local home scene installed. Enter Play Mode to continue the existing home save.";
        }
    }
}
