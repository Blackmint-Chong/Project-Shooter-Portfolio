using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(MatchLifeHud))]
public sealed class BattlePauseMenu : MonoBehaviour
{
    private const string DefaultGameSetupScenePath =
        "Assets/Scenes/GameSetup.unity";

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

    private readonly List<Behaviour> suspendedInputSources = new();
    private readonly InputAction pauseInputAction = new(
        "Pause",
        InputActionType.Button,
        "<Keyboard>/escape");

    private MatchController subscribedController;
    private GameObject overlayRoot;
    private Image overlayBackground;
    private Text titleText;
    private Button resumeButton;
    private Button restartButton;
    private Button changeSetupButton;
    private bool isPaused;
    private bool isTransitioning;
    private float timeScaleBeforePause = 1f;

    private static Font runtimeFont;

    public MatchController MatchController => matchController;
    public BattleSetup BattleSetup => battleSetup;
    public InputAction PauseInputAction => pauseInputAction;
    public Image OverlayBackground => overlayBackground;
    public Text TitleText => titleText;
    public Button ResumeButton => resumeButton;
    public Button RestartButton => restartButton;
    public Button ChangeSetupButton => changeSetupButton;
    public string GameSetupScenePath => gameSetupScenePath;
    public bool IsPaused => isPaused;
    public bool IsVisible => overlayRoot != null && overlayRoot.activeSelf;
    public bool IsTransitioning => isTransitioning;
    public bool AreParticipantInputsSuspended =>
        suspendedInputSources.Count > 0;

    private void Awake()
    {
        ResolveDependencies();
        EnsureBuilt();
        ClosePauseMenu();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        EnsureBuilt();
        SubscribeToMatch();
        pauseInputAction.performed += HandlePausePerformed;
        pauseInputAction.Enable();

        if (matchController == null || !matchController.IsMatchRunning)
        {
            ClosePauseMenu();
        }
    }

    private void OnDisable()
    {
        pauseInputAction.Disable();
        pauseInputAction.performed -= HandlePausePerformed;
        UnsubscribeFromMatch();
        ClosePauseMenu();
    }

    private void OnDestroy()
    {
        pauseInputAction.Dispose();
    }

    public void Bind(MatchController controller)
    {
        if (matchController == controller)
        {
            if (isActiveAndEnabled)
            {
                SubscribeToMatch();
            }

            return;
        }

        ClosePauseMenu();
        UnsubscribeFromMatch();
        matchController = controller;
        if (isActiveAndEnabled)
        {
            SubscribeToMatch();
        }
    }

    public void TogglePause()
    {
        if (isTransitioning)
        {
            return;
        }

        if (isPaused)
        {
            ResumeBattle();
        }
        else
        {
            PauseBattle();
        }
    }

    public bool PauseBattle()
    {
        if (isPaused
            || isTransitioning
            || matchController == null
            || !matchController.isActiveAndEnabled
            || !matchController.IsMatchRunning
            || Time.timeScale <= 0f)
        {
            return false;
        }

        EnsureBuilt();
        if (overlayRoot == null)
        {
            return false;
        }

        timeScaleBeforePause = Time.timeScale;
        isPaused = true;
        overlayRoot.SetActive(true);
        SetButtonsInteractable(true);
        SuspendParticipantInputs();
        Time.timeScale = 0f;

        if (EventSystem.current != null && resumeButton != null)
        {
            EventSystem.current.SetSelectedGameObject(
                resumeButton.gameObject);
        }

        return true;
    }

    public void ResumeBattle()
    {
        if (isTransitioning || !isPaused)
        {
            return;
        }

        ClosePauseMenu();
    }

    public void RestartBattle()
    {
        if (!isPaused || isTransitioning || matchController == null)
        {
            return;
        }

        isTransitioning = true;
        SetButtonsInteractable(false);
        RecycleSpawnedBullets();
        ClosePauseMenu();

        bool restarted = battleSetup != null
            ? battleSetup.RestartBattle()
            : matchController.RestartMatch();
        if (restarted)
        {
            return;
        }

        isTransitioning = false;
        PauseBattle();
    }

    public void ChangeSetup()
    {
        if (!isPaused || isTransitioning)
        {
            return;
        }

        int sceneBuildIndex =
            SceneUtility.GetBuildIndexByScenePath(gameSetupScenePath);
        if (sceneBuildIndex < 0)
        {
            Debug.LogError(
                $"The game setup scene is not enabled in Build Settings: {gameSetupScenePath}",
                this);
            return;
        }

        isTransitioning = true;
        SetButtonsInteractable(false);
        ClosePauseMenu();
        SceneManager.LoadScene(sceneBuildIndex, LoadSceneMode.Single);
    }

    private void HandlePausePerformed(InputAction.CallbackContext context)
    {
        TogglePause();
    }

    private void ResolveDependencies()
    {
        ResolveBattleSetup();

        if (matchController != null
            && matchController.gameObject.scene == gameObject.scene)
        {
            return;
        }

        MatchLifeHud lifeHud = GetComponent<MatchLifeHud>();
        matchController = lifeHud != null ? lifeHud.MatchController : null;
        if (matchController != null)
        {
            return;
        }

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
        ClosePauseMenu();
    }

    private void HandleMatchEnded(PlayerLife winner)
    {
        isTransitioning = false;
        ClosePauseMenu();
    }

    private void ClosePauseMenu()
    {
        if (overlayRoot != null)
        {
            ClearOverlaySelection();
            overlayRoot.SetActive(false);
        }

        bool restoreTimeScale = isPaused;
        isPaused = false;
        if (restoreTimeScale)
        {
            Time.timeScale = timeScaleBeforePause;
        }

        RestoreParticipantInputs();
    }

    private void SuspendParticipantInputs()
    {
        if (matchController == null)
        {
            return;
        }

        foreach (PlayerLife participant in matchController.Participants)
        {
            if (participant == null)
            {
                continue;
            }

            if (participant.TryGetComponent(
                    out PlayerInputCommandSource humanInput))
            {
                SuspendInputSource(humanInput);
            }

            if (participant.TryGetComponent(
                    out AIPlayerCommandSource aiInput))
            {
                SuspendInputSource(aiInput);
            }
        }
    }

    private void SuspendInputSource(Behaviour inputSource)
    {
        if (inputSource == null
            || !inputSource.enabled
            || suspendedInputSources.Contains(inputSource))
        {
            return;
        }

        inputSource.enabled = false;
        suspendedInputSources.Add(inputSource);
    }

    private void RestoreParticipantInputs()
    {
        foreach (Behaviour inputSource in suspendedInputSources)
        {
            if (inputSource != null)
            {
                inputSource.enabled = true;
            }
        }

        suspendedInputSources.Clear();
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

        overlayRoot = CreateUiObject("PauseOverlay", canvasRect);
        RectTransform overlayRect =
            (RectTransform)overlayRoot.transform;
        Stretch(overlayRect);

        overlayBackground = overlayRoot.AddComponent<Image>();
        overlayBackground.color = OverlayColor;
        overlayBackground.raycastTarget = true;

        titleText = CreateText(
            "PauseTitle",
            overlayRect,
            "PAUSED",
            new Vector2(0f, 48f),
            new Vector2(720f, 120f),
            72);
        resumeButton = CreateButton(
            "ResumeButton",
            overlayRect,
            "RESUME",
            -324f,
            -110f,
            PrimaryButtonColor);
        restartButton = CreateButton(
            "RestartButton",
            overlayRect,
            "RESTART",
            0f,
            -110f,
            SecondaryButtonColor);
        changeSetupButton = CreateButton(
            "ChangeSetupButton",
            overlayRect,
            "CHANGE SETUP",
            324f,
            -110f,
            SecondaryButtonColor);

        resumeButton.onClick.AddListener(ResumeBattle);
        restartButton.onClick.AddListener(RestartBattle);
        changeSetupButton.onClick.AddListener(ChangeSetup);
        overlayRoot.SetActive(false);
        EnsureEventSystem();
    }

    private static Text CreateText(
        string objectName,
        RectTransform parent,
        string value,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        RectTransform textRect =
            (RectTransform)textObject.transform;
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = anchoredPosition;
        textRect.sizeDelta = size;

        Text text = textObject.AddComponent<Text>();
        text.font = RuntimeFont;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        text.supportRichText = false;
        return text;
    }

    private static Button CreateButton(
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

        Text labelText = CreateText(
            "Label",
            buttonRect,
            label,
            Vector2.zero,
            new Vector2(280f, 60f),
            24);
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = Vector2.one;
        labelText.rectTransform.offsetMin = new Vector2(10f, 6f);
        labelText.rectTransform.offsetMax = new Vector2(-10f, -6f);
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

    private void SetButtonsInteractable(bool interactable)
    {
        if (resumeButton != null)
        {
            resumeButton.interactable = interactable;
        }

        if (restartButton != null)
        {
            restartButton.interactable = interactable;
        }

        if (changeSetupButton != null)
        {
            changeSetupButton.interactable = interactable;
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

    private static void EnsureEventSystem()
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
