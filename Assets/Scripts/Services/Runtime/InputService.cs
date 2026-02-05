using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Reflection;

namespace LittleBeakCluck.Services
{
    [Obfuscation(Feature = "rename", Exclude = true, ApplyToMembers = true)]
    public class InputService : IInputService
    {
        private Joystick _moveJoystick;
        private Joystick _aimJoystick;
        private readonly InputSystem_Actions _actions;
        private Vector2 _cachedMove;
        private Vector2 _cachedAim;
        private bool _attackHeld;
        private bool _actionsEnabled;

        public event Action AttackStarted;
        public event Action AttackPerformed;
        public event Action AttackCanceled;

        public InputService()
        {
            _actions = new InputSystem_Actions();
            BindActionCallbacks();
            EnableActionMap();
        }

        public Vector2 MoveAxis
        {
            get
            {
                if (_moveJoystick != null)
                {
                    Vector2 joystickDirection = _moveJoystick.Direction;
                    if (joystickDirection != Vector2.zero)
                        return joystickDirection;
                }

                return _cachedMove;
            }
        }

        public Vector2 AimAxis
        {
            get
            {
                if (_aimJoystick != null)
                {
                    Vector2 joystickDirection = _aimJoystick.Direction;
                    if (joystickDirection != Vector2.zero)
                        return joystickDirection;
                    // When a dedicated aim joystick exists we ignore fallback input so
                    // accidental screen touches do not rotate the head.
                    return Vector2.zero;
                }

                return _cachedAim;
            }
        }

        public bool AttackHeld => _attackHeld;
        public InputActionAsset ActionsAsset => _actions?.asset;

        public void RegisterMoveJoystick(Joystick joystick)
        {
            _moveJoystick = joystick;
        }

        public void RegisterAimJoystick(Joystick joystick)
        {
            _aimJoystick = joystick;
        }

        private void BindActionCallbacks()
        {
            if (_actions == null)
                return;

            var playerMap = _actions.Player;

            playerMap.Move.performed += OnMove;
            playerMap.Move.canceled += OnMove;

            playerMap.Look.performed += OnLook;
            playerMap.Look.canceled += OnLook;

            playerMap.Attack.started += OnAttackStarted;
            playerMap.Attack.performed += OnAttackPerformed;
            playerMap.Attack.canceled += OnAttackCanceled;
        }

        private void EnableActionMap()
        {
            if (_actionsEnabled)
                return;

            _actions.Player.Enable();
            _actionsEnabled = true;
        }

        private void OnMove(InputAction.CallbackContext context)
        {
            _cachedMove = context.ReadValue<Vector2>();
        }

        private void OnLook(InputAction.CallbackContext context)
        {
            _cachedAim = context.ReadValue<Vector2>();
        }

        private void OnAttackStarted(InputAction.CallbackContext context)
        {
            _attackHeld = true;
            AttackStarted?.Invoke();
        }

        private void OnAttackPerformed(InputAction.CallbackContext context)
        {
            AttackPerformed?.Invoke();
        }

        private void OnAttackCanceled(InputAction.CallbackContext context)
        {
            _attackHeld = false;
            AttackCanceled?.Invoke();
        }
    }
}

