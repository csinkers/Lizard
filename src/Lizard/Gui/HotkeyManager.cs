using Veldrid;

namespace Lizard.Gui;

public class HotkeyManager
{
    readonly HashSet<Key> _pressedKeys = new();
    readonly Dictionary<KeyBinding, Action> _bindings = new();

    public bool IsAltPressed => _pressedKeys.Contains(Key.AltLeft) || _pressedKeys.Contains(Key.AltRight);
    public bool IsCtrlPressed  => _pressedKeys.Contains(Key.ControlLeft) || _pressedKeys.Contains(Key.ControlRight);
    public bool IsShiftPressed  => _pressedKeys.Contains(Key.ShiftLeft) || _pressedKeys.Contains(Key.ShiftRight);

    public void HandleInput(InputSnapshot input)
    {
        foreach (var keyEvent in input.KeyEvents)
        {
            if (!keyEvent.Down)
            {
                _pressedKeys.Remove(keyEvent.Key);
                continue;
            }

            _pressedKeys.Add(keyEvent.Key);

            if (IsModifier(keyEvent.Key))
                continue;

            var binding = new KeyBinding(keyEvent.Key, keyEvent.Modifiers);
            if (_bindings.TryGetValue(binding, out var action))
                action();
        }
    }

    static bool IsModifier(Key key) =>
        key switch
        {
            Key.LControl => true, Key.RControl => true,
            Key.LShift => true,   Key.RShift => true,
            Key.LAlt => true,     Key.RAlt => true,
            Key.LWin => true,     Key.RWin => true,
            _ => false
        };

    public void Add(KeyBinding binding, Action action)
    {
        if (!_bindings.TryAdd(binding, action))
            throw new InvalidOperationException($"Tried to register a hotkey for {binding}, but another action is already using that hotkey");
    }

    public void Remove(KeyBinding binding) => _bindings.Remove(binding);
}