using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace TejedorDePared
{
    /// <summary>
    /// Lectura de teclado, mouse y joystick. Usa el Input System nuevo si está activo
    /// (por defecto en Unity 6) y si no, el Input Manager clásico (sin joystick).
    /// Los nombres de teclas son los del Input System: "A", "Space", "LeftArrow", "Digit1", "F1", "Backquote"...
    /// </summary>
    public static class Controls
    {
#if ENABLE_INPUT_SYSTEM
        static readonly Dictionary<string, Key> keyMap = new Dictionary<string, Key>();
        static Key K(string name)
        {
            Key k;
            if (!keyMap.TryGetValue(name, out k)) { k = (Key)System.Enum.Parse(typeof(Key), name); keyMap[name] = k; }
            return k;
        }
        public static bool Held(string key) { var kb = Keyboard.current; return kb != null && kb[K(key)].isPressed; }
        public static bool Pressed(string key) { var kb = Keyboard.current; return kb != null && kb[K(key)].wasPressedThisFrame; }

        public static Vector2 MouseScreen { get { var m = Mouse.current; return m != null ? m.position.ReadValue() : Vector2.zero; } }
        public static bool MouseHeld(int b) { var m = Mouse.current; return m != null && Btn(m, b).isPressed; }
        public static bool MousePressed(int b) { var m = Mouse.current; return m != null && Btn(m, b).wasPressedThisFrame; }
        static ButtonControl Btn(Mouse m, int b) { return b == 0 ? m.leftButton : b == 1 ? m.rightButton : m.middleButton; }

        public static bool HasPad { get { return Gamepad.current != null; } }
        public static Vector2 LeftStick { get { var g = Gamepad.current; return g != null ? g.leftStick.ReadValue() : Vector2.zero; } }
        public static Vector2 RightStick { get { var g = Gamepad.current; return g != null ? g.rightStick.ReadValue() : Vector2.zero; } }
        public static bool PadHeld(string b) { var c = PadBtn(b); return c != null && c.isPressed; }
        public static bool PadPressed(string b) { var c = PadBtn(b); return c != null && c.wasPressedThisFrame; }
        static ButtonControl PadBtn(string b)
        {
            var g = Gamepad.current;
            if (g == null) return null;
            switch (b)
            {
                case "south": return g.buttonSouth;
                case "east": return g.buttonEast;
                case "west": return g.buttonWest;
                case "north": return g.buttonNorth;
                case "rb": return g.rightShoulder;
                case "rt": return g.rightTrigger;
                case "start": return g.startButton;
                case "select": return g.selectButton;
                case "up": return g.dpad.up;
                case "down": return g.dpad.down;
                case "left": return g.dpad.left;
                case "right": return g.dpad.right;
            }
            return null;
        }
#else
        static readonly Dictionary<string, KeyCode> keyMap = new Dictionary<string, KeyCode>();
        static KeyCode K(string name)
        {
            KeyCode k;
            if (keyMap.TryGetValue(name, out k)) return k;
            string n = name;
            if (n.StartsWith("Digit")) n = "Alpha" + n.Substring(5);
            else if (n == "LeftCtrl") n = "LeftControl";
            else if (n == "Backquote") n = "BackQuote";
            else if (n == "Enter") n = "Return";
            k = (KeyCode)System.Enum.Parse(typeof(KeyCode), n);
            keyMap[name] = k;
            return k;
        }
        public static bool Held(string key) { return Input.GetKey(K(key)); }
        public static bool Pressed(string key) { return Input.GetKeyDown(K(key)); }
        public static Vector2 MouseScreen { get { return Input.mousePosition; } }
        public static bool MouseHeld(int b) { return Input.GetMouseButton(b); }
        public static bool MousePressed(int b) { return Input.GetMouseButtonDown(b); }
        public static bool HasPad { get { return false; } }
        public static Vector2 LeftStick { get { return Vector2.zero; } }
        public static Vector2 RightStick { get { return Vector2.zero; } }
        public static bool PadHeld(string b) { return false; }
        public static bool PadPressed(string b) { return false; }
#endif

        public static bool AnyHeld(params string[] keys) { foreach (var k in keys) if (Held(k)) return true; return false; }
        public static bool AnyPressed(params string[] keys) { foreach (var k in keys) if (Pressed(k)) return true; return false; }
    }
}
