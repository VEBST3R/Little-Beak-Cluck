using System;
using LittleBeakCluck.Infrastructure;
using LittleBeakCluck.Services;
using UnityEngine;

namespace LittleBeakCluck.Player
{
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        public event Action<bool> OnFlipped; // true = flipped left

        [SerializeField] private float speed = 5f;
        [SerializeField] private Transform headTransform;
        [SerializeField] private bool usePhysicsMovement = false;
        [SerializeField] private Rigidbody2D movementBody;
        [SerializeField] private float animatorIdleSpeed = 1f;
        [SerializeField] private float animatorMaxMoveSpeed = 1.4f;
        [Header("Camera Bounds")]
        [SerializeField] private bool constrainToCamera = true;
        [SerializeField] private Camera boundaryCamera;
        [SerializeField] private Vector2 cameraPadding = new Vector2(0.4f, 0.4f);

        private IInputService _inputService;
        private Animator _animator;
        private Vector2 _moveDirection;
        private Vector2 _aimDirection;
        private IPlayerProgressService _progressService;
        [SerializeField] private float headRotateSpeed = 12f; // швидкість плавного нахилу голови
        [SerializeField] private float aimDeadZone = 0.05f;    // мертва зона для джойстика прицілу
        private float _currentZ; // поточний (плавний) Z кут
        private bool _hasInputService;
        private bool _loggedServiceFallback;

        private void Awake()
        {
            ResolveServices();
            _animator = GetComponent<Animator>();
            if (_animator != null)
            {
                _animator.speed = animatorIdleSpeed;
            }

            if (headTransform != null)
            {
                _currentZ = NormalizeAngle(headTransform.localEulerAngles.z);
            }

            if (constrainToCamera && boundaryCamera == null)
            {
                boundaryCamera = Camera.main;
            }

            if (usePhysicsMovement && movementBody == null)
            {
                movementBody = GetComponent<Rigidbody2D>();
                if (movementBody == null)
                {
                    usePhysicsMovement = false;
                    Debug.LogWarning($"[{name}] PlayerController: Rigidbody2D not found, defaulting to transform-based movement.", this);
                }
            }
        }

        private void Update()
        {
            SampleInput();

            float speedMultiplier = ResolveSpeedMultiplier();
            float horizontalSpeed = GetCurrentHorizontalSpeed(speedMultiplier);
            float maxPossibleSpeed = Mathf.Max(0.01f, speed * speedMultiplier);
            float normalizedSpeed = Mathf.Clamp01(horizontalSpeed / maxPossibleSpeed);

            if (_animator != null)
            {
                _animator.SetFloat("Speed", normalizedSpeed);
                float targetAnimSpeed = Mathf.Lerp(animatorIdleSpeed, animatorMaxMoveSpeed, normalizedSpeed);
                _animator.speed = Mathf.Max(animatorIdleSpeed, targetAnimSpeed);
            }

            HandleCharacterFlip(_moveDirection.x);
            ApplyHorizontalMovement(Time.deltaTime, speedMultiplier);
        }

        private void LateUpdate()
        {
            HandleHeadRotation(_aimDirection);
            ClampToCameraBounds();
        }

        private void ResolveServices()
        {
            if (_inputService != null)
            {
                _hasInputService = true;
                return;
            }

            var locator = ServiceLocator.Instance;
            var service = locator.Get<IInputService>();
            if (service == null)
            {
                service = new InputService();
                locator.Register<IInputService>(service);
                if (!_loggedServiceFallback)
                {
                    Debug.LogWarning($"[{name}] PlayerController: IInputService missing; created fallback instance.", this);
                    _loggedServiceFallback = true;
                }
            }

            _inputService = service;
            _hasInputService = _inputService != null;
        }

        private void SampleInput()
        {
            if (!_hasInputService)
            {
                ResolveServices();
                if (!_hasInputService)
                {
                    _moveDirection = Vector2.zero;
                    _aimDirection = Vector2.zero;
                    return;
                }
            }

            _moveDirection = _inputService.MoveAxis;
            _aimDirection = _inputService.AimAxis;
        }

        private void ApplyHorizontalMovement(float deltaTime, float speedMultiplier)
        {
            float horizontalSpeed = _moveDirection.x * speed * speedMultiplier;

            if (usePhysicsMovement && movementBody != null)
            {
                Vector2 velocity = movementBody.linearVelocity;
                velocity.x = horizontalSpeed;
                movementBody.linearVelocity = velocity;
            }
            else
            {
                transform.position += new Vector3(horizontalSpeed * deltaTime, 0f, 0f);
            }
        }

        private void HandleHeadRotation(Vector2 direction)
        {
            if (headTransform == null) return;

            float sqrMag = direction.sqrMagnitude;
            Vector2 dir;
            if (sqrMag <= aimDeadZone * aimDeadZone)
            {
                // повертаємось до нейтрального нахилу (горизонтальна лінія) -90
                dir = Vector2.zero;
            }
            else
            {
                dir = direction.normalized;
            }

            // X flip: логіка відносно напрямку обличчя ТІЛА.
            // Якщо тіло дивиться вправо (rotation.y ~ 0): права півсфера -> X=0, ліва -> X=180
            // Якщо тіло вліво (rotation.y ~ 180): інвертуємо (права -> 180, ліва -> 0)
            bool bodyFacingLeft = Mathf.Abs(transform.rotation.eulerAngles.y - 180f) < 1f;
            float xFlip;
            if (dir == Vector2.zero)
            {
                // залишаємо існуюче
                xFlip = headTransform.localRotation.eulerAngles.x > 90f ? 180f : 0f;
            }
            else
            {
                bool aimLeft = dir.x < 0f;
                if (!bodyFacingLeft)
                {
                    // тіло вправо
                    xFlip = aimLeft ? 180f : 0f;
                }
                else
                {
                    // тіло вліво – інвертуємо
                    xFlip = aimLeft ? 0f : 180f;
                }
            }

            // Цільовий кут по Z: лінійна інтерполяція між -180 (y=-1) і 0 (y=1). y=0 -> -90
            float targetZ = -90f; // нейтраль
            if (dir != Vector2.zero)
            {
                float yClamped = Mathf.Clamp(dir.y, -1f, 1f);
                targetZ = Mathf.Lerp(-180f, 0f, (yClamped + 1f) * 0.5f);
            }

            // Плавна інтерполяція
            _currentZ = Mathf.LerpAngle(_currentZ, targetZ, Time.deltaTime * headRotateSpeed);

            headTransform.localRotation = Quaternion.Euler(xFlip, 0f, _currentZ);
        }

        private void HandleCharacterFlip(float moveX)
        {
            if (moveX > 0.01f)
            {
                if (Mathf.Abs(transform.rotation.eulerAngles.y) > 0.1f)
                {
                    transform.rotation = Quaternion.Euler(0, 0, 0);
                    OnFlipped?.Invoke(false);
                }
            }
            else if (moveX < -0.01f)
            {
                if (Mathf.Abs(transform.rotation.eulerAngles.y - 180f) > 0.1f)
                {
                    transform.rotation = Quaternion.Euler(0, 180, 0);
                    OnFlipped?.Invoke(true);
                }
            }
        }

        private void ClampToCameraBounds()
        {
            if (!constrainToCamera || boundaryCamera == null || !boundaryCamera.orthographic)
                return;

            float halfHeight = boundaryCamera.orthographicSize;
            float halfWidth = halfHeight * boundaryCamera.aspect;
            Vector3 camPos = boundaryCamera.transform.position;

            float minX = camPos.x - halfWidth + cameraPadding.x;
            float maxX = camPos.x + halfWidth - cameraPadding.x;
            float minY = camPos.y - halfHeight + cameraPadding.y;
            float maxY = camPos.y + halfHeight - cameraPadding.y;

            if (minX > maxX)
            {
                float mid = (minX + maxX) * 0.5f;
                minX = maxX = mid;
            }

            if (minY > maxY)
            {
                float mid = (minY + maxY) * 0.5f;
                minY = maxY = mid;
            }

            Vector3 currentPosition;
            if (usePhysicsMovement && movementBody != null)
            {
                Vector2 rbPos = movementBody.position;
                currentPosition = new Vector3(rbPos.x, rbPos.y, transform.position.z);
            }
            else
            {
                currentPosition = transform.position;
            }

            currentPosition.x = Mathf.Clamp(currentPosition.x, minX, maxX);
            currentPosition.y = Mathf.Clamp(currentPosition.y, minY, maxY);

            if (usePhysicsMovement && movementBody != null)
            {
                movementBody.position = new Vector2(currentPosition.x, currentPosition.y);
            }
            else
            {
                transform.position = currentPosition;
            }
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f)
            {
                angle -= 360f;
            }
            return angle;
        }

        private float ResolveSpeedMultiplier()
        {
            if (_progressService == null)
            {
                _progressService = ServiceLocator.Instance.Get<IPlayerProgressService>();
            }

            if (_progressService == null)
                return 1f;

            float multiplier = _progressService.GetStatMultiplier(PlayerUpgradeType.MovementSpeed);
            return multiplier > 0f ? multiplier : 1f;
        }

        private float GetCurrentHorizontalSpeed(float speedMultiplier)
        {
            if (usePhysicsMovement && movementBody != null)
            {
                return Mathf.Abs(movementBody.linearVelocity.x);
            }

            return Mathf.Abs(_moveDirection.x) * speed * speedMultiplier;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && constrainToCamera && boundaryCamera == null)
            {
                var main = Camera.main;
                if (main != null)
                {
                    boundaryCamera = main;
                }
            }
        }
#endif
    }
}
