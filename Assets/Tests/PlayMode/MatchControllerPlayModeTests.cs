using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using UnityEngine.UI;

public sealed class MatchControllerPlayModeTests
{
    private const string TestSceneName = "TestScene";

    private readonly List<GameObject> temporaryObjects = new();

    private MatchController matchController;
    private PlayerLife scenePlayer;
    private PlayerLife sceneAiPlayer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        sceneAiPlayer = null;

        AsyncOperation load = SceneManager.LoadSceneAsync(TestSceneName, LoadSceneMode.Single);
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
        foreach (AIPlayerCommandSource aiSource in aiSources)
        {
            aiSource.enabled = false;
        }

        RecycleSpawnedBullets();
        yield return null;

        matchController = Object.FindFirstObjectByType<MatchController>();
        PlayerInputCommandSource humanInput =
            Object.FindFirstObjectByType<PlayerInputCommandSource>();

        Assert.That(matchController, Is.Not.Null);
        Assert.That(humanInput, Is.Not.Null);
        scenePlayer = humanInput.GetComponent<PlayerLife>();
        Assert.That(scenePlayer, Is.Not.Null);

        foreach (AIPlayerCommandSource aiSource in aiSources)
        {
            PlayerLife aiLife = aiSource.GetComponent<PlayerLife>();
            sceneAiPlayer ??= aiLife;
            if (matchController.IsParticipant(aiLife))
            {
                Assert.That(matchController.UnregisterParticipant(aiLife), Is.True);
            }
        }

        Assert.That(matchController.RestartMatch(), Is.True);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        CompleteActiveCountdown();
        Time.timeScale = 1f;

        foreach (GameObject temporaryObject in temporaryObjects)
        {
            if (temporaryObject != null)
            {
                Object.Destroy(temporaryObject);
            }
        }

        temporaryObjects.Clear();
        yield return null;
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

    [UnityTest]
    public IEnumerator SinglePlayerSceneStartsWithoutDeclaringAWinner()
    {
        Assert.That(matchController.State, Is.EqualTo(MatchState.Playing));
        Assert.That(matchController.IsMatchRunning, Is.True);
        Assert.That(matchController.IsMatchFinished, Is.False);
        Assert.That(matchController.Winner, Is.Null);
        Assert.That(matchController.Participants, Has.Count.EqualTo(1));
        Assert.That(matchController.IsParticipant(scenePlayer), Is.True);
        Assert.That(scenePlayer.Life, Is.EqualTo(scenePlayer.StartingLife));
        Assert.That(scenePlayer.gameObject.activeSelf, Is.True);
        yield return null;
    }

    [UnityTest]
    public IEnumerator LifeHudBindsBothParticipantsAndTracksTheirLives()
    {
        MatchLifeHud hud = Object.FindFirstObjectByType<MatchLifeHud>();

        Assert.That(hud, Is.Not.Null);
        hud.Bind(matchController, null);
        Assert.That(hud.MatchController, Is.SameAs(matchController));
        Assert.That(hud.GetComponent<Canvas>().renderMode,
            Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(hud.LeftParticipant, Is.SameAs(scenePlayer));
        Assert.That(hud.LeftDisplayText,
            Is.EqualTo($"LIFE {scenePlayer.StartingLife}"));
        Assert.That(hud.IsLeftSlotVisible, Is.True);
        BattleParticipantPresentation scenePlayerPresentation =
            scenePlayer.GetComponent<BattleParticipantPresentation>();
        Assert.That(scenePlayerPresentation, Is.Not.Null);
        Assert.That(hud.LeftPortrait, Is.Not.Null);
        Assert.That(hud.LeftPortrait.sprite,
            Is.SameAs(scenePlayerPresentation.Portrait));
        Assert.That(hud.IsLeftPortraitVisible, Is.True);
        Assert.That(hud.RightParticipant, Is.Null);
        Assert.That(hud.IsRightSlotVisible, Is.False);
        Assert.That(hud.RightPortrait, Is.Not.Null);
        Assert.That(hud.RightPortrait.sprite, Is.Null);
        Assert.That(hud.IsRightPortraitVisible, Is.False);
        Assert.That(hud.LeftDisplayBackground, Is.Not.Null);
        Assert.That(hud.RightDisplayBackground, Is.Not.Null);
        Assert.That(hud.LeftDisplayBackground.gameObject.activeSelf, Is.True);
        Assert.That(hud.RightDisplayBackground.gameObject.activeSelf, Is.False);
        Assert.That(hud.LeftDisplayBackground.color, Is.EqualTo(Color.white));
        Assert.That(hud.RightDisplayBackground.color, Is.EqualTo(Color.white));
        Assert.That(
            hud.LeftDisplayTransform.GetComponent<Text>().color,
            Is.EqualTo(Color.black));
        Assert.That(
            hud.RightDisplayTransform.GetComponent<Text>().color,
            Is.EqualTo(Color.black));
        Assert.That(hud.LeftDisplayTransform.anchorMin,
            Is.EqualTo(new Vector2(0f, 0f))
                .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(hud.RightDisplayTransform.anchorMin,
            Is.EqualTo(new Vector2(1f, 0f))
                .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(hud.LeftDisplayTransform.anchoredPosition.y,
            Is.GreaterThan(0f));
        Assert.That(hud.RightDisplayTransform.anchoredPosition.y,
            Is.GreaterThan(0f));

        PlayerLife opponent = sceneAiPlayer;
        Assert.That(opponent, Is.Not.Null);
        BattleParticipantPresentation opponentPresentation =
            opponent.GetComponent<BattleParticipantPresentation>();
        Assert.That(opponentPresentation, Is.Not.Null);
        Assert.That(matchController.RegisterParticipant(opponent), Is.True);

        Assert.That(hud.RightParticipant, Is.SameAs(opponent));
        Assert.That(hud.RightDisplayText,
            Is.EqualTo($"LIFE {opponent.StartingLife}"));
        Assert.That(hud.IsRightSlotVisible, Is.True);
        Assert.That(hud.RightDisplayBackground.gameObject.activeSelf, Is.True);
        Assert.That(hud.RightPortrait.sprite,
            Is.SameAs(opponentPresentation.Portrait));
        Assert.That(hud.IsRightPortraitVisible, Is.True);

        Assert.That(matchController.UnregisterParticipant(scenePlayer), Is.True);
        Assert.That(hud.LeftParticipant, Is.SameAs(opponent));
        Assert.That(hud.RightParticipant, Is.Null);
        Assert.That(hud.LeftPortrait.sprite,
            Is.SameAs(opponentPresentation.Portrait));
        Assert.That(hud.IsLeftPortraitVisible, Is.True);
        Assert.That(hud.RightPortrait.sprite, Is.Null);
        Assert.That(hud.IsRightPortraitVisible, Is.False);
        Assert.That(matchController.RegisterParticipant(scenePlayer), Is.True);
        Assert.That(hud.LeftParticipant, Is.SameAs(scenePlayer));
        Assert.That(hud.RightParticipant, Is.SameAs(opponent));
        Assert.That(hud.LeftPortrait.sprite,
            Is.SameAs(scenePlayerPresentation.Portrait));
        Assert.That(hud.RightPortrait.sprite,
            Is.SameAs(opponentPresentation.Portrait));

        Assert.That(matchController.ReportPlayerFall(opponent), Is.True);
        Assert.That(hud.RightDisplayText,
            Is.EqualTo($"LIFE {opponent.StartingLife - 1}"));
        Assert.That(hud.LeftDisplayText,
            Is.EqualTo($"LIFE {scenePlayer.StartingLife}"));

        Assert.That(matchController.ReportPlayerFall(scenePlayer), Is.True);
        Assert.That(hud.LeftDisplayText,
            Is.EqualTo($"LIFE {scenePlayer.StartingLife - 1}"));
        Assert.That(hud.RightDisplayText,
            Is.EqualTo($"LIFE {opponent.StartingLife - 1}"));

        Assert.That(matchController.UnregisterParticipant(opponent), Is.True);
        Assert.That(hud.RightParticipant, Is.Null);
        Assert.That(hud.IsRightSlotVisible, Is.False);
        Assert.That(hud.RightDisplayBackground.gameObject.activeSelf, Is.False);
        Assert.That(hud.RightPortrait.sprite, Is.Null);
        Assert.That(hud.IsRightPortraitVisible, Is.False);

        yield return null;

        Assert.That(matchController.IsMatchFinished, Is.True);
        Assert.That(matchController.RestartMatch(), Is.True);
        Assert.That(hud.LeftDisplayText,
            Is.EqualTo($"LIFE {scenePlayer.StartingLife}"));
        Assert.That(hud.LeftPortrait.sprite,
            Is.SameAs(scenePlayerPresentation.Portrait));
        Assert.That(hud.RightPortrait.sprite, Is.Null);
        Assert.That(hud.IsRightPortraitVisible, Is.False);
    }

    [UnityTest]
    public IEnumerator DuplicateFallReportsConsumeOneLifeAndScheduleOneRespawn()
    {
        int respawnCount = 0;
        matchController.PlayerRespawned += _ => respawnCount++;
        PlayerRespawner respawner = scenePlayer.GetComponent<PlayerRespawner>();
        scenePlayer.GetComponent<Rigidbody2D>().gravityScale = 0f;

        Assert.That(matchController.ReportPlayerFall(scenePlayer), Is.True);
        Assert.That(matchController.ReportPlayerFall(scenePlayer), Is.False);
        Assert.That(scenePlayer.Life, Is.EqualTo(2));
        Assert.That(scenePlayer.gameObject.activeSelf, Is.False);

        yield return new WaitForSeconds(respawner.RespawnDelay + 0.1f);

        Assert.That(scenePlayer.gameObject.activeSelf, Is.True);
        Assert.That(respawnCount, Is.EqualTo(1));
        Assert.That(scenePlayer.Life, Is.EqualTo(2));
        Assert.That((Vector2)scenePlayer.transform.position,
            Is.EqualTo(respawner.RespawnPosition)
                .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(matchController.State, Is.EqualTo(MatchState.Playing));

        Assert.That(matchController.ReportPlayerFall(scenePlayer), Is.True);
        Assert.That(scenePlayer.Life, Is.EqualTo(1));

        yield return new WaitForSeconds(respawner.RespawnDelay + 0.1f);

        Assert.That(scenePlayer.gameObject.activeSelf, Is.True);
        Assert.That(respawnCount, Is.EqualTo(2));
        Assert.That(scenePlayer.Life, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator FallRespawnActivatesShieldAndReducesIncomingImpulseByEightyPercent()
    {
        PlayerRespawner respawner = scenePlayer.GetComponent<PlayerRespawner>();
        PlayerRespawnShield shield = scenePlayer.GetComponent<PlayerRespawnShield>();
        BulletImpulseReceiver receiver = scenePlayer.GetComponent<BulletImpulseReceiver>();
        Rigidbody2D body = scenePlayer.GetComponent<Rigidbody2D>();
        bool shieldWasActiveWhenRespawnReported = false;
        bool barrierWasVisibleWhenRespawnReported = false;

        Assert.That(shield, Is.Not.Null);
        Assert.That(receiver, Is.Not.Null);
        Assert.That(shield.IsActive, Is.False);
        Assert.That(shield.IsBarrierVisible, Is.False);

        matchController.PlayerRespawned += participant =>
        {
            if (participant == scenePlayer)
            {
                shieldWasActiveWhenRespawnReported = shield.IsActive;
                barrierWasVisibleWhenRespawnReported =
                    shield.IsBarrierVisible;
            }
        };

        body.gravityScale = 0f;
        Assert.That(matchController.ReportPlayerFall(scenePlayer), Is.True);
        Assert.That(shield.IsActive, Is.False);
        Assert.That(shield.IsBarrierVisible, Is.False);

        yield return new WaitForSeconds(respawner.RespawnDelay + 0.1f);

        Assert.That(scenePlayer.gameObject.activeSelf, Is.True);
        Assert.That(shieldWasActiveWhenRespawnReported, Is.True);
        Assert.That(barrierWasVisibleWhenRespawnReported, Is.True);
        Assert.That(shield.IsActive, Is.True);
        Assert.That(shield.IsBarrierVisible, Is.True);
        Assert.That(shield.RemainingDuration,
            Is.InRange(shield.Duration - 0.2f, shield.Duration));

        body.linearVelocity = new Vector2(-3f, 4f);
        BulletHitData hitData = new(
            new Vector2(80f, 0f),
            body.position,
            null);
        receiver.ReceiveBulletHit(in hitData);

        float shieldedImpulse = 80f * (1f - shield.ImpulseReduction);
        Assert.That(body.linearVelocity.x,
            Is.EqualTo(shieldedImpulse / body.mass).Within(0.001f));
        Assert.That(body.linearVelocity.y, Is.EqualTo(4f).Within(0.001f));

        shield.ActivateAt(Time.time - shield.Duration);
        yield return null;

        Assert.That(shield.IsActive, Is.False);
        Assert.That(shield.IsBarrierVisible, Is.False);

        body.linearVelocity = new Vector2(-3f, 4f);
        receiver.ReceiveBulletHit(in hitData);

        Assert.That(body.linearVelocity.x,
            Is.EqualTo(80f / body.mass).Within(0.001f));
        Assert.That(body.linearVelocity.y, Is.EqualTo(4f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator LastLifeDeclaresOtherParticipantWinnerAndLocksTheResult()
    {
        PlayerLife opponent = CreateParticipant("Opponent", new Vector2(10f, 10f));
        Assert.That(matchController.RegisterParticipant(opponent), Is.True);

        Assert.That(scenePlayer.TryLoseLife(), Is.True);
        Assert.That(scenePlayer.TryLoseLife(), Is.True);
        Assert.That(scenePlayer.Life, Is.EqualTo(1));

        int matchEndedCount = 0;
        int respawnCount = 0;
        PlayerLife reportedWinner = null;
        matchController.PlayerRespawned += player =>
        {
            if (player == scenePlayer)
            {
                respawnCount++;
            }
        };
        matchController.MatchEnded += winner =>
        {
            matchEndedCount++;
            reportedWinner = winner;
        };

        Assert.That(matchController.ReportPlayerFall(scenePlayer), Is.True);
        for (int i = 0; i < 3 && !matchController.IsMatchFinished; i++)
        {
            yield return null;
        }

        Assert.That(scenePlayer.Life, Is.Zero);
        Assert.That(scenePlayer.gameObject.activeSelf, Is.False);
        Assert.That(matchController.State, Is.EqualTo(MatchState.Finished));
        Assert.That(matchController.Winner, Is.SameAs(opponent));
        Assert.That(reportedWinner, Is.SameAs(opponent));
        Assert.That(matchEndedCount, Is.EqualTo(1));

        int winnerLife = opponent.Life;
        Assert.That(matchController.ReportPlayerFall(opponent), Is.False);
        Assert.That(opponent.Life, Is.EqualTo(winnerLife));

        yield return new WaitForSeconds(
            scenePlayer.GetComponent<PlayerRespawner>().RespawnDelay + 0.1f);
        Assert.That(scenePlayer.gameObject.activeSelf, Is.False);
        Assert.That(respawnCount, Is.Zero);

        Assert.That(matchController.RestartMatch(), Is.True);
        Assert.That(matchController.State, Is.EqualTo(MatchState.Playing));
        Assert.That(matchController.Winner, Is.Null);
        Assert.That(scenePlayer.Life, Is.EqualTo(scenePlayer.StartingLife));
        Assert.That(opponent.Life, Is.EqualTo(opponent.StartingLife));
        Assert.That(scenePlayer.gameObject.activeSelf, Is.True);
        Assert.That(opponent.gameObject.activeSelf, Is.True);
    }

    [UnityTest]
    public IEnumerator ReenablingControllerRestartsAnInterruptedMatchCleanly()
    {
        Assert.That(matchController.ReportPlayerFall(scenePlayer), Is.True);
        Assert.That(scenePlayer.gameObject.activeSelf, Is.False);

        matchController.gameObject.SetActive(false);
        Assert.That(matchController.State, Is.EqualTo(MatchState.Waiting));

        matchController.gameObject.SetActive(true);
        yield return null;

        Assert.That(matchController.State, Is.EqualTo(MatchState.Playing));
        Assert.That(scenePlayer.Life, Is.EqualTo(scenePlayer.StartingLife));
        Assert.That(scenePlayer.gameObject.activeSelf, Is.True);
        Assert.That(matchController.ReportPlayerFall(scenePlayer), Is.True);
    }

    [UnityTest]
    public IEnumerator DestroyedParticipantIsRemovedAndTheSurvivorWins()
    {
        PlayerLife opponent = CreateParticipant("Opponent", new Vector2(10f, 10f));
        Assert.That(matchController.RegisterParticipant(opponent), Is.True);

        Object.Destroy(opponent.gameObject);
        for (int i = 0; i < 4 && !matchController.IsMatchFinished; i++)
        {
            yield return null;
        }

        Assert.That(matchController.State, Is.EqualTo(MatchState.Finished));
        Assert.That(matchController.Winner, Is.SameAs(scenePlayer));
        Assert.That(matchController.Participants, Has.Count.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator SimultaneousLastLifeFallsFinishAsADraw()
    {
        PlayerLife opponent = CreateParticipant("Opponent", new Vector2(10f, 10f));
        Assert.That(matchController.RegisterParticipant(opponent), Is.True);

        Assert.That(scenePlayer.TryLoseLife(), Is.True);
        Assert.That(scenePlayer.TryLoseLife(), Is.True);
        Assert.That(opponent.TryLoseLife(), Is.True);
        Assert.That(opponent.TryLoseLife(), Is.True);

        Assert.That(matchController.ReportPlayerFall(scenePlayer), Is.True);
        Assert.That(matchController.ReportPlayerFall(opponent), Is.True);

        for (int i = 0; i < 3 && !matchController.IsMatchFinished; i++)
        {
            yield return null;
        }

        Assert.That(scenePlayer.Life, Is.Zero);
        Assert.That(opponent.Life, Is.Zero);
        Assert.That(matchController.State, Is.EqualTo(MatchState.Finished));
        Assert.That(matchController.Winner, Is.Null);
    }

    private PlayerLife CreateParticipant(string objectName, Vector2 position)
    {
        GameObject participantObject = new(objectName);
        participantObject.SetActive(false);
        participantObject.transform.position = position;
        temporaryObjects.Add(participantObject);

        Rigidbody2D body = participantObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.position = position;
        participantObject.AddComponent<BoxCollider2D>();
        participantObject.AddComponent<PlayerRespawner>();
        PlayerLife participant = participantObject.AddComponent<PlayerLife>();
        participantObject.SetActive(true);
        return participant;
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
}
