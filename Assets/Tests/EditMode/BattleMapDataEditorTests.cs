using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BattleMapDataEditorTests
{
    private const string CatalogAssetPath =
        "Assets/Data/Maps/BattleMapCatalog.asset";
    private const string FactoryMapAssetPath =
        "Assets/Data/Maps/FactoryMap.asset";
    private const string FactoryMapScenePath =
        "Assets/Scenes/Battle/FactoryMap.unity";
    private const string TestScenePath =
        "Assets/Scenes/Testing/TestScene.unity";

    private readonly List<UnityEngine.Object> temporaryObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = temporaryObjects.Count - 1; index >= 0; index--)
        {
            if (temporaryObjects[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(temporaryObjects[index]);
            }
        }

        temporaryObjects.Clear();
    }

    [Test]
    public void DefinitionExposesConfiguredMetadataAndValidatesScenePath()
    {
        Texture2D thumbnailTexture = new(1, 1);
        temporaryObjects.Add(thumbnailTexture);
        Sprite thumbnail = Sprite.Create(
            thumbnailTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f));
        temporaryObjects.Add(thumbnail);

        BattleMapDefinition definition = CreateDefinition(
            "  test-map  ",
            "  Test Map  ",
            "  A map used to verify battle map data.  ",
            "  Assets\\Scenes\\Testing\\TestScene.unity  ",
            thumbnail);

        Assert.That(definition.MapId, Is.EqualTo("test-map"));
        Assert.That(definition.DisplayName, Is.EqualTo("Test Map"));
        Assert.That(definition.Description,
            Is.EqualTo("A map used to verify battle map data."));
        Assert.That(definition.Thumbnail, Is.SameAs(thumbnail));
        Assert.That(definition.ScenePath, Is.EqualTo(TestScenePath));
        Assert.That(definition.SceneName, Is.EqualTo("TestScene"));
        Assert.That(definition.TryValidate(out string validationError), Is.True,
            validationError);
    }

    [Test]
    public void DefinitionRejectsMissingIdentityAndMalformedScenePaths()
    {
        BattleMapDefinition definition = CreateDefinition(
            string.Empty,
            "Test Map",
            string.Empty,
            TestScenePath);

        AssertInvalid(definition);

        definition.Configure(
            "Invalid Map ID",
            "Test Map",
            string.Empty,
            TestScenePath,
            null);
        AssertInvalid(definition);

        definition.Configure(
            "test-map",
            "   ",
            string.Empty,
            TestScenePath,
            null);
        AssertInvalid(definition);

        definition.Configure(
            "test-map",
            "Test Map",
            string.Empty,
            "TestScene.unity",
            null);
        AssertInvalid(definition);

        definition.Configure(
            "test-map",
            "Test Map",
            string.Empty,
            "Assets/Scenes/Testing/TestScene.scene",
            null);
        AssertInvalid(definition);
    }

    [Test]
    public void CatalogPreservesOrderAndFindsMapsByStableIdentityOrScenePath()
    {
        BattleMapDefinition firstMap = CreateDefinition(
            "first-map",
            "First Map",
            string.Empty,
            "Assets/Scenes/Battle/FirstMap.unity");
        BattleMapDefinition secondMap = CreateDefinition(
            "second-map",
            "Second Map",
            string.Empty,
            "Assets/Scenes/Battle/SecondMap.unity");
        List<BattleMapDefinition> configuredMaps = new()
        {
            secondMap,
            firstMap
        };
        BattleMapCatalog catalog = CreateCatalog(firstMap, configuredMaps);

        configuredMaps.Clear();

        Assert.That(catalog.DefaultMap, Is.SameAs(firstMap));
        Assert.That(catalog.Maps, Is.EqualTo(new[] { secondMap, firstMap }));
        Assert.That(catalog.Contains(firstMap), Is.True);
        Assert.That(catalog.Contains(secondMap), Is.True);
        Assert.That(catalog.Contains(null), Is.False);
        Assert.That(catalog.TryGetMap(
            firstMap.MapId,
            out BattleMapDefinition mapById), Is.True);
        Assert.That(mapById, Is.SameAs(firstMap));
        Assert.That(catalog.TryGetMap(
            "  FIRST-MAP  ",
            out BattleMapDefinition normalizedMapById), Is.True);
        Assert.That(normalizedMapById, Is.SameAs(firstMap));
        Assert.That(catalog.TryGetMap(
            "missing-map",
            out BattleMapDefinition missingMap), Is.False);
        Assert.That(missingMap, Is.Null);
        Assert.That(catalog.TryGetMapByScenePath(
            secondMap.ScenePath,
            out BattleMapDefinition mapByScenePath), Is.True);
        Assert.That(mapByScenePath, Is.SameAs(secondMap));
        Assert.That(catalog.TryGetMapByScenePath(
            "  Assets\\Scenes\\Battle\\SecondMap.unity  ",
            out BattleMapDefinition normalizedMapByScenePath), Is.True);
        Assert.That(normalizedMapByScenePath, Is.SameAs(secondMap));
        Assert.That(catalog.TryGetMapByScenePath(
            string.Empty,
            out BattleMapDefinition missingSceneMap), Is.False);
        Assert.That(missingSceneMap, Is.Null);
        Assert.That(catalog.TryValidate(out string validationError), Is.True,
            validationError);
    }

    [Test]
    public void CatalogRejectsMissingDefaultNullEntriesDuplicateIdsAndScenePaths()
    {
        BattleMapDefinition validMap = CreateDefinition(
            "valid-map",
            "Valid Map",
            string.Empty,
            "Assets/Scenes/Battle/ValidMap.unity");
        BattleMapDefinition duplicateIdMap = CreateDefinition(
            validMap.MapId,
            "Duplicate Map",
            string.Empty,
            "Assets/Scenes/Battle/DuplicateMap.unity");
        BattleMapDefinition duplicateSceneMap = CreateDefinition(
            "duplicate-scene-map",
            "Duplicate Scene Map",
            string.Empty,
            validMap.ScenePath);
        BattleMapCatalog catalog = CreateCatalog(
            null,
            new[] { validMap });

        AssertInvalid(catalog);

        catalog.Configure(
            validMap,
            new BattleMapDefinition[] { validMap, null });
        AssertInvalid(catalog);

        catalog.Configure(validMap, new[] { validMap, duplicateIdMap });
        AssertInvalid(catalog);

        catalog.Configure(validMap, new[] { validMap, duplicateSceneMap });
        AssertInvalid(catalog);

        catalog.Configure(validMap, Array.Empty<BattleMapDefinition>());
        AssertInvalid(catalog);

        catalog.Configure(duplicateIdMap, new[] { validMap });
        AssertInvalid(catalog);
    }

    [Test]
    public void ProjectCatalogReferencesValidBuildEnabledBattleScenes()
    {
        BattleMapCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BattleMapCatalog>(CatalogAssetPath);
        BattleMapDefinition factoryMap =
            AssetDatabase.LoadAssetAtPath<BattleMapDefinition>(FactoryMapAssetPath);

        Assert.That(catalog, Is.Not.Null);
        Assert.That(factoryMap, Is.Not.Null);
        Assert.That(catalog.Maps, Has.Count.EqualTo(1));
        Assert.That(catalog.Maps[0], Is.SameAs(factoryMap));
        Assert.That(catalog.DefaultMap, Is.SameAs(factoryMap));
        Assert.That(catalog.Contains(factoryMap), Is.True);
        Assert.That(catalog.TryValidate(out string catalogError), Is.True,
            catalogError);
        Assert.That(factoryMap.TryValidate(out string mapError), Is.True, mapError);
        Assert.That(factoryMap.MapId, Is.Not.Empty);
        Assert.That(factoryMap.DisplayName, Is.Not.Empty);
        Assert.That(factoryMap.Description, Is.Not.Empty);
        Assert.That(factoryMap.Thumbnail, Is.Not.Null);
        Assert.That(factoryMap.ScenePath, Is.EqualTo(FactoryMapScenePath));
        Assert.That(factoryMap.SceneName, Is.EqualTo("FactoryMap"));
        Assert.That(catalog.TryGetMap(
            factoryMap.MapId,
            out BattleMapDefinition resolvedById), Is.True);
        Assert.That(resolvedById, Is.SameAs(factoryMap));
        Assert.That(catalog.TryGetMapByScenePath(
            factoryMap.ScenePath,
            out BattleMapDefinition resolvedByScene), Is.True);
        Assert.That(resolvedByScene, Is.SameAs(factoryMap));

        string[] enabledScenePaths = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        foreach (BattleMapDefinition map in catalog.Maps)
        {
            Assert.That(map, Is.Not.Null);
            Assert.That(map.TryValidate(out string validationError), Is.True,
                validationError);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(map.ScenePath),
                Is.Not.Null,
                $"Map '{map.MapId}' must reference an existing Scene asset.");
            Assert.That(enabledScenePaths.Count(path => path == map.ScenePath),
                Is.EqualTo(1),
                $"Map '{map.MapId}' must be enabled exactly once in Build Settings.");

            AssertValidBattleComposition(map);
        }
    }

    private BattleMapDefinition CreateDefinition(
        string mapId,
        string displayName,
        string description,
        string scenePath,
        Sprite thumbnail = null)
    {
        BattleMapDefinition definition =
            ScriptableObject.CreateInstance<BattleMapDefinition>();
        temporaryObjects.Add(definition);
        definition.Configure(
            mapId,
            displayName,
            description,
            scenePath,
            thumbnail);
        return definition;
    }

    private BattleMapCatalog CreateCatalog(
        BattleMapDefinition defaultMap,
        IEnumerable<BattleMapDefinition> maps)
    {
        BattleMapCatalog catalog =
            ScriptableObject.CreateInstance<BattleMapCatalog>();
        temporaryObjects.Add(catalog);
        catalog.Configure(defaultMap, maps);
        return catalog;
    }

    private static void AssertInvalid(BattleMapDefinition definition)
    {
        Assert.That(definition.TryValidate(out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    private static void AssertInvalid(BattleMapCatalog catalog)
    {
        Assert.That(catalog.TryValidate(out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    private static void AssertValidBattleComposition(BattleMapDefinition map)
    {
        Scene scene = SceneManager.GetSceneByPath(map.ScenePath);
        bool openedByTest = !scene.IsValid() || !scene.isLoaded;
        if (openedByTest)
        {
            scene = EditorSceneManager.OpenScene(
                map.ScenePath,
                OpenSceneMode.Additive);
        }

        try
        {
            BattleSetup[] setups = GetComponentsInScene<BattleSetup>(scene);
            BattleMapContext[] contexts =
                GetComponentsInScene<BattleMapContext>(scene);

            Assert.That(setups, Has.Length.EqualTo(1),
                $"Map '{map.MapId}' requires exactly one BattleSetup.");
            Assert.That(contexts, Has.Length.EqualTo(1),
                $"Map '{map.MapId}' requires exactly one BattleMapContext.");
            BattleMapContext context = contexts[0];
            Assert.That(context.TryValidate(out string contextError), Is.True,
                contextError);
            Assert.That(
                context.SpawnSlots.Select(spawnSlot => spawnSlot.Slot),
                Is.EquivalentTo(new[]
                {
                    BattlePlayerSlot.P1,
                    BattlePlayerSlot.P2
                }));

            AssertValidDeathZones(map, scene, context);
        }
        finally
        {
            if (openedByTest)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void AssertValidDeathZones(
        BattleMapDefinition map,
        Scene scene,
        BattleMapContext context)
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int projectileLayer = LayerMask.NameToLayer("Projectile");
        int projectileDeadZoneLayer =
            LayerMask.NameToLayer("ProjectileDeadZone");

        Assert.That(playerLayer, Is.GreaterThanOrEqualTo(0),
            "The Player layer is required.");
        Assert.That(projectileLayer, Is.GreaterThanOrEqualTo(0),
            "The Projectile layer is required.");
        Assert.That(projectileDeadZoneLayer, Is.GreaterThanOrEqualTo(0),
            "The ProjectileDeadZone layer is required.");
        Assert.That(
            Physics2D.GetIgnoreLayerCollision(
                projectileLayer,
                projectileDeadZoneLayer),
            Is.False,
            "Projectile and ProjectileDeadZone layers must interact.");

        ProjectileDeadZone[] projectileDeadZones =
            GetComponentsInScene<ProjectileDeadZone>(scene);
        Assert.That(projectileDeadZones, Is.Not.Empty,
            $"Map '{map.MapId}' requires at least one ProjectileDeadZone.");

        foreach (ProjectileDeadZone deadZone in projectileDeadZones)
        {
            Assert.That(deadZone.gameObject.activeInHierarchy, Is.True,
                $"Projectile dead zone '{deadZone.name}' must be active.");
            Assert.That(deadZone.enabled, Is.True,
                $"Projectile dead zone '{deadZone.name}' must be enabled.");
            Assert.That(deadZone.gameObject.layer,
                Is.EqualTo(projectileDeadZoneLayer),
                $"Projectile dead zone '{deadZone.name}' must use the ProjectileDeadZone layer.");
            AssertValidTriggerCollider(deadZone, "Projectile dead zone");
        }

        PlayerFallDeathZone[] sceneFallDeathZones =
            GetComponentsInScene<PlayerFallDeathZone>(scene);
        Assert.That(sceneFallDeathZones, Is.Not.Empty,
            $"Map '{map.MapId}' requires at least one PlayerFallDeathZone.");
        Assert.That(context.FallDeathZones,
            Is.EquivalentTo(sceneFallDeathZones),
            $"Map '{map.MapId}' must register every PlayerFallDeathZone in its BattleMapContext.");

        foreach (PlayerFallDeathZone fallDeathZone in sceneFallDeathZones)
        {
            Assert.That(fallDeathZone.gameObject.activeInHierarchy, Is.True,
                $"Player fall death zone '{fallDeathZone.name}' must be active.");
            Assert.That(fallDeathZone.enabled, Is.True,
                $"Player fall death zone '{fallDeathZone.name}' must be enabled.");
            Assert.That(
                Physics2D.GetIgnoreLayerCollision(
                    playerLayer,
                    fallDeathZone.gameObject.layer),
                Is.False,
                $"Player must interact with fall death zone '{fallDeathZone.name}'.");
            AssertValidTriggerCollider(fallDeathZone, "Player fall death zone");
        }
    }

    private static void AssertValidTriggerCollider(
        Component zone,
        string zoneType)
    {
        BoxCollider2D collider = zone.GetComponent<BoxCollider2D>();
        Assert.That(collider, Is.Not.Null,
            $"{zoneType} '{zone.name}' requires a BoxCollider2D.");
        Assert.That(collider.enabled, Is.True,
            $"The collider on {zoneType.ToLowerInvariant()} '{zone.name}' must be enabled.");
        Assert.That(collider.isTrigger, Is.True,
            $"The collider on {zoneType.ToLowerInvariant()} '{zone.name}' must be a trigger.");
        Assert.That(collider.size.x, Is.GreaterThan(0f),
            $"The collider on {zoneType.ToLowerInvariant()} '{zone.name}' must have a positive width.");
        Assert.That(collider.size.y, Is.GreaterThan(0f),
            $"The collider on {zoneType.ToLowerInvariant()} '{zone.name}' must have a positive height.");
    }

    private static T[] GetComponentsInScene<T>(Scene scene)
        where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
    }
}
