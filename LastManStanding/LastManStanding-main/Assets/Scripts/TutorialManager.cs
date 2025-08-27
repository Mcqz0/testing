using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    [System.Serializable]
    public class TutorialStep
    {
        public string message;
        public KeyCode requiredKey;
        public TutorialType type;
        public float timeoutDuration = 10f; // Auto-advance after this time if no input
    }

    public enum TutorialType
    {
        Movement,
        Aiming,
        Shooting,
        Pickup,
        Complete
    }

    [Header("Tutorial Settings")]
    [SerializeField] private bool _skipTutorial = false; // For testing
    [SerializeField] private TutorialStep[] _tutorialSteps;

    [Header("UI References")]
    [SerializeField] private GameObject _tutorialPanel;
    [SerializeField] private TextMeshProUGUI _tutorialText;
    [SerializeField] private GameObject _skipButton;

    [Header("Game References")]
    [SerializeField] private Player _player;
    [SerializeField] private GameTimer _gameTimer;

    private int _currentStepIndex = 0;
    private bool _tutorialActive = false;
    private bool _waitingForInput = false;
    private Coroutine _timeoutCoroutine;

    // Events
    public System.Action OnTutorialComplete;

    private void Start()
    {
        InitializeTutorialSteps();

        if (_skipTutorial)
        {
            CompleteTutorial();
            return;
        }

        StartTutorial();
    }

    private void InitializeTutorialSteps()
    {
        _tutorialSteps = new TutorialStep[]
        {
            new TutorialStep
            {
                message = "Welcome to Last Man Standing!\n\nUse WASD keys to move around\nTry moving now...",
                requiredKey = KeyCode.W, // Any movement key will work
                type = TutorialType.Movement,
                timeoutDuration = 8f
            },
            new TutorialStep
            {
                message = "Move your mouse to aim\nNotice how your weapon follows the cursor",
                requiredKey = KeyCode.None, // Mouse movement (no key required)
                type = TutorialType.Aiming,
                timeoutDuration = 5f
            },
            new TutorialStep
            {
                message = "Click LEFT MOUSE BUTTON to shoot\nTry firing your weapon now!",
                requiredKey = KeyCode.Mouse0,
                type = TutorialType.Shooting,
                timeoutDuration = 8f
            },
            new TutorialStep
            {
                message = "Press C to pick up items like vaccines and weapons\nLook for items on the ground and press C when nearby",
                requiredKey = KeyCode.C,
                type = TutorialType.Pickup,
                timeoutDuration = 10f
            },
            new TutorialStep
            {
                message = "Tutorial Complete!\nSurvive for 10 minutes and reach the green exit zone!\nPress ESC anytime to pause\n\nGood luck!",
                requiredKey = KeyCode.None,
                type = TutorialType.Complete,
                timeoutDuration = 3f
            }
        };
    }

    private void Update()
    {
        if (!_tutorialActive || !_waitingForInput) return;

        // Check for the required input for current step
        TutorialStep currentStep = _tutorialSteps[_currentStepIndex];

        bool inputDetected = false;

        switch (currentStep.type)
        {
            case TutorialType.Movement:
                inputDetected = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
                               Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) ||
                               Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                               Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow);
                break;

            case TutorialType.Aiming:
                // Check if mouse moved significantly
                inputDetected = Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0;
                break;

            case TutorialType.Shooting:
                inputDetected = Input.GetMouseButtonDown(0);
                break;

            case TutorialType.Pickup:
                inputDetected = Input.GetKeyDown(KeyCode.C);
                break;

            default:
                inputDetected = currentStep.requiredKey != KeyCode.None && Input.GetKeyDown(currentStep.requiredKey);
                break;
        }

        if (inputDetected)
        {
            NextStep();
        }
    }

    public void StartTutorial()
    {
        _tutorialActive = true;
        _currentStepIndex = 0;

        // Pause the game timer during tutorial
        if (_gameTimer != null)
            _gameTimer.enabled = false;

        // Show tutorial UI
        if (_tutorialPanel != null)
            _tutorialPanel.SetActive(true);

        // Disable player initially for movement tutorial
        if (_player != null)
            _player.enabled = false;

        ShowCurrentStep();
    }

    private void ShowCurrentStep()
    {
        if (_currentStepIndex >= _tutorialSteps.Length)
        {
            CompleteTutorial();
            return;
        }

        TutorialStep currentStep = _tutorialSteps[_currentStepIndex];

        // Update UI text
        if (_tutorialText != null)
            _tutorialText.text = currentStep.message;

        // Enable player for movement step
        if (currentStep.type == TutorialType.Movement && _player != null)
            _player.enabled = true;

        _waitingForInput = true;

        // Start timeout coroutine
        if (_timeoutCoroutine != null)
            StopCoroutine(_timeoutCoroutine);

        _timeoutCoroutine = StartCoroutine(TimeoutStep(currentStep.timeoutDuration));

        Debug.Log($"Tutorial Step {_currentStepIndex + 1}: {currentStep.type}");
    }

    private IEnumerator TimeoutStep(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (_waitingForInput) // Still waiting for input
        {
            NextStep(); // Auto-advance
        }
    }

    private void NextStep()
    {
        _waitingForInput = false;

        if (_timeoutCoroutine != null)
        {
            StopCoroutine(_timeoutCoroutine);
            _timeoutCoroutine = null;
        }

        _currentStepIndex++;

        if (_currentStepIndex >= _tutorialSteps.Length)
        {
            CompleteTutorial();
        }
        else
        {
            // Brief pause between steps
            StartCoroutine(ShowNextStepAfterDelay(1f));
        }
    }

    private IEnumerator ShowNextStepAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowCurrentStep();
    }

    public void CompleteTutorial()
    {
        _tutorialActive = false;
        _waitingForInput = false;

        // Hide tutorial UI
        if (_tutorialPanel != null)
            _tutorialPanel.SetActive(false);

        // Enable player if not already enabled
        if (_player != null)
            _player.enabled = true;

        // Resume game timer
        if (_gameTimer != null)
            _gameTimer.enabled = true;

        // Invoke completion event
        OnTutorialComplete?.Invoke();

        Debug.Log("Tutorial completed!");
    }

    public void SkipTutorial()
    {
        if (_tutorialActive)
        {
            CompleteTutorial();
        }
    }

    // Public method to check if tutorial is running (for other systems)
    public bool IsTutorialActive()
    {
        return _tutorialActive;
    }
}