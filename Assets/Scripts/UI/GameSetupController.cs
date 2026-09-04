using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(GameSetupView))]
public sealed class GameSetupController : MonoBehaviour
{
    [SerializeField] private BattleMapCatalog mapCatalog;
    [SerializeField] private AIDifficultyCatalog aiDifficultyCatalog;
    [SerializeField] private GameSetupView view;
    [SerializeField] private string mainMenuScenePath =
        "Assets/Scenes/MainMenu.unity";

    private int selectedMapIndex = -1;
    private int selectedDifficultyIndex = -1;
    private bool isInitialized;
    private bool isTransitioning;

    public BattleMapCatalog MapCatalog => mapCatalog;
    public AIDifficultyCatalog AIDifficultyCatalog => aiDifficultyCatalog;
    public GameSetupView View => view;
    public BattleMapDefinition SelectedMap =>
        selectedMapIndex >= 0
        && mapCatalog != null
        && selectedMapIndex < mapCatalog.Maps.Count
            ? mapCatalog.Maps[selectedMapIndex]
            : null;
    public AIDifficultyDefinition SelectedAIDifficulty =>
        selectedDifficultyIndex >= 0
        && aiDifficultyCatalog != null
        && selectedDifficultyIndex < aiDifficultyCatalog.Profiles.Count
            ? aiDifficultyCatalog.Profiles[selectedDifficultyIndex]
            : null;
    public int SelectedMapIndex => selectedMapIndex;
    public int SelectedDifficultyIndex => selectedDifficultyIndex;
    public bool IsTransitioning => isTransitioning;

    private void Awake()
    {
        ResolveView();
        Initialize();
    }

    private void OnEnable()
    {
        ResolveView();
        Initialize();
        SubscribeToView();
    }

    private void OnDisable()
    {
        UnsubscribeFromView();
    }

    public bool Initialize()
    {
        if (isInitialized)
        {
            return true;
        }

        if (view == null)
        {
            Debug.LogError("A GameSetupView is required.", this);
            return false;
        }

        view.EnsureBuilt();
        if (mapCatalog == null)
        {
            return FailInitialization(
                "A battle map catalog is required for game setup.");
        }

        if (!mapCatalog.TryValidate(out string catalogError))
        {
            return FailInitialization(
                $"The battle map catalog is invalid: {catalogError}");
        }

        if (aiDifficultyCatalog == null)
        {
            return FailInitialization(
                "An AI difficulty catalog is required for game setup.");
        }

        if (!aiDifficultyCatalog.TryValidate(out string difficultyError))
        {
            return FailInitialization(
                $"The AI difficulty catalog is invalid: {difficultyError}");
        }

        BattleMapDefinition initialMap = mapCatalog.DefaultMap;
        AIDifficultyDefinition initialDifficulty =
            aiDifficultyCatalog.DefaultProfile;
        if (BattleSession.TryResolveSelectedMap(
                mapCatalog,
                out BattleMapDefinition sessionMap))
        {
            initialMap = sessionMap;
            AIDifficultyDefinition sessionDifficulty =
                BattleSession.Configuration.AIDifficultyProfile;
            if (!aiDifficultyCatalog.Contains(sessionDifficulty))
            {
                return FailInitialization(
                    "The saved AI difficulty is not present in the current catalog.");
            }

            initialDifficulty = sessionDifficulty;
        }

        selectedMapIndex = IndexOf(initialMap);
        if (selectedMapIndex < 0)
        {
            return FailInitialization(
                "The default battle map is not present in the catalog.");
        }

        selectedDifficultyIndex = IndexOf(initialDifficulty);
        if (selectedDifficultyIndex < 0)
        {
            return FailInitialization(
                "The default AI difficulty is not present in the catalog.");
        }

        view.ConfigureMaps(mapCatalog.Maps, selectedMapIndex);
        view.ConfigureDifficultyProfiles(aiDifficultyCatalog.Profiles);
        RefreshView();
        view.ShowStatus(string.Empty);
        view.SetInteractionEnabled(true);
        isInitialized = true;
        return true;
    }

    public bool TrySelectMap(string mapId)
    {
        if (mapCatalog == null
            || !mapCatalog.TryGetMap(mapId, out BattleMapDefinition map))
        {
            return false;
        }

        int mapIndex = IndexOf(map);
        if (mapIndex < 0)
        {
            return false;
        }

        SelectMapIndex(mapIndex);
        return true;
    }

    public void StartBattle()
    {
        if (isTransitioning || !Initialize())
        {
            return;
        }

        BattleMapDefinition selectedMap = SelectedMap;
        if (selectedMap == null)
        {
            ShowError("Select a battle map before starting.");
            return;
        }

        int sceneBuildIndex =
            SceneUtility.GetBuildIndexByScenePath(selectedMap.ScenePath);
        if (sceneBuildIndex < 0)
        {
            ShowError(
                $"The selected map is not enabled in Build Settings: {selectedMap.ScenePath}");
            return;
        }

        AIDifficultyDefinition selectedDifficulty = SelectedAIDifficulty;
        if (selectedDifficulty == null)
        {
            ShowError("Select an AI difficulty before starting.");
            return;
        }

        BattleSession.Configure(selectedMap, selectedDifficulty);
        BeginTransition();
        SceneManager.LoadScene(sceneBuildIndex, LoadSceneMode.Single);
    }

    public void ReturnToMainMenu()
    {
        if (isTransitioning)
        {
            return;
        }

        int sceneBuildIndex =
            SceneUtility.GetBuildIndexByScenePath(mainMenuScenePath);
        if (sceneBuildIndex < 0)
        {
            ShowError(
                $"The main menu is not enabled in Build Settings: {mainMenuScenePath}");
            return;
        }

        BeginTransition();
        SceneManager.LoadScene(sceneBuildIndex, LoadSceneMode.Single);
    }

    private void ResolveView()
    {
        if (view == null)
        {
            view = GetComponent<GameSetupView>();
        }

        if (view == null)
        {
            view = gameObject.AddComponent<GameSetupView>();
        }
    }

    private void SubscribeToView()
    {
        if (view == null)
        {
            return;
        }

        UnsubscribeFromView();
        view.MapDropdown.onValueChanged.AddListener(SelectMapIndex);
        view.AIDifficultyDropdown.onValueChanged.AddListener(
            SelectDifficultyIndex);
        view.StartButton.onClick.AddListener(StartBattle);
        view.BackButton.onClick.AddListener(ReturnToMainMenu);
    }

    private void UnsubscribeFromView()
    {
        if (view == null || !view.IsBuilt)
        {
            return;
        }

        view.MapDropdown.onValueChanged.RemoveListener(SelectMapIndex);
        view.AIDifficultyDropdown.onValueChanged.RemoveListener(
            SelectDifficultyIndex);
        view.StartButton.onClick.RemoveListener(StartBattle);
        view.BackButton.onClick.RemoveListener(ReturnToMainMenu);
    }

    private void SelectMapIndex(int mapIndex)
    {
        if (isTransitioning
            || mapCatalog == null
            || mapIndex < 0
            || mapIndex >= mapCatalog.Maps.Count)
        {
            return;
        }

        selectedMapIndex = mapIndex;
        view.MapDropdown.SetValueWithoutNotify(mapIndex);
        view.MapDropdown.RefreshShownValue();
        RefreshView();
    }

    private void SelectDifficultyIndex(int difficultyIndex)
    {
        if (isTransitioning
            || aiDifficultyCatalog == null
            || difficultyIndex < 0
            || difficultyIndex >= aiDifficultyCatalog.Profiles.Count)
        {
            return;
        }

        selectedDifficultyIndex = difficultyIndex;
        view.ShowRoster(SelectedAIDifficulty);
    }

    private void RefreshView()
    {
        if (view == null)
        {
            return;
        }

        view.ShowMap(SelectedMap);
        view.ShowRoster(SelectedAIDifficulty);
    }

    private int IndexOf(BattleMapDefinition map)
    {
        if (map == null || mapCatalog == null)
        {
            return -1;
        }

        IReadOnlyList<BattleMapDefinition> maps = mapCatalog.Maps;
        for (int index = 0; index < maps.Count; index++)
        {
            if (maps[index] == map)
            {
                return index;
            }
        }

        return -1;
    }

    private int IndexOf(AIDifficultyDefinition difficulty)
    {
        if (difficulty == null || aiDifficultyCatalog == null)
        {
            return -1;
        }

        IReadOnlyList<AIDifficultyDefinition> profiles =
            aiDifficultyCatalog.Profiles;
        for (int index = 0; index < profiles.Count; index++)
        {
            if (profiles[index] == difficulty)
            {
                return index;
            }
        }

        return -1;
    }

    private bool FailInitialization(string message)
    {
        ShowError(message);
        view?.SetInteractionEnabled(false);
        return false;
    }

    private void ShowError(string message)
    {
        Debug.LogError(message, this);
        view?.ShowStatus(message);
    }

    private void BeginTransition()
    {
        isTransitioning = true;
        view.ShowStatus("LOADING...");
        view.SetInteractionEnabled(false);
    }
}
