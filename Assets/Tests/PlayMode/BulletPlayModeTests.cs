using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

public sealed class BulletPlayModeTests
{
    private const string TestSceneName = "TestScene";

    private readonly List<GameObject> temporaryObjects = new();

    private BulletPool pool;
    private int playerLayer;
    private int platformLayer;
    private int projectileLayer;
    private int deadZoneLayer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(TestSceneName, LoadSceneMode.Single);
        while (!load.isDone)
        {
            yield return null;
        }

        yield return null;
        CompleteActiveCountdown();

        foreach (AIPlayerCommandSource aiSource in
                 Object.FindObjectsByType<AIPlayerCommandSource>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            aiSource.enabled = false;
        }

        RecycleSpawnedBullets();
        yield return null;

        pool = Object.FindFirstObjectByType<BulletPool>();
        playerLayer = LayerMask.NameToLayer("Player");
        platformLayer = LayerMask.NameToLayer("OneWayPlatform");
        projectileLayer = LayerMask.NameToLayer("Projectile");
        deadZoneLayer = LayerMask.NameToLayer("ProjectileDeadZone");

        Assert.That(pool, Is.Not.Null);
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
    public IEnumerator PlatformPrefabAndPlayerBindingsMatchThePlan()
    {
        EdgeCollider2D[] platformEdges = Object.FindObjectsByType<EdgeCollider2D>(
            FindObjectsSortMode.None);
        Dictionary<string, float> expectedHalfWidths = new()
        {
            { "PF_OneWayPlatform_6x1", 3f },
            { "PF_OneWayPlatform_12x1", 6f }
        };
        Assert.That(platformEdges, Has.Length.EqualTo(expectedHalfWidths.Count));

        HashSet<string> foundPlatforms = new();

        foreach (EdgeCollider2D edge in platformEdges)
        {
            Assert.That(
                expectedHalfWidths.TryGetValue(edge.name, out float halfWidth),
                Is.True,
                $"Unexpected one-way platform in {TestSceneName}: {edge.name}");
            Assert.That(foundPlatforms.Add(edge.name), Is.True,
                $"Duplicate one-way platform variant in {TestSceneName}: {edge.name}");
            Assert.That(edge.gameObject.layer, Is.EqualTo(platformLayer));
            Assert.That(edge.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(edge.points, Has.Length.EqualTo(2));
            Assert.That(edge.points[0], Is.EqualTo(new Vector2(-halfWidth, 0.5f))
                .Using(Vector2ComparerWithEqualsOperator.Instance));
            Assert.That(edge.points[1], Is.EqualTo(new Vector2(halfWidth, 0.5f))
                .Using(Vector2ComparerWithEqualsOperator.Instance));

            PlatformEffector2D effector = edge.GetComponent<PlatformEffector2D>();
            Assert.That(effector, Is.Not.Null);
            Assert.That(effector.useOneWay, Is.True);
            Assert.That(effector.surfaceArc, Is.EqualTo(120f));
        }

        CollectionAssert.AreEquivalent(
            expectedHalfWidths.Keys,
            foundPlatforms);

        PlayerController2D scenePlayer = FindHumanPlayerController();
        PlayerInput playerInput = scenePlayer.GetComponent<PlayerInput>();
        InputAction dropAction = playerInput.actions.FindAction("Player/Drop", throwIfNotFound: false);
        Assert.That(dropAction, Is.Not.Null);
        Assert.That(dropAction.type, Is.EqualTo(InputActionType.Button));

        InputAction moveAction = playerInput.actions.FindAction("Player/Move", throwIfNotFound: true);
        InputAction jumpAction = playerInput.actions.FindAction("Player/Jump", throwIfNotFound: true);
        InputAction attackAction = playerInput.actions.FindAction("Player/Attack", throwIfNotFound: true);

        CollectionAssert.AreEquivalent(
            new[] { "<Keyboard>/leftArrow", "<Keyboard>/rightArrow" },
            GetKeyboardBindingPaths(moveAction));
        CollectionAssert.AreEquivalent(
            new[] { "<Keyboard>/upArrow" },
            GetKeyboardBindingPaths(jumpAction));
        CollectionAssert.AreEquivalent(
            new[] { "<Keyboard>/downArrow" },
            GetKeyboardBindingPaths(dropAction));
        CollectionAssert.AreEquivalent(
            new[] { "<Keyboard>/z" },
            GetKeyboardBindingPaths(attackAction));
        yield return null;
    }

    [UnityTest]
    public IEnumerator SceneAndPrefabConfigurationMatchThePlan()
    {
        Assert.That(projectileLayer, Is.EqualTo(8));
        Assert.That(deadZoneLayer, Is.EqualTo(9));
        Assert.That(Physics2D.GetIgnoreLayerCollision(projectileLayer, playerLayer), Is.False);
        Assert.That(Physics2D.GetIgnoreLayerCollision(projectileLayer, deadZoneLayer), Is.False);
        Assert.That(Physics2D.GetIgnoreLayerCollision(projectileLayer, platformLayer), Is.True);
        Assert.That(Physics2D.GetIgnoreLayerCollision(projectileLayer, projectileLayer), Is.True);
        Assert.That(Physics2D.GetIgnoreLayerCollision(playerLayer, playerLayer), Is.True);

        ProjectileDeadZone[] deadZones = Object.FindObjectsByType<ProjectileDeadZone>(
            FindObjectsSortMode.None);
        Assert.That(deadZones, Has.Length.EqualTo(4));
        foreach (ProjectileDeadZone deadZone in deadZones)
        {
            Assert.That(deadZone.gameObject.layer, Is.EqualTo(deadZoneLayer));
            Assert.That(deadZone.GetComponent<BoxCollider2D>().isTrigger, Is.True);
        }

        PlayerFallDeathZone[] fallDeathZones = Object.FindObjectsByType<PlayerFallDeathZone>(
            FindObjectsSortMode.None);
        Assert.That(fallDeathZones, Has.Length.EqualTo(3));

        MatchController[] matchControllers = Object.FindObjectsByType<MatchController>(
            FindObjectsSortMode.None);
        Assert.That(matchControllers, Has.Length.EqualTo(1));
        Assert.That(matchControllers[0].State, Is.EqualTo(MatchState.Playing));

        foreach (PlayerFallDeathZone fallDeathZone in fallDeathZones)
        {
            Assert.That(fallDeathZone.gameObject.layer, Is.EqualTo(0));
            Assert.That(fallDeathZone.GetComponent<BoxCollider2D>().isTrigger, Is.True);
            Assert.That(fallDeathZone.MatchController, Is.SameAs(matchControllers[0]));
        }

        PlayerFallDeathZone bottomFallDeathZone = FindPlayerFallDeathZone(
            "PlayerFallDeathZone");
        PlayerFallDeathZone leftFallDeathZone = FindPlayerFallDeathZone(
            "PlayerFallDeathZone_Left");
        PlayerFallDeathZone rightFallDeathZone = FindPlayerFallDeathZone(
            "PlayerFallDeathZone_Right");
        Assert.That(bottomFallDeathZone, Is.Not.Null);
        Assert.That(leftFallDeathZone, Is.Not.Null);
        Assert.That(rightFallDeathZone, Is.Not.Null);

        BoxCollider2D bottomFallDeathCollider =
            bottomFallDeathZone.GetComponent<BoxCollider2D>();
        BoxCollider2D leftFallDeathCollider =
            leftFallDeathZone.GetComponent<BoxCollider2D>();
        BoxCollider2D rightFallDeathCollider =
            rightFallDeathZone.GetComponent<BoxCollider2D>();
        Assert.That(bottomFallDeathZone.transform.position.y, Is.LessThan(0f));
        Assert.That(bottomFallDeathCollider.size, Is.EqualTo(new Vector2(26f, 1f))
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(leftFallDeathCollider.size, Is.EqualTo(new Vector2(2f, 30f))
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(rightFallDeathCollider.size, Is.EqualTo(new Vector2(2f, 30f))
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(leftFallDeathZone.transform.position.x, Is.EqualTo(-13f));
        Assert.That(rightFallDeathZone.transform.position.x, Is.EqualTo(13f));

        Camera mainCamera = Camera.main;
        Assert.That(mainCamera, Is.Not.Null);
        float defaultHalfWidth = mainCamera.orthographicSize * (16f / 9f);
        Assert.That(leftFallDeathCollider.bounds.max.x,
            Is.LessThan(mainCamera.transform.position.x - defaultHalfWidth - 2f));
        Assert.That(rightFallDeathCollider.bounds.min.x,
            Is.GreaterThan(mainCamera.transform.position.x + defaultHalfWidth + 2f));
        Assert.That(leftFallDeathCollider.bounds.min.y,
            Is.LessThan(mainCamera.transform.position.y - mainCamera.orthographicSize));
        Assert.That(leftFallDeathCollider.bounds.max.y,
            Is.GreaterThan(mainCamera.transform.position.y + mainCamera.orthographicSize));
        Assert.That(rightFallDeathCollider.bounds.min.y,
            Is.LessThan(mainCamera.transform.position.y - mainCamera.orthographicSize));
        Assert.That(rightFallDeathCollider.bounds.max.y,
            Is.GreaterThan(mainCamera.transform.position.y + mainCamera.orthographicSize));
        Assert.That(Physics2D.GetIgnoreLayerCollision(playerLayer, 0), Is.False);

        BulletImpulseReceiver[] impulseReceivers = Object.FindObjectsByType<BulletImpulseReceiver>(
            FindObjectsSortMode.None);
        Assert.That(impulseReceivers, Has.Length.EqualTo(2));

        PlayerController2D player = FindHumanPlayerController();
        Assert.That(player, Is.Not.Null);
        SpriteRenderer playerRenderer = player.transform.Find("Visual")
            .GetComponent<SpriteRenderer>();
        Assert.That(playerRenderer, Is.Not.Null);
        foreach (EdgeCollider2D platformEdge in Object.FindObjectsByType<EdgeCollider2D>(
                     FindObjectsSortMode.None))
        {
            SpriteRenderer platformRenderer = platformEdge.GetComponentInChildren<SpriteRenderer>(true);
            Assert.That(platformRenderer, Is.Not.Null);
            Assert.That(playerRenderer.sortingOrder,
                Is.GreaterThan(platformRenderer.sortingOrder));
        }

        Assert.That(player.AdditionalJumpCount, Is.EqualTo(1));
        Assert.That(player.RemainingAdditionalJumps, Is.EqualTo(1));
        PlayerShooter playerShooter = player.GetComponent<PlayerShooter>();
        Assert.That(playerShooter, Is.Not.Null);
        Weapon weapon = playerShooter.EquippedWeapon;
        Assert.That(weapon, Is.Not.Null);
        Assert.That(weapon.Definition, Is.Not.Null);
        Assert.That(weapon.Renderer, Is.Not.Null);
        Assert.That(weapon.Muzzle, Is.Not.Null);
        Assert.That(weapon.Definition.BulletSpeed, Is.EqualTo(15f));
        Assert.That(weapon.Definition.Impulse, Is.EqualTo(25f));
        Assert.That(weapon.Definition.FireInterval, Is.EqualTo(0.4f));
        Assert.That(weapon.Renderer.sortingOrder,
            Is.GreaterThan(playerRenderer.sortingOrder));
        Assert.That(weapon.transform.position.x, Is.GreaterThan(player.transform.position.x));
        PlayerLife playerLife = player.GetComponent<PlayerLife>();
        Assert.That(playerLife, Is.Not.Null);
        Assert.That(playerLife.StartingLife, Is.EqualTo(3));
        Assert.That(playerLife.Life, Is.EqualTo(3));
        Assert.That(matchControllers[0].IsParticipant(playerLife), Is.True);
        PlayerRespawner playerRespawner = player.GetComponent<PlayerRespawner>();
        Assert.That(playerRespawner, Is.Not.Null);
        Assert.That(playerRespawner.RespawnPosition, Is.EqualTo(new Vector2(0f, 4f))
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(playerRespawner.RespawnDelay, Is.EqualTo(0.5f));
        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        PlayerCommandReceiver commandReceiver = player.GetComponent<PlayerCommandReceiver>();
        PlayerInputCommandSource inputSource = player.GetComponent<PlayerInputCommandSource>();
        Assert.That(playerInput, Is.Not.Null);
        Assert.That(playerInput.notificationBehavior,
            Is.EqualTo(PlayerNotifications.InvokeCSharpEvents));
        Assert.That(commandReceiver, Is.Not.Null);
        Assert.That(commandReceiver.Controller, Is.SameAs(player));
        Assert.That(commandReceiver.Shooter, Is.SameAs(playerShooter));
        Assert.That(inputSource, Is.Not.Null);
        Assert.That(inputSource.PlayerInput, Is.SameAs(playerInput));
        Assert.That(inputSource.CommandReceiver, Is.SameAs(commandReceiver));
        Assert.That(inputSource.IsSubscribed, Is.True);
        Assert.That(player.GetComponent<BoxCollider2D>(), Is.Not.Null);

        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        Assert.That(playerBody.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
        Assert.That(playerBody.mass, Is.EqualTo(1f));
        Assert.That(playerBody.gravityScale, Is.EqualTo(3f));

        Assert.That(pool.CountAll, Is.EqualTo(16));
        Assert.That(pool.CountInactive, Is.EqualTo(16));

        Bullet bullet = pool.Spawn(new Vector2(-4f, 4f), Vector2.right, null);
        Assert.That(bullet, Is.Not.Null);
        Assert.That(bullet.gameObject.layer, Is.EqualTo(projectileLayer));
        Assert.That(bullet.DefaultStats.Speed, Is.EqualTo(15f));
        Assert.That(bullet.DefaultStats.Impulse, Is.EqualTo(5f));

        Rigidbody2D body = bullet.GetComponent<Rigidbody2D>();
        CircleCollider2D bulletCollider = bullet.GetComponent<CircleCollider2D>();
        Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
        Assert.That(body.gravityScale, Is.Zero);
        Assert.That(body.linearDamping, Is.Zero);
        Assert.That(body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode2D.Continuous));
        Assert.That(bulletCollider.isTrigger, Is.True);
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.right * 15f).Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(
            Vector2.Dot((Vector2)bullet.transform.right, Vector2.right),
            Is.GreaterThan(0.999f));

        float startX = body.position.x;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.That(body.position.x, Is.GreaterThan(startX));
        Assert.That(body.linearVelocity.y, Is.EqualTo(0f).Within(0.001f));
        bullet.RequestRecycle();
    }

    [UnityTest]
    public IEnumerator CombatResolverAppliesMassAdjustedHorizontalImpulseAndPreservesVerticalSpeed()
    {
        DisableScenePlayerColliders();

        GameObject target = CreateBox("ImpulseTarget", new Vector2(0f, 3f), playerLayer, Vector2.one);
        Rigidbody2D targetBody = target.AddComponent<Rigidbody2D>();
        targetBody.gravityScale = 0f;
        targetBody.mass = 2f;
        targetBody.linearVelocity = new Vector2(-1f, 3f);
        target.AddComponent<BulletImpulseReceiver>();

        BulletStats stats = new(15f, 2f);
        Bullet bullet = pool.Spawn(new Vector2(-2f, 3f), Vector2.right, null, in stats);

        yield return WaitForFixedSteps(() => !bullet.IsSpawned, 100);

        Assert.That(targetBody.linearVelocity, Is.EqualTo(new Vector2(1f, 3f))
            .Using(Vector2ComparerWithEqualsOperator.Instance));
    }

    [UnityTest]
    public IEnumerator OwnerIsIgnoredAndTargetReceivesExactlyOneDefaultHit()
    {
        DisableScenePlayerColliders();

        GameObject owner = CreateBox("Owner", new Vector2(-2f, 3f), playerLayer, Vector2.one);
        GameObject target = CreateBox("Target", new Vector2(1f, 3f), playerLayer, Vector2.one);
        // Two overlapping colliders deliberately stress the same-frame duplicate-hit guard.
        target.AddComponent<BoxCollider2D>().size = new Vector2(0.8f, 0.8f);
        BulletHitReceiverSpy receiver = target.AddComponent<BulletHitReceiverSpy>();

        Bullet bullet = pool.Spawn(owner.transform.position, Vector2.right, owner);
        yield return WaitForFixedSteps(() => receiver.HitCount == 1, 100);

        Assert.That(receiver.HitCount, Is.EqualTo(1));
        Assert.That(receiver.LastHit.ImpulseVector, Is.EqualTo(new Vector2(5f, 0f))
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(receiver.LastHit.Owner, Is.SameAs(owner));
        Assert.That(bullet.IsSpawned, Is.False);
        Assert.That(bullet.gameObject.activeSelf, Is.False);

        yield return new WaitForFixedUpdate();
        Assert.That(receiver.HitCount, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator PlayerWithoutReceiverStillConsumesBullet()
    {
        DisableScenePlayerColliders();
        CreateBox("TargetWithoutReceiver", new Vector2(0f, 2f), playerLayer, Vector2.one);

        Bullet bullet = pool.Spawn(new Vector2(-2f, 2f), Vector2.right, null);
        yield return WaitForFixedSteps(() => !bullet.IsSpawned, 100);

        Assert.That(bullet.gameObject.activeSelf, Is.False);
        Assert.That(pool.CountInactive, Is.EqualTo(16));
    }

    [UnityTest]
    public IEnumerator OverrideStatsAndAllRuntimeStateAreResetWhenReused()
    {
        DisableScenePlayerColliders();

        GameObject firstOwner = Track(new GameObject("FirstOwner"));
        GameObject secondOwner = Track(new GameObject("SecondOwner"));
        GameObject target = CreateBox("Target", Vector2.zero, playerLayer, Vector2.one);
        BulletHitReceiverSpy receiver = target.AddComponent<BulletHitReceiverSpy>();
        BulletStats firstStats = new(7f, 9f);

        Bullet firstUse = pool.Spawn(new Vector2(-3f, 0f), Vector2.right, firstOwner, in firstStats);
        Assert.That(firstUse.GetComponent<Rigidbody2D>().linearVelocity.x, Is.EqualTo(7f).Within(0.001f));

        yield return WaitForFixedSteps(() => receiver.HitCount == 1, 100);

        Assert.That(receiver.LastHit.ImpulseVector, Is.EqualTo(new Vector2(9f, 0f))
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(firstUse.Direction, Is.EqualTo(Vector2.zero)
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(firstUse.Owner, Is.Null);
        Assert.That(firstUse.ActiveStats.Speed, Is.Zero);
        Assert.That(firstUse.GetComponent<Rigidbody2D>().linearVelocity, Is.EqualTo(Vector2.zero)
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(firstUse.GetComponent<Rigidbody2D>().simulated, Is.False);

        BulletStats secondStats = new(4f, 1f);
        Bullet secondUse = pool.Spawn(new Vector2(3f, 0f), Vector2.left, secondOwner, in secondStats);

        Assert.That(secondUse, Is.SameAs(firstUse));
        Assert.That(secondUse.Owner, Is.SameAs(secondOwner));
        Assert.That(secondUse.Direction, Is.EqualTo(Vector2.left)
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(secondUse.ActiveStats.Speed, Is.EqualTo(4f));
        Assert.That(secondUse.ActiveStats.Impulse, Is.EqualTo(1f));
        Assert.That(secondUse.GetComponent<Rigidbody2D>().linearVelocity, Is.EqualTo(new Vector2(-4f, 0f))
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(
            Vector2.Dot((Vector2)secondUse.transform.right, Vector2.left),
            Is.GreaterThan(0.999f),
            "A reused bullet must rotate its visual toward its new travel direction.");

        secondUse.RequestRecycle();
    }

    [UnityTest]
    public IEnumerator BulletCrossesPlatformAndReturnsAtDeadZone()
    {
        DisableScenePlayerColliders();
        CreateBox("Platform", new Vector2(3f, 4f), platformLayer, new Vector2(1f, 2f));

        Bullet bullet = pool.Spawn(new Vector2(0f, 4f), Vector2.right, null);
        Rigidbody2D body = bullet.GetComponent<Rigidbody2D>();
        for (int i = 0; i < 30 && bullet.IsSpawned && body.position.x <= 4.2f; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(bullet.IsSpawned, Is.True, "Bullet was consumed before crossing the platform.");
        Assert.That(body.position.x, Is.GreaterThan(4.2f));

        yield return WaitForFixedSteps(() => !bullet.IsSpawned, 100);
        Assert.That(bullet.gameObject.activeSelf, Is.False);
        Assert.That(pool.CountInactive, Is.EqualTo(16));
    }

    [UnityTest]
    public IEnumerator BulletReturnsAfterFiveSecondSafetyLifetime()
    {
        DisableScenePlayerColliders();
        BulletStats stationaryStats = new(0f, 5f);
        Bullet bullet = pool.Spawn(new Vector2(0f, 3f), Vector2.right, null, in stationaryStats);

        for (int i = 0; i < 240; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(bullet.IsSpawned, Is.True);
        yield return WaitForFixedSteps(() => !bullet.IsSpawned, 20);
        Assert.That(bullet.gameObject.activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator PrewarmedCapacityDoesNotCreateExtraInstances()
    {
        int countBefore = pool.CountAll;
        HashSet<int> expectedInstanceIds = null;

        for (int cycle = 0; cycle < 3; cycle++)
        {
            List<Bullet> bullets = new(16);
            HashSet<int> currentInstanceIds = new();
            for (int i = 0; i < 16; i++)
            {
                Bullet bullet = pool.Spawn(new Vector2(0f, 4f), Vector2.right, null);
                bullets.Add(bullet);
                currentInstanceIds.Add(bullet.GetInstanceID());
            }

            expectedInstanceIds ??= currentInstanceIds;
            CollectionAssert.AreEquivalent(expectedInstanceIds, currentInstanceIds);
            Assert.That(countBefore, Is.EqualTo(16));
            Assert.That(pool.CountAll, Is.EqualTo(16));
            Assert.That(pool.CountActive, Is.EqualTo(16));
            Assert.That(pool.CountInactive, Is.Zero);

            foreach (Bullet bullet in bullets)
            {
                bullet.RequestRecycle();
            }

            Assert.That(pool.CountAll, Is.EqualTo(16));
            Assert.That(pool.CountActive, Is.Zero);
            Assert.That(pool.CountInactive, Is.EqualTo(16));
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator FacingPersistsAtRestAndShooterUsesIt()
    {
        PlayerController2D player = FindHumanPlayerController();
        PlayerShooter shooter = player.GetComponent<PlayerShooter>();
        Assert.That(player, Is.Not.Null);
        Assert.That(shooter, Is.Not.Null);
        Weapon weapon = shooter.EquippedWeapon;
        Assert.That(weapon, Is.Not.Null);
        SpriteRenderer visualRenderer = player.transform.Find("Visual")
            .GetComponent<SpriteRenderer>();
        Assert.That(visualRenderer, Is.Not.Null);
        Assert.That(player.FacingDirection, Is.EqualTo(Vector2.right)
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(visualRenderer.flipX, Is.False);

        player.SetHorizontalInput(-1f);
        player.SetHorizontalInput(0f);
        Assert.That(player.FacingDirection, Is.EqualTo(Vector2.left)
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(visualRenderer.flipX, Is.True);

        Bullet bullet = shooter.Fire();
        Assert.That(bullet, Is.Not.Null);
        Assert.That(weapon.transform.parent.localScale.x, Is.LessThan(0f));
        Assert.That((Vector2)bullet.transform.position, Is.EqualTo(weapon.MuzzlePosition)
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(bullet.Direction, Is.EqualTo(Vector2.left)
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(bullet.ActiveStats.Speed, Is.EqualTo(weapon.Definition.BulletSpeed));
        Assert.That(bullet.ActiveStats.Impulse, Is.EqualTo(weapon.Definition.Impulse));
        Assert.That(bullet.Owner, Is.SameAs(player.gameObject));
        bullet.RequestRecycle();

        player.SetHorizontalInput(1f);
        player.SetHorizontalInput(0f);
        Assert.That(player.FacingDirection, Is.EqualTo(Vector2.right)
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(visualRenderer.flipX, Is.False);
        yield return null;
        Assert.That(weapon.transform.parent.localScale.x, Is.GreaterThan(0f));
    }

    [UnityTest]
    public IEnumerator WeaponFireIntervalBlocksEarlyShotAndAllowsNextShot()
    {
        PlayerShooter shooter = FindHumanPlayerController().GetComponent<PlayerShooter>();
        Weapon weapon = shooter.EquippedWeapon;

        Assert.That(shooter.CanFire, Is.True);

        Bullet firstBullet = shooter.Fire();
        Assert.That(firstBullet, Is.Not.Null);
        Assert.That(shooter.CanFire, Is.False);
        Assert.That(shooter.RemainingFireCooldown, Is.GreaterThan(0f));
        firstBullet.RequestRecycle();

        Assert.That(shooter.Fire(), Is.Null);

        yield return new WaitForSeconds(weapon.FireInterval + 0.05f);

        Assert.That(shooter.CanFire, Is.True);
        Bullet secondBullet = shooter.Fire();
        Assert.That(secondBullet, Is.Not.Null);
        secondBullet.RequestRecycle();
    }

    [UnityTest]
    public IEnumerator EquippingWeaponSwapsVisualAndProjectileStatsTogether()
    {
        PlayerShooter shooter = FindHumanPlayerController().GetComponent<PlayerShooter>();
        Weapon weapon = shooter.EquippedWeapon;
        WeaponDefinition originalDefinition = weapon.Definition;
        WeaponDefinition alternateDefinition = ScriptableObject.CreateInstance<WeaponDefinition>();
        Texture2D texture = new(1, 1);
        texture.SetPixel(0, 0, Color.magenta);
        texture.Apply();
        Sprite alternateSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        BulletStats alternateStats = new(23f, 11f);
        alternateDefinition.Configure(
            "Alternate Weapon",
            alternateSprite,
            in alternateStats,
            0.2f,
            new Vector2(0.4f, 0.15f),
            new Vector2(0.42f, 0.05f));

        try
        {
            weapon.Renderer.sprite = alternateSprite;
            weapon.gameObject.SetActive(false);
            weapon.gameObject.SetActive(true);
            Assert.That(weapon.Renderer.sprite, Is.SameAs(alternateSprite),
                "A sprite assigned directly to WeaponVisual must not be overwritten when Weapon is enabled.");

            Assert.That(shooter.EquipWeapon(alternateDefinition), Is.True);
            Assert.That(weapon.Definition, Is.SameAs(alternateDefinition));
            Assert.That(weapon.Definition.FireInterval, Is.EqualTo(0.4f));
            Assert.That(weapon.Renderer.sprite, Is.SameAs(alternateSprite));
            Assert.That(weapon.Muzzle.localPosition,
                Is.EqualTo(new Vector3(0.42f, 0.05f, 0f)));

            Bullet bullet = shooter.Fire();
            Assert.That(bullet, Is.Not.Null);
            Assert.That(bullet.ActiveStats.Speed, Is.EqualTo(23f));
            Assert.That(bullet.ActiveStats.Impulse, Is.EqualTo(11f));
            Assert.That((Vector2)bullet.transform.position, Is.EqualTo(weapon.MuzzlePosition)
                .Using(Vector2ComparerWithEqualsOperator.Instance));
            bullet.RequestRecycle();
        }
        finally
        {
            shooter.EquipWeapon(originalDefinition);
            Object.Destroy(alternateSprite);
            Object.Destroy(texture);
            Object.Destroy(alternateDefinition);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator ZAttackBindingAndShooterSpawnAreConfigured()
    {
        PlayerController2D player = FindHumanPlayerController();
        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        PlayerCommandReceiver commandReceiver = player.GetComponent<PlayerCommandReceiver>();
        PlayerInputCommandSource inputSource = player.GetComponent<PlayerInputCommandSource>();
        InputAction attackAction = playerInput.actions.FindAction(
            "Player/Attack",
            throwIfNotFound: true);
        bool hasZBinding = false;
        foreach (InputBinding binding in attackAction.bindings)
        {
            if (binding.path == "<Keyboard>/z")
            {
                hasZBinding = true;
                break;
            }
        }

        Assert.That(hasZBinding, Is.True);
        Assert.That(inputSource.IsSubscribed, Is.True);
        Assert.That(commandReceiver, Is.Not.Null);

        Keyboard keyboard = null;
        Mouse mouse = null;
        Bullet bullet = null;
        try
        {
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            playerInput.neverAutoSwitchControlSchemes = true;
            playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);

            Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
            playerBody.linearVelocity = Vector2.zero;
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(Key.RightArrow));
            InputSystem.Update();
            yield return new WaitForFixedUpdate();
            Assert.That(playerBody.linearVelocity.x, Is.GreaterThan(0f));

            inputSource.enabled = false;
            yield return new WaitForFixedUpdate();
            Assert.That(playerBody.linearVelocity.x, Is.Zero.Within(0.001f));

            inputSource.enabled = true;
            yield return new WaitForFixedUpdate();
            Assert.That(playerBody.linearVelocity.x, Is.GreaterThan(0f),
                "Re-enabling the input source must restore the currently held movement input.");

            int activeBefore = pool.CountActive;
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(Key.RightArrow, Key.Z));
            InputSystem.Update();

            Assert.That(pool.CountActive, Is.EqualTo(activeBefore + 1));
            foreach (Bullet activeBullet in Object.FindObjectsByType<Bullet>(
                         FindObjectsSortMode.None))
            {
                if (activeBullet.IsSpawned && activeBullet.Owner == player.gameObject)
                {
                    bullet = activeBullet;
                    break;
                }
            }

            Assert.That(bullet, Is.Not.Null);

            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(Key.RightArrow));
            InputSystem.Update();
            Assert.That(pool.CountActive, Is.EqualTo(activeBefore + 1),
                "Releasing Z must not fire a second bullet.");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }
        finally
        {
            if (bullet != null)
            {
                bullet.RequestRecycle();
            }

            if (mouse != null)
            {
                InputSystem.RemoveDevice(mouse);
            }

            if (keyboard != null)
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator PlayerControllerExecutesCommandsWithoutPlayerInput()
    {
        GameObject commandDrivenPlayer = new("CommandDrivenPlayer");
        commandDrivenPlayer.SetActive(false);
        temporaryObjects.Add(commandDrivenPlayer);

        Rigidbody2D body = commandDrivenPlayer.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.position = new Vector2(-20f, 20f);
        commandDrivenPlayer.AddComponent<BoxCollider2D>();
        PlayerController2D controller = commandDrivenPlayer.AddComponent<PlayerController2D>();
        PlayerCommandReceiver commandReceiver =
            commandDrivenPlayer.AddComponent<PlayerCommandReceiver>();
        commandDrivenPlayer.SetActive(true);

        Assert.That(commandDrivenPlayer.GetComponent<PlayerInput>(), Is.Null);
        Assert.That(commandDrivenPlayer.GetComponent<PlayerInputCommandSource>(), Is.Null);

        PlayerCommand moveLeft = new(-1f);
        commandReceiver.SubmitCommand(in moveLeft);
        yield return new WaitForFixedUpdate();

        Assert.That(body.linearVelocity.x, Is.LessThan(0f));
        Assert.That(controller.FacingDirection, Is.EqualTo(Vector2.left)
            .Using(Vector2ComparerWithEqualsOperator.Instance));
    }

    [UnityTest]
    public IEnumerator AimHorizontalOverridesFacingWithoutChangingMovement()
    {
        GameObject commandDrivenPlayer = new("AimCommandDrivenPlayer");
        commandDrivenPlayer.SetActive(false);
        temporaryObjects.Add(commandDrivenPlayer);

        Rigidbody2D body = commandDrivenPlayer.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.position = new Vector2(-20f, 20f);
        commandDrivenPlayer.AddComponent<BoxCollider2D>();
        PlayerController2D controller = commandDrivenPlayer.AddComponent<PlayerController2D>();
        PlayerCommandReceiver commandReceiver =
            commandDrivenPlayer.AddComponent<PlayerCommandReceiver>();
        commandDrivenPlayer.SetActive(true);

        PlayerCommand moveRightAimLeft = new(1f, aimHorizontal: -1f);
        commandReceiver.SubmitCommand(in moveRightAimLeft);

        Assert.That(controller.FacingDirection, Is.EqualTo(Vector2.left)
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        yield return new WaitForFixedUpdate();
        Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));

        PlayerCommand moveRightWithoutAimOverride = new(1f);
        commandReceiver.SubmitCommand(in moveRightWithoutAimOverride);

        Assert.That(controller.FacingDirection, Is.EqualTo(Vector2.right)
            .Using(Vector2ComparerWithEqualsOperator.Instance));
    }

    [UnityTest]
    public IEnumerator HorizontalMovementAndBulletImpulseAreAppliedByThePlayerController()
    {
        PlayerController2D player = FindHumanPlayerController();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        BulletImpulseReceiver receiver = player.GetComponent<BulletImpulseReceiver>();

        body.linearVelocity = Vector2.zero;
        player.SetHorizontalInput(1f);
        yield return new WaitForFixedUpdate();
        Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));

        player.SetHorizontalInput(0f);
        BulletHitData hitData = new(Vector2.right * 5f, body.position, null);
        receiver.ReceiveBulletHit(in hitData);
        Assert.That(body.linearVelocity.x, Is.EqualTo(5f).Within(0.001f));

        yield return new WaitForFixedUpdate();
        Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
        Assert.That(body.linearVelocity.x, Is.LessThan(5f));
    }

    [UnityTest]
    public IEnumerator PlayerCanJumpFromTheOneWayPlatform()
    {
        PlayerController2D player = FindHumanPlayerController();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        SpriteRenderer visualRenderer = player.transform.Find("Visual")
            .GetComponent<SpriteRenderer>();
        EdgeCollider2D platform = PreparePlayerOnHighestPlatform(player, body);

        for (int i = 0; i < 20 && !player.IsGrounded; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(platform, Is.Not.Null);
        Assert.That(player.IsGrounded, Is.True);

        Sprite groundedSprite = visualRenderer.sprite;
        Texture2D airborneTexture = new(1, 1);
        airborneTexture.SetPixel(0, 0, Color.white);
        airborneTexture.Apply();
        Sprite airborneSprite = Sprite.Create(
            airborneTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        player.SetMovementSprites(groundedSprite, airborneSprite);
        Assert.That(visualRenderer.sprite, Is.SameAs(groundedSprite));

        body.gravityScale = 0f;
        player.RequestJump();
        yield return new WaitForFixedUpdate();

        Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
        Assert.That(player.IsGrounded, Is.False);
        Assert.That(visualRenderer.sprite, Is.SameAs(airborneSprite));
        Assert.That(player.RemainingAdditionalJumps, Is.EqualTo(1));

        body.linearVelocity = new Vector2(body.linearVelocity.x, -1f);
        player.RequestJump();
        yield return new WaitForFixedUpdate();

        Assert.That(body.linearVelocity.y, Is.EqualTo(player.JumpSpeed).Within(0.001f));
        Assert.That(player.RemainingAdditionalJumps, Is.Zero);

        body.linearVelocity = new Vector2(body.linearVelocity.x, -1f);
        player.RequestJump();
        yield return new WaitForFixedUpdate();

        Assert.That(body.linearVelocity.y, Is.LessThan(0f));
        Assert.That(player.RemainingAdditionalJumps, Is.Zero);

        player.SetMovementSprites(groundedSprite, null);
        Object.Destroy(airborneSprite);
        Object.Destroy(airborneTexture);
    }

    [UnityTest]
    public IEnumerator PlayerCanDropThroughAndRestoresPlatformCollision()
    {
        PlayerController2D player = FindHumanPlayerController();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        BoxCollider2D playerCollider = player.GetComponent<BoxCollider2D>();
        EdgeCollider2D platform = PreparePlayerOnHighestPlatform(player, body);

        for (int i = 0; i < 20 && !player.IsGrounded; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(player.IsGrounded, Is.True);

        player.RequestDrop();
        yield return new WaitForFixedUpdate();

        Assert.That(player.IsDroppingThroughPlatform, Is.True);
        Assert.That(Physics2D.GetIgnoreCollision(playerCollider, platform), Is.True);

        for (int i = 0; i < 60 && player.IsDroppingThroughPlatform; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(player.IsDroppingThroughPlatform, Is.False);
        Assert.That(Physics2D.GetIgnoreCollision(playerCollider, platform), Is.False);
        Assert.That(playerCollider.bounds.max.y, Is.LessThan(platform.bounds.min.y));
    }

    [UnityTest]
    public IEnumerator BottomFallDeathZoneConsumesOneLifeAndRespawnsPlayer()
    {
        PlayerController2D player = FindHumanPlayerController();
        PlayerLife playerLife = player.GetComponent<PlayerLife>();
        PlayerRespawner respawner = player.GetComponent<PlayerRespawner>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        PlayerFallDeathZone fallDeathZone = FindPlayerFallDeathZone("PlayerFallDeathZone");
        AIPlayerCommandSource aiSource = Object.FindFirstObjectByType<AIPlayerCommandSource>();
        BoxCollider2D fallDeathCollider = fallDeathZone.GetComponent<BoxCollider2D>();

        Assert.That(aiSource, Is.Not.Null);
        aiSource.enabled = false;
        Assert.That(playerLife.Life, Is.EqualTo(3));
        body.gravityScale = 0f;
        body.linearVelocity = new Vector2(3f, -5f);
        body.position = fallDeathCollider.bounds.center;
        player.transform.position = fallDeathCollider.bounds.center;
        Physics2D.SyncTransforms();

        for (int i = 0; i < 5 && playerLife.Life == 3; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(playerLife.Life, Is.EqualTo(2));
        Assert.That(player.gameObject.activeSelf, Is.False);

        yield return new WaitForSeconds(respawner.RespawnDelay * 0.5f);
        Assert.That(player.gameObject.activeSelf, Is.False);

        yield return new WaitForSeconds(respawner.RespawnDelay * 0.5f + 0.1f);
        Assert.That(player.gameObject.activeSelf, Is.True);
        Assert.That((Vector2)player.transform.position, Is.EqualTo(respawner.RespawnPosition)
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(body.position, Is.EqualTo(respawner.RespawnPosition)
            .Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero)
            .Using(Vector2ComparerWithEqualsOperator.Instance));
    }

    [UnityTest]
    public IEnumerator BottomFallDeathZoneDoesNotRespawnAfterLastLifeIsLost()
    {
        PlayerController2D player = FindHumanPlayerController();
        PlayerLife playerLife = player.GetComponent<PlayerLife>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        PlayerFallDeathZone fallDeathZone = FindPlayerFallDeathZone("PlayerFallDeathZone");
        MatchController matchController = Object.FindFirstObjectByType<MatchController>();
        AIPlayerCommandSource aiSource = Object.FindFirstObjectByType<AIPlayerCommandSource>();
        BoxCollider2D fallDeathCollider = fallDeathZone.GetComponent<BoxCollider2D>();

        Assert.That(aiSource, Is.Not.Null);
        PlayerLife survivingAi = aiSource.GetComponent<PlayerLife>();
        aiSource.enabled = false;

        Assert.That(playerLife.TryLoseLife(), Is.True);
        Assert.That(playerLife.TryLoseLife(), Is.True);
        Assert.That(playerLife.Life, Is.EqualTo(1));

        body.linearVelocity = Vector2.down;
        body.position = fallDeathCollider.bounds.center;
        player.transform.position = fallDeathCollider.bounds.center;
        Physics2D.SyncTransforms();

        for (int i = 0; i < 5 && player.gameObject.activeSelf; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(playerLife.Life, Is.Zero);
        Assert.That(player.gameObject.activeSelf, Is.False);

        for (int i = 0; i < 3 && !matchController.IsMatchFinished; i++)
        {
            yield return null;
        }

        Assert.That(matchController.State, Is.EqualTo(MatchState.Finished));
        Assert.That(matchController.Winner, Is.SameAs(survivingAi));
    }

    [UnityTest]
    public IEnumerator SideFallDeathZonesConsumeLifeAndRespawnPlayer()
    {
        PlayerController2D player = FindHumanPlayerController();
        PlayerLife playerLife = player.GetComponent<PlayerLife>();
        PlayerRespawner respawner = player.GetComponent<PlayerRespawner>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        AIPlayerCommandSource aiSource = Object.FindFirstObjectByType<AIPlayerCommandSource>();
        BoxCollider2D leftFallDeathCollider = FindPlayerFallDeathZone(
            "PlayerFallDeathZone_Left").GetComponent<BoxCollider2D>();
        BoxCollider2D rightFallDeathCollider = FindPlayerFallDeathZone(
            "PlayerFallDeathZone_Right").GetComponent<BoxCollider2D>();

        Assert.That(aiSource, Is.Not.Null);
        aiSource.enabled = false;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        body.position = leftFallDeathCollider.bounds.center;
        player.transform.position = leftFallDeathCollider.bounds.center;
        Physics2D.SyncTransforms();

        for (int i = 0; i < 5 && playerLife.Life == 3; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(playerLife.Life, Is.EqualTo(2));
        Assert.That(player.gameObject.activeSelf, Is.False);

        yield return new WaitForSeconds(respawner.RespawnDelay + 0.1f);
        Assert.That(player.gameObject.activeSelf, Is.True);

        body.linearVelocity = Vector2.zero;
        body.position = rightFallDeathCollider.bounds.center;
        player.transform.position = rightFallDeathCollider.bounds.center;
        Physics2D.SyncTransforms();

        for (int i = 0; i < 5 && playerLife.Life == 2; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(playerLife.Life, Is.EqualTo(1));
        Assert.That(player.gameObject.activeSelf, Is.False);

        yield return new WaitForSeconds(respawner.RespawnDelay + 0.1f);
        Assert.That(player.gameObject.activeSelf, Is.True);
        Assert.That((Vector2)player.transform.position, Is.EqualTo(respawner.RespawnPosition)
            .Using(Vector2ComparerWithEqualsOperator.Instance));
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

    private static PlayerController2D FindHumanPlayerController()
    {
        PlayerInputCommandSource inputSource =
            Object.FindFirstObjectByType<PlayerInputCommandSource>();
        return inputSource != null
            ? inputSource.GetComponent<PlayerController2D>()
            : null;
    }

    private static HashSet<string> GetKeyboardBindingPaths(InputAction action)
    {
        HashSet<string> paths = new();
        foreach (InputBinding binding in action.bindings)
        {
            if (binding.path.StartsWith("<Keyboard>/"))
            {
                paths.Add(binding.path);
            }
        }

        return paths;
    }

    private static PlayerFallDeathZone FindPlayerFallDeathZone(string objectName)
    {
        PlayerFallDeathZone[] fallDeathZones =
            Object.FindObjectsByType<PlayerFallDeathZone>(FindObjectsSortMode.None);
        foreach (PlayerFallDeathZone fallDeathZone in fallDeathZones)
        {
            if (fallDeathZone.name == objectName)
            {
                return fallDeathZone;
            }
        }

        return null;
    }

    private void DisableScenePlayerColliders()
    {
        Collider2D[] colliders = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        foreach (Collider2D collider in colliders)
        {
            if (collider.gameObject.layer == playerLayer)
            {
                collider.enabled = false;
            }
        }
    }

    private static EdgeCollider2D PreparePlayerOnHighestPlatform(
        PlayerController2D player,
        Rigidbody2D body)
    {
        EdgeCollider2D[] platforms = Object.FindObjectsByType<EdgeCollider2D>(
            FindObjectsSortMode.None);
        Assert.That(platforms.Length, Is.GreaterThan(0));

        EdgeCollider2D highestPlatform = platforms[0];
        for (int i = 1; i < platforms.Length; i++)
        {
            if (platforms[i].bounds.max.y > highestPlatform.bounds.max.y)
            {
                highestPlatform = platforms[i];
            }
        }

        foreach (EdgeCollider2D platform in platforms)
        {
            platform.enabled = platform == highestPlatform;
        }

        BoxCollider2D playerCollider = player.GetComponent<BoxCollider2D>();
        float halfHeight = playerCollider.bounds.extents.y;
        Vector2 position = new(
            highestPlatform.bounds.center.x,
            highestPlatform.bounds.max.y + halfHeight + 0.01f);

        player.SetHorizontalInput(0f);
        body.linearVelocity = Vector2.zero;
        body.position = position;
        player.transform.position = position;
        Physics2D.SyncTransforms();
        return highestPlatform;
    }

    private GameObject CreateBox(string objectName, Vector2 position, int layer, Vector2 size)
    {
        GameObject gameObject = Track(new GameObject(objectName));
        gameObject.layer = layer;
        gameObject.transform.position = position;
        gameObject.AddComponent<BoxCollider2D>().size = size;
        return gameObject;
    }

    private GameObject Track(GameObject gameObject)
    {
        temporaryObjects.Add(gameObject);
        return gameObject;
    }

    private static IEnumerator WaitForFixedSteps(System.Func<bool> condition, int maximumSteps)
    {
        for (int i = 0; i < maximumSteps && !condition(); i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(condition(), Is.True, "Timed out while waiting for a physics result.");
    }
}

public sealed class BulletHitReceiverSpy : MonoBehaviour, IBulletHitReceiver
{
    public int HitCount { get; private set; }
    public BulletHitData LastHit { get; private set; }

    public void ReceiveBulletHit(in BulletHitData hitData)
    {
        HitCount++;
        LastHit = hitData;
    }
}
