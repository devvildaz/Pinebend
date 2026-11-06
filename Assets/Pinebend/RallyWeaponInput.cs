using UnityEngine;
using UnityEngine.InputSystem;

namespace Pinebend
{
    // Both input devices produce a screen-space sight and the same two trigger commands.
    public class RallyWeaponInput : MonoBehaviour
    {
        RallyWeapons weapons;
        TestCar car;
        public Vector2 Sight { get; private set; }
        bool stickAim;

        void Start()
        {
            car = GetComponent<TestCar>();
            weapons = GetComponent<RallyWeapons>();
            Sight = new Vector2(Screen.width, Screen.height) * .5f;
        }

        void Update()
        {
            if(RaceStartSequence.Holding)return;
            if (!Application.isFocused) {car.SetRearView(false);PeerRace.Weapon(Vector3.zero,false,false,false,null);return;}
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            var keyboard = Keyboard.current;
            car.SetRearView((keyboard != null && keyboard.cKey.isPressed) || (pad != null && pad.leftShoulder.isPressed));
            var stick = pad == null ? Vector2.zero : pad.rightStick.ReadValue();
            if (stick.sqrMagnitude > .01f) stickAim = true;
            if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 0 || mouse.leftButton.wasPressedThisFrame))
                stickAim = false;
            if (stickAim) Sight += stick * (Screen.height * .85f * Time.deltaTime);
            else if (mouse != null) Sight = mouse.position.ReadValue();
            Sight = new Vector2(Mathf.Clamp(Sight.x,0,Screen.width), Mathf.Clamp(Sight.y,0,Screen.height));
            bool missile = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (pad != null && pad.leftTrigger.wasPressedThisFrame);
            bool gun = (mouse != null && mouse.rightButton.isPressed) || (pad != null && pad.rightTrigger.isPressed);
            if ((mouse != null && mouse.rightButton.wasPressedThisFrame) || (pad != null && pad.rightTrigger.wasPressedThisFrame))
                weapons.SelectTarget(Sight);
            weapons.Command(Sight, missile, gun);
        }
    }
}

