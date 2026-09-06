using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only onboarding for a fresh clone; never included in a player build.
[InitializeOnLoad]
internal static class PortfolioStartupScene
{
    private const string StartupScenePath = "Assets/Scenes/MainMenu.unity";
    private const string MarkerPath = "Library/PortfolioStartupScene.initialized";
    private const string SessionKey = "PortfolioStartupScene.Checked";

    static PortfolioStartupScene()
    {
        if (Application.isBatchMode || AssetDatabase.IsAssetImportWorkerProcess())
            return;

        EditorApplication.delayCall += OpenOnFirstUse;
    }

    private static void OpenOnFirstUse()
    {
        if (SessionState.GetBool(SessionKey, false) || File.Exists(MarkerPath))
            return;

        // Asset imports and script reloads must finish before loading a scene.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += OpenOnFirstUse;
            return;
        }

        SessionState.SetBool(SessionKey, true);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        // Existing scenes, prefab editing and unsaved changes belong to the user.
        Scene currentScene = SceneManager.GetActiveScene();
        bool canOpen = SceneManager.sceneCount == 1
            && currentScene.IsValid()
            && currentScene.isLoaded
            && string.IsNullOrEmpty(currentScene.path)
            && !currentScene.isDirty
            && PrefabStageUtility.GetCurrentPrefabStage() == null;

        if (canOpen)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(StartupScenePath) == null)
            {
                Debug.LogWarning("Open the startup scene manually: " + StartupScenePath);
                return;
            }

            EditorSceneManager.OpenScene(StartupScenePath, OpenSceneMode.Single);
        }

        // Keep the decision local to this clone, across future editor sessions.
        try
        {
            File.WriteAllText(MarkerPath, StartupScenePath);
        }
        catch (IOException exception)
        {
            Debug.LogWarning("Could not save the startup scene preference: " + exception.Message);
        }
        catch (System.UnauthorizedAccessException exception)
        {
            Debug.LogWarning("Could not save the startup scene preference: " + exception.Message);
        }
    }

    [MenuItem("Portfolio/Open Startup Scene", priority = 0)]
    private static void OpenStartupScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(StartupScenePath, OpenSceneMode.Single);
    }

    [MenuItem("Portfolio/Open Startup Scene", true)]
    private static bool CanOpenStartupScene()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorApplication.isCompiling;
    }
}
