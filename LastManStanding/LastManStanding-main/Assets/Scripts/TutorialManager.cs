using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

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
    [SerializeField] private Button _skipButtonComponent;

    [Header("Game References")]
    [SerializeField] private Player _player;
    [SerializeField] private GameTimer _gameTimer;

    [Header("Tutorial Movement Limits")]
    [SerializeField] private float _tutorialMoveDistance = 0.5f; // How far the player moves per key press
    [SerializeField] private float _tutorialMoveTime = 0.3f; // Duration of each movement

    private int _currentStepIndex = 0;
    private bool _tutorialActive = false;
    private bool _waitingForInput = false;
    private Coroutine _timeoutCoroutine;
    private Vector3 _playerStartPosition;
    private bool _hasMovedThisStep = false;
    private bool _hasFiredThisStep = false;
    private Coroutine _limitedMoveCoroutine;
    private bool _isMovementStep = false;  // Track if we're in movement step
    private bool _isShootingStep = false;  // Track if we're in shooting step

    // Events
    public System.Action OnTutorialComplete;

    private void Start()
    {
        InitializeTutorialSteps();
        SetupSkipButton();

        // Find references if not set in inspector
        if (_player == null)
            _player = FindObjectOfType<Player>();
        if (_gameTimer == null)
            _gameTimer = FindObjectOfType<GameTimer>();

        if (_skipTutorial)
        {
            CompleteTutorial();
            return;
        }

        StartTutorial();
    }

    private void SetupSkipButton()
    {
        // Position skip button at top-middle of the screen
        if (_skipButton != null)
        {
            RectTransform skipRect = _skipButton.GetComponent<RectTransform>();
            if (skipRect != null)
            {
                // Set anchor to top-center
                skipRect.anchorMin = new Vector2(0.5f, 1f);
                skipRect.anchorMax = new Vector2(0.5f, 1f);

                // Position it slightly below the top edge
                skipRect.anchoredPosition = new Vector2(0, -50f); // 50 pixels from top

                // Set a reasonable size for the button
                skipRect.sizeDelta = new Vector2(150f, 40f);
            }
        }

        // Connect skip button click event
        if (_skipButtonComponent == null && _skipButton != null)
        {
            _skipButtonComponent = _skipButton.GetComponent<Button>();
        }

        if (_skipButtonComponent != null)
        {
            _skipButtonComponent.onClick.RemoveAllListeners();
            _skipButtonComponent.onClick.AddListener(SkipTutorial);
        }
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
                message = "Great! Move your mouse to aim\nNotice your weapon follows the cursor",
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
                message = "Press C to pick up items\nLook for items on the ground and press C when nearby",
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
                // Check for movement keys and perform limited movement
                if (!_hasMovedThisStep)
                {
                    if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                    {
                        PerformLimitedMovement(Vector3.up);
                        inputDetected = true;
                    }
                    else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                    {
                        PerformLimitedMovement(Vector3.down);
                        inputDetected = true;
                    }
                    else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                    {
                        PerformLimitedMovement(Vector3.left);
                        inputDetected = true;
                    }
                    else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                    {
                        PerformLimitedMovement(Vector3.right);
                        inputDetected = true;
                    }
                }
                break;

            case TutorialType.Aiming:
                // Check if mouse moved significantly
                inputDetected = Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0;
                break;

            case TutorialType.Shooting:
                // Fire a single shot when clicked
                if (!_hasFiredThisStep && Input.GetMouseButtonDown(0))
                {
                    FireSingleTutorialShot();
                    inputDetected = true;
                }
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
            // Add a small delay before moving to next step for better feedback
            StartCoroutine(DelayedNextStep(0.5f));
        }
    }

    private void PerformLimitedMovement(Vector3 direction)
    {
        if (_player == null || _hasMovedThisStep) return;

        _hasMovedThisStep = true;

        if (_limitedMoveCoroutine != null)
            StopCoroutine(_limitedMoveCoroutine);

        _limitedMoveCoroutine = StartCoroutine(MovePlayerLimited(direction));
    }

    private IEnumerator MovePlayerLimited(Vector3 direction)
    {
        Vector3 startPos = _player.transform.position;
        Vector3 endPos = startPos + (direction * _tutorialMoveDistance);
        float elapsedTime = 0f;

        // Smoothly move the player a short distance
        while (elapsedTime < _tutorialMoveTime)
        {
            _player.transform.position = Vector3.Lerp(startPos, endPos, elapsedTime / _tutorialMoveTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        _player.transform.position = endPos;
        Debug.Log("Tutorial: Player moved " + direction);
    }

    private void FireSingleTutorialShot()
    {
        if (_player == null || _hasFiredThisStep) return;

        _hasFiredThisStep = true;

        // Get the Player component and call the fire demo method
        Player playerScript = _player.GetComponent<Player>();
        if (playerScript != null)
        {
            playerScript.FireTutorialShot();
            Debug.Log("Tutorial: Single shot fired!");
        }
    }

    private IEnumerator DelayedNextStep(float delay)
    {
        _waitingForInput = false; // Prevent additional input
        yield return new WaitForSeconds(delay);
        NextStep();
    }

    public void StartTutorial()
    {
        _tutorialActive = true;
        _currentStepIndex = 0;

        // Store player start position
        if (_player != null)
        {
            _playerStartPosition = _player.transform.position;
        }

        // Pause the game timer during tutorial
        if (_gameTimer != null)
            _gameTimer.enabled = false;

        // Show tutorial UI
        if (_tutorialPanel != null)
            _tutorialPanel.SetActive(true);

        // Show skip button
        if (_skipButton != null)
            _skipButton.SetActive(true);

        // Disable player controls initially
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

        // Reset step-specific flags
        _hasMovedThisStep = false;
        _hasFiredThisStep = false;
        _isMovementStep = false;
        _isShootingStep = false;

        // Update UI text
        if (_tutorialText != null)
            _tutorialText.text = currentStep.message;

        // Set flags for specific tutorial steps
        if (_player != null)
        {
            switch (currentStep.type)
            {
                case TutorialType.Movement:
                    _isMovementStep = true;
                    // Restrict player movement
                    _player.enabled = false;
                    break;
                case TutorialType.Aiming:
                    // Enable player for mouse aiming
                    _player.enabled = true;
                    break;
                case TutorialType.Shooting:
                    _isShootingStep = true;
                    // Enable player for shooting
                    _player.enabled = true;
                    break;
                case TutorialType.Pickup:
                    // Enable player for pickup
                    _player.enabled = true;
                    break;
                default:
                    _player.enabled = false;
                    break;
            }
        }

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

        if (_waitingForInput)
        {
            NextStep();
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

        if (_limitedMoveCoroutine != null)
        {
            StopCoroutine(_limitedMoveCoroutine);
            _limitedMoveCoroutine = null;
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

        // Stop any running coroutines
        if (_timeoutCoroutine != null)
        {
            StopCoroutine(_timeoutCoroutine);
            _timeoutCoroutine = null;
        }

        if (_limitedMoveCoroutine != null)
        {
            StopCoroutine(_limitedMoveCoroutine);
            _limitedMoveCoroutine = null;
        }

        // Hide tutorial UI
        if (_tutorialPanel != null)
            _tutorialPanel.SetActive(false);

        // Hide skip button
        if (_skipButton != null)
            _skipButton.SetActive(false);

        // Enable player fully
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
            Debug.Log("Tutorial skipped by user");
            CompleteTutorial();
        }
    }

    // Public methods to check tutorial state for specific steps
    public bool IsTutorialActive()
    {
        return _tutorialActive;
    }

    public bool IsInMovementTutorial()
    {
        return _tutorialActive && _isMovementStep;
    }

    public bool IsInShootingTutorial()
    {
        return _tutorialActive && _isShootingStep;
    }

    public bool ShouldBlockPlayerInput()
    {
        // Only block player input during movement tutorial
        return _tutorialActive && _isMovementStep;
    }

    private void OnDestroy()
    {
        // Clean up button listener
        if (_skipButtonComponent != null)
        {
            _skipButtonComponent.onClick.RemoveListener(SkipTutorial);
        }
    }
}