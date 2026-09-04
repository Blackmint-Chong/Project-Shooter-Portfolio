using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class AIPlayerPrefabEditorTests
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/PF_Player.prefab";
    private const string AiPrefabPath = "Assets/Prefabs/Player/PF_AIPlayer.prefab";
    private const string BarrierSpritePath = "Assets/Art/fx/barrier.png";
    private const string BasicDifficultyPath =
        "Assets/Data/AI/BasicAIDifficulty.asset";
    private const string DifficultyCatalogPath =
        "Assets/Data/AI/AIDifficultyCatalog.asset";

    [Test]
    public void AiPrefabKeepsPlayerActorStructureAndNoHumanInput()
    {
        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(AiPrefabPath);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.name, Is.EqualTo("PF_AIPlayer"));
            Assert.That(root.layer, Is.EqualTo(LayerMask.NameToLayer("Player")));

            PlayerController2D controller = root.GetComponent<PlayerController2D>();
            PlayerShooter shooter = root.GetComponent<PlayerShooter>();
            PlayerCommandReceiver receiver = root.GetComponent<PlayerCommandReceiver>();
            AIPlayerCommandSource aiSource = root.GetComponent<AIPlayerCommandSource>();
            PlayerLife selfLife = root.GetComponent<PlayerLife>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(shooter, Is.Not.Null);
            Assert.That(receiver, Is.Not.Null);
            Assert.That(aiSource, Is.Not.Null);
            Assert.That(root.GetComponent<Rigidbody2D>(), Is.Not.Null);
            Assert.That(root.GetComponent<BoxCollider2D>(), Is.Not.Null);
            Assert.That(root.GetComponent<BulletImpulseReceiver>(), Is.Not.Null);
            Assert.That(root.GetComponent<PlayerLife>(), Is.Not.Null);
            Assert.That(root.GetComponent<PlayerRespawner>(), Is.Not.Null);
            Assert.That(receiver.Controller, Is.SameAs(controller));
            Assert.That(receiver.Shooter, Is.SameAs(shooter));
            Assert.That(aiSource.CommandReceiver, Is.SameAs(receiver));
            Assert.That(aiSource.SelfLife, Is.SameAs(selfLife));
            Assert.That(aiSource.MatchController, Is.Null);
            AIDifficultyDefinition basicDifficulty =
                AssetDatabase.LoadAssetAtPath<AIDifficultyDefinition>(
                    BasicDifficultyPath);
            Assert.That(basicDifficulty, Is.Not.Null);
            Assert.That(basicDifficulty.TryValidate(
                out string difficultyError), Is.True,
                difficultyError);
            Assert.That(aiSource.DifficultyProfile,
                Is.SameAs(basicDifficulty));
            aiSource.ApplyDifficultyProfile(basicDifficulty);
            Assert.That(aiSource.DecisionInterval,
                Is.EqualTo(basicDifficulty.DecisionInterval));
            Assert.That(aiSource.RoamDurationRange,
                Is.EqualTo(basicDifficulty.RoamDurationRange));
            Assert.That(aiSource.ExplorationIntervalRange,
                Is.EqualTo(basicDifficulty.ExplorationIntervalRange));
            Assert.That(aiSource.ExplorationDropChance,
                Is.EqualTo(basicDifficulty.ExplorationDropChance));
            Assert.That(aiSource.HeightActionThreshold,
                Is.EqualTo(basicDifficulty.HeightActionThreshold));
            Assert.That(aiSource.ShootHeightTolerance,
                Is.EqualTo(basicDifficulty.ShootHeightTolerance));

            SerializedObject serializedAi = new(aiSource);
            Assert.That(
                serializedAi.FindProperty("platformLayers").intValue,
                Is.EqualTo(1 << LayerMask.NameToLayer("OneWayPlatform")));
            Assert.That(
                serializedAi.FindProperty("roamDurationRange").vector2Value.x,
                Is.GreaterThan(0f));
            Vector2 explorationInterval = serializedAi
                .FindProperty("explorationIntervalRange")
                .vector2Value;
            Assert.That(explorationInterval.x, Is.GreaterThan(0f));
            Assert.That(explorationInterval.y,
                Is.GreaterThanOrEqualTo(explorationInterval.x));
            Assert.That(
                serializedAi.FindProperty("explorationDropChance").floatValue,
                Is.InRange(0f, 1f));
            Assert.That(aiSource.HeightActionThreshold,
                Is.EqualTo(aiSource.ShootHeightTolerance));
            Assert.That(
                serializedAi.FindProperty("recoveryVerticalSearchDistance").floatValue,
                Is.GreaterThan(0f));

            Assert.That(root.GetComponentInChildren<PlayerInput>(true), Is.Null);
            Assert.That(
                root.GetComponentInChildren<PlayerInputCommandSource>(true),
                Is.Null);

            Transform bodyVisual = root.transform.Find("Visual");
            Transform weaponRoot = root.transform.Find("WeaponFacingRoot/Weapon");
            Transform weaponVisual = root.transform.Find(
                "WeaponFacingRoot/Weapon/WeaponVisual");
            Transform muzzle = root.transform.Find("WeaponFacingRoot/Weapon/Muzzle");
            Assert.That(bodyVisual, Is.Not.Null);
            Assert.That(weaponRoot, Is.Not.Null);
            Assert.That(weaponVisual, Is.Not.Null);
            Assert.That(muzzle, Is.Not.Null);

            SpriteRenderer bodyRenderer = bodyVisual.GetComponent<SpriteRenderer>();
            SpriteRenderer weaponRenderer = weaponVisual.GetComponent<SpriteRenderer>();
            Weapon weapon = weaponRoot.GetComponent<Weapon>();
            Assert.That(bodyRenderer, Is.Not.Null);
            Assert.That(weaponRenderer, Is.Not.Null);
            Assert.That(weaponRenderer.sprite, Is.Not.Null);
            Assert.That(weapon, Is.Not.Null);
            Assert.That(weapon.Renderer, Is.SameAs(weaponRenderer));
            Assert.That(weapon.Muzzle, Is.SameAs(muzzle));
            Assert.That(shooter.EquippedWeapon, Is.SameAs(weapon));

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(child.gameObject.layer,
                    Is.EqualTo(LayerMask.NameToLayer("Player")));
            }
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    [Test]
    public void DifficultyCatalogContainsTheConfiguredBasicProfile()
    {
        AIDifficultyCatalog catalog =
            AssetDatabase.LoadAssetAtPath<AIDifficultyCatalog>(
                DifficultyCatalogPath);
        AIDifficultyDefinition basic =
            AssetDatabase.LoadAssetAtPath<AIDifficultyDefinition>(
                BasicDifficultyPath);

        Assert.That(catalog, Is.Not.Null);
        Assert.That(basic, Is.Not.Null);
        Assert.That(catalog.TryValidate(out string error), Is.True, error);
        Assert.That(catalog.DefaultProfile, Is.SameAs(basic));
        Assert.That(catalog.Profiles, Does.Contain(basic));
        Assert.That(catalog.TryGetProfile(
            "BASIC",
            out AIDifficultyDefinition resolved), Is.True);
        Assert.That(resolved, Is.SameAs(basic));
    }

    [TestCase(PlayerPrefabPath)]
    [TestCase(AiPrefabPath)]
    public void PlayerPrefabHasConfiguredRespawnShield(string prefabPath)
    {
        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(prefabPath);
            PlayerRespawnShield shield = root.GetComponent<PlayerRespawnShield>();
            Transform barrierVisual = root.transform.Find("BarrierVisual");
            Transform bodyVisual = root.transform.Find("Visual");
            Transform weaponVisual = root.transform.Find(
                "WeaponFacingRoot/Weapon/WeaponVisual");

            Assert.That(shield, Is.Not.Null);
            Assert.That(shield.Duration, Is.EqualTo(3f));
            Assert.That(shield.ImpulseReduction, Is.EqualTo(0.8f));
            Assert.That(shield.IsActive, Is.False);
            Assert.That(barrierVisual, Is.Not.Null);
            Assert.That(barrierVisual.parent, Is.SameAs(root.transform));
            Assert.That(barrierVisual.gameObject.activeSelf, Is.False);
            Assert.That(barrierVisual.gameObject.layer,
                Is.EqualTo(LayerMask.NameToLayer("Player")));
            Assert.That(barrierVisual.GetComponent<Collider2D>(), Is.Null);

            SpriteRenderer barrierRenderer =
                barrierVisual.GetComponent<SpriteRenderer>();
            SpriteRenderer bodyRenderer = bodyVisual.GetComponent<SpriteRenderer>();
            SpriteRenderer weaponRenderer =
                weaponVisual.GetComponent<SpriteRenderer>();
            Assert.That(barrierRenderer, Is.Not.Null);
            Assert.That(shield.BarrierRenderer, Is.SameAs(barrierRenderer));
            Assert.That(barrierRenderer.sprite, Is.Not.Null);
            Assert.That(barrierRenderer.sprite.name, Is.EqualTo("barrier_0"));
            Assert.That(AssetDatabase.GetAssetPath(barrierRenderer.sprite),
                Is.EqualTo(BarrierSpritePath));
            Assert.That(barrierRenderer.sortingOrder,
                Is.GreaterThan(bodyRenderer.sortingOrder));
            Assert.That(barrierRenderer.sortingOrder,
                Is.GreaterThan(weaponRenderer.sortingOrder));
            Assert.That(barrierRenderer.color.a, Is.InRange(0.4f, 0.5f));
            Assert.That(barrierVisual.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(barrierVisual.localScale,
                Is.EqualTo(new Vector3(0.15f, 0.15f, 1f)));
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    [TestCase(PlayerPrefabPath)]
    [TestCase(AiPrefabPath)]
    public void BodyVisualSpriteIsAssignedByControllerAtRuntime(string prefabPath)
    {
        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(prefabPath);
            PlayerController2D controller = root.GetComponent<PlayerController2D>();
            SpriteRenderer bodyRenderer = root.transform.Find("Visual")
                .GetComponent<SpriteRenderer>();
            SerializedObject serializedController = new(controller);
            Sprite groundedSprite = serializedController.FindProperty("groundedSprite")
                .objectReferenceValue as Sprite;
            Sprite airborneSprite = serializedController.FindProperty("airborneSprite")
                .objectReferenceValue as Sprite;

            Assert.That(bodyRenderer.sprite, Is.Null,
                "Visual.sprite is runtime output; assign state sprites on PlayerController2D.");
            Assert.That(
                serializedController.FindProperty("visualRenderer").objectReferenceValue,
                Is.SameAs(bodyRenderer));
            Assert.That(groundedSprite, Is.Not.Null);
            Assert.That(airborneSprite, Is.Not.Null);

            if (prefabPath == AiPrefabPath)
            {
                Assert.That(groundedSprite.name, Is.EqualTo("idle_w_0"));
                Assert.That(airborneSprite.name, Is.EqualTo("jump_w_1"));
            }
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
