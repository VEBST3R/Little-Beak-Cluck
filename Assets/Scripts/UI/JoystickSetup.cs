using LittleBeakCluck.Infrastructure;
using LittleBeakCluck.Services;
using UnityEngine;

namespace LittleBeakCluck.UI
{
    public class JoystickSetup : MonoBehaviour
    {
        [SerializeField] private Joystick moveJoystick;
        [SerializeField] private Joystick aimJoystick;

        private void Start()
        {
            var inputService = ServiceLocator.Instance.Get<IInputService>();
            if (moveJoystick != null)
            {
                inputService.RegisterMoveJoystick(moveJoystick);
            }
            if (aimJoystick != null)
            {
                inputService.RegisterAimJoystick(aimJoystick);
            }
        }
    }
}
