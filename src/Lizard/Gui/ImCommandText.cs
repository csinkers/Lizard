using System.Numerics;
using System.Text;
using ImGuiNET;

namespace Lizard.Gui;

/// <summary>
/// An ImGui text box with popup suggestions
/// Based on https://github.com/tooll3/t3/blob/e714456316591077ef979b5d5e2011c24550fcfe/Editor/Gui/Styling/InputWithTypeAheadSearch.cs#L10 
/// MIT License - Copyright 2010 Thomas Mann, Daniel Szymanski, Andreas Rose, Framefield GmbH
/// </summary>
public class ImCommandText
{
    const int MaxResults = 10;
    const int CommandHistoryLimit = 100;
    public delegate void GetCompletionsFunc(string text, List<string> results, int maxResults);

    readonly GetCompletionsFunc _getCompletionsFunc;
    readonly List<string> _lastResults = new();
    readonly IndexQueue<string> _commandHistory = new();
    readonly byte[] _buffer;
    int _commandHistoryPosition;
    int _selectedResultIndex;
    bool _keepNavEnableKeyboard;
    uint _activeInputId;
    string _cachedText = "";

    public ImCommandText(int maxLength, GetCompletionsFunc getCompletionsFunc)
    {
        _getCompletionsFunc = getCompletionsFunc ?? throw new ArgumentNullException(nameof(getCompletionsFunc));
        _buffer = new byte[maxLength];
    }

    public ImCommandText(int maxLength, string initialText, GetCompletionsFunc getCompletionsFunc)
    {
        _getCompletionsFunc = getCompletionsFunc ?? throw new ArgumentNullException(nameof(getCompletionsFunc));
        _buffer = new byte[maxLength];
        Encoding.ASCII.GetBytes(initialText.AsSpan(), _buffer.AsSpan());
        _buffer[initialText.Length] = 0;
    }

    public string Text
    {
        get
        {
            var text = Encoding.ASCII.GetString(_buffer);
            int index = text.IndexOf((char)0, StringComparison.Ordinal);
            return text[..index];
        }
        set
        {
            if (value.Length > _buffer.Length)
                value = value[..(_buffer.Length - 1)];

            Encoding.ASCII.GetBytes(value.AsSpan(), _buffer.AsSpan());
            _buffer[value.Length] = 0;
        }
    }

    public unsafe bool Draw(string id) // Returns true if a command was entered
    {
        var inputId = ImGui.GetID(id);
        var isSearchResultWindowOpen = inputId == _activeInputId;

        if (isSearchResultWindowOpen)
        {
            bool tab = ImGui.IsKeyPressed(ImGuiKey.Tab, true);
            bool shift = ImGui.IsKeyDown(ImGuiKey.ModShift);
            if (!shift && tab)
            {
                if (_lastResults.Count > 0)
                {
                    _selectedResultIndex++;
                    _selectedResultIndex %= _lastResults.Count;
                    _cachedText = _lastResults[_selectedResultIndex];
                    Text = _cachedText;
                }
            }
            else if (shift && tab)
            {
                if (_lastResults.Count > 0)
                {
                    _selectedResultIndex--;
                    if (_selectedResultIndex < 0)
                        _selectedResultIndex = _lastResults.Count - 1;

                    _cachedText = _lastResults[_selectedResultIndex];
                    Text = _cachedText;
                }
            }
        }

        var changed = ImGui.InputText(id, _buffer, (uint)_buffer.Length, ImGuiInputTextFlags.CallbackHistory | ImGuiInputTextFlags.CallbackCompletion, CommandCallback);
        if (changed)
            _cachedText = Text;

        var complete = ImGui.IsKeyPressed(ImGuiKey.Enter);

        if (ImGui.IsItemActivated())
        {
            _lastResults.Clear();
            _selectedResultIndex = -1;
            DisableImGuiKeyboardNavigation();
        }

        // We defer exit to get clicks on opened popup list
        var lostFocus =  ImGui.IsItemDeactivated() || ImGui.IsKeyDown(ImGuiKey.Escape);

        if ((ImGui.IsItemActive() || isSearchResultWindowOpen) && _cachedText.Length > 2)
        {
            _activeInputId = inputId;

            if (changed)
                _getCompletionsFunc(_cachedText, _lastResults, MaxResults);

            if (_lastResults.Count > 0)
            {
                ImGui.SetNextWindowPos(new Vector2(ImGui.GetItemRectMin().X, ImGui.GetItemRectMin().Y - 200));
                ImGui.SetNextWindowSize(new Vector2(ImGui.GetItemRectSize().X, 200));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(7, 7));

                if (ImGui.Begin("##typeAheadSearchPopup", ref isSearchResultWindowOpen,
                        ImGuiWindowFlags.NoTitleBar
                      | ImGuiWindowFlags.NoMove
                      | ImGuiWindowFlags.NoResize
                      | ImGuiWindowFlags.Tooltip
                      | ImGuiWindowFlags.NoFocusOnAppearing
                      | ImGuiWindowFlags.ChildWindow))
                {
                    int index = 0;
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.6f, 0.6f, 0.6f, 1.0f));
                    foreach (var word in _lastResults)
                    {
                        var isSelected = index == _selectedResultIndex;
                        ImGui.Selectable(word, isSelected);
                        index++;
                    }
                    ImGui.PopStyleColor();
                }

                ImGui.End();
                ImGui.PopStyleVar();
            }
        }

        if (lostFocus)
        {
            RestoreImGuiKeyboardNavigation();
            _activeInputId = 0;
        }

        if (complete)
        {
            if (_commandHistory.Count >= CommandHistoryLimit)
                _commandHistory.Dequeue();
            _commandHistory.Enqueue(_cachedText);
        }

        return complete;
    }

    void DisableImGuiKeyboardNavigation()
    {
        // Keep navigation setting to restore after window gets closed
        _keepNavEnableKeyboard = (ImGui.GetIO().ConfigFlags & ImGuiConfigFlags.NavEnableKeyboard) != ImGuiConfigFlags.None;
        ImGui.GetIO().ConfigFlags &= ~ImGuiConfigFlags.NavEnableKeyboard;
    }

    void RestoreImGuiKeyboardNavigation()
    {
        if (_keepNavEnableKeyboard)
            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
    }

    unsafe int CommandCallback(ImGuiInputTextCallbackData* dataPtr)
    {
        var data = new ImGuiInputTextCallbackDataPtr(dataPtr);
        switch (data.EventFlag)
        {
            case ImGuiInputTextFlags.CallbackCompletion:
                data.DeleteChars(0, data.BufTextLen);
                data.InsertChars(0, Text);
                return 0;

            case ImGuiInputTextFlags.CallbackHistory:
                if (_commandHistory.Count == 0)
                    break;

                if (data.EventKey == ImGuiKey.UpArrow && _commandHistoryPosition < _commandHistory.Count)
                {
                    _commandHistoryPosition++;
                    data.DeleteChars(0, data.BufTextLen);
                    data.InsertChars(0, _commandHistory[^_commandHistoryPosition] ?? "");
                }
                else if (data.EventKey == ImGuiKey.DownArrow && _commandHistoryPosition > 1)
                {
                    _commandHistoryPosition--;
                    data.DeleteChars(0, data.BufTextLen);
                    data.InsertChars(0, _commandHistory[^_commandHistoryPosition] ?? "");
                }
                break;
        }

        return 0;
    }
}

