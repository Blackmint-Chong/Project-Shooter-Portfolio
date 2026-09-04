using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class AIPlayerMovementPlayModeTests
{
    private const string TestSceneName = "TestScene";

    private readonly List<GameObject> temporaryObjects = new();

    private MatchController matchController;
    private PlayerInputCommandSource humanInput;
    private PlayerController2D humanController;
    private Rigidbody2D humanBody;
    private BoxCollider2D humanCollider;
    private AIPlayerCommandSource aiSource;
    private PlayerController2D aiController;
    private Rigidbody2D aiBody;
    private BoxCollider2D aiCollider;
    private BulletPool bulletPool;
    private EdgeCollider2D upperPlatform;
    private EdgeCollider2D lowerPlatform;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
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
        humanInput = Object.FindFirstObjectByType<PlayerInputCommandSource>();
        AIPlayerCommandSource[] aiSources =
            Object.FindObjectsByType<AIPlayerCommandSource>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        Assert.That(matchController, Is.Not.Null);
        Assert.That(matchController.IsMatchRunning, Is.True);
        Assert.That(humanInput, Is.Not.Null);
        Assert.That(aiSources, Has.Length.EqualTo(1));

        aiSource = aiSources[0];
        humanController = humanInput.GetComponent<PlayerController2D>();
        humanBody = humanInput.GetComponent<Rigidbody2D>();
        humanCollider = humanInput.GetComponent<BoxCollider2D>();
        aiController = aiSource.GetComponent<PlayerController2D>();
        aiBody = aiSource.GetComponent<Rigidbody2D>();
        aiCollider = aiSource.GetComponent<BoxCollider2D>();
        bulletPool = Object.FindFirstObjectByType<BulletPool>();

        humanInput.enabled = false;
        aiSource.enabled = false;
        aiSource.gameObject.SetActive(false);
        aiSource.gameObject.SetActive(true);
        Assert.That(aiSource.enabled, Is.False);

        EdgeCollider2D[] platforms = Object.FindObjectsByType<EdgeCollider2D>(
            FindObjectsSortMode.None);
        Assert.That(platforms, Has.Length.EqualTo(2));
        upperPlatform = platforms[0].bounds.max.y > platforms[1].bounds.max.y
            ? platforms[0]
            : platforms[1];
        lowerPlatform = upperPlatform == platforms[0]
            ? platforms[1]
            : platforms[0];

        RecycleActiveBullets();
        Assert.That(bulletPool, Is.Not.Null);
        Assert.That(aiController, Is.Not.Null);
        Assert.That(humanController, Is.Not.Null);
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
    public IEnumerator AiRoamsIndependentlyOfTargetAndReversesBeforeEdge()
    {
        yield return PlaceActorOnPlatform(
            aiSource.gameObject,
            aiController,
            aiBody,
            aiCollider,
            lowerPlatform,
            0f);

        SetActorPosition(
            humanInput.gameObject,
            humanBody,
            new Vector2(3f, aiBody.position.y + 0.6f));
        humanBody.gravityScale = 0f;

        aiSource.EvaluateDecision();
        float initialDirection = aiSource.LastHorizontalMovement;
        Assert.That(Mathf.Abs(initialDirection), Is.EqualTo(1f));
        Assert.That(aiSource.CurrentTarget, Is.SameAs(humanInput.GetComponent<PlayerLife>()));

        SetActorPosition(
            humanInput.gameObject,
            humanBody,
            new Vector2(-2f, aiBody.position.y + 0.6f));
        aiSource.EvaluateDecision();
        Assert.That(aiSource.LastHorizontalMovement, Is.EqualTo(initialDirection));

        float edgeX = initialDirection > 0f
            ? lowerPlatform.bounds.max.x - aiCollider.bounds.extents.x - 0.2f
            : lowerPlatform.bounds.min.x + aiCollider.bounds.extents.x + 0.2f;
        yield return PlaceActorOnPlatform(
            aiSource.gameObject,
            aiController,
            aiBody,
            aiCollider,
            lowerPlatform,
            edgeX);

        aiSource.EvaluateDecision();
        Assert.That(aiSource.LastHorizontalMovement, Is.EqualTo(-initialDirection));
    }

    [UnityTest]
    public IEnumerator AlignedAiFacesTargetAndFiresOppositeItsRoamDirection()
    {
        yield return PlaceActorOnPlatform(
            aiSource.gameObject,
            aiController,
            aiBody,
            aiCollider,
            lowerPlatform,
            0f);
        yield return PlaceActorOnPlatform(
            humanInput.gameObject,
            humanController,
            humanBody,
            humanCollider,
            lowerPlatform,
            3f);

        aiSource.EvaluateDecision();
        float roamDirection = aiSource.LastHorizontalMovement;
        aiSource.CommandReceiver.ResetCommands();
        RecycleActiveBullets();

        PlayerShooter aiShooter = aiSource.CommandReceiver.Shooter;
        yield return new WaitForSeconds(
            aiShooter.EquippedWeapon.FireInterval + 0.05f);

        float targetX = roamDirection > 0f ? -2f : 3f;
        float aiLandingY = lowerPlatform.bounds.max.y + aiCollider.bounds.extents.y;
        float humanLandingY = lowerPlatform.bounds.max.y + humanCollider.bounds.extents.y;
        aiBody.linearVelocity = Vector2.zero;
        humanBody.linearVelocity = Vector2.zero;
        SetActorPosition(
            aiSource.gameObject,
            aiBody,
            new Vector2(0f, aiLandingY));
        SetActorPosition(
            humanInput.gameObject,
            humanBody,
            new Vector2(targetX, humanLandingY));
        RecycleActiveBullets();

        int activeBefore = bulletPool.CountActive;
        aiSource.EvaluateDecision();

        float expectedAim = Mathf.Sign(targetX - aiBody.position.x);
        Assert.That(aiSource.LastHorizontalMovement, Is.EqualTo(roamDirection));
        Assert.That(aiSource.LastSubmittedCommand.AimHorizontal, Is.EqualTo(expectedAim));
        Assert.That(aiSource.LastSubmittedCommand.AttackPressed, Is.True);
        Assert.That(aiController.FacingDirection.x, Is.EqualTo(expectedAim));
        Assert.That(Mathf.Sign(roamDirection), Is.EqualTo(-expectedAim));
        Assert.That(bulletPool.CountActive, Is.EqualTo(activeBefore + 1));

        Bullet firedBullet = FindSpawnedBulletOwnedBy(aiSource.gameObject);
        Assert.That(firedBullet, Is.Not.Null);
        Assert.That(firedBullet.Direction.x, Is.EqualTo(expectedAim));

        int activeAfterShot = bulletPool.CountActive;
        aiSource.EvaluateDecision();
        Assert.That(aiSource.LastSubmittedCommand.AttackPressed, Is.False);
        Assert.That(bulletPool.CountActive, Is.EqualTo(activeAfterShot));
    }

    [UnityTest]
    public IEnumerator AlignedAiCanChooseAnExplorationJump()
    {
        yield return PlaceActorOnPlatform(
            aiSource.gameObject,
            aiController,
            aiBody,
            aiCollider,
            upperPlatform,
            0f);
        yield return PlaceActorOnPlatform(
            humanInput.gameObject,
            humanController,
            humanBody,
            humanCollider,
            upperPlatform,
            2f);

        aiSource.EvaluateDecision(0f, 1f);
        Assert.That(aiSource.LastSubmittedCommand.JumpPressed, Is.False);
        Assert.That(aiSource.LastSubmittedCommand.DropPressed, Is.False);

        aiSource.EvaluateDecision(100f, 1f);
        Assert.That(aiSource.LastSubmittedCommand.JumpPressed, Is.True);
        Assert.That(aiSource.LastSubmittedCommand.DropPressed, Is.False);
        Assert.That(aiSource.LastSubmittedCommand.AttackPressed, Is.False);

        aiSource.EvaluateDecision(100.16f, 0f);
        Assert.That(aiSource.LastSubmittedCommand.JumpPressed, Is.False);
        Assert.That(aiSource.LastSubmittedCommand.DropPressed, Is.False);

        yield return new WaitForFixedUpdate();
        Assert.That(aiBody.linearVelocity.y, Is.GreaterThan(0f));
    }

    [UnityTest]
    public IEnumerator AlignedAiCanExploreByDroppingOntoASafePlatform()
    {
        yield return PlaceActorOnPlatform(
            aiSource.gameObject,
            aiController,
            aiBody,
            aiCollider,
            upperPlatform,
            0f);
        yield return PlaceActorOnPlatform(
            humanInput.gameObject,
            humanController,
            humanBody,
            humanCollider,
            upperPlatform,
            2f);

        aiSource.EvaluateDecision(0f, 0f);
        aiSource.EvaluateDecision(100f, 0f);

        Assert.That(aiSource.LastSubmittedCommand.JumpPressed, Is.False);
        Assert.That(aiSource.LastSubmittedCommand.DropPressed, Is.True);
        Assert.That(aiSource.LastSubmittedCommand.AttackPressed, Is.False);

        yield return new WaitForFixedUpdate();
        Assert.That(aiController.IsDroppingThroughPlatform, Is.True);

        for (int i = 0; i < 100 && !aiController.IsGrounded; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(aiController.IsGrounded, Is.True);
        Assert.That(
            aiCollider.bounds.min.y,
            Is.EqualTo(lowerPlatform.bounds.max.y).Within(0.1f));
    }

    [UnityTest]
    public IEnumerator ExplorationDropFallsBackToJumpWithoutASafePlatform()
    {
        GameObject narrowPlatform = new("NarrowLowerPlatform");
        temporaryObjects.Add(narrowPlatform);
        narrowPlatform.layer = LayerMask.NameToLayer("OneWayPlatform");
        narrowPlatform.transform.position = new Vector2(
            0f,
            lowerPlatform.bounds.max.y - 1.5f);
        BoxCollider2D narrowCollider =
            narrowPlatform.AddComponent<BoxCollider2D>();
        narrowCollider.size = new Vector2(0.4f, 0.2f);
        Physics2D.SyncTransforms();

        yield return PlaceActorOnPlatform(
            aiSource.gameObject,
            aiController,
            aiBody,
            aiCollider,
            lowerPlatform,
            0f);
        yield return PlaceActorOnPlatform(
            humanInput.gameObject,
            humanController,
            humanBody,
            humanCollider,
            lowerPlatform,
            2f);

        aiSource.EvaluateDecision(0f, 0f);
        aiSource.EvaluateDecision(100f, 0f);

        Assert.That(aiSource.LastSubmittedCommand.JumpPressed, Is.True);
        Assert.That(aiSource.LastSubmittedCommand.DropPressed, Is.False);
    }

    [UnityTest]
    public IEnumerator AiUsesGroundAndAdditionalJumpToReachHigherPlatform()
    {
        yield return PlaceActorOnPlatform(
            aiSource.gameObject,
            aiController,
            aiBody,
            aiCollider,
            lowerPlatform,
            0f);
        yield return PlaceActorOnPlatform(
            humanInput.gameObject,
            humanController,
            humanBody,
            humanCollider,
            upperPlatform,
            2f);

        aiSource.EvaluateDecision();
        Assert.That(aiSource.LastSubmittedCommand.JumpPressed, Is.True);
        Assert.That(aiSource.LastSubmittedCommand.DropPressed, Is.False);

        yield return new WaitForFixedUpdate();
        Assert.That(aiBody.linearVelocity.y, Is.GreaterThan(0f));
        Assert.That(aiController.RemainingAdditionalJumps, Is.EqualTo(1));

        for (int i = 0; i < 20 && aiBody.linearVelocity.y > 1f; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(aiBody.linearVelocity.y, Is.LessThanOrEqualTo(1f));
        aiSource.EvaluateDecision();
        Assert.That(aiSource.LastSubmittedCommand.JumpPressed, Is.True);

        yield return new WaitForFixedUpdate();
        Assert.That(aiController.RemainingAdditionalJumps, Is.Zero);
        Assert.That(aiBody.linearVelocity.y, Is.GreaterThan(0f));

        for (int i = 0; i < 100; i++)
        {
            bool landedOnUpperPlatform = aiController.IsGrounded
                && Mathf.Abs(aiCollider.bounds.min.y - upperPlatform.bounds.max.y) < 0.1f;
            if (landedOnUpperPlatform)
            {
                break;
            }

            yield return new WaitForFixedUpdate();
        }

        Assert.That(aiController.IsGrounded, Is.True);
        Assert.That(
            aiCollider.bounds.min.y,
            Is.EqualTo(upperPlatform.bounds.max.y).Within(0.1f));
    }

    [UnityTest]
    public IEnumerator AiDropsOnlyWhenAPlatformExistsBelow()
    {
        yield return PlaceActorOnPlatform(
            aiSource.gameObject,
            aiController,
            aiBody,
            aiCollider,
            upperPlatform,
            0f);
        yield return PlaceActorOnPlatform(
            humanInput.gameObject,
            humanController,
            humanBody,
            humanCollider,
            lowerPlatform,
            0f);

        aiSource.EvaluateDecision();
        Assert.That(aiSource.LastSubmittedCommand.DropPressed, Is.True);

        yield return new WaitForFixedUpdate();
        Assert.That(aiController.IsDroppingThroughPlatform, Is.True);

        GameObject remotePlatform = new("RemoteLowerPlatform");
        temporaryObjects.Add(remotePlatform);
        remotePlatform.layer = LayerMask.NameToLayer("OneWayPlatform");
        remotePlatform.transform.position = new Vector2(9f, -4f);
        BoxCollider2D remoteCollider = remotePlatform.AddComponent<BoxCollider2D>();
        remoteCollider.size = new Vector2(3f, 0.2f);
        Physics2D.SyncTransforms();

        yield return PlaceActorOnPlatform(
            aiSource.gameObject,
            aiController,
            aiBody,
            aiCollider,
            lowerPlatform,
            0f);
        yield return PlaceActorOnPlatform(
            humanInput.gameObject,
            humanController,
            humanBody,
            humanCollider,
            remoteCollider,
            9f);

        aiSource.EvaluateDecision();
        Assert.That(aiSource.LastSubmittedCommand.DropPressed, Is.False);
    }

    [UnityTest]
    public IEnumerator FallingAiSteersBackOverAReachablePlatform()
    {
        SetActorPosition(
            humanInput.gameObject,
            humanBody,
            new Vector2(0f, 4f));
        humanBody.gravityScale = 0f;

        aiBody.gravityScale = 3f;
        float recoveryStartX = lowerPlatform.bounds.max.x
            + aiCollider.bounds.extents.x
            + 0.5f;
        SetActorPosition(
            aiSource.gameObject,
            aiBody,
            new Vector2(recoveryStartX, 0.5f));
        aiBody.linearVelocity = new Vector2(1f, -1f);
        yield return new WaitForFixedUpdate();

        Assert.That(aiController.IsGrounded, Is.False);
        aiSource.EvaluateDecision();

        Assert.That(aiSource.LastHorizontalMovement, Is.EqualTo(-1f));

        for (int i = 0; i < 100 && !aiController.IsGrounded; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(aiController.IsGrounded, Is.True);
        Assert.That(aiCollider.bounds.min.y,
            Is.EqualTo(lowerPlatform.bounds.max.y).Within(0.1f));
        Assert.That(aiCollider.bounds.min.x,
            Is.GreaterThanOrEqualTo(lowerPlatform.bounds.min.x - 0.05f));
        Assert.That(aiCollider.bounds.max.x,
            Is.LessThanOrEqualTo(lowerPlatform.bounds.max.x + 0.05f));
    }

    [UnityTest]
    public IEnumerator DisablingSourceClearsItsLastCommand()
    {
        yield return PlaceActorOnPlatform(
            aiSource.gameObject,
            aiController,
            aiBody,
            aiCollider,
            lowerPlatform,
            0f);
        SetActorPosition(
            humanInput.gameObject,
            humanBody,
            new Vector2(3f, aiBody.position.y + 0.6f));
        humanBody.gravityScale = 0f;

        aiSource.EvaluateDecision();
        Assert.That(aiSource.HasSubmittedCommand, Is.True);
        Assert.That(Mathf.Abs(aiSource.LastHorizontalMovement), Is.EqualTo(1f));

        aiSource.enabled = true;
        aiSource.enabled = false;

        Assert.That(aiSource.CurrentTarget, Is.Null);
        Assert.That(aiSource.HasSubmittedCommand, Is.False);
        Assert.That(aiSource.LastHorizontalMovement, Is.Zero);
    }

    private static IEnumerator PlaceActorOnPlatform(
        GameObject actor,
        PlayerController2D controller,
        Rigidbody2D body,
        BoxCollider2D actorCollider,
        Collider2D platform,
        float xPosition)
    {
        controller.ResetCommandState();
        body.gravityScale = 3f;
        body.linearVelocity = Vector2.zero;
        Vector2 position = new(
            xPosition,
            platform.bounds.max.y + actorCollider.bounds.extents.y + 0.03f);
        SetActorPosition(actor, body, position);

        yield return new WaitForFixedUpdate();

        for (int i = 0; i < 12 && !controller.IsGrounded; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(controller.IsGrounded, Is.True, $"{actor.name} did not land on {platform.name}.");
        body.linearVelocity = Vector2.zero;
    }

    private static void SetActorPosition(
        GameObject actor,
        Rigidbody2D body,
        Vector2 position)
    {
        actor.transform.position = position;
        body.position = position;
        Physics2D.SyncTransforms();
    }

    private void RecycleActiveBullets()
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

    private static Bullet FindSpawnedBulletOwnedBy(GameObject owner)
    {
        Bullet[] bullets = Object.FindObjectsByType<Bullet>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (Bullet bullet in bullets)
        {
            if (bullet.IsSpawned && bullet.Owner == owner)
            {
                return bullet;
            }
        }

        return null;
    }
}
