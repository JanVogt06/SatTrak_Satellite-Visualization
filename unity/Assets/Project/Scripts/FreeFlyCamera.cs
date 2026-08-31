using Satellites;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class FreeFlyCamera : MonoBehaviour
{
    #region UI

    [Space]
    private Vector3 _movementInput = Vector3.zero;

    [SerializeField]
    [Tooltip("The script is currently active")]
    private bool _active = true;

    [Space]

    [SerializeField]
    [Tooltip("Camera rotation by mouse movement is active")]
    private bool _enableRotation = true;

    [SerializeField]
    [Tooltip("Sensitivity of mouse rotation")]
    private float _mouseSense = 1.8f;

    [Space]

    [SerializeField]
    [Tooltip("Camera zooming in/out by 'Mouse Scroll Wheel' is active")]
    private bool _enableTranslation = true;

    [SerializeField]
    [Tooltip("Velocity of camera zooming in/out")]
    private float _translationSpeed = 55f;

    [Space]

    [SerializeField]
    [Tooltip("Camera movement by 'W','A','S','D','Q','E' keys is active")]
    private bool _enableMovement = true;

    [SerializeField]
    [Tooltip("Camera movement speed")]
    private float _movementSpeed = 100f;

    [SerializeField]
    [Tooltip("Speed of the quick camera movement when holding the 'Left Shift' key")]
    private float _boostedSpeed = 200f;

    [SerializeField]
    [Tooltip("Boost speed")]
    private KeyCode _boostSpeed = KeyCode.LeftShift;

    [SerializeField]
    [Tooltip("Move up")]
    private KeyCode _moveUp = KeyCode.E;

    [SerializeField]
    [Tooltip("Move down")]
    private KeyCode _moveDown = KeyCode.Q;

    [Space]

    [SerializeField]
    [Tooltip("Acceleration at camera movement is active")]
    private bool _enableSpeedAcceleration = true;

    [SerializeField]
    [Tooltip("Rate which is applied during camera movement")]
    private float _speedAccelerationFactor = 1.5f;

    [Space]

    [SerializeField]
    [Tooltip("This keypress will move the camera to initialization position")]
    private KeyCode _initPositonButton = KeyCode.R;

    #endregion UI

    private CursorLockMode _wantedMode;

    private float _currentIncrease = 1;
    private float _currentIncreaseMem = 0;

    private Vector3 _initPosition;
    private Vector3 _initRotation;

    [SerializeField] private Image crosshairImage;

    [SerializeField] private SatelliteLabelUI satelliteLabelUI;
    [SerializeField] private float maxSelectionAngle = 0.2f;
    [SerializeField] private float maxDistance = 1000000f;

    public Button cameraButton;
    public Button inspectorButton;
    public Image cameraIcon;
    public Image inspectorIcon;
    public Sprite cameraNormalSprite;
    public Sprite cameraUnavailableSprite;
    public Sprite inspectorNormalSprite;
    public Sprite inspectorUnavailableSprite;

    public Color activeColor = new Color(1f, 0.7058824f, 0f, 1f);
    public Color inactiveColor = Color.white;

    public bool cameraModeAllowed = true;
    public bool inspectorModeAllowed = true;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_boostedSpeed < _movementSpeed)
            _boostedSpeed = _movementSpeed;
    }
#endif

    private void Start()
    {
        if (crosshairImage != null)
        {
            if (CrosshairSettings.selectedSprite != null)
            {
                crosshairImage.sprite = CrosshairSettings.selectedSprite;
                crosshairImage.color = CrosshairSettings.selectedColor;
            }
        }

        crosshairImage.gameObject.SetActive(false);

        _initPosition = transform.position;
        _initRotation = transform.eulerAngles;

        _cursorLocked = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        cameraButton.onClick.AddListener(() => TrySetMode(true));
        inspectorButton.onClick.AddListener(() => TrySetMode(false));
        UpdateModeUI();
    }

    private bool _cursorLocked = true;

    private void SetCursorState()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TrySetMode(!_cursorLocked);
            }
        }
    }

    public void TrySetMode(bool cameraWanted)
    {
        if (cameraWanted && !cameraModeAllowed) return;
        if (!cameraWanted && !inspectorModeAllowed) return;

        _cursorLocked = cameraWanted;
        Cursor.lockState = _cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !_cursorLocked;
        crosshairImage.gameObject.SetActive(_cursorLocked);
        UpdateModeUI();
    }

    public void ForceInspectorMode() => TrySetMode(false);

    public void UpdateModeUI()
    {
        cameraIcon.sprite = cameraModeAllowed ? cameraNormalSprite : cameraUnavailableSprite;
        inspectorIcon.sprite = inspectorModeAllowed ? inspectorNormalSprite : inspectorUnavailableSprite;

        cameraButton.interactable = cameraModeAllowed;
        inspectorButton.interactable = inspectorModeAllowed;

        cameraIcon.color = _cursorLocked ? activeColor : inactiveColor;
        inspectorIcon.color = !_cursorLocked ? activeColor : inactiveColor;
    }

    private Satellite FindNearestLookingAtSatellite()
    {
        var allSats = SatelliteManager.Instance.GetAllSatellites();
        Vector3 camPos = Camera.main.transform.position;
        Vector3 camFwd = Camera.main.transform.forward;

        Satellite closest = null;
        float closestAngle = maxSelectionAngle;

        foreach (var sat in allSats)
        {
            if (!sat.gameObject.activeInHierarchy) continue;

            Vector3 dirToSat = (sat.transform.position - camPos);
            float distance = dirToSat.magnitude;
            if (distance > maxDistance) continue;

            float angle = Vector3.Angle(camFwd, dirToSat.normalized);
            if (angle < closestAngle)
            {
                closestAngle = angle;
                closest = sat;
            }
        }

        return closest;
    }

    private void CalculateCurrentIncrease(bool moving)
    {
        _currentIncrease = Time.deltaTime;

        if (!_enableSpeedAcceleration || _enableSpeedAcceleration && !moving)
        {
            _currentIncreaseMem = 0;
            return;
        }

        _currentIncreaseMem += Time.deltaTime * (_speedAccelerationFactor - 1);
        _currentIncrease = Time.deltaTime + Mathf.Pow(_currentIncreaseMem, 3) * Time.deltaTime;
    }

    private void Update()
    {
        if (!_active)
            return;

        SetCursorState();

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        _movementInput = Vector3.zero;

        if (_enableMovement)
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) _movementInput.z += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) _movementInput.z -= 1;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) _movementInput.x -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) _movementInput.x += 1;
            if (Input.GetKey(_moveUp)) _movementInput.y += 1;
            if (Input.GetKey(_moveDown)) _movementInput.y -= 1;
        }

        if (_enableTranslation)
        {
            transform.Translate(Vector3.forward * Input.mouseScrollDelta.y * Time.deltaTime * _translationSpeed, Space.Self);
        }

        if (_enableRotation)
        {
            float mouseX = Input.GetAxis("Mouse X") * _mouseSense;
            float mouseY = Input.GetAxis("Mouse Y") * _mouseSense;

            _initRotation.y += mouseX;
            _initRotation.x -= mouseY;
            _initRotation.x = Mathf.Clamp(_initRotation.x, -90f, 90f);

            transform.rotation = Quaternion.Euler(_initRotation.x, _initRotation.y, 0f);
        }

        if (Input.GetKeyDown(_initPositonButton))
        {
            transform.position = _initPosition;
            transform.eulerAngles = _initRotation;
        }

        if (_cursorLocked)
        {
            Satellite target = FindNearestLookingAtSatellite();

            if (target != null)
            {
                if (satelliteLabelUI.target != target.transform)
                {
                    satelliteLabelUI.SetTarget(target.transform, target.name);
                }
            }
            else
            {
                satelliteLabelUI.Hide();
            }
        }

    }

    private void FixedUpdate()
    {
        if (!_active)
            return;

        if (_movementInput != Vector3.zero)
        {
            float currentSpeed = Input.GetKey(_boostSpeed) ? _boostedSpeed : _movementSpeed;
            CalculateCurrentIncrease(true);

            transform.Translate(_movementInput.normalized * currentSpeed * _currentIncrease, Space.Self);
        }
        else
        {
            CalculateCurrentIncrease(false);
        }
    }

    public void SyncInitTransform()
    {
        _initPosition = transform.position;
        _initRotation = transform.eulerAngles;
    }
}
