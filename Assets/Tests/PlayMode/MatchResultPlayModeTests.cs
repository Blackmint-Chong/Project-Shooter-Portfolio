using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using UnityEngine.UI;

public sealed class MatchResultPlayModeTests
{
    private const string TestSceneName = "TestScene";

    private MatchController matchController;
    private MatchLifeHud hud;
    private MatchResultMenu resultMenu;
    private PlayerLife humanPlayer;
    private PlayerLife aiPlayer;
    private PlayerInputCommandSource humanInputSource;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Time.timeScale = 1f;
        AsyncOperation load = SceneManager.LoadSceneAsync(
            TestSceneName,
            LoadSceneMode.Single);
        while (!load.isDone)
        {
            yield return null;
        }

        yield return null;
        CompleteActiveCountdown();

        AIPlayerCommandSource[] aiSources =
            Object.FindObjectsByType<AIPlayerCommandSource>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        PlayerInputCommandSource[] humanSources =
            Object.FindObjectsByType<PlayerInputCommandSource>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        Assert.That(aiSources, Has.Length.EqualTo(1));
        Assert.That(humanSources, Has.Length.EqualTo(1));

        aiPlayer = aiSources[0].GetComponent<PlayerLife>();
        humanInputSource = humanSources[0];
        humanPlayer = humanInputSource.GetComponent<PlayerLife>();
        aiSources[0].enabled = false;
        humanInputSource.enabled = false;
        RecycleSpawnedBullets();

        yield return null;

        matchController = Object.FindFirstObjectByType<MatchController>();
        hud = Object.FindFirstObjectByType<MatchLifeHud>();
        resultMenu = Object.FindFirstObjectByType<MatchResultMenu>();

        Assert.That(matchController, Is.Not.Null);
        Assert.That(hud, Is.Not.Null);
        Assert.That(resultMenu, Is.Not.Null);
        Assert.That(matchController.State, Is.EqualTo(MatchState.Playing));
        Assert.That(matchController.Participants, Has.Count.EqualTo(2));
        Assert.That(matchController.IsParticipant(humanPlayer), Is.True);
        Assert.That(matchController.IsParticipant(aiPlayer), Is.True);

        hud.Refresh();
        Assert.That(hud.Result, Is.EqualTo(PlayerMatchResult.None));
        Assert.That(hud.IsResultVisible, Is.False);
        Assert.That(resultMenu.IsVisible, Is.False);
    }

    [TearDown]
    public void TearDown()
    {
        CompleteActiveCountdown();
        Time.timeScale = 1f;
    }

    [UnityTest]
    public IEnumerator ResultMenuBuildsTheExpectedOverlayAndActions()
    {
        Assert.That(resultMenu.MatchController, Is.SameAs(matchController));
        Assert.That(resultMenu.OverlayBackground, Is.Not.Null);
        Assert.That(resultMenu.OverlayBackground.color.r,
            Is.EqualTo(0f).Within(0.001f));
        Assert.That(resultMenu.OverlayBackground.color.g,
            Is.EqualTo(0f).Within(0.001f));
        Assert.That(resultMenu.OverlayBackground.color.b,
            Is.EqualTo(0f).Within(0.001f));
        Assert.That(resultMenu.OverlayBackground.color.a,
            Is.GreaterThan(0f).And.LessThan(1f));
        Assert.That(resultMenu.OverlayBackground.raycastTarget, Is.True);
        Assert.That(resultMenu.OverlayBackground.rectTransform.anchorMin,
            Is.EqualTo(Vector2.zero)
                .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(resultMenu.OverlayBackground.rectTransform.anchorMax,
            Is.EqualTo(Vector2.one)
                .Using(Vector2ComparerWithEqualsOperator.Instance));

        AssertButton(resultMenu.RematchButton, "REMATCH");
        AssertButton(resultMenu.ChangeSetupButton, "CHANGE SETUP");
        AssertButton(resultMenu.MainMenuButton, "MAIN MENU");
        Assert.That(Object.FindFirstObjectByType<EventSystem>(), Is.Not.Null);
        yield return null;
    }

    [UnityTest]
    public IEnumerator AiLastLifeDisplaysVictoryAndRestartClearsTheResult()
    {
        humanInputSource.enabled = true;
        ReduceToLastLife(aiPlayer);

        Assert.That(matchController.ReportPlayerFall(aiPlayer), Is.True);
        yield return WaitForMatchEnd();

        Assert.That(aiPlayer.Life, Is.Zero);
        Assert.That(humanPlayer.Life, Is.GreaterThan(0));
        Assert.That(matchController.Winner, Is.SameAs(humanPlayer));
        Assert.That(hud.Result, Is.EqualTo(PlayerMatchResult.Victory));
        Assert.That(hud.ResultDisplayText, Is.EqualTo("VICTORY"));
        Assert.That(hud.IsResultVisible, Is.True);
        AssertResultMenuVisible();
        Assert.That(humanInputSource.enabled, Is.False);
        Assert.That(resultMenu.IsPlayerInputSuspended, Is.True);
        Assert.That(hud.ResultDisplayTransform.anchorMin,
            Is.EqualTo(new Vector2(0.5f, 0.5f))
                .Using(Vector2ComparerWithEqualsOperator.Instance));

        BulletPool bulletPool = Object.FindFirstObjectByType<BulletPool>();
        Assert.That(bulletPool, Is.Not.Null);
        Bullet staleBullet = bulletPool.Spawn(
            Vector2.zero,
            Vector2.right,
            humanPlayer.gameObject);
        Assert.That(staleBullet, Is.Not.Null);
        Assert.That(staleBullet.IsSpawned, Is.True);

        resultMenu.RematchButton.onClick.Invoke();

        BattleCountdown countdown =
            Object.FindFirstObjectByType<BattleCountdown>();
        Assert.That(countdown, Is.Not.Null);
        Assert.That(matchController.State, Is.EqualTo(MatchState.Countdown));
        Assert.That(countdown.IsCountingDown, Is.True);
        Assert.That(matchController.Winner, Is.Null);
        Assert.That(humanPlayer.Life, Is.EqualTo(humanPlayer.StartingLife));
        Assert.That(aiPlayer.Life, Is.EqualTo(aiPlayer.StartingLife));
        Assert.That(humanPlayer.gameObject.activeSelf, Is.True);
        Assert.That(aiPlayer.gameObject.activeSelf, Is.True);
        Assert.That(resultMenu.IsVisible, Is.False);
        Assert.That(resultMenu.IsPlayerInputSuspended, Is.False);
        Assert.That(humanInputSource.enabled, Is.False);
        Assert.That(staleBullet.IsSpawned, Is.False,
            "Rematch must recycle projectiles left by the previous round.");
        Assert.That(Time.timeScale, Is.EqualTo(0f));

        countdown.CompleteImmediately();

        Assert.That(matchController.State, Is.EqualTo(MatchState.Playing));
        Assert.That(hud.Result, Is.EqualTo(PlayerMatchResult.None));
        Assert.That(hud.ResultDisplayText, Is.Empty);
        Assert.That(hud.IsResultVisible, Is.False);
        Assert.That(humanInputSource.enabled, Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator HumanLastLifeDisplaysDefeat()
    {
        ReduceToLastLife(humanPlayer);

        Assert.That(matchController.ReportPlayerFall(humanPlayer), Is.True);
        yield return WaitForMatchEnd();

        Assert.That(humanPlayer.Life, Is.Zero);
        Assert.That(humanPlayer.gameObject.activeSelf, Is.False);
        Assert.That(matchController.Winner, Is.SameAs(aiPlayer));
        Assert.That(hud.LeftParticipant, Is.SameAs(humanPlayer));
        Assert.That(hud.Result, Is.EqualTo(PlayerMatchResult.Defeat));
        Assert.That(hud.ResultDisplayText, Is.EqualTo("DEFEAT"));
        Assert.That(hud.IsResultVisible, Is.True);
        AssertResultMenuVisible();
    }

    [UnityTest]
    public IEnumerator SimultaneousEliminationIsDefeatWhenHumanLifeIsZero()
    {
        ReduceToLastLife(humanPlayer);
        ReduceToLastLife(aiPlayer);

        Assert.That(matchController.ReportPlayerFall(humanPlayer), Is.True);
        Assert.That(matchController.ReportPlayerFall(aiPlayer), Is.True);
        yield return WaitForMatchEnd();

        Assert.That(matchController.Winner, Is.Null);
        Assert.That(humanPlayer.Life, Is.Zero);
        Assert.That(aiPlayer.Life, Is.Zero);
        Assert.That(hud.Result, Is.EqualTo(PlayerMatchResult.Defeat));
        Assert.That(hud.ResultDisplayText, Is.EqualTo("DEFEAT"));
        Assert.That(hud.IsResultVisible, Is.True);
        AssertResultMenuVisible();
    }

    [UnityTest]
    public IEnumerator ChangeSetupButtonLoadsGameSetupScene()
    {
        ReduceToLastLife(aiPlayer);
        Assert.That(matchController.ReportPlayerFall(aiPlayer), Is.True);
        yield return WaitForMatchEnd();

        resultMenu.ChangeSetupButton.onClick.Invoke();
        yield return WaitForActiveScene("Assets/Scenes/GameSetup.unity");

        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator MainMenuButtonLoadsMainMenuScene()
    {
        ReduceToLastLife(aiPlayer);
        Assert.That(matchController.ReportPlayerFall(aiPlayer), Is.True);
        yield return WaitForMatchEnd();

        resultMenu.MainMenuButton.onClick.Invoke();
        yield return WaitForActiveScene("Assets/Scenes/MainMenu.unity");

        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    private static void ReduceToLastLife(PlayerLife participant)
    {
        while (participant.Life > 1)
        {
            Assert.That(participant.TryLoseLife(), Is.True);
        }

        Assert.That(participant.Life, Is.EqualTo(1));
    }

    private IEnumerator WaitForMatchEnd()
    {
        for (int i = 0; i < 4 && !matchController.IsMatchFinished; i++)
        {
            yield return null;
        }

        Assert.That(matchController.IsMatchFinished, Is.True);
    }

    private void AssertResultMenuVisible()
    {
        Assert.That(resultMenu.IsVisible, Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(1f),
            "The result menu must not pause scaled gameplay coroutines.");
        Assert.That(resultMenu.RematchButton.interactable, Is.True);
        Assert.That(resultMenu.ChangeSetupButton.interactable, Is.True);
        Assert.That(resultMenu.MainMenuButton.interactable, Is.True);
        Assert.That(hud.ResultDisplayTransform.GetSiblingIndex(),
            Is.GreaterThan(
                resultMenu.OverlayBackground.rectTransform.GetSiblingIndex()),
            "The result label must render above the dark overlay.");
    }

    private static void AssertButton(Button button, string expectedLabel)
    {
        Assert.That(button, Is.Not.Null);
        Text label = button.GetComponentInChildren<Text>(true);
        Assert.That(label, Is.Not.Null);
        Assert.That(label.text, Is.EqualTo(expectedLabel));
    }

    private static IEnumerator WaitForActiveScene(string expectedScenePath)
    {
        const int maximumFrameCount = 120;
        for (int frame = 0;
             frame < maximumFrameCount
             && SceneManager.GetActiveScene().path != expectedScenePath;
             frame++)
        {
            yield return null;
        }

        Assert.That(SceneManager.GetActiveScene().path,
            Is.EqualTo(expectedScenePath));
    }

    private static void RecycleSpawnedBullets()
    {
        Bullet[] bullets = Object.FindObjectsByType<Bullet>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (Bullet bullet in bullets)
        {
            if (bullet.IsSpawned)
            {
                bullet.RequestRecycle();
            }
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
