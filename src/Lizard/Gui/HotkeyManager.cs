using Veldrid;

namespace Lizard.Gui;

public class HotkeyManager
{
    readonly Dictionary<KeyBinding, (bool IsGlobal, Action Action)> _bindings = new();

    public void HandleInput(InputSnapshot input, bool imguiCaptured)
    {
        foreach (var keyEvent in input.KeyEvents)
        {
            if (!keyEvent.Down)
                continue;
            if (IsModifier(keyEvent.Key))
                continue;

            var binding = new KeyBinding(keyEvent.Key, keyEvent.Modifiers);
            if (_bindings.TryGetValue(binding, out var info) && (info.IsGlobal || !imguiCaptured))
                info.Action();
        }
    }

    static bool IsModifier(Key key) =>
        key switch
        {
            Key.LControl => true,
            Key.RControl => true,
            Key.LShift => true,
            Key.RShift => true,
            Key.LAlt => true,
            Key.RAlt => true,
            Key.LWin => true,
            Key.RWin => true,
            _ => false
        };

    public void Add(KeyBinding binding, Action action, bool isGlobal)
    {
        if (!_bindings.TryAdd(binding, (isGlobal, action)))
            throw new InvalidOperationException(
                $"Tried to register a hotkey for {binding}, but another action is already using that hotkey"
            );
    }

    public void Remove(KeyBinding binding) => _bindings.Remove(binding);
}
