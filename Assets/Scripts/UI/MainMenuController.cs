using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private string gameSetupScenePath =
        "Assets/Scenes/GameSetup.unity";

    private bool isStarting;

    public Button StartButton => startButton;
    public string GameSetupScenePath => gameSetupScenePath;
    public bool IsStarting => isStarting;

    private void OnEnable()
    {
        isStarting = false;
        if (startButton == null)
        {
            return;
        }

        startButton.interactable = true;
        startButton.onClick.AddListener(StartGame);
    }

    private void OnDisable()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
        }
    }

    public void StartGame()
    {
        if (isStarting)
        {
            return;
        }

        int setupSceneBuildIndex =
            SceneUtility.GetBuildIndexByScenePath(gameSetupScenePath);
        if (setupSceneBuildIndex < 0)
        {
            Debug.LogError(
                $"Game setup scene is not enabled in Build Settings: {gameSetupScenePath}",
                this);
            return;
        }

        isStarting = true;
        if (startButton != null)
        {
            startButton.interactable = false;
        }

        SceneManager.LoadScene(setupSceneBuildIndex, LoadSceneMode.Single);
    }
}
