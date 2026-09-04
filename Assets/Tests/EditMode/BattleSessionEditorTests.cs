using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleSessionEditorTests
{
    private const string FactoryScenePath =
        "Assets/Scenes/Battle/FactoryMap.unity";

    private readonly List<Object> temporaryObjects = new();

    [SetUp]
    public void SetUp()
    {
        BattleSession.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        BattleSession.Reset();

        for (int index = temporaryObjects.Count - 1; index >= 0; index--)
        {
            if (temporaryObjects[index] != null)
            {
                Object.DestroyImmediate(temporaryObjects[index]);
            }
        }

        temporaryObjects.Clear();
    }

    [Test]
    public void ConfigureStoresMapAndAIDifficulty()
    {
        BattleMapDefinition map = CreateMap(
            "factory-map",
            "Factory Map",
            FactoryScenePath);
        AIDifficultyDefinition difficulty =
            CreateDifficulty("basic", "Basic");

        BattleSession.Configure(map, difficulty);

        Assert.That(BattleSession.HasConfiguration, Is.True);
        BattleLaunchConfiguration configuration =
            BattleSession.Configuration;
        Assert.That(configuration.MapId, Is.EqualTo(map.MapId));
        Assert.That(configuration.ScenePath, Is.EqualTo(map.ScenePath));
        Assert.That(configuration.AIDifficultyProfile,
            Is.SameAs(difficulty));
    }

    [Test]
    public void ConfigureRejectsMissingOrInvalidMap()
    {
        AIDifficultyDefinition difficulty =
            CreateDifficulty("basic", "Basic");
        BattleMapDefinition invalidMap = CreateMap(
            string.Empty,
            "Invalid Map",
            FactoryScenePath);

        Assert.That(
            () => BattleSession.Configure(null, difficulty),
            Throws.TypeOf<System.ArgumentNullException>());
        Assert.That(
            () => BattleSession.Configure(invalidMap, difficulty),
            Throws.TypeOf<System.ArgumentException>());
        Assert.That(BattleSession.HasConfiguration, Is.False);
    }

    [Test]
    public void ConfigureRejectsMissingOrInvalidDifficultyProfile()
    {
        BattleMapDefinition map = CreateMap(
            "factory-map",
            "Factory Map",
            FactoryScenePath);
        AIDifficultyDefinition invalid =
            ScriptableObject.CreateInstance<AIDifficultyDefinition>();
        temporaryObjects.Add(invalid);
        invalid.Configure(string.Empty, "Invalid");

        Assert.That(
            () => BattleSession.Configure(map, null),
            Throws.TypeOf<System.ArgumentNullException>());
        Assert.That(
            () => BattleSession.Configure(map, invalid),
            Throws.TypeOf<System.ArgumentException>());
        Assert.That(BattleSession.HasConfiguration, Is.False);
    }

    [Test]
    public void ResetClearsConfigurationAndPreventsLookups()
    {
        BattleMapDefinition map = CreateMap(
            "factory-map",
            "Factory Map",
            FactoryScenePath);
        BattleMapCatalog catalog = CreateCatalog(map, map);
        BattleSession.Configure(map, CreateDifficulty("basic", "Basic"));

        BattleSession.Reset();

        Assert.That(BattleSession.HasConfiguration, Is.False);
        Assert.That(BattleSession.TryResolveSelectedMap(
            catalog,
            out BattleMapDefinition resolvedMap), Is.False);
        Assert.That(resolvedMap, Is.Null);
        Assert.That(BattleSession.TryGetForScene(
            FactoryScenePath,
            out BattleLaunchConfiguration configuration), Is.False);
        Assert.That(configuration,
            Is.EqualTo(default(BattleLaunchConfiguration)));
    }

    [Test]
    public void SelectedMapResolvesFromCatalogByStoredMapId()
    {
        BattleMapDefinition selectedMap = CreateMap(
            "factory-map",
            "Setup Selection",
            FactoryScenePath);
        BattleMapDefinition catalogMap = CreateMap(
            "factory-map",
            "Catalog Map",
            FactoryScenePath);
        BattleMapCatalog catalog = CreateCatalog(catalogMap, catalogMap);
        BattleSession.Configure(
            selectedMap,
            CreateDifficulty("basic", "Basic"));

        Assert.That(BattleSession.TryResolveSelectedMap(
            catalog,
            out BattleMapDefinition resolvedMap), Is.True);
        Assert.That(resolvedMap, Is.SameAs(catalogMap));

        Assert.That(BattleSession.TryResolveSelectedMap(
            null,
            out BattleMapDefinition nullCatalogMap), Is.False);
        Assert.That(nullCatalogMap, Is.Null);
    }

    [Test]
    public void SceneLookupNormalizesPathAndReturnsConfiguredDifficulty()
    {
        BattleMapDefinition map = CreateMap(
            "factory-map",
            "Factory Map",
            FactoryScenePath);
        AIDifficultyDefinition difficulty =
            CreateDifficulty("basic", "Basic");
        BattleSession.Configure(map, difficulty);

        Assert.That(BattleSession.TryGetForScene(
            "  assets\\scenes\\battle\\factorymap.unity  ",
            out BattleLaunchConfiguration configuration), Is.True);
        Assert.That(configuration.AIDifficultyProfile,
            Is.SameAs(difficulty));

        Assert.That(BattleSession.TryGetForScene(
            "Assets/Scenes/Battle/AnotherMap.unity",
            out BattleLaunchConfiguration otherConfiguration), Is.False);
        Assert.That(otherConfiguration,
            Is.EqualTo(default(BattleLaunchConfiguration)));
    }

    private BattleMapDefinition CreateMap(
        string mapId,
        string displayName,
        string scenePath)
    {
        BattleMapDefinition map =
            ScriptableObject.CreateInstance<BattleMapDefinition>();
        temporaryObjects.Add(map);
        map.Configure(
            mapId,
            displayName,
            string.Empty,
            scenePath,
            null);
        return map;
    }

    private AIDifficultyDefinition CreateDifficulty(
        string difficultyId,
        string displayName)
    {
        AIDifficultyDefinition difficulty =
            ScriptableObject.CreateInstance<AIDifficultyDefinition>();
        temporaryObjects.Add(difficulty);
        difficulty.Configure(difficultyId, displayName);
        return difficulty;
    }

    private BattleMapCatalog CreateCatalog(
        BattleMapDefinition defaultMap,
        params BattleMapDefinition[] maps)
    {
        BattleMapCatalog catalog =
            ScriptableObject.CreateInstance<BattleMapCatalog>();
        temporaryObjects.Add(catalog);
        catalog.Configure(defaultMap, maps);
        return catalog;
    }
}
