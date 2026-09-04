using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameSetupView : MonoBehaviour
{
    public const int PlayerSlotCapacity = 1;
    public const int AISlotCapacity = 1;

    [Header("Player Preview")]
    [SerializeField] private Sprite playerPreviewSprite;

    private static readonly Color BackgroundColor =
        new Color32(13, 18, 27, 255);
    private static readonly Color PanelColor =
        new Color32(27, 35, 48, 242);
    private static readonly Color PanelRaisedColor =
        new Color32(36, 46, 62, 255);
    private static readonly Color MutedSlotColor =
        new Color32(38, 44, 55, 255);
    private static readonly Color HumanColor =
        new Color32(39, 145, 214, 255);
    private static readonly Color AiColor =
        new Color32(211, 78, 72, 255);
    private static readonly Color AccentColor =
        new Color32(72, 178, 232, 255);
    private static readonly Color PrimaryTextColor =
        new Color32(239, 244, 250, 255);
    private static readonly Color SecondaryTextColor =
        new Color32(157, 171, 189, 255);

    private Canvas runtimeCanvas;
    private CanvasGroup canvasGroup;
    private Dropdown mapDropdown;
    private Dropdown[] aiDifficultyDropdowns;
    private IReadOnlyList<AIDifficultyDefinition> difficultyProfiles;
    private Image selectedMapThumbnail;
    private Text thumbnailPlaceholderText;
    private Text selectedMapNameText;
    private Image playerPreviewImage;
    private Text[] leftSlotTexts;
    private Text[] rightSlotTexts;
    private Button startButton;
    private Button backButton;
    private Text statusText;

    private bool isBuilt;
    private bool isBuilding;
    private bool interactionEnabled = true;
    private int configuredMapCount;

    private static Font runtimeFont;

    public bool IsBuilt => isBuilt;

    public Canvas RuntimeCanvas
    {
        get
        {
            EnsureBuilt();
            return runtimeCanvas;
        }
    }

    public Dropdown MapDropdown
    {
        get
        {
            EnsureBuilt();
            return mapDropdown;
        }
    }

    public Dropdown AIDifficultyDropdown
    {
        get
        {
            EnsureBuilt();
            return aiDifficultyDropdowns[0];
        }
    }

    public Image SelectedMapThumbnail
    {
        get
        {
            EnsureBuilt();
            return selectedMapThumbnail;
        }
    }

    public Text SelectedMapNameText
    {
        get
        {
            EnsureBuilt();
            return selectedMapNameText;
        }
    }

    public Sprite PlayerPreviewSprite => playerPreviewSprite;

    public Image PlayerPreviewImage
    {
        get
        {
            EnsureBuilt();
            return playerPreviewImage;
        }
    }

    public Button StartButton
    {
        get
        {
            EnsureBuilt();
            return startButton;
        }
    }

    public Button BackButton
    {
        get
        {
            EnsureBuilt();
            return backButton;
        }
    }

    public Text StatusText
    {
        get
        {
            EnsureBuilt();
            return statusText;
        }
    }

    public IReadOnlyList<Text> LeftSlotTexts
    {
        get
        {
            EnsureBuilt();
            return leftSlotTexts;
        }
    }

    public IReadOnlyList<Text> RightSlotTexts
    {
        get
        {
            EnsureBuilt();
            return rightSlotTexts;
        }
    }

    public int LeftSlotCount => PlayerSlotCapacity;
    public int RightSlotCount => AISlotCapacity;

    private void Awake()
    {
        EnsureBuilt();
    }

    public void EnsureBuilt()
    {
        if (isBuilt || isBuilding)
        {
            return;
        }

        isBuilding = true;
        try
        {
            BuildCanvas();
            BuildHeader();
            BuildMapPanel();
            BuildTeamPanels();
            BuildFooter();
            EnsureEventSystem();

            isBuilt = true;
            ConfigureDifficultyProfiles(null);
            ShowRoster(null);
            ShowStatus(string.Empty);
            ApplyInteractionState();
        }
        finally
        {
            isBuilding = false;
        }
    }

    public void ConfigureMaps(
        IReadOnlyList<BattleMapDefinition> maps,
        int selectedIndex)
    {
        EnsureBuilt();

        List<Dropdown.OptionData> options = new();
        configuredMapCount = maps?.Count ?? 0;
        for (int index = 0; index < configuredMapCount; index++)
        {
            BattleMapDefinition map = maps[index];
            string displayName = map != null
                ? GetMapDisplayName(map)
                : "INVALID MAP";
            options.Add(new Dropdown.OptionData(displayName));
        }

        if (options.Count == 0)
        {
            options.Add(new Dropdown.OptionData("NO MAPS AVAILABLE"));
            selectedIndex = 0;
        }
        else
        {
            selectedIndex = Mathf.Clamp(selectedIndex, 0, options.Count - 1);
        }

        mapDropdown.ClearOptions();
        mapDropdown.AddOptions(options);
        mapDropdown.SetValueWithoutNotify(selectedIndex);
        mapDropdown.RefreshShownValue();

        BattleMapDefinition selectedMap = maps != null
            && maps.Count > 0
            && selectedIndex >= 0
            && selectedIndex < maps.Count
                ? maps[selectedIndex]
                : null;
        ShowMap(selectedMap);
        ApplyInteractionState();
    }

    public void ShowMap(BattleMapDefinition map)
    {
        EnsureBuilt();

        if (map == null)
        {
            selectedMapNameText.text = "NO MAP SELECTED";
            selectedMapThumbnail.sprite = null;
            selectedMapThumbnail.enabled = false;
            thumbnailPlaceholderText.gameObject.SetActive(true);
            return;
        }

        selectedMapNameText.text = GetMapDisplayName(map).ToUpperInvariant();

        selectedMapThumbnail.sprite = map.Thumbnail;
        selectedMapThumbnail.enabled = map.Thumbnail != null;
        thumbnailPlaceholderText.gameObject.SetActive(map.Thumbnail == null);
    }

    public void ConfigureDifficultyProfiles(
        IReadOnlyList<AIDifficultyDefinition> profiles)
    {
        EnsureBuilt();

        difficultyProfiles = profiles;
        List<Dropdown.OptionData> options = new(profiles?.Count ?? 0);
        if (profiles != null)
        {
            for (int profileIndex = 0;
                 profileIndex < profiles.Count;
                 profileIndex++)
            {
                AIDifficultyDefinition profile = profiles[profileIndex];
                string label = profile != null
                    ? GetDifficultyLabel(profile)
                    : "INVALID PROFILE";
                options.Add(new Dropdown.OptionData(label));
            }
        }

        for (int slotIndex = 0;
             slotIndex < aiDifficultyDropdowns.Length;
             slotIndex++)
        {
            Dropdown dropdown = aiDifficultyDropdowns[slotIndex];
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            if (options.Count > 0)
            {
                dropdown.SetValueWithoutNotify(0);
                dropdown.RefreshShownValue();
            }
        }

        ApplyInteractionState();
    }

    public void ShowRoster(AIDifficultyDefinition difficultyProfile)
    {
        EnsureBuilt();

        leftSlotTexts[0].text = "SLOT 1   -   PLAYER\nHUMAN";
        Text aiSlotText = rightSlotTexts[0];
        Dropdown difficultyDropdown = aiDifficultyDropdowns[0];
        if (difficultyProfile == null)
        {
            aiSlotText.text = "SLOT 1\nAI NOT CONFIGURED";
            aiSlotText.color = SecondaryTextColor;
            aiSlotText.transform.parent.GetComponent<Image>().color =
                MutedSlotColor;
            difficultyDropdown.gameObject.SetActive(false);
            ApplyInteractionState();
            return;
        }

        aiSlotText.text = "SLOT 1   -   AI";
        aiSlotText.color = PrimaryTextColor;
        aiSlotText.transform.parent.GetComponent<Image>().color = AiColor;
        difficultyDropdown.gameObject.SetActive(true);

        int difficultyIndex = IndexOfDifficultyProfile(difficultyProfile);
        if (difficultyIndex >= 0 && difficultyDropdown.options.Count > 0)
        {
            difficultyDropdown.SetValueWithoutNotify(difficultyIndex);
            difficultyDropdown.RefreshShownValue();
        }

        ApplyInteractionState();
    }

    public void ShowStatus(string message)
    {
        EnsureBuilt();
        statusText.text = message ?? string.Empty;
    }

    public void SetInteractionEnabled(bool enabled)
    {
        EnsureBuilt();
        interactionEnabled = enabled;
        ApplyInteractionState();
    }

    private void BuildCanvas()
    {
        runtimeCanvas = GetComponent<Canvas>();
        GameObject canvasObject;
        if (runtimeCanvas != null)
        {
            canvasObject = runtimeCanvas.gameObject;
        }
        else
        {
            canvasObject = CreateUiObject("GameSetupCanvas", transform);
            runtimeCanvas = canvasObject.AddComponent<Canvas>();
        }

        runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        runtimeCanvas.pixelPerfect = false;
        runtimeCanvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvasObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        canvasGroup = canvasObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        }

        RectTransform canvasRect = (RectTransform)canvasObject.transform;
        Stretch(canvasRect);

        Image background = CreateStretchImage(
            "Background",
            canvasRect,
            BackgroundColor);
        background.raycastTarget = false;

        RectTransform accentStrip = CreateFixedRect(
            canvasRect,
            "AccentStrip",
            0f,
            0f,
            14f,
            1080f);
        Image accentImage = accentStrip.gameObject.AddComponent<Image>();
        accentImage.color = AccentColor;
        accentImage.raycastTarget = false;
    }

    private void BuildHeader()
    {
        RectTransform canvasRect = runtimeCanvas.GetComponent<RectTransform>();
        CreateText(
            "Title",
            canvasRect,
            "BATTLE SETUP",
            52,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            PrimaryTextColor,
            70f,
            42f,
            900f,
            66f);
        CreateText(
            "Subtitle",
            canvasRect,
            "CHOOSE THE MAP AND REVIEW THE MATCHUP",
            20,
            FontStyle.Normal,
            TextAnchor.MiddleLeft,
            SecondaryTextColor,
            74f,
            105f,
            900f,
            34f);
    }

    private void BuildMapPanel()
    {
        RectTransform canvasRect = runtimeCanvas.GetComponent<RectTransform>();
        RectTransform panel = CreatePanel(
            "MapSelectionPanel",
            canvasRect,
            70f,
            160f,
            1780f,
            260f,
            PanelColor);

        CreateText(
            "MapSelectionLabel",
            panel,
            "SELECT MAP",
            20,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            AccentColor,
            28f,
            22f,
            350f,
            34f);

        mapDropdown = CreateDropdown(
            "MapDropdown",
            panel,
            28f,
            66f,
            360f,
            58f,
            new[] { "NO MAPS AVAILABLE" },
            true);

        CreateText(
            "MapHint",
            panel,
            "The selected map determines which battle scene will be loaded.",
            17,
            FontStyle.Normal,
            TextAnchor.UpperLeft,
            SecondaryTextColor,
            30f,
            145f,
            350f,
            70f);

        RectTransform thumbnailFrame = CreatePanel(
            "ThumbnailFrame",
            panel,
            430f,
            36f,
            330f,
            186f,
            new Color32(10, 14, 21, 255));
        RectTransform thumbnailRect = CreateInsetRect(
            "SelectedMapThumbnail",
            thumbnailFrame,
            6f);
        selectedMapThumbnail = thumbnailRect.gameObject.AddComponent<Image>();
        selectedMapThumbnail.color = Color.white;
        selectedMapThumbnail.preserveAspect = true;
        selectedMapThumbnail.raycastTarget = false;
        selectedMapThumbnail.enabled = false;

        thumbnailPlaceholderText = CreateStretchText(
            "ThumbnailPlaceholder",
            thumbnailFrame,
            "NO THUMBNAIL",
            18,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            SecondaryTextColor,
            new Vector2(8f, 8f),
            new Vector2(-8f, -8f));

        selectedMapNameText = CreateText(
            "SelectedMapName",
            panel,
            "NO MAP SELECTED",
            32,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            PrimaryTextColor,
            800f,
            100f,
            610f,
            60f);

    }

    private void BuildTeamPanels()
    {
        RectTransform canvasRect = runtimeCanvas.GetComponent<RectTransform>();
        RectTransform playerPanel = CreateTeamPanel(
            "LeftTeamPanel",
            canvasRect,
            "PLAYER",
            70f,
            450f,
            HumanColor,
            PlayerSlotCapacity,
            false,
            out leftSlotTexts,
            out _);
        BuildPlayerPreview(playerPanel);
        CreateTeamPanel(
            "RightTeamPanel",
            canvasRect,
            "AI",
            1090f,
            450f,
            AiColor,
            AISlotCapacity,
            true,
            out rightSlotTexts,
            out aiDifficultyDropdowns);
    }

    private void BuildPlayerPreview(RectTransform playerPanel)
    {
        RectTransform previewFrame = CreatePanel(
            "PlayerPreviewFrame",
            playerPanel,
            30f,
            190f,
            700f,
            225f,
            new Color32(49, 70, 91, 255));

        CreateText(
            "PlayerPreviewLabel",
            previewFrame,
            "CURRENT CHARACTER",
            18,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            AccentColor,
            24f,
            18f,
            200f,
            34f);

        RectTransform previewRect = CreateFixedRect(
            previewFrame,
            "PlayerPreview",
            250f,
            8f,
            200f,
            209f);
        playerPreviewImage = previewRect.gameObject.AddComponent<Image>();
        playerPreviewImage.sprite = playerPreviewSprite;
        playerPreviewImage.color = Color.white;
        playerPreviewImage.preserveAspect = true;
        playerPreviewImage.raycastTarget = false;
        playerPreviewImage.enabled = playerPreviewSprite != null;
    }

    private void BuildFooter()
    {
        RectTransform canvasRect = runtimeCanvas.GetComponent<RectTransform>();
        statusText = CreateText(
            "StatusText",
            canvasRect,
            string.Empty,
            18,
            FontStyle.Normal,
            TextAnchor.MiddleLeft,
            SecondaryTextColor,
            72f,
            945f,
            1160f,
            68f);

        backButton = CreateButton(
            "BackButton",
            canvasRect,
            "BACK",
            1430f,
            940f,
            180f,
            70f,
            new Color32(63, 74, 89, 255),
            out _);
        startButton = CreateButton(
            "StartButton",
            canvasRect,
            "START BATTLE",
            1630f,
            940f,
            220f,
            70f,
            new Color32(211, 139, 50, 255),
            out Text startLabel);
        startLabel.fontStyle = FontStyle.Bold;
    }

    private RectTransform CreateTeamPanel(
        string objectName,
        RectTransform parent,
        string heading,
        float x,
        float y,
        Color teamColor,
        int slotCapacity,
        bool createDifficultyDropdowns,
        out Text[] slotTexts,
        out Dropdown[] difficultyDropdowns)
    {
        RectTransform panel = CreatePanel(
            objectName,
            parent,
            x,
            y,
            760f,
            445f,
            PanelColor);

        RectTransform colorBar = CreateFixedRect(
            panel,
            "TeamColorBar",
            0f,
            0f,
            8f,
            445f);
        Image colorBarImage = colorBar.gameObject.AddComponent<Image>();
        colorBarImage.color = teamColor;
        colorBarImage.raycastTarget = false;

        CreateText(
            "TeamHeading",
            panel,
            heading,
            26,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            PrimaryTextColor,
            30f,
            20f,
            690f,
            48f);

        slotTexts = new Text[slotCapacity];
        difficultyDropdowns = createDifficultyDropdowns
            ? new Dropdown[slotCapacity]
            : null;
        for (int slotIndex = 0; slotIndex < slotCapacity; slotIndex++)
        {
            float slotY = 88f + slotIndex * 105f;
            Color slotColor = teamColor;
            RectTransform slotRect = CreateFixedRect(
                panel,
                $"Slot{slotIndex + 1}",
                30f,
                slotY,
                700f,
                82f);
            Image slotImage = slotRect.gameObject.AddComponent<Image>();
            slotImage.color = slotColor;
            slotImage.raycastTarget = false;

            Text slotText = CreateStretchText(
                "Label",
                slotRect,
                $"SLOT {slotIndex + 1}",
                21,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                PrimaryTextColor,
                new Vector2(24f, 4f),
                new Vector2(
                    createDifficultyDropdowns ? -290f : -20f,
                    -4f));
            slotTexts[slotIndex] = slotText;

            if (createDifficultyDropdowns)
            {
                Dropdown dropdown = CreateDropdown(
                    "DifficultyDropdown",
                    slotRect,
                    440f,
                    12f,
                    235f,
                    58f,
                    new[] { "NO PROFILE" },
                    false);
                dropdown.gameObject.SetActive(false);
                difficultyDropdowns[slotIndex] = dropdown;
            }
        }

        return panel;
    }

    private Dropdown CreateDropdown(
        string objectName,
        RectTransform parent,
        float x,
        float y,
        float width,
        float height,
        IReadOnlyList<string> optionLabels,
        bool showItemCheckmark)
    {
        RectTransform dropdownRect = CreateFixedRect(
            parent,
            objectName,
            x,
            y,
            width,
            height);
        Image dropdownImage = dropdownRect.gameObject.AddComponent<Image>();
        dropdownImage.color = PanelRaisedColor;

        Dropdown dropdown = dropdownRect.gameObject.AddComponent<Dropdown>();
        dropdown.targetGraphic = dropdownImage;
        dropdown.colors = CreateColorBlock(PanelRaisedColor);

        Text caption = CreateStretchText(
            "Caption",
            dropdownRect,
            optionLabels.Count > 0 ? optionLabels[0] : string.Empty,
            19,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            PrimaryTextColor,
            new Vector2(18f, 5f),
            new Vector2(-55f, -5f));

        CreateText(
            "Arrow",
            dropdownRect,
            "v",
            17,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            AccentColor,
            width - 50f,
            4f,
            42f,
            height - 8f);

        RectTransform template = CreateDropdownTemplate(
            dropdownRect,
            width,
            showItemCheckmark);
        Text itemText = template.Find("Viewport/Content/Item/ItemLabel")
            .GetComponent<Text>();

        dropdown.template = template;
        dropdown.captionText = caption;
        dropdown.itemText = itemText;

        List<Dropdown.OptionData> options = new(optionLabels.Count);
        for (int optionIndex = 0;
             optionIndex < optionLabels.Count;
             optionIndex++)
        {
            options.Add(new Dropdown.OptionData(optionLabels[optionIndex]));
        }

        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(0);
        dropdown.RefreshShownValue();
        template.gameObject.SetActive(false);
        return dropdown;
    }

    private RectTransform CreateDropdownTemplate(
        RectTransform dropdownRect,
        float width,
        bool showItemCheckmark)
    {
        RectTransform template = CreateAnchoredRect(
            "Template",
            dropdownRect,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -4f),
            new Vector2(0f, 210f));
        Image templateImage = template.gameObject.AddComponent<Image>();
        templateImage.color = new Color32(22, 29, 40, 255);
        template.gameObject.AddComponent<CanvasGroup>();

        ScrollRect scrollRect = template.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        RectTransform viewport = CreateInsetRect("Viewport", template, 5f);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Color.white;
        viewportImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateAnchoredRect(
            "Content",
            viewport,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            new Vector2(0f, 46f));

        RectTransform item = CreateAnchoredRect(
            "Item",
            content,
            new Vector2(0f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(0f, 46f));
        Image itemBackground = item.gameObject.AddComponent<Image>();
        itemBackground.color = PanelRaisedColor;
        Toggle itemToggle = item.gameObject.AddComponent<Toggle>();
        itemToggle.targetGraphic = itemBackground;
        itemToggle.colors = CreateColorBlock(PanelRaisedColor);

        if (showItemCheckmark)
        {
            Text checkmark = CreateText(
                "ItemCheckmark",
                item,
                "CHECK",
                15,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                AccentColor,
                width - 48f,
                0f,
                36f,
                46f);
            itemToggle.graphic = checkmark;
        }

        CreateStretchText(
            "ItemLabel",
            item,
            "OPTION",
            18,
            FontStyle.Normal,
            TextAnchor.MiddleLeft,
            PrimaryTextColor,
            new Vector2(16f, 2f),
            new Vector2(showItemCheckmark ? -52f : -16f, -2f));

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        return template;
    }

    private Button CreateButton(
        string objectName,
        RectTransform parent,
        string label,
        float x,
        float y,
        float width,
        float height,
        Color color,
        out Text labelText)
    {
        RectTransform buttonRect = CreateFixedRect(
            parent,
            objectName,
            x,
            y,
            width,
            height);
        Image image = buttonRect.gameObject.AddComponent<Image>();
        image.color = color;

        Button button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = CreateColorBlock(color);

        labelText = CreateStretchText(
            "Label",
            buttonRect,
            label,
            20,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            PrimaryTextColor,
            new Vector2(10f, 6f),
            new Vector2(-10f, -6f));
        return button;
    }

    private void ApplyInteractionState()
    {
        if (!isBuilt)
        {
            return;
        }

        canvasGroup.interactable = interactionEnabled;
        canvasGroup.blocksRaycasts = interactionEnabled;
        mapDropdown.interactable = interactionEnabled && configuredMapCount > 0;
        if (aiDifficultyDropdowns != null)
        {
            for (int slotIndex = 0;
                 slotIndex < aiDifficultyDropdowns.Length;
                 slotIndex++)
            {
                Dropdown dropdown = aiDifficultyDropdowns[slotIndex];
                dropdown.interactable = interactionEnabled
                    && dropdown.options.Count > 0;
            }
        }
        startButton.interactable = interactionEnabled && configuredMapCount > 0;
        backButton.interactable = interactionEnabled;
    }

    private void EnsureEventSystem()
    {
        EventSystem existingEventSystem = FindFirstObjectByType<EventSystem>(
            FindObjectsInactive.Include);
        if (existingEventSystem != null)
        {
            return;
        }

        GameObject eventSystemObject = new("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        InputSystemUIInputModule inputModule =
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private static string GetMapDisplayName(BattleMapDefinition map)
    {
        return string.IsNullOrWhiteSpace(map.DisplayName)
            ? map.name
            : map.DisplayName;
    }

    private int IndexOfDifficultyProfile(AIDifficultyDefinition profile)
    {
        if (profile == null || difficultyProfiles == null)
        {
            return -1;
        }

        for (int profileIndex = 0;
             profileIndex < difficultyProfiles.Count;
             profileIndex++)
        {
            AIDifficultyDefinition candidate =
                difficultyProfiles[profileIndex];
            if (candidate == profile
                || (candidate != null
                    && string.Equals(
                        candidate.DifficultyId,
                        profile.DifficultyId,
                        System.StringComparison.OrdinalIgnoreCase)))
            {
                return profileIndex;
            }
        }

        return -1;
    }

    private static string GetDifficultyLabel(
        AIDifficultyDefinition difficulty)
    {
        return string.IsNullOrWhiteSpace(difficulty.DisplayName)
            ? difficulty.name.ToUpperInvariant()
            : difficulty.DisplayName.ToUpperInvariant();
    }

    private static ColorBlock CreateColorBlock(Color normalColor)
    {
        return new ColorBlock
        {
            normalColor = normalColor,
            highlightedColor = Color.Lerp(normalColor, Color.white, 0.16f),
            pressedColor = Color.Lerp(normalColor, Color.black, 0.18f),
            selectedColor = Color.Lerp(normalColor, Color.white, 0.1f),
            disabledColor = new Color(
                normalColor.r * 0.48f,
                normalColor.g * 0.48f,
                normalColor.b * 0.48f,
                0.72f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };
    }

    private static RectTransform CreatePanel(
        string objectName,
        RectTransform parent,
        float x,
        float y,
        float width,
        float height,
        Color color)
    {
        RectTransform panel = CreateFixedRect(
            parent,
            objectName,
            x,
            y,
            width,
            height);
        Image image = panel.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return panel;
    }

    private static Image CreateStretchImage(
        string objectName,
        RectTransform parent,
        Color color)
    {
        GameObject imageObject = CreateUiObject(objectName, parent);
        RectTransform rectTransform =
            (RectTransform)imageObject.transform;
        Stretch(rectTransform);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(
        string objectName,
        RectTransform parent,
        string value,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment,
        Color color,
        float x,
        float y,
        float width,
        float height)
    {
        RectTransform rect = CreateFixedRect(
            parent,
            objectName,
            x,
            y,
            width,
            height);
        return ConfigureText(
            rect.gameObject.AddComponent<Text>(),
            value,
            fontSize,
            fontStyle,
            alignment,
            color);
    }

    private static Text CreateStretchText(
        string objectName,
        RectTransform parent,
        string value,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment,
        Color color,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        RectTransform rect = (RectTransform)textObject.transform;
        Stretch(rect);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return ConfigureText(
            textObject.AddComponent<Text>(),
            value,
            fontSize,
            fontStyle,
            alignment,
            color);
    }

    private static Text ConfigureText(
        Text text,
        string value,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment,
        Color color)
    {
        text.font = RuntimeFont;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        text.supportRichText = false;
        return text;
    }

    private static RectTransform CreateFixedRect(
        RectTransform parent,
        string objectName,
        float x,
        float y,
        float width,
        float height)
    {
        return CreateAnchoredRect(
            objectName,
            parent,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(x, -y),
            new Vector2(width, height));
    }

    private static RectTransform CreateInsetRect(
        string objectName,
        RectTransform parent,
        float inset)
    {
        GameObject insetObject = CreateUiObject(objectName, parent);
        RectTransform rect = (RectTransform)insetObject.transform;
        Stretch(rect);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        return rect;
    }

    private static RectTransform CreateAnchoredRect(
        string objectName,
        RectTransform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject uiObject = CreateUiObject(objectName, parent);
        RectTransform rect = (RectTransform)uiObject.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return rect;
    }

    private static GameObject CreateUiObject(
        string objectName,
        Transform parent)
    {
        GameObject uiObject = new(objectName, typeof(RectTransform));
        int uiLayer = LayerMask.NameToLayer("UI");
        uiObject.layer = uiLayer >= 0 ? uiLayer : 5;
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Font RuntimeFont
    {
        get
        {
            if (runtimeFont == null)
            {
                runtimeFont = Resources.GetBuiltinResource<Font>(
                    "LegacyRuntime.ttf");
            }

            return runtimeFont;
        }
    }
}
