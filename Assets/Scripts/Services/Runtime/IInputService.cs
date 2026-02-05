using System;
using LittleBeakCluck.Infrastructure;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LittleBeakCluck.Services
{
    public interface IInputService : IGameService
    {
        Vector2 MoveAxis { get; }
        Vector2 AimAxis { get; }
        bool AttackHeld { get; }
        InputActionAsset ActionsAsset { get; }

        event Action AttackStarted;
        event Action AttackPerformed;
        event Action AttackCanceled;

        void RegisterMoveJoystick(Joystick joystick);
        void RegisterAimJoystick(Joystick joystick);
    }
}
