using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(MatchLifeHud))]
public sealed class MatchResultMenu : MonoBehaviour
{
    private const string DefaultGameSetupScenePath =
        "Assets/Scenes/GameSetup.unity";
    private const string DefaultMainMenuScenePath =
        "Assets/Scenes/MainMenu.unity";

    private static readonly Color OverlayColor =
        new(0f, 0f, 0f, 0.76f);
    private static readonly Color PrimaryButtonColor =
        new Color32(211, 139, 50, 255);
    private static readonly Color SecondaryButtonColor =
        new Color32(63, 74, 89, 255);

    [SerializeField] private MatchController matchController;
    [SerializeField] private BattleSetup battleSetup;
    [SerializeField] private string gameSetupScenePath =
        DefaultGameSetupScenePath;
    [SerializeField] private string mainMenuScenePath =
        DefaultMainMenuScenePath;

    private readonly List<PlayerInputCommandSource> suspendedInputSources =
        new();
    private MatchController subscribedController;
    private MatchLifeHud lifeHud;
    private GameObject overlayRoot;
    private Image overlayBackground;
    private Button rematchButton;
    private Button changeSetupButton;
    private Button mainMenuButton;
    private bool isTransitioning;

    private static Font runtimeFont;

    public MatchController MatchController => matchController;
    public BattleSetup BattleSetup => battleSetup;
    public Image OverlayBackground => overlayBackground;
    public Button RematchButton => rematchButton;
    public Button ChangeSetupButton => changeSetupButton;
    public Button MainMenuButton => mainMenuButton;
    public string GameSetupScenePath => gameSetupScenePath;
    public string MainMenuScenePath => mainMenuScenePath;
    public bool IsVisible => overlayRoot != null && overlayRoot.activeSelf;
    public bool IsTransitioning => isTransitioning;
    public bool IsPlayerInputSuspended => suspendedInputSources.Count > 0;

    private void Awake()
    {
        ResolveDependencies();
        EnsureBuilt();
        RefreshVisibility();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        EnsureBuilt();
        SubscribeToMatch();
        RefreshVisibility();
    }

    private void Start()
    {
        RefreshVisibility();
    }

    private void OnDisable()
    {
        UnsubscribeFromMatch();
        HideOverlay();
    }

    public void Bind(MatchController controller)
    {
        if (matchController == controller)
        {
            if (isActiveAndEnabled)
            {
                SubscribeToMatch();
                RefreshVisibility();
            }

            return;
        }

        UnsubscribeFromMatch();
        matchController = controller;
        if (isActiveAndEnabled)
        {
            SubscribeToMatch();
            RefreshVisibility();
        }
    }

    public void Rematch()
    {
        if (isTransitioning || matchController == null)
        {
            return;
        }

        isTransitioning = true;
        SetButtonsInteractable(false);
        RecycleSpawnedBullets();
        HideOverlay();

        bool restarted = battleSetup != null
            ? battleSetup.RestartBattle()
            : matchController.RestartMatch();
        if (restarted)
        {
            return;
        }

        isTransitioning = false;
        ShowOverlay();
    }

    private void RecycleSpawnedBullets()
    {
        Bullet[] bullets = FindObjectsByType<Bullet>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (Bullet bullet in bullets)
        {
            if (bullet != null
                && bullet.gameObject.scene == gameObject.scene
                && bullet.IsSpawned)
            {
                bullet.RequestRecycle();
            }
        }
    }

    public void ChangeSetup()
    {
        LoadScene(gameSetupScenePath, "game setup");
    }

    public void ReturnToMainMenu()
    {
        LoadScene(mainMenuScenePath, "main menu");
    }

    private void ResolveDependencies()
    {
        ResolveBattleSetup();

        if (lifeHud == null)
        {
            lifeHud = GetComponent<MatchLifeHud>();
        }

        if (matchController == null && lifeHud != null)
        {
            matchController = lifeHud.MatchController;
        }

        if (matchController != null
            && matchController.gameObject.scene == gameObject.scene)
        {
            return;
        }

        matchController = null;
        MatchController[] controllers = FindObjectsByType<MatchController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (MatchController candidate in controllers)
        {
            if (candidate.gameObject.scene == gameObject.scene)
            {
                matchController = candidate;
                return;
            }
        }
    }

    private void ResolveBattleSetup()
    {
        if (battleSetup != null
            && battleSetup.gameObject.scene == gameObject.scene)
        {
            return;
        }

        battleSetup = null;
        BattleSetup[] setups = FindObjectsByType<BattleSetup>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (BattleSetup candidate in setups)
        {
            if (candidate.gameObject.scene == gameObject.scene)
            {
                battleSetup = candidate;
                return;
            }
        }
    }

    private void SubscribeToMatch()
    {
        if (subscribedController == matchController)
        {
            return;
        }

        UnsubscribeFromMatch();
        if (matchController == null)
        {
            return;
        }

        matchController.MatchStarted += HandleMatchStarted;
        matchController.MatchEnded += HandleMatchEnded;
        subscribedController = matchController;
    }

    private void UnsubscribeFromMatch()
    {
        if (subscribedController == null)
        {
            subscribedController = null;
            return;
        }

        subscribedController.MatchStarted -= HandleMatchStarted;
        subscribedController.MatchEnded -= HandleMatchEnded;
        subscribedController = null;
    }

    private void HandleMatchStarted()
    {
        isTransitioning = false;
        HideOverlay();
    }

    private void HandleMatchEnded(PlayerLife winner)
    {
        ShowOverlay();
    }

    private void RefreshVisibility()
    {
        if (matchController != null && matchController.IsMatchFinished)
        {
            ShowOverlay();
        }
        else
        {
            HideOverlay();
        }
    }

    private void ShowOverlay()
    {
        EnsureBuilt();
        if (overlayRoot == null)
        {
            return;
        }

        isTransitioning = false;
        overlayRoot.SetActive(true);
        SetButtonsInteractable(true);
        BringResultTextToFront();
        SuspendPlayerInput();

        if (EventSystem.current != null && rematchButton != null)
        {
            EventSystem.current.SetSelectedGameObject(
                rematchButton.gameObject);
        }
    }

    private void HideOverlay()
    {
        if (overlayRoot != null)
        {
            ClearOverlaySelection();
            overlayRoot.SetActive(false);
        }

        RestorePlayerInput();
    }

    private void LoadScene(string scenePath, string destinationName)
    {
        if (isTransitioning)
        {
            return;
        }

        int sceneBuildIndex =
            SceneUtility.GetBuildIndexByScenePath(scenePath);
        if (sceneBuildIndex < 0)
        {
            Debug.LogError(
                $"The {destinationName} scene is not enabled in Build Settings: {scenePath}",
                this);
            return;
        }

        isTransitioning = true;
        SetButtonsInteractable(false);
        RestorePlayerInput();
        SceneManager.LoadScene(sceneBuildIndex, LoadSceneMode.Single);
    }

    private void SuspendPlayerInput()
    {
        if (matchController == null)
        {
            return;
        }

        foreach (PlayerLife participant in matchController.Participants)
        {
            if (participant == null
                || !participant.TryGetComponent(
                    out PlayerInputCommandSource inputSource)
                || !inputSource.enabled
                || suspendedInputSources.Contains(inputSource))
            {
                continue;
            }

            inputSource.enabled = false;
            suspendedInputSources.Add(inputSource);
        }
    }

    private void RestorePlayerInput()
    {
        foreach (PlayerInputCommandSource inputSource in suspendedInputSources)
        {
            if (inputSource != null)
            {
                inputSource.enabled = true;
            }
        }

        suspendedInputSources.Clear();
    }

    private void EnsureBuilt()
    {
        if (overlayRoot != null)
        {
            return;
        }

        RectTransform canvasRect = transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        overlayRoot = CreateUiObject("MatchResultOverlay", canvasRect);
        RectTransform overlayRect =
            (RectTransform)overlayRoot.transform;
        Stretch(overlayRect);

        overlayBackground = overlayRoot.AddComponent<Image>();
        overlayBackground.color = OverlayColor;
        overlayBackground.raycastTarget = true;

        rematchButton = CreateButton(
            "RematchButton",
            overlayRect,
            "REMATCH",
            -324f,
            -150f,
            PrimaryButtonColor);
        changeSetupButton = CreateButton(
            "ChangeSetupButton",
            overlayRect,
            "CHANGE SETUP",
            0f,
            -150f,
            SecondaryButtonColor);
        mainMenuButton = CreateButton(
            "MainMenuButton",
            overlayRect,
            "MAIN MENU",
            324f,
            -150f,
            SecondaryButtonColor);

        rematchButton.onClick.AddListener(Rematch);
        changeSetupButton.onClick.AddListener(ChangeSetup);
        mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        overlayRoot.SetActive(false);
        EnsureEventSystem();
    }

    private Button CreateButton(
        string objectName,
        RectTransform parent,
        string label,
        float x,
        float y,
        Color color)
    {
        GameObject buttonObject = CreateUiObject(objectName, parent);
        RectTransform buttonRect =
            (RectTransform)buttonObject.transform;
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(x, y);
        buttonRect.sizeDelta = new Vector2(300f, 72f);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = color;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.colors = CreateColorBlock(color);

        GameObject labelObject = CreateUiObject("Label", buttonRect);
        RectTransform labelRect =
            (RectTransform)labelObject.transform;
        Stretch(labelRect);
        labelRect.offsetMin = new Vector2(10f, 6f);
        labelRect.offsetMax = new Vector2(-10f, -6f);

        Text labelText = labelObject.AddComponent<Text>();
        labelText.font = RuntimeFont;
        labelText.text = label;
        labelText.fontSize = 24;
        labelText.fontStyle = FontStyle.Bold;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
        labelText.verticalOverflow = VerticalWrapMode.Truncate;
        labelText.raycastTarget = false;
        labelText.supportRichText = false;
        return button;
    }

    private static ColorBlock CreateColorBlock(Color normalColor)
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = normalColor;
        colors.highlightedColor = Color.Lerp(
            normalColor,
            Color.white,
            0.18f);
        colors.pressedColor = Color.Lerp(
            normalColor,
            Color.black,
            0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(
            normalColor.r,
            normalColor.g,
            normalColor.b,
            0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        return colors;
    }

    private void BringResultTextToFront()
    {
        RectTransform resultTransform =
            lifeHud != null ? lifeHud.ResultDisplayTransform : null;
        if (resultTransform != null)
        {
            resultTransform.SetAsLastSibling();
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (rematchButton != null)
        {
            rematchButton.interactable = interactable;
        }

        if (changeSetupButton != null)
        {
            changeSetupButton.interactable = interactable;
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.interactable = interactable;
        }
    }

    private void ClearOverlaySelection()
    {
        if (EventSystem.current == null
            || EventSystem.current.currentSelectedGameObject == null
            || overlayRoot == null)
        {
            return;
        }

        Transform selectedTransform =
            EventSystem.current.currentSelectedGameObject.transform;
        if (selectedTransform == overlayRoot.transform
            || selectedTransform.IsChildOf(overlayRoot.transform))
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void EnsureEventSystem()
    {
        EventSystem existingEventSystem = FindFirstObjectByType<EventSystem>(
            FindObjectsInactive.Exclude);
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
