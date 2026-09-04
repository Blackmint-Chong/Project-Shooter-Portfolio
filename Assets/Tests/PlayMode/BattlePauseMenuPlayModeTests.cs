using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using UnityEngine.UI;

public sealed class BattlePauseMenuPlayModeTests
{
    private const string TestSceneName = "TestScene";
    private const string GameSetupScenePath =
        "Assets/Scenes/GameSetup.unity";

    private MatchController matchController;
    private BattlePauseMenu pauseMenu;
    private PlayerInputCommandSource humanInput;
    private AIPlayerCommandSource aiInput;
    private PlayerLife humanPlayer;
    private PlayerLife aiPlayer;
    private Keyboard keyboard;
    private bool createdKeyboard;

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
        yield return null;

        matchController = Object.FindFirstObjectByType<MatchController>();
        pauseMenu = Object.FindFirstObjectByType<BattlePauseMenu>();
        humanInput = Object.FindFirstObjectByType<PlayerInputCommandSource>();
        aiInput = Object.FindFirstObjectByType<AIPlayerCommandSource>();

        Assert.That(matchController, Is.Not.Null);
        Assert.That(pauseMenu, Is.Not.Null);
        Assert.That(humanInput, Is.Not.Null);
        Assert.That(aiInput, Is.Not.Null);
        Assert.That(matchController.IsMatchRunning, Is.True);
        Assert.That(humanInput.enabled, Is.True);
        Assert.That(aiInput.enabled, Is.True);

        humanPlayer = humanInput.GetComponent<PlayerLife>();
        aiPlayer = aiInput.GetComponent<PlayerLife>();
        Assert.That(humanPlayer, Is.Not.Null);
        Assert.That(aiPlayer, Is.Not.Null);

        keyboard = Keyboard.current;
        if (keyboard == null)
        {
            keyboard = InputSystem.AddDevice<Keyboard>();
            createdKeyboard = true;
        }

        RecycleSpawnedBullets();
    }

    [TearDown]
    public void TearDown()
    {
        CompleteActiveCountdown();

        if (pauseMenu != null && pauseMenu.IsPaused)
        {
            pauseMenu.ResumeBattle();
        }

        Time.timeScale = 1f;
        if (keyboard != null && keyboard.added)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }

        if (createdKeyboard && keyboard != null && keyboard.added)
        {
            InputSystem.RemoveDevice(keyboard);
        }
    }

    [UnityTest]
    public IEnumerator EscapeTogglesPauseAndLocksBothInputSources()
    {
        Assert.That(pauseMenu.MatchController, Is.SameAs(matchController));
        Assert.That(pauseMenu.PauseInputAction.bindings, Has.Count.EqualTo(1));
        Assert.That(pauseMenu.PauseInputAction.bindings[0].effectivePath,
            Is.EqualTo("<Keyboard>/escape"));
        Assert.That(pauseMenu.IsPaused, Is.False);
        Assert.That(pauseMenu.IsVisible, Is.False);

        SetEscapePressed(true);

        Assert.That(pauseMenu.IsPaused, Is.True);
        Assert.That(pauseMenu.IsVisible, Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(0f));
        Assert.That(humanInput.enabled, Is.False);
        Assert.That(aiInput.enabled, Is.False);
        Assert.That(pauseMenu.AreParticipantInputsSuspended, Is.True);
        Assert.That(pauseMenu.TitleText.text, Is.EqualTo("PAUSED"));
        Assert.That(pauseMenu.OverlayBackground.color.r,
            Is.EqualTo(0f).Within(0.001f));
        Assert.That(pauseMenu.OverlayBackground.color.g,
            Is.EqualTo(0f).Within(0.001f));
        Assert.That(pauseMenu.OverlayBackground.color.b,
            Is.EqualTo(0f).Within(0.001f));
        Assert.That(pauseMenu.OverlayBackground.color.a,
            Is.GreaterThan(0f).And.LessThan(1f));
        Assert.That(pauseMenu.OverlayBackground.raycastTarget, Is.True);
        Assert.That(pauseMenu.OverlayBackground.rectTransform.anchorMin,
            Is.EqualTo(Vector2.zero)
                .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(pauseMenu.OverlayBackground.rectTransform.anchorMax,
            Is.EqualTo(Vector2.one)
                .Using(Vector2ComparerWithEqualsOperator.Instance));
        AssertButton(pauseMenu.ResumeButton, "RESUME");
        AssertButton(pauseMenu.RestartButton, "RESTART");
        AssertButton(pauseMenu.ChangeSetupButton, "CHANGE SETUP");

        Vector2 humanPosition = humanPlayer.transform.position;
        Vector2 aiPosition = aiPlayer.transform.position;
        yield return new WaitForSecondsRealtime(0.05f);
        Assert.That((Vector2)humanPlayer.transform.position,
            Is.EqualTo(humanPosition)
                .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That((Vector2)aiPlayer.transform.position,
            Is.EqualTo(aiPosition)
                .Using(Vector2ComparerWithEqualsOperator.Instance));

        SetEscapePressed(false);
        SetEscapePressed(true);

        Assert.That(pauseMenu.IsPaused, Is.False);
        Assert.That(pauseMenu.IsVisible, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(humanInput.enabled, Is.True);
        Assert.That(aiInput.enabled, Is.True);
        Assert.That(pauseMenu.AreParticipantInputsSuspended, Is.False);
        SetEscapePressed(false);

        Assert.That(pauseMenu.PauseBattle(), Is.True);
        pauseMenu.ResumeButton.onClick.Invoke();
        Assert.That(pauseMenu.IsPaused, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator RestartButtonResetsRoundAndRecyclesExistingBullets()
    {
        BattleMapContext mapContext =
            Object.FindFirstObjectByType<BattleMapContext>();
        Assert.That(mapContext, Is.Not.Null);
        Assert.That(mapContext.TryGetSpawnSlot(
            humanPlayer,
            out BattleSpawnSlot humanSpawnSlot), Is.True);
        Assert.That(mapContext.TryGetSpawnSlot(
            aiPlayer,
            out BattleSpawnSlot aiSpawnSlot), Is.True);
        Assert.That(humanSpawnSlot.InitialPosition,
            Is.Not.EqualTo(aiSpawnSlot.InitialPosition)
                .Using(Vector2ComparerWithEqualsOperator.Instance));

        Assert.That(humanPlayer.TryLoseLife(), Is.True);
        Assert.That(humanPlayer.Life,
            Is.LessThan(humanPlayer.StartingLife));
        Assert.That(pauseMenu.PauseBattle(), Is.True);

        Vector2 sharedPosition = new(-9f, 5f);
        MoveParticipant(humanPlayer, sharedPosition, Vector2.right);
        MoveParticipant(aiPlayer, sharedPosition, Vector2.left);

        BulletPool bulletPool = Object.FindFirstObjectByType<BulletPool>();
        Assert.That(bulletPool, Is.Not.Null);
        Bullet staleBullet = bulletPool.Spawn(
            Vector2.zero,
            Vector2.right,
            humanPlayer.gameObject);
        Assert.That(staleBullet, Is.Not.Null);
        Assert.That(staleBullet.IsSpawned, Is.True);

        pauseMenu.RestartButton.onClick.Invoke();

        BattleCountdown countdown =
            Object.FindFirstObjectByType<BattleCountdown>();
        Assert.That(countdown, Is.Not.Null);
        Assert.That(matchController.State, Is.EqualTo(MatchState.Countdown));
        Assert.That(countdown.IsCountingDown, Is.True);
        Assert.That(matchController.Winner, Is.Null);
        Assert.That(humanPlayer.Life, Is.EqualTo(humanPlayer.StartingLife));
        Assert.That(aiPlayer.Life, Is.EqualTo(aiPlayer.StartingLife));
        AssertParticipantRestartedAt(humanPlayer, humanSpawnSlot);
        AssertParticipantRestartedAt(aiPlayer, aiSpawnSlot);
        Assert.That(staleBullet.IsSpawned, Is.False);
        Assert.That(pauseMenu.IsPaused, Is.False);
        Assert.That(pauseMenu.IsVisible, Is.False);
        Assert.That(pauseMenu.IsTransitioning, Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(0f));
        Assert.That(humanInput.enabled, Is.False);
        Assert.That(aiInput.enabled, Is.False);

        countdown.CompleteImmediately();

        Assert.That(matchController.IsMatchRunning, Is.True);
        Assert.That(pauseMenu.IsTransitioning, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(humanInput.enabled, Is.True);
        Assert.That(aiInput.enabled, Is.True);
        yield return null;
    }

    private static void MoveParticipant(
        PlayerLife participant,
        Vector2 position,
        Vector2 velocity)
    {
        Rigidbody2D body = participant.GetComponent<Rigidbody2D>();
        Assert.That(body, Is.Not.Null);
        participant.transform.position = position;
        body.position = position;
        body.linearVelocity = velocity;
        body.angularVelocity = 1f;
    }

    private static void AssertParticipantRestartedAt(
        PlayerLife participant,
        BattleSpawnSlot spawnSlot)
    {
        Rigidbody2D body = participant.GetComponent<Rigidbody2D>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body.position,
            Is.EqualTo(spawnSlot.InitialPosition)
                .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(body.linearVelocity,
            Is.EqualTo(Vector2.zero)
                .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(body.angularVelocity, Is.EqualTo(0f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator ChangeSetupButtonRestoresTimeAndLoadsGameSetup()
    {
        Assert.That(pauseMenu.PauseBattle(), Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(0f));

        pauseMenu.ChangeSetupButton.onClick.Invoke();
        yield return WaitForActiveScene(GameSetupScenePath);

        Assert.That(SceneManager.GetActiveScene().path,
            Is.EqualTo(GameSetupScenePath));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator MatchEndClosesPauseAndPreventsReopening()
    {
        Assert.That(pauseMenu.PauseBattle(), Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(0f));

        while (aiPlayer.Life > 1)
        {
            Assert.That(aiPlayer.TryLoseLife(), Is.True);
        }

        Assert.That(matchController.ReportPlayerFall(aiPlayer), Is.True);
        for (int frame = 0;
             frame < 4 && !matchController.IsMatchFinished;
             frame++)
        {
            yield return null;
        }

        Assert.That(matchController.IsMatchFinished, Is.True);
        MatchResultMenu resultMenu =
            Object.FindFirstObjectByType<MatchResultMenu>();
        Assert.That(resultMenu, Is.Not.Null);
        Assert.That(resultMenu.IsVisible, Is.True);
        Assert.That(humanInput.enabled, Is.False,
            "The result menu must retain the human input lock after pause closes.");
        Assert.That(pauseMenu.PauseBattle(), Is.False);
        Assert.That(pauseMenu.IsPaused, Is.False);
        Assert.That(pauseMenu.IsVisible, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    private void SetEscapePressed(bool pressed)
    {
        KeyboardState state = pressed
            ? new KeyboardState(Key.Escape)
            : new KeyboardState();
        InputSystem.QueueStateEvent(keyboard, state);
        InputSystem.Update();
    }

    private static void AssertButton(Button button, string expectedLabel)
    {
        Assert.That(button, Is.Not.Null);
        Text label = button.GetComponentInChildren<Text>(true);
        Assert.That(label, Is.Not.Null);
        Assert.That(label.text, Is.EqualTo(expectedLabel));
        Assert.That(button.interactable, Is.True);
    }

    private static IEnumerator WaitForActiveScene(string scenePath)
    {
        const int maximumFrameCount = 120;
        for (int frame = 0;
             frame < maximumFrameCount
             && SceneManager.GetActiveScene().path != scenePath;
             frame++)
        {
            yield return null;
        }
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
