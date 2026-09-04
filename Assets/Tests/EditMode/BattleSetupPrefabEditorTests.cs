using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleSetupPrefabEditorTests
{
    private const string BattleSetupPrefabPath =
        "Assets/Prefabs/Battle/PF_BattleSetup.prefab";
    private const string PlayerPrefabPath =
        "Assets/Prefabs/Player/PF_Player.prefab";
    private const string AiPlayerPrefabPath =
        "Assets/Prefabs/Player/PF_AIPlayer.prefab";
    private const string PlayerPortraitPath =
        "Assets/Art/player/1_portrait.png";
    private const string AiPlayerPortraitPath =
        "Assets/Art/player/2_portrait.png";

    [Test]
    public void BattleSetupPrefabContainsReusableBattleServicesAndPresentation()
    {
        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(BattleSetupPrefabPath);

            Assert.That(root, Is.Not.Null);
            Assert.That(root.name, Is.EqualTo("PF_BattleSetup"));

            BattleSetup setup = root.GetComponent<BattleSetup>();
            MatchController[] matchControllers =
                root.GetComponentsInChildren<MatchController>(true);
            BulletPool[] bulletPools =
                root.GetComponentsInChildren<BulletPool>(true);
            MatchLifeHud[] lifeHuds =
                root.GetComponentsInChildren<MatchLifeHud>(true);
            MatchResultMenu[] resultMenus =
                root.GetComponentsInChildren<MatchResultMenu>(true);
            BattlePauseMenu[] pauseMenus =
                root.GetComponentsInChildren<BattlePauseMenu>(true);
            BattleCountdown[] countdowns =
                root.GetComponentsInChildren<BattleCountdown>(true);
            Camera[] cameras = root.GetComponentsInChildren<Camera>(true);

            Assert.That(setup, Is.Not.Null);
            Assert.That(matchControllers, Has.Length.EqualTo(1));
            Assert.That(bulletPools, Has.Length.EqualTo(1));
            Assert.That(lifeHuds, Has.Length.EqualTo(1));
            Assert.That(resultMenus, Has.Length.EqualTo(1));
            Assert.That(pauseMenus, Has.Length.EqualTo(1));
            Assert.That(countdowns, Has.Length.EqualTo(1));
            Assert.That(cameras, Has.Length.EqualTo(1));
            Assert.That(setup.MatchController, Is.SameAs(matchControllers[0]));
            Assert.That(setup.BulletPool, Is.SameAs(bulletPools[0]));
            Assert.That(setup.LifeHud, Is.SameAs(lifeHuds[0]));
            Assert.That(setup.Countdown, Is.SameAs(countdowns[0]));
            Assert.That(setup.MapContext, Is.Null,
                "The map-owned context must be supplied by each battle scene.");

            SerializedObject serializedSetup = new(setup);
            SerializedObject serializedMatch = new(matchControllers[0]);
            SerializedObject serializedPool = new(bulletPools[0]);
            Assert.That(serializedSetup.FindProperty("autoStart").boolValue,
                Is.True);
            Assert.That(
                serializedMatch.FindProperty("autoDiscoverParticipants").boolValue,
                Is.False);
            Assert.That(serializedMatch.FindProperty("autoStart").boolValue,
                Is.False);
            Assert.That(
                serializedPool.FindProperty("bulletPrefab").objectReferenceValue,
                Is.Not.Null);

            Assert.That(lifeHuds[0].MatchController,
                Is.SameAs(matchControllers[0]));
            Assert.That(resultMenus[0].MatchController,
                Is.SameAs(matchControllers[0]));
            Assert.That(resultMenus[0].gameObject,
                Is.SameAs(lifeHuds[0].gameObject));
            Assert.That(resultMenus[0].GameSetupScenePath,
                Is.EqualTo("Assets/Scenes/GameSetup.unity"));
            Assert.That(resultMenus[0].MainMenuScenePath,
                Is.EqualTo("Assets/Scenes/MainMenu.unity"));
            Assert.That(pauseMenus[0].MatchController,
                Is.SameAs(matchControllers[0]));
            Assert.That(pauseMenus[0].gameObject,
                Is.SameAs(lifeHuds[0].gameObject));
            Assert.That(pauseMenus[0].GameSetupScenePath,
                Is.EqualTo("Assets/Scenes/GameSetup.unity"));
            Assert.That(pauseMenus[0].BattleSetup, Is.SameAs(setup));
            Assert.That(resultMenus[0].BattleSetup, Is.SameAs(setup));
            Assert.That(countdowns[0].MatchController,
                Is.SameAs(matchControllers[0]));
            Assert.That(countdowns[0].PhaseDuration,
                Is.EqualTo(0.5f).Within(0.001f));
            SerializedObject serializedCountdown = new(countdowns[0]);
            AssertCountdownSprite(
                serializedCountdown,
                "threeSprite",
                "Assets/Art/UI/sprite_3.png");
            AssertCountdownSprite(
                serializedCountdown,
                "twoSprite",
                "Assets/Art/UI/sprite_2.png");
            AssertCountdownSprite(
                serializedCountdown,
                "oneSprite",
                "Assets/Art/UI/sprite_1.png");
            AssertCountdownSprite(
                serializedCountdown,
                "goSprite",
                "Assets/Art/UI/sprite_go.png");
            Canvas canvas = lifeHuds[0].GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));

            MatchLifeHud lifeHud = lifeHuds[0];
            Assert.That(lifeHud.LeftDisplayBackground, Is.Not.Null);
            Assert.That(lifeHud.RightDisplayBackground, Is.Not.Null);
            Assert.That(lifeHud.LeftPortrait, Is.Not.Null);
            Assert.That(lifeHud.RightPortrait, Is.Not.Null);
            Assert.That(lifeHud.LeftPortrait,
                Is.Not.SameAs(lifeHud.RightPortrait));
            Assert.That(lifeHud.LeftDisplayBackground.color,
                Is.EqualTo(Color.white));
            Assert.That(lifeHud.RightDisplayBackground.color,
                Is.EqualTo(Color.white));
            Assert.That(lifeHud.LeftDisplayBackground.raycastTarget, Is.False);
            Assert.That(lifeHud.RightDisplayBackground.raycastTarget, Is.False);
            AssertPortraitLayout(
                lifeHud.LeftPortrait,
                Vector2.zero,
                Vector2.zero,
                new Vector2(32f, 24f));
            AssertPortraitLayout(
                lifeHud.RightPortrait,
                Vector2.right,
                new Vector2(0.5f, 0f),
                new Vector2(-72f, 24f));
            Assert.That(lifeHud.LeftPortrait.rectTransform.localScale.x,
                Is.EqualTo(1f));
            Assert.That(lifeHud.RightPortrait.rectTransform.localScale.x,
                Is.EqualTo(-1f),
                "The right portrait must face inward toward the left.");
            Assert.That(lifeHud.LeftDisplayTransform.anchorMin,
                Is.EqualTo(Vector2.zero));
            Assert.That(lifeHud.RightDisplayTransform.anchorMin,
                Is.EqualTo(Vector2.right));
            Assert.That(
                lifeHud.LeftDisplayTransform.GetComponent<Text>().color,
                Is.EqualTo(Color.black));
            Assert.That(
                lifeHud.RightDisplayTransform.GetComponent<Text>().color,
                Is.EqualTo(Color.black));
            Assert.That(
                lifeHud.LeftDisplayBackground.rectTransform.anchoredPosition.x,
                Is.EqualTo(128f));
            Assert.That(
                lifeHud.RightDisplayBackground.rectTransform.anchoredPosition.x,
                Is.EqualTo(-128f));
            Assert.That(lifeHud.LeftDisplayTransform.anchoredPosition.x,
                Is.EqualTo(144f));
            Assert.That(lifeHud.RightDisplayTransform.anchoredPosition.x,
                Is.EqualTo(-144f));
            Assert.That(
                lifeHud.LeftDisplayBackground.rectTransform.GetSiblingIndex(),
                Is.LessThan(lifeHud.LeftDisplayTransform.GetSiblingIndex()));
            Assert.That(
                lifeHud.RightDisplayBackground.rectTransform.GetSiblingIndex(),
                Is.LessThan(lifeHud.RightDisplayTransform.GetSiblingIndex()));

            Assert.That(cameras[0].CompareTag("MainCamera"), Is.True);
            Assert.That(cameras[0].GetComponent<AudioListener>(), Is.Not.Null);

            Assert.That(root.transform.Find("Systems"), Is.Not.Null);
            Assert.That(root.transform.Find("Presentation"), Is.Not.Null);
            Assert.That(root.transform.Find("ActorRoot"), Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<BattleMapContext>(true),
                Is.Empty);
            Assert.That(root.GetComponentsInChildren<BattleSpawnArea>(true),
                Is.Empty);
            Assert.That(root.GetComponentsInChildren<BattleSpawnSlot>(true),
                Is.Empty,
                "Map-owned spawn slots must not live in the common setup prefab.");
            Assert.That(
                root.GetComponentsInChildren<BattleParticipantSlot>(true),
                Is.Empty,
                "Actor-owned participant slots must not live in the common setup prefab.");
            Assert.That(root.GetComponentsInChildren<PlayerLife>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<PlayerFallDeathZone>(true),
                Is.Empty);

            string[] directChildNames = root.transform
                .Cast<Transform>()
                .Select(child => child.name)
                .ToArray();
            Assert.That(directChildNames,
                Is.EquivalentTo(new[] { "Systems", "Presentation", "ActorRoot" }));
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void AssertCountdownSprite(
        SerializedObject serializedCountdown,
        string propertyName,
        string expectedAssetPath)
    {
        SerializedProperty spriteProperty =
            serializedCountdown.FindProperty(propertyName);
        Assert.That(spriteProperty, Is.Not.Null);
        Assert.That(spriteProperty.objectReferenceValue, Is.Not.Null);
        Assert.That(
            AssetDatabase.GetAssetPath(spriteProperty.objectReferenceValue),
            Is.EqualTo(expectedAssetPath));

        TextureImporter importer =
            AssetImporter.GetAtPath(expectedAssetPath) as TextureImporter;
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
    }

    private static void AssertPortraitLayout(
        Image portrait,
        Vector2 expectedAnchor,
        Vector2 expectedPivot,
        Vector2 expectedPosition)
    {
        Assert.That(portrait.color, Is.EqualTo(Color.white));
        Assert.That(portrait.raycastTarget, Is.False);
        Assert.That(portrait.preserveAspect, Is.True);

        RectTransform portraitTransform = portrait.rectTransform;
        Assert.That(portraitTransform.anchorMin, Is.EqualTo(expectedAnchor));
        Assert.That(portraitTransform.anchorMax, Is.EqualTo(expectedAnchor));
        Assert.That(portraitTransform.pivot, Is.EqualTo(expectedPivot));
        Assert.That(portraitTransform.anchoredPosition,
            Is.EqualTo(expectedPosition));
        Assert.That(portraitTransform.sizeDelta,
            Is.EqualTo(new Vector2(80f, 80f)));
    }

    [TestCase(PlayerPrefabPath, BattlePlayerSlot.P1, PlayerPortraitPath)]
    [TestCase(AiPlayerPrefabPath, BattlePlayerSlot.P2, AiPlayerPortraitPath)]
    public void ParticipantPrefabHasExpectedDefaultPlayerSlot(
        string prefabPath,
        BattlePlayerSlot expectedSlot,
        string expectedPortraitPath)
    {
        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(prefabPath);

            Assert.That(root, Is.Not.Null);
            PlayerLife participant = root.GetComponent<PlayerLife>();
            BattleParticipantSlot[] participantSlots =
                root.GetComponents<BattleParticipantSlot>();
            BattleParticipantPresentation[] presentations =
                root.GetComponents<BattleParticipantPresentation>();

            Assert.That(participant, Is.Not.Null);
            Assert.That(participantSlots, Has.Length.EqualTo(1));
            Assert.That(presentations, Has.Length.EqualTo(1));
            Assert.That(participantSlots[0].Slot, Is.EqualTo(expectedSlot));
            Assert.That(participantSlots[0].Participant,
                Is.SameAs(participant));
            Assert.That(presentations[0].Portrait, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(presentations[0].Portrait),
                Is.EqualTo(expectedPortraitPath));

            TextureImporter portraitImporter =
                AssetImporter.GetAtPath(expectedPortraitPath) as TextureImporter;
            Assert.That(portraitImporter, Is.Not.Null);
            Assert.That(portraitImporter.spriteImportMode,
                Is.EqualTo(SpriteImportMode.Single));
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
