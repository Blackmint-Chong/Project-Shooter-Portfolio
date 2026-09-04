using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class MainMenuPlayModeTests
{
    private const string GameSetupScenePath =
        "Assets/Scenes/GameSetup.unity";
    private const string FactoryMapScenePath =
        "Assets/Scenes/Battle/FactoryMap.unity";

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        CompleteActiveCountdown();
        Time.timeScale = 1f;
        BattleSession.Reset();
        yield return null;
    }

    [UnityTest]
    public IEnumerator MainMenuOpensSetupAndSetupStartsSelectedBattle()
    {
        BattleSession.Reset();
        AsyncOperation loadMenu = SceneManager.LoadSceneAsync(
            "MainMenu",
            LoadSceneMode.Single);
        Assert.That(loadMenu, Is.Not.Null);
        while (!loadMenu.isDone)
        {
            yield return null;
        }

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        Camera menuCamera = Camera.main;
        Assert.That(menuCamera, Is.Not.Null);
        Assert.That(menuCamera.gameObject.scene,
            Is.EqualTo(SceneManager.GetActiveScene()));
        Assert.That(menuCamera.isActiveAndEnabled, Is.True);
        Assert.That(menuCamera.targetDisplay, Is.Zero);
        Assert.That(menuCamera.targetTexture, Is.Null);

        MainMenuController menu =
            Object.FindFirstObjectByType<MainMenuController>();
        Assert.That(menu, Is.Not.Null);
        Button startButton = menu.StartButton;
        Assert.That(startButton, Is.Not.Null);
        Assert.That(startButton.interactable, Is.True);
        Assert.That(menu.GameSetupScenePath, Is.EqualTo(GameSetupScenePath));

        startButton.onClick.Invoke();
        yield return WaitForActiveScene(GameSetupScenePath);

        Assert.That(SceneManager.GetActiveScene().path,
            Is.EqualTo(GameSetupScenePath));
        Assert.That(SceneManager.GetActiveScene().name,
            Is.EqualTo("GameSetup"));
        Assert.That(Camera.main, Is.Not.Null);

        GameSetupController setup =
            Object.FindFirstObjectByType<GameSetupController>();
        GameSetupView view = Object.FindFirstObjectByType<GameSetupView>();
        Assert.That(setup, Is.Not.Null);
        Assert.That(view, Is.Not.Null);
        Assert.That(setup.Initialize(), Is.True);
        Assert.That(setup.View, Is.SameAs(view));
        Assert.That(setup.MapCatalog, Is.Not.Null);
        Assert.That(setup.AIDifficultyCatalog, Is.Not.Null);
        Assert.That(setup.AIDifficultyCatalog.TryValidate(
            out string difficultyCatalogError), Is.True,
            difficultyCatalogError);
        Assert.That(setup.SelectedAIDifficulty,
            Is.SameAs(setup.AIDifficultyCatalog.DefaultProfile));
        Assert.That(setup.SelectedMap, Is.SameAs(setup.MapCatalog.DefaultMap));
        Assert.That(setup.SelectedMap.ScenePath,
            Is.EqualTo(FactoryMapScenePath));
        Assert.That(setup.SelectedMap.Thumbnail, Is.Not.Null);
        Assert.That(view.SelectedMapThumbnail.sprite,
            Is.SameAs(setup.SelectedMap.Thumbnail));
        Assert.That(view.MapDropdown.options,
            Has.Count.EqualTo(setup.MapCatalog.Maps.Count));
        Assert.That(view.LeftSlotCount,
            Is.EqualTo(GameSetupView.PlayerSlotCapacity));
        Assert.That(view.PlayerPreviewSprite, Is.Not.Null);
        Assert.That(view.PlayerPreviewImage, Is.Not.Null);
        Assert.That(view.PlayerPreviewImage.sprite,
            Is.SameAs(view.PlayerPreviewSprite));
        Assert.That(view.PlayerPreviewImage.enabled, Is.True);
        Assert.That(view.PlayerPreviewImage.preserveAspect, Is.True);
        Assert.That(view.PlayerPreviewImage.raycastTarget, Is.False);
        Assert.That(view.RightSlotCount,
            Is.EqualTo(GameSetupView.AISlotCapacity));
        Assert.That(view.LeftSlotTexts, Has.Count.EqualTo(1));
        Assert.That(view.RightSlotTexts, Has.Count.EqualTo(1));
        Assert.That(view.AIDifficultyDropdown.gameObject.activeSelf,
            Is.True);
        Assert.That(view.AIDifficultyDropdown.options,
            Has.Count.EqualTo(setup.AIDifficultyCatalog.Profiles.Count));

        Transform setupCanvas = view.RuntimeCanvas.transform;
        Assert.That(
            setupCanvas.Find(
                "LeftTeamPanel/PlayerPreviewFrame/PlayerPreview"),
            Is.SameAs(view.PlayerPreviewImage.transform));
        Assert.That(setupCanvas.Find("Versus"), Is.Null);
        Assert.That(
            setupCanvas.Find("MapSelectionPanel/SelectedMapDescription"),
            Is.Null);
        Assert.That(
            setupCanvas.Find("MapSelectionPanel/DifficultyDropdown"),
            Is.Null,
            "AI difficulty must be selected in the AI participant panel.");
        Assert.That(
            setupCanvas.Find("LeftTeamPanel/TeamHeading")
                .GetComponent<Text>().text,
            Is.EqualTo("PLAYER"));
        Assert.That(
            setupCanvas.Find("RightTeamPanel/TeamHeading")
                .GetComponent<Text>().text,
            Is.EqualTo("AI"));
        Assert.That(setupCanvas.Find("LeftTeamPanel/Slot2"), Is.Null);
        Assert.That(setupCanvas.Find("RightTeamPanel/Slot2"), Is.Null);
        Assert.That(
            setupCanvas.Find("RightTeamPanel/Slot1/DifficultyDropdown"),
            Is.Not.Null);
        Transform difficultyDropdownItem = setupCanvas.Find(
            "RightTeamPanel/Slot1/DifficultyDropdown/Template/Viewport/Content/Item");
        Assert.That(difficultyDropdownItem, Is.Not.Null);
        Assert.That(difficultyDropdownItem.Find("ItemCheckmark"), Is.Null);
        Assert.That(
            difficultyDropdownItem.GetComponent<Toggle>().graphic,
            Is.Null,
            "The AI difficulty dropdown must not show a selection checkmark.");

        Assert.That(view.LeftSlotTexts[0].text, Does.Contain("PLAYER"));
        Assert.That(view.RightSlotTexts[0].text, Does.Contain("AI"));
        Assert.That(
            view.LeftSlotTexts[0].GetComponentInParent<Button>(),
            Is.Null,
            "The fixed Player team slot must not be selectable.");
        Assert.That(
            view.RightSlotTexts[0].GetComponentInParent<Button>(),
            Is.Null,
            "The fixed AI team slot must not be selectable.");

        AIDifficultyDefinition selectedDifficulty =
            setup.SelectedAIDifficulty;

        view.StartButton.onClick.Invoke();
        yield return WaitForActiveScene(FactoryMapScenePath);
        yield return null;

        Assert.That(SceneManager.GetActiveScene().path,
            Is.EqualTo(FactoryMapScenePath));
        Assert.That(SceneManager.GetActiveScene().name,
            Is.EqualTo("FactoryMap"));
        Assert.That(BattleSession.HasConfiguration, Is.True);
        Assert.That(BattleSession.Configuration.AIDifficultyProfile,
            Is.SameAs(selectedDifficulty));
        MatchController matchController =
            Object.FindFirstObjectByType<MatchController>();
        BattleCountdown countdown =
            Object.FindFirstObjectByType<BattleCountdown>();
        Assert.That(matchController, Is.Not.Null);
        Assert.That(countdown, Is.Not.Null);
        Assert.That(matchController.State, Is.EqualTo(MatchState.Countdown));
        Assert.That(countdown.IsVisible, Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(0f));

        PlayerInputCommandSource human =
            Object.FindFirstObjectByType<PlayerInputCommandSource>();
        AIPlayerCommandSource ai =
            Object.FindFirstObjectByType<AIPlayerCommandSource>();
        BattleMapContext mapContext =
            Object.FindFirstObjectByType<BattleMapContext>();
        Assert.That(human, Is.Not.Null);
        Assert.That(ai, Is.Not.Null);
        Assert.That(ai.DifficultyProfile, Is.SameAs(selectedDifficulty));
        Assert.That(mapContext, Is.Not.Null);
        Assert.That(mapContext.TryGetSideSpawnSlots(
            out BattleSpawnSlot leftSpawn,
            out BattleSpawnSlot rightSpawn), Is.True);
        Assert.That(
            human.GetComponent<BattleParticipantSlot>().Slot,
            Is.EqualTo(leftSpawn.Slot));
        Assert.That(
            ai.GetComponent<BattleParticipantSlot>().Slot,
            Is.EqualTo(rightSpawn.Slot));

        countdown.CompleteImmediately();
        Assert.That(matchController.State, Is.EqualTo(MatchState.Playing));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    private static IEnumerator WaitForActiveScene(string scenePath)
    {
        const int maximumFrames = 120;
        for (int frame = 0;
             frame < maximumFrames
             && SceneManager.GetActiveScene().path != scenePath;
             frame++)
        {
            yield return null;
        }
    }

    private static void CompleteActiveCountdown()
    {
        BattleCountdown countdown =
            Object.FindFirstObjectByType<BattleCountdown>();
        if (countdown != null && countdown.IsCountingDown)
        {
            countdown.CompleteImmediately();
        }
    }
}
