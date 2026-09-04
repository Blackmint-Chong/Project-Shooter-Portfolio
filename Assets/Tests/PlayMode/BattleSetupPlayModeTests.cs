using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class BattleSetupPlayModeTests
{
    private const string TestSceneName = "TestScene";
    private const string FactoryMapSceneName = "FactoryMap";
    private const string FactoryMapAssetPath =
        "Assets/Data/Maps/FactoryMap.asset";

    private SimulationMode2D originalPhysicsSimulationMode;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        originalPhysicsSimulationMode = Physics2D.simulationMode;
        BattleSession.Reset();

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
    }

    [TearDown]
    public void TearDown()
    {
        CompleteActiveCountdown();
        Time.timeScale = 1f;
        BattleSession.Reset();
        Physics2D.simulationMode = originalPhysicsSimulationMode;
    }

    [UnityTest]
    public IEnumerator CountdownFreezesBattleAndDisplaysEveryPhase()
    {
        BattleSetup setup = Object.FindFirstObjectByType<BattleSetup>();
        BattleCountdown countdown =
            Object.FindFirstObjectByType<BattleCountdown>();
        PlayerInputCommandSource humanInput =
            Object.FindFirstObjectByType<PlayerInputCommandSource>();
        AIPlayerCommandSource aiInput =
            Object.FindFirstObjectByType<AIPlayerCommandSource>();

        Assert.That(setup, Is.Not.Null);
        Assert.That(countdown, Is.Not.Null);
        Assert.That(humanInput, Is.Not.Null);
        Assert.That(aiInput, Is.Not.Null);
        MatchController matchController = setup.MatchController;
        PlayerLife aiParticipant = aiInput.GetComponent<PlayerLife>();
        PlayerController2D aiController =
            aiInput.GetComponent<PlayerController2D>();
        Assert.That(aiParticipant, Is.Not.Null);
        Assert.That(aiController, Is.Not.Null);
        Assert.That(setup.MapContext.TryGetSideSpawnSlot(
            BattleTeamSide.Right,
            out BattleSpawnSlot rightSpawnSlot), Is.True);
        Assert.That(setup.MapContext.TryGetSpawnSlot(
            aiParticipant,
            out BattleSpawnSlot aiSpawnSlot), Is.True);
        Assert.That(aiSpawnSlot, Is.SameAs(rightSpawnSlot));

        aiController.SetFacingDirection(1f);
        Assert.That(aiController.FacingDirection, Is.EqualTo(Vector2.right));
        Assert.That(setup.RestartBattle(), Is.True);
        Assert.That(aiController.FacingDirection, Is.EqualTo(Vector2.left));
        Assert.That(aiController.transform.Find("Visual")
            .GetComponent<SpriteRenderer>().flipX, Is.True);
        Assert.That(matchController.State, Is.EqualTo(MatchState.Countdown));
        Assert.That(matchController.IsCountingDown, Is.True);
        Assert.That(countdown.IsCountingDown, Is.True);
        Assert.That(countdown.IsVisible, Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(0f));
        Assert.That(humanInput.enabled, Is.False);
        Assert.That(aiInput.enabled, Is.False);
        Assert.That(countdown.AreParticipantInputsSuspended, Is.True);
        Assert.That(countdown.OverlayBackground, Is.Not.Null);
        Assert.That(countdown.OverlayBackground.color.r,
            Is.EqualTo(0f).Within(0.001f));
        Assert.That(countdown.OverlayBackground.color.g,
            Is.EqualTo(0f).Within(0.001f));
        Assert.That(countdown.OverlayBackground.color.b,
            Is.EqualTo(0f).Within(0.001f));
        Assert.That(countdown.OverlayBackground.color.a,
            Is.GreaterThan(0f).And.LessThan(1f));
        Assert.That(countdown.OverlayBackground.raycastTarget, Is.True);
        Assert.That(countdown.CountdownImage, Is.Not.Null);
        Assert.That(countdown.CountdownImage.preserveAspect, Is.True);
        Assert.That(countdown.CountdownImage.raycastTarget, Is.False);
        Assert.That(countdown.CountdownImage.rectTransform.anchorMin.x,
            Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(countdown.CountdownImage.rectTransform.anchorMin.y,
            Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(countdown.CountdownImage.rectTransform.anchoredPosition.x,
            Is.EqualTo(0f).Within(0.001f));
        Assert.That(countdown.CountdownImage.rectTransform.anchoredPosition.y,
            Is.EqualTo(0f).Within(0.001f));
        Assert.That(countdown.FallbackText, Is.Not.Null);
        Assert.That(countdown.ThreeSprite, Is.Not.Null);
        Assert.That(countdown.TwoSprite, Is.Not.Null);
        Assert.That(countdown.OneSprite, Is.Not.Null);
        Assert.That(countdown.GoSprite, Is.Not.Null);

        Dictionary<string, Sprite> expectedPhaseSprites = new()
        {
            { "3", countdown.ThreeSprite },
            { "2", countdown.TwoSprite },
            { "1", countdown.OneSprite },
            { "GO!", countdown.GoSprite }
        };
        AssertCountdownUsesSprite(countdown, expectedPhaseSprites["3"]);

        List<string> displayedPhases = new()
        {
            countdown.CurrentPhaseLabel
        };
        float timeout = Time.realtimeSinceStartup
            + countdown.PhaseDuration * 4f
            + 1f;
        while (countdown.IsCountingDown
               && Time.realtimeSinceStartup < timeout)
        {
            yield return null;
            string currentPhase = countdown.CurrentPhaseLabel;
            if (displayedPhases[displayedPhases.Count - 1] != currentPhase)
            {
                displayedPhases.Add(currentPhase);
                AssertCountdownUsesSprite(
                    countdown,
                    expectedPhaseSprites[currentPhase]);
            }
        }

        Assert.That(displayedPhases,
            Is.EqualTo(new[] { "3", "2", "1", "GO!" }));
        Assert.That(countdown.IsCountingDown, Is.False);
        Assert.That(countdown.IsVisible, Is.False);
        Assert.That(matchController.State, Is.EqualTo(MatchState.Playing));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(humanInput.enabled, Is.True);
        Assert.That(aiInput.enabled, Is.True);
        Assert.That(countdown.AreParticipantInputsSuspended, Is.False);
    }

    private static void AssertCountdownUsesSprite(
        BattleCountdown countdown,
        Sprite expectedSprite)
    {
        Assert.That(countdown.CountdownImage.enabled, Is.True);
        Assert.That(countdown.CountdownImage.sprite, Is.SameAs(expectedSprite));
        Assert.That(countdown.FallbackText.enabled, Is.False,
            "The authored countdown sprites must replace the fallback text.");
    }

    [UnityTest]
    public IEnumerator TestSceneInitializesAndBindsTheCommonBattleSetup()
    {
        Assert.That(BattleSession.HasConfiguration, Is.False,
            "Direct battle-scene loads must use the authored P1/P2 fallback roster.");

        BattleSetup[] setups = Object.FindObjectsByType<BattleSetup>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        BattleMapContext[] contexts =
            Object.FindObjectsByType<BattleMapContext>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        Assert.That(setups, Has.Length.EqualTo(1));
        Assert.That(contexts, Has.Length.EqualTo(1));

        BattleSetup setup = setups[0];
        BattleMapContext context = contexts[0];
        MatchController matchController = setup.MatchController;

        Assert.That(setup.IsInitialized, Is.True);
        Assert.That(setup.MapContext, Is.SameAs(context));
        Assert.That(setup.BulletPool, Is.Not.Null);
        Assert.That(setup.LifeHud, Is.Not.Null);
        Assert.That(matchController, Is.Not.Null);
        Assert.That(matchController.IsMatchRunning, Is.True);
        Assert.That(context.TryValidate(out string validationError), Is.True,
            validationError);

        BattlePlayerSlot[] expectedSlots =
        {
            BattlePlayerSlot.P1,
            BattlePlayerSlot.P2
        };

        Assert.That(context.SpawnSlots, Has.Count.EqualTo(2));
        Assert.That(
            context.SpawnSlots.Select(spawnSlot => spawnSlot.Slot),
            Is.Unique);
        Assert.That(
            context.SpawnSlots.Select(spawnSlot => spawnSlot.Slot),
            Is.EquivalentTo(expectedSlots));

        foreach (BattleSpawnSlot spawnSlot in context.SpawnSlots)
        {
            Assert.That(spawnSlot, Is.Not.Null);
            Assert.That(spawnSlot.RespawnArea, Is.Not.Null);
            Assert.That(context.TryGetSpawnSlot(
                spawnSlot.Slot,
                out BattleSpawnSlot resolvedSpawnSlot), Is.True);
            Assert.That(resolvedSpawnSlot, Is.SameAs(spawnSlot));
            Assert.That(context.TryGetRespawnArea(
                spawnSlot.Slot,
                out BattleSpawnArea resolvedArea), Is.True);
            Assert.That(resolvedArea, Is.SameAs(spawnSlot.RespawnArea));
            Assert.That(spawnSlot.RespawnArea.Contains(
                spawnSlot.RespawnArea.Center), Is.True);
        }

        Assert.That(setup.ParticipantSlots, Has.Count.EqualTo(2));
        Assert.That(
            setup.ParticipantSlots.Select(participantSlot => participantSlot.Slot),
            Is.EqualTo(expectedSlots),
            "BattleSetup must keep participants ordered from P1 to P2.");
        Assert.That(
            setup.ParticipantSlots.Select(participantSlot =>
                participantSlot.Participant),
            Is.Unique);

        PlayerLife[] boundParticipants = setup.ParticipantSlots
            .Select(participantSlot => participantSlot.Participant)
            .ToArray();
        Assert.That(matchController.Participants,
            Is.EqualTo(boundParticipants),
            "Match registration order must follow the P1/P2 slot order.");

        foreach (BattleParticipantSlot participantSlot in
                 setup.ParticipantSlots)
        {
            PlayerLife participant = participantSlot.Participant;
            Assert.That(participant, Is.Not.Null);
            Assert.That(context.TryGetSpawnSlot(
                participantSlot.Slot,
                out BattleSpawnSlot spawnSlot), Is.True);
            Assert.That(context.TryGetSpawnSlot(
                participant,
                out BattleSpawnSlot participantSpawnSlot), Is.True);
            Assert.That(participantSpawnSlot, Is.SameAs(spawnSlot));
            Assert.That(context.TryGetRespawnArea(
                participant,
                out BattleSpawnArea resolvedArea), Is.True);
            Assert.That(resolvedArea, Is.SameAs(spawnSlot.RespawnArea));
            Assert.That(setup.TryGetParticipant(
                participantSlot.Slot,
                out PlayerLife resolvedParticipant), Is.True);
            Assert.That(resolvedParticipant, Is.SameAs(participant));

            PlayerRespawner respawner =
                participant.GetComponent<PlayerRespawner>();
            Assert.That(respawner, Is.Not.Null);
            Assert.That(respawner.RespawnArea, Is.SameAs(spawnSlot.RespawnArea));
            Assert.That(respawner.HasInitialSpawnPosition, Is.True);
            Assert.That(respawner.InitialSpawnPosition.x,
                Is.EqualTo(spawnSlot.InitialPosition.x).Within(0.001f));
            Assert.That(respawner.InitialSpawnPosition.y,
                Is.EqualTo(spawnSlot.InitialPosition.y).Within(0.001f));
            Assert.That(matchController.IsParticipant(participant),
                Is.True);

            if (participant.TryGetComponent(
                    out AIPlayerCommandSource aiSource))
            {
                Assert.That(aiSource.MatchController,
                    Is.SameAs(matchController));
            }
        }

        Assert.That(setup.TryGetParticipant(
            BattlePlayerSlot.P1,
            out PlayerLife playerOne), Is.True);
        Assert.That(playerOne.GetComponent<PlayerInputCommandSource>(),
            Is.Not.Null);
        Assert.That(playerOne.GetComponent<AIPlayerCommandSource>(), Is.Null);

        Assert.That(setup.TryGetParticipant(
            BattlePlayerSlot.P2,
            out PlayerLife playerTwo), Is.True);
        Assert.That(playerTwo.GetComponent<AIPlayerCommandSource>(), Is.Not.Null);
        Assert.That(playerTwo.GetComponent<PlayerInputCommandSource>(), Is.Null);

        Assert.That(context.FallDeathZones, Is.Not.Empty);
        foreach (PlayerFallDeathZone fallDeathZone in context.FallDeathZones)
        {
            Assert.That(fallDeathZone.MatchController,
                Is.SameAs(matchController));
        }

        Assert.That(setup.LifeHud.MatchController,
            Is.SameAs(matchController));

        int participantCount = matchController.Participants.Count;
        BattleParticipantSlot[] participantSlotsBeforeReinitializing =
            setup.ParticipantSlots.ToArray();
        Assert.That(setup.Initialize(), Is.True);
        Assert.That(setup.StartBattle(), Is.True);
        Assert.That(matchController.Participants,
            Has.Count.EqualTo(participantCount),
            "Repeated initialization must not register duplicate participants.");
        Assert.That(setup.ParticipantSlots,
            Is.EqualTo(participantSlotsBeforeReinitializing),
            "Repeated initialization must preserve the resolved slot mapping.");
        Assert.That(matchController.Participants,
            Is.EqualTo(boundParticipants),
            "Repeated initialization must preserve P1/P2 registration order.");

        yield return null;
    }

    [UnityTest]
    public IEnumerator FactoryMapPlacesTheHumanOnTheFixedLeftSide()
    {
#if UNITY_EDITOR
        BattleMapDefinition factoryMap =
            AssetDatabase.LoadAssetAtPath<BattleMapDefinition>(
                FactoryMapAssetPath);
        Assert.That(factoryMap, Is.Not.Null);

        AIDifficultyDefinition testDifficulty =
            ScriptableObject.CreateInstance<AIDifficultyDefinition>();
        testDifficulty.Configure(
            "battle-setup-test",
            "Battle Setup Test",
            0.23f,
            new Vector2(0.25f, 0.5f),
            new Vector2(1f, 2f),
            0.7f,
            0.3f,
            0.2f,
            3f,
            4f,
            0.08f);

        BattleSession.Configure(factoryMap, testDifficulty);
        Physics2D.simulationMode = SimulationMode2D.Script;

        AsyncOperation load = SceneManager.LoadSceneAsync(
            FactoryMapSceneName,
            LoadSceneMode.Single);
        while (!load.isDone)
        {
            yield return null;
        }

        BattleSetup setup = Object.FindFirstObjectByType<BattleSetup>();
        Assert.That(setup, Is.Not.Null);
        Assert.That(setup.IsInitialized, Is.True);

        BattleMapContext context = setup.MapContext;
        Assert.That(context, Is.Not.Null);
        Assert.That(context.TryGetSideSpawnSlots(
            out BattleSpawnSlot leftSpawnSlot,
            out BattleSpawnSlot rightSpawnSlot), Is.True);

        PlayerInputCommandSource humanSource =
            Object.FindFirstObjectByType<PlayerInputCommandSource>();
        AIPlayerCommandSource aiSource =
            Object.FindFirstObjectByType<AIPlayerCommandSource>();
        Assert.That(humanSource, Is.Not.Null);
        Assert.That(aiSource, Is.Not.Null);
        Assert.That(aiSource.DifficultyProfile, Is.SameAs(testDifficulty));
        Assert.That(aiSource.DecisionInterval,
            Is.EqualTo(testDifficulty.DecisionInterval));
        Assert.That(aiSource.ShootHeightTolerance,
            Is.EqualTo(testDifficulty.ShootHeightTolerance));
        Assert.That(aiSource.ExplorationDropChance,
            Is.EqualTo(testDifficulty.ExplorationDropChance));

        PlayerLife human = humanSource.GetComponent<PlayerLife>();
        PlayerLife ai = aiSource.GetComponent<PlayerLife>();
        Assert.That(human, Is.Not.Null);
        Assert.That(ai, Is.Not.Null);

        BattleParticipantSlot humanSlot =
            human.GetComponent<BattleParticipantSlot>();
        BattleParticipantSlot aiSlot = ai.GetComponent<BattleParticipantSlot>();
        Assert.That(humanSlot.Slot, Is.EqualTo(leftSpawnSlot.Slot),
            "The human player must always use the physical left spawn side.");
        Assert.That(aiSlot.Slot, Is.EqualTo(rightSpawnSlot.Slot),
            "The AI player must always use the physical right spawn side.");
        Assert.That(context.TryGetSpawnSlot(
            human,
            out BattleSpawnSlot resolvedHumanSpawn), Is.True);
        Assert.That(resolvedHumanSpawn, Is.SameAs(leftSpawnSlot));
        Assert.That(context.TryGetSpawnSlot(
            ai,
            out BattleSpawnSlot resolvedAiSpawn), Is.True);
        Assert.That(resolvedAiSpawn, Is.SameAs(rightSpawnSlot));

        AssertParticipantPlacedAtSpawn(human, leftSpawnSlot);
        AssertParticipantPlacedAtSpawn(ai, rightSpawnSlot);
        PlayerController2D humanController =
            human.GetComponent<PlayerController2D>();
        PlayerController2D aiController = ai.GetComponent<PlayerController2D>();
        Assert.That(humanController.FacingDirection, Is.EqualTo(Vector2.right));
        Assert.That(aiController.FacingDirection, Is.EqualTo(Vector2.left),
            "The AI spawned in the physical right slot must face left.");
        Assert.That(aiController.transform.Find("Visual")
            .GetComponent<SpriteRenderer>().flipX, Is.True);

        Assert.That(setup.LifeHud, Is.Not.Null);
        Assert.That(setup.LifeHud.LeftParticipant, Is.SameAs(human),
            "The HUD's left entry must display the fixed-left human player.");
        Assert.That(setup.LifeHud.RightParticipant, Is.SameAs(ai),
            "The HUD's right entry must display the fixed-right AI player.");
        BattleParticipantPresentation humanPresentation =
            human.GetComponent<BattleParticipantPresentation>();
        BattleParticipantPresentation aiPresentation =
            ai.GetComponent<BattleParticipantPresentation>();
        Assert.That(humanPresentation, Is.Not.Null);
        Assert.That(aiPresentation, Is.Not.Null);
        Assert.That(setup.LifeHud.LeftPortrait.sprite,
            Is.SameAs(humanPresentation.Portrait),
            "The left portrait must follow the participant on the physical left side.");
        Assert.That(setup.LifeHud.RightPortrait.sprite,
            Is.SameAs(aiPresentation.Portrait),
            "The right portrait must follow the participant on the physical right side.");
        Assert.That(setup.LifeHud.IsLeftPortraitVisible, Is.True);
        Assert.That(setup.LifeHud.IsRightPortraitVisible, Is.True);
        Assert.That(AssetDatabase.GetAssetPath(setup.LifeHud.LeftPortrait.sprite),
            Is.EqualTo("Assets/Art/player/1_portrait.png"));
        Assert.That(AssetDatabase.GetAssetPath(setup.LifeHud.RightPortrait.sprite),
            Is.EqualTo("Assets/Art/player/2_portrait.png"));
        Assert.That(setup.LifeHud.LeftPortrait.rectTransform.anchoredPosition.x,
            Is.EqualTo(32f));
        Assert.That(setup.LifeHud.LeftDisplayTransform.anchoredPosition.x,
            Is.EqualTo(128f),
            "FactoryMap's HUD override must leave room for the left portrait.");
        Assert.That(setup.LifeHud.RightPortrait.rectTransform.anchoredPosition.x,
            Is.EqualTo(-72f));
        Assert.That(setup.LifeHud.RightDisplayTransform.anchoredPosition.x,
            Is.EqualTo(-128f),
            "FactoryMap's HUD override must leave room for the right portrait.");
        Object.Destroy(testDifficulty);
#else
        Assert.Ignore("FactoryMap asset lookup is only available in the Unity Editor.");
#endif

        yield return null;
    }

    private static void AssertParticipantPlacedAtSpawn(
        PlayerLife participant,
        BattleSpawnSlot spawnSlot)
    {
        Rigidbody2D body = participant.GetComponent<Rigidbody2D>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body.position.x,
            Is.EqualTo(spawnSlot.InitialPosition.x).Within(0.001f));
        Assert.That(body.position.y,
            Is.EqualTo(spawnSlot.InitialPosition.y).Within(0.001f));
        Assert.That(body.linearVelocity.x, Is.EqualTo(0f).Within(0.001f));
        Assert.That(body.linearVelocity.y, Is.EqualTo(0f).Within(0.001f));
        Assert.That(body.angularVelocity, Is.EqualTo(0f).Within(0.001f));
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
