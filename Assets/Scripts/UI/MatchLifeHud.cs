using UnityEngine;
using UnityEngine.UI;

public enum PlayerMatchResult
{
    None,
    Victory,
    Defeat
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class MatchLifeHud : MonoBehaviour
{
    [SerializeField] private MatchController matchController;
    [SerializeField] private BattleMapContext mapContext;
    [SerializeField] private PlayerLife leftParticipant;
    [SerializeField] private PlayerLife rightParticipant;
    [SerializeField] private Image leftLifeBackground;
    [SerializeField] private Image rightLifeBackground;
    [SerializeField] private Image leftPortrait;
    [SerializeField] private Image rightPortrait;
    [SerializeField] private Text leftLifeText;
    [SerializeField] private Text rightLifeText;
    [SerializeField] private Text resultText;
    [SerializeField] private string lifeLabel = "LIFE";
    [SerializeField] private string victoryLabel = "VICTORY";
    [SerializeField] private string defeatLabel = "DEFEAT";

    private MatchController subscribedController;

    public MatchController MatchController => matchController;
    public BattleMapContext MapContext => mapContext;
    public PlayerLife LeftParticipant => leftParticipant;
    public PlayerLife RightParticipant => rightParticipant;
    public Image LeftDisplayBackground => leftLifeBackground;
    public Image RightDisplayBackground => rightLifeBackground;
    public Image LeftPortrait => leftPortrait;
    public Image RightPortrait => rightPortrait;
    public string LeftDisplayText => leftLifeText != null ? leftLifeText.text : string.Empty;
    public string RightDisplayText => rightLifeText != null ? rightLifeText.text : string.Empty;
    public PlayerMatchResult Result { get; private set; }
    public string ResultDisplayText => resultText != null ? resultText.text : string.Empty;
    public bool IsLeftSlotVisible => leftLifeText != null && leftLifeText.gameObject.activeSelf;
    public bool IsRightSlotVisible => rightLifeText != null && rightLifeText.gameObject.activeSelf;
    public bool IsLeftPortraitVisible =>
        leftPortrait != null && leftPortrait.gameObject.activeSelf;
    public bool IsRightPortraitVisible =>
        rightPortrait != null && rightPortrait.gameObject.activeSelf;
    public bool IsResultVisible => resultText != null && resultText.gameObject.activeSelf;
    public RectTransform LeftDisplayTransform =>
        leftLifeText != null ? leftLifeText.rectTransform : null;
    public RectTransform RightDisplayTransform =>
        rightLifeText != null ? rightLifeText.rectTransform : null;
    public RectTransform ResultDisplayTransform =>
        resultText != null ? resultText.rectTransform : null;

    public void Bind(MatchController controller)
    {
        Bind(controller, mapContext);
    }

    public void Bind(
        MatchController controller,
        BattleMapContext battleMapContext)
    {
        if (matchController == controller && mapContext == battleMapContext)
        {
            Refresh();
            return;
        }

        UnsubscribeFromMatch();
        matchController = controller;
        mapContext = battleMapContext;
        if (isActiveAndEnabled)
        {
            SubscribeToMatch();
            Refresh();
        }
    }

    private void Awake()
    {
        ResolveMatchController();
    }

    private void OnEnable()
    {
        ResolveMatchController();
        SubscribeToMatch();
        Refresh();
    }

    private void Start()
    {
        // MatchController may discover its participants after this HUD's OnEnable.
        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeFromMatch();
    }

    public void Refresh()
    {
        ResolveMatchController();
        SubscribeToMatch();
        AssignParticipants();
        RefreshDisplays();
        RefreshResultDisplay();
    }

    private void ResolveMatchController()
    {
        if (matchController != null)
        {
            return;
        }

        MatchController[] controllers = FindObjectsByType<MatchController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (MatchController controller in controllers)
        {
            if (controller.gameObject.scene == gameObject.scene)
            {
                matchController = controller;
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
        matchController.PlayerLifeChanged += HandlePlayerLifeChanged;
        matchController.MatchEnded += HandleMatchEnded;
        matchController.ParticipantsChanged += HandleParticipantsChanged;
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
        subscribedController.PlayerLifeChanged -= HandlePlayerLifeChanged;
        subscribedController.MatchEnded -= HandleMatchEnded;
        subscribedController.ParticipantsChanged -= HandleParticipantsChanged;
        subscribedController = null;
    }

    private void HandleMatchStarted()
    {
        Refresh();
    }

    private void HandlePlayerLifeChanged(PlayerLife participant, int remainingLife)
    {
        Refresh();
    }

    private void HandleMatchEnded(PlayerLife winner)
    {
        Refresh();
    }

    private void HandleParticipantsChanged()
    {
        Refresh();
    }

    private void AssignParticipants()
    {
        if (matchController == null)
        {
            leftParticipant = null;
            rightParticipant = null;
            return;
        }

        if (TryAssignParticipantsByMapSide())
        {
            return;
        }

        leftParticipant = FindHumanParticipant()
            ?? FindFirstUnassignedParticipant(null);
        rightParticipant = FindFirstUnassignedParticipant(leftParticipant);
    }

    private bool TryAssignParticipantsByMapSide()
    {
        if (mapContext == null
            || !mapContext.TryGetSideSpawnSlots(
                out BattleSpawnSlot leftSpawnSlot,
                out BattleSpawnSlot rightSpawnSlot))
        {
            return false;
        }

        leftParticipant = FindParticipant(leftSpawnSlot.Slot);
        rightParticipant = FindParticipant(rightSpawnSlot.Slot);
        return leftParticipant != null || rightParticipant != null;
    }

    private PlayerLife FindParticipant(BattlePlayerSlot playerSlot)
    {
        foreach (PlayerLife participant in matchController.Participants)
        {
            if (participant != null
                && participant.TryGetComponent(
                    out BattleParticipantSlot participantSlot)
                && participantSlot.Slot == playerSlot)
            {
                return participant;
            }
        }

        return null;
    }

    private PlayerLife FindHumanParticipant()
    {
        foreach (PlayerLife participant in matchController.Participants)
        {
            if (participant == null)
            {
                continue;
            }

            if (participant.TryGetComponent(out PlayerInputCommandSource _))
            {
                return participant;
            }
        }

        return null;
    }

    private PlayerLife FindFirstUnassignedParticipant(PlayerLife excludedParticipant)
    {
        foreach (PlayerLife participant in matchController.Participants)
        {
            if (participant != null && participant != excludedParticipant)
            {
                return participant;
            }
        }

        return null;
    }

    private void RefreshDisplays()
    {
        RefreshDisplay(
            leftLifeText,
            leftLifeBackground,
            leftPortrait,
            leftParticipant);
        RefreshDisplay(
            rightLifeText,
            rightLifeBackground,
            rightPortrait,
            rightParticipant);
    }

    private void RefreshDisplay(
        Text lifeText,
        Image lifeBackground,
        Image portrait,
        PlayerLife participant)
    {
        bool hasParticipant = participant != null;
        if (lifeBackground != null)
        {
            lifeBackground.gameObject.SetActive(hasParticipant);
        }

        RefreshPortrait(portrait, participant);

        if (lifeText == null)
        {
            return;
        }

        lifeText.gameObject.SetActive(hasParticipant);
        if (hasParticipant)
        {
            lifeText.text = $"{lifeLabel} {participant.Life}";
        }
    }

    private static void RefreshPortrait(Image portrait, PlayerLife participant)
    {
        if (portrait == null)
        {
            return;
        }

        Sprite portraitSprite = null;
        if (participant != null
            && participant.TryGetComponent(
                out BattleParticipantPresentation presentation))
        {
            portraitSprite = presentation.Portrait;
        }

        portrait.sprite = portraitSprite;
        portrait.gameObject.SetActive(portraitSprite != null);
    }

    private void RefreshResultDisplay()
    {
        Result = DeterminePlayerResult();
        if (resultText == null)
        {
            return;
        }

        bool showResult = Result != PlayerMatchResult.None;
        resultText.gameObject.SetActive(showResult);
        resultText.text = Result switch
        {
            PlayerMatchResult.Victory => victoryLabel,
            PlayerMatchResult.Defeat => defeatLabel,
            _ => string.Empty
        };
    }

    private PlayerMatchResult DeterminePlayerResult()
    {
        if (matchController == null || !matchController.IsMatchFinished)
        {
            return PlayerMatchResult.None;
        }

        PlayerLife humanParticipant = FindHumanParticipant();
        if (humanParticipant == null)
        {
            return PlayerMatchResult.None;
        }

        if (humanParticipant.Life <= 0)
        {
            return PlayerMatchResult.Defeat;
        }

        return matchController.Winner == humanParticipant
            ? PlayerMatchResult.Victory
            : PlayerMatchResult.Defeat;
    }
}
