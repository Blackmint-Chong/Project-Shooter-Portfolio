using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuSceneEditorTests
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string GameSetupScenePath =
        "Assets/Scenes/GameSetup.unity";
    private const string FactoryMapScenePath =
        "Assets/Scenes/Battle/FactoryMap.unity";
    private const string MapCatalogPath =
        "Assets/Data/Maps/BattleMapCatalog.asset";
    private const string AIDifficultyCatalogPath =
        "Assets/Data/AI/AIDifficultyCatalog.asset";
    private const string PlayerPreviewSpritePath =
        "Assets/Art/player/1.png";
    private const string PlayerPrefabPath =
        "Assets/Prefabs/Player/PF_Player.prefab";

    [Test]
    public void MainMenuIsFirstBuildSceneAndOpensGameSetup()
    {
        EditorBuildSettingsScene[] enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .ToArray();

        Assert.That(enabledScenes, Is.Not.Empty);
        Assert.That(enabledScenes[0].path, Is.EqualTo(MainMenuScenePath));
        Assert.That(enabledScenes[1].path, Is.EqualTo(GameSetupScenePath));
        Assert.That(enabledScenes.Select(scene => scene.path),
            Does.Contain(FactoryMapScenePath));
        Assert.That(PlayerSettings.defaultScreenWidth, Is.EqualTo(1280));
        Assert.That(PlayerSettings.defaultScreenHeight, Is.EqualTo(720));
        Assert.That(
            PlayerSettings.fullScreenMode,
            Is.EqualTo(FullScreenMode.Windowed));
        Assert.That(PlayerSettings.resizableWindow, Is.False);
        Assert.That(PlayerSettings.allowFullscreenSwitch, Is.False);
        Assert.That(PlayerSettings.defaultWebScreenWidth, Is.EqualTo(960));
        Assert.That(PlayerSettings.defaultWebScreenHeight, Is.EqualTo(540));

        Scene menuScene = SceneManager.GetSceneByPath(MainMenuScenePath);
        bool openedByTest = !menuScene.IsValid() || !menuScene.isLoaded;
        if (openedByTest)
        {
            menuScene = EditorSceneManager.OpenScene(
                MainMenuScenePath,
                OpenSceneMode.Additive);
        }

        try
        {
            Canvas[] canvases = GetComponentsInScene<Canvas>(menuScene);
            Button[] buttons = GetComponentsInScene<Button>(menuScene);
            EventSystem[] eventSystems =
                GetComponentsInScene<EventSystem>(menuScene);
            Camera[] cameras = GetComponentsInScene<Camera>(menuScene);
            MainMenuController[] controllers =
                GetComponentsInScene<MainMenuController>(menuScene);

            Assert.That(cameras, Has.Length.EqualTo(1));
            Assert.That(cameras[0].gameObject.activeSelf, Is.True);
            Assert.That(cameras[0].isActiveAndEnabled, Is.True);
            Assert.That(cameras[0].targetDisplay, Is.Zero);
            Assert.That(cameras[0].targetTexture, Is.Null);
            Assert.That(cameras[0].CompareTag("MainCamera"), Is.True);
            Assert.That(cameras[0].GetComponent<AudioListener>(), Is.Not.Null);

            Assert.That(canvases, Has.Length.EqualTo(1));
            Assert.That(canvases[0].renderMode,
                Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            CanvasScaler scaler = canvases[0].GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null);
            Assert.That(scaler.uiScaleMode,
                Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution,
                Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(canvases[0].GetComponent<GraphicRaycaster>(),
                Is.Not.Null);

            Assert.That(buttons, Has.Length.EqualTo(1));
            Button startButton = buttons[0];
            Assert.That(startButton.name, Is.EqualTo("StartButton"));
            Assert.That(startButton.gameObject.activeSelf, Is.True);
            Assert.That(startButton.interactable, Is.True);
            Assert.That(startButton.transform.IsChildOf(canvases[0].transform),
                Is.True);
            Assert.That(startButton.targetGraphic,
                Is.SameAs(startButton.GetComponent<Image>()));
            Assert.That(startButton.GetComponentInChildren<Text>().text,
                Is.EqualTo("START"));

            Assert.That(controllers, Has.Length.EqualTo(1));
            Assert.That(controllers[0].StartButton, Is.SameAs(startButton));
            Assert.That(controllers[0].GameSetupScenePath,
                Is.EqualTo(GameSetupScenePath));

            Assert.That(eventSystems, Has.Length.EqualTo(1));
            Assert.That(eventSystems[0].firstSelectedGameObject,
                Is.SameAs(startButton.gameObject));
            InputSystemUIInputModule inputModule =
                eventSystems[0].GetComponent<InputSystemUIInputModule>();
            Assert.That(inputModule, Is.Not.Null);
            Assert.That(inputModule.actionsAsset, Is.Not.Null);
        }
        finally
        {
            if (openedByTest)
            {
                EditorSceneManager.CloseScene(menuScene, true);
            }
        }
    }

    [Test]
    public void GameSetupSceneContainsCatalogViewCameraAndInputSystem()
    {
        Scene setupScene = SceneManager.GetSceneByPath(GameSetupScenePath);
        bool openedByTest = !setupScene.IsValid() || !setupScene.isLoaded;
        if (openedByTest)
        {
            setupScene = EditorSceneManager.OpenScene(
                GameSetupScenePath,
                OpenSceneMode.Additive);
        }

        try
        {
            Camera[] cameras = GetComponentsInScene<Camera>(setupScene);
            Canvas[] canvases = GetComponentsInScene<Canvas>(setupScene);
            EventSystem[] eventSystems =
                GetComponentsInScene<EventSystem>(setupScene);
            GameSetupController[] controllers =
                GetComponentsInScene<GameSetupController>(setupScene);
            GameSetupView[] views =
                GetComponentsInScene<GameSetupView>(setupScene);

            Assert.That(cameras, Has.Length.EqualTo(1));
            Assert.That(cameras[0].CompareTag("MainCamera"), Is.True);
            Assert.That(cameras[0].isActiveAndEnabled, Is.True);
            Assert.That(cameras[0].targetDisplay, Is.Zero);
            Assert.That(cameras[0].targetTexture, Is.Null);
            Assert.That(cameras[0].GetComponent<AudioListener>(), Is.Not.Null);

            Assert.That(canvases, Has.Length.EqualTo(1));
            Assert.That(canvases[0].renderMode,
                Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            CanvasScaler scaler = canvases[0].GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null);
            Assert.That(scaler.referenceResolution,
                Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(canvases[0].GetComponent<GraphicRaycaster>(),
                Is.Not.Null);

            Assert.That(controllers, Has.Length.EqualTo(1));
            Assert.That(views, Has.Length.EqualTo(1));
            Assert.That(controllers[0].View, Is.SameAs(views[0]));
            Sprite playerPreviewSprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(PlayerPreviewSpritePath);
            Assert.That(playerPreviewSprite, Is.Not.Null);
            Assert.That(views[0].PlayerPreviewSprite,
                Is.SameAs(playerPreviewSprite));
            Assert.That(
                AssetDatabase.GetAssetPath(views[0].PlayerPreviewSprite),
                Is.EqualTo(PlayerPreviewSpritePath));

            GameObject playerPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(playerPrefab, Is.Not.Null);
            PlayerController2D playerController =
                playerPrefab.GetComponent<PlayerController2D>();
            Assert.That(playerController, Is.Not.Null);
            Sprite groundedPlayerSprite = new SerializedObject(playerController)
                .FindProperty("groundedSprite")
                .objectReferenceValue as Sprite;
            Assert.That(groundedPlayerSprite, Is.Not.Null);
            Assert.That(views[0].PlayerPreviewSprite,
                Is.SameAs(groundedPlayerSprite),
                "The setup preview must match the human player's grounded sprite.");
            Assert.That(GameSetupView.PlayerSlotCapacity, Is.EqualTo(1));
            Assert.That(GameSetupView.AISlotCapacity, Is.EqualTo(1));

            BattleMapCatalog mapCatalog =
                AssetDatabase.LoadAssetAtPath<BattleMapCatalog>(MapCatalogPath);
            Assert.That(mapCatalog, Is.Not.Null);
            Assert.That(mapCatalog.TryValidate(out string catalogError), Is.True,
                catalogError);
            Assert.That(mapCatalog.DefaultMap.ScenePath,
                Is.EqualTo(FactoryMapScenePath));
            Assert.That(mapCatalog.DefaultMap.Thumbnail, Is.Not.Null);
            Assert.That(controllers[0].MapCatalog, Is.SameAs(mapCatalog));

            AIDifficultyCatalog aiDifficultyCatalog =
                AssetDatabase.LoadAssetAtPath<AIDifficultyCatalog>(
                    AIDifficultyCatalogPath);
            Assert.That(aiDifficultyCatalog, Is.Not.Null);
            Assert.That(aiDifficultyCatalog.TryValidate(
                out string difficultyError), Is.True,
                difficultyError);
            Assert.That(aiDifficultyCatalog.Profiles, Has.Count.EqualTo(1));
            Assert.That(aiDifficultyCatalog.DefaultProfile.DifficultyId,
                Is.EqualTo("basic"));
            Assert.That(controllers[0].AIDifficultyCatalog,
                Is.SameAs(aiDifficultyCatalog));

            Assert.That(eventSystems, Has.Length.EqualTo(1));
            InputSystemUIInputModule inputModule =
                eventSystems[0].GetComponent<InputSystemUIInputModule>();
            Assert.That(inputModule, Is.Not.Null);
            Assert.That(inputModule.actionsAsset, Is.Not.Null);
        }
        finally
        {
            if (openedByTest)
            {
                EditorSceneManager.CloseScene(setupScene, true);
            }
        }
    }

    private static T[] GetComponentsInScene<T>(Scene scene)
        where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
    }
}
