﻿using Microsoft.Xna.Framework.Input;
using Murder.Assets.Input;
using Murder.Core.Geometry;
using Murder.Diagnostics;
using Murder.Save;
using Murder.Utilities;
using System.Collections.Immutable;
using System.Numerics;
using System.Text;

namespace Murder.Core.Input;

public class PlayerInput
{
    // Debug only
    public int[] AllButtons => [.. _buttons.Keys];

    // Debug only
    public int[] AllAxis => [.. _axis.Keys];

    private readonly Dictionary<int, VirtualButton> _buttons = [];
    private readonly Dictionary<int, VirtualAxis> _axis = [];

    private KeyboardState _rawPreviousKeyboardState;
    private KeyboardState _rawCurrentKeyboardState;

    private KeyboardState _previousKeyboardState;
    private KeyboardState _currentKeyboardState;

    public KeyboardState CurrentKeyboardState => _currentKeyboardState;

    /// <summary>
    /// Cursor position on the screen. Null when using an ImGui window.
    /// </summary>
    public Point CursorPosition;

    /// <summary>
    /// If true player is using the keyboard, false means the player is using a game controller
    /// </summary>
    public bool UsingKeyboard = false;
    public float ControlChange = 0;

    /// <summary>
    /// Keyboard ignored because the player is probably typing something on ImGui
    /// </summary>
    public bool KeyboardConsumed = false;

    public bool MouseConsumed = false;

    /// <summary>
    /// This will freeze any external input from the user.
    /// </summary>
    private bool _lockInput = false;

    /// <summary>
    /// Scrollwheel delta
    /// </summary>
    public int ScrollWheel
    {
        get
        {
            return _previousScrollWheel - _scrollWheel;
        }
    }

    private int _scrollWheel = 0;
    private int _previousScrollWheel = 0;

    private readonly KeyboardState _emptyKeyboardState = new KeyboardState();
    private readonly MouseState _emptyMouseState = new MouseState();

    public void LockInput(bool @lock)
    {
        if (!@lock)
        {
            _lockInput = false;
            return;
        }

        UpdateOnEmpty();
        _lockInput = true;
    }

    public void MockInput(int button)
    {
        if (_buttons.TryGetValue(button, out VirtualButton? virtualButton))
        {
            virtualButton.Press();
        }
    }

    public void MockInput(int axis, Vector2 value)
    {
        if (_axis.TryGetValue(axis, out VirtualAxis? virtualAxis))
        {
            virtualAxis.Press(value);
        }
    }

    public void MockNoInput()
    {
        UpdateOnEmpty();
    }

    public VirtualButton GetOrCreateButton(int button)
    {
        if (!_buttons.ContainsKey(button) || _buttons[button] == null)
        {
            _buttons[button] = new VirtualButton();
            //GameDebugger.Log($"Creating a VirtualButton called '{button}'");
        }

        return _buttons[button];
    }

    public string GetAxisDescriptor(int axis)
    {
        return GetOrCreateAxis(axis).GetDescriptor();
    }

    public string GetButtonDescriptor(int button)
    {
        return GetOrCreateButton(button).GetDescriptor();
    }

    public VirtualAxis GetOrCreateAxis(int axis)
    {
        if (!_axis.TryGetValue(axis, out VirtualAxis? value) || value is null)
        {
            value = new VirtualAxis();
            _axis[axis] = value;
        }

        return value;
    }

    /// <summary>
    /// Registers input axes
    /// </summary>
    public void DeregisterAll(int axis)
    {
        VirtualAxis a = GetOrCreateAxis(axis);
        a.DeregisterAll();
    }

    /// <summary>
    /// Registers input axes
    /// </summary>
    public void Register(int axis, params InputButtonAxis[] buttonAxes)
    {
        var a = GetOrCreateAxis(axis);
        a.Register(buttonAxes);
    }

    /// <summary>
    /// Registers a gamepad axis as a button
    /// </summary>
    public void RegisterAxesAsButton(int button, params GamepadAxis[] gamepadAxis)
    {
        var b = GetOrCreateButton(button);
        b.Register(gamepadAxis);
    }

    /// <summary>
    /// Registers a gamepad axis as a button
    /// </summary>
    public void RegisterAxes(int axis, params GamepadAxis[] gamepadAxis)
    {
        var b = GetOrCreateAxis(axis);
        b.Register(gamepadAxis);
    }

    /// <summary>
    /// Registers a keyboard key as a button
    /// </summary>
    public void RegisterButton(int button, params Keys[] keys)
    {
        var b = GetOrCreateButton(button);
        b.Register(keys);
    }

    /// <summary>
    /// Registers a mouse button as a button
    /// </summary>
    public void RegisterButton(int button, params Buttons[] buttons)
    {
        var b = GetOrCreateButton(button);
        b.Register(buttons);
    }

    /// <summary>
    /// Clears all binds from a button
    /// </summary>
    public void ClearBinds(int button)
    {
        var b = GetOrCreateButton(button);
        b.ClearBinds();
    }

    public void Register(int button, params MouseButtons[] buttons)
    {
        var b = GetOrCreateButton(button);
        b.Register(buttons);
    }

    public void Update()
    {
        if (_lockInput)
        {
            return;
        }

        _previousKeyboardState = _currentKeyboardState;
        if (!KeyboardConsumed)
        {
            _currentKeyboardState = Keyboard.GetState();
        }
        else
        {
            _currentKeyboardState = _emptyKeyboardState;
        }

        _rawPreviousKeyboardState = _rawCurrentKeyboardState;
        _rawCurrentKeyboardState = Keyboard.GetState();

        MouseState mouseState = Mouse.GetState();

        bool gamepadAvailable = false;
        if (GamePad.GetState(Microsoft.Xna.Framework.PlayerIndex.One).IsConnected)
        {
            var capabilities = GamePad.GetCapabilities(Microsoft.Xna.Framework.PlayerIndex.One);
            gamepadAvailable = capabilities.IsConnected && capabilities.GamePadType == GamePadType.GamePad;
        }

        GamePadState gamepadState = gamepadAvailable ? GamePad.GetState(Microsoft.Xna.Framework.PlayerIndex.One, GamePadDeadZone.Circular) : new();
        InputState inputState = new(_currentKeyboardState, gamepadState, MouseConsumed ? _emptyMouseState : mouseState);
        var scale = Game.Instance.GameScale;

#if DEBUG
        if (MouseConsumed)
        {
            _buttons[MurderInputButtons.Debug].Update(inputState);
        }
#endif

        foreach (var button in _buttons)
        {
            button.Value.Update(inputState);
        }

        foreach (var axis in _axis)
        {
            axis.Value.Update(inputState);
        }

        _previousScrollWheel = _scrollWheel;
        _scrollWheel = inputState.MouseState.ScrollWheelValue;

        // Even if the mouse is consumed, we can still know it's position.
        CursorPosition = new(
            Calculator.RoundToInt(mouseState.X),
            Calculator.RoundToInt(mouseState.Y));
    }

    public void UpdateOnEmpty()
    {
        _currentKeyboardState = _emptyKeyboardState;

        InputState inputState = new(_currentKeyboardState, gamePadState: new(), _emptyMouseState);
        foreach (var button in _buttons)
        {
            button.Value.Update(inputState);
        }

        foreach (var axis in _axis)
        {
            axis.Value.Update(inputState);
        }
    }

    public void Bind(int button, Action<InputState> action)
    {
        GetOrCreateButton(button).OnPress += action;
    }

    public bool Shortcut(Chord chord) => Shortcut(chord.Key, chord.Modifiers);

    public bool Shortcut(Keys key, params Keys[] modifiers)
    {
        if (key == Keys.None)
        {
            return false;
        }

        foreach (Keys k in modifiers)
        {
            if (!_rawCurrentKeyboardState.IsKeyDown(k))
            {
                return false;
            }
        }

        if (!_rawPreviousKeyboardState.IsKeyDown(key) && _rawCurrentKeyboardState.IsKeyDown(key))
        {
            return true;
        }

        return false;
    }

    public bool Released(int button)
    {
        return _buttons[button].Released;
    }

    public bool Pressed(Keys key)
    {
        return Keyboard.GetState().IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }

    public bool PressedAndConsume(int button)
    {
        if (Pressed(button))
        {
            Consume(button);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Consumes all buttons that have anything in common with this
    /// </summary>
    /// <param name="button"></param>
    public void Consume(int button)
    {
        if (_buttons.TryGetValue(button, out VirtualButton? virtualButton))
        {
            virtualButton.Consume();

            foreach (var otherVirtualButtonPair in _buttons)
            {
                if (otherVirtualButtonPair.Value is VirtualButton other)
                {
                    if (other.Consumed) continue;

                    // Check all gamepad buttons
                    foreach (var value in virtualButton.Buttons)
                    {
                        if (other.Buttons.Contains(value))
                        {
                            other.Consume();
                        }
                    }
                    if (other.Consumed) continue;

                }
            }
        }
    }

    public void ConsumeAll()
    {
        foreach (var button in _buttons)
        {
            button.Value.Consume();
        }

        foreach (var axis in _axis)
        {
            axis.Value.Consume();
        }
    }

    public VirtualAxis GetAxis(int axis)
    {
        if (_axis.TryGetValue(axis, out var a))
        {
            return a;
        }

        throw new Exception($"Couldn't find button of type {axis}");
    }

    public bool Pressed(int button, bool raw = false)
    {
        if (_buttons.TryGetValue(button, out var btn))
        {
            return btn.Pressed && (raw || !btn.Consumed);
        }

        GameLogger.Error($"Couldn't find button of type {button}");
        return false;
    }

    public bool Down(int button, bool raw = false)
    {
        if (_buttons.TryGetValue(button, out var btn))
        {
            return btn.Down && (raw || !btn.Consumed);
        }

        GameLogger.Error($"Couldn't find button of type {button}");
        return false;
    }

    internal bool Released(int button, bool raw = false)
    {
        if (_buttons.TryGetValue(button, out var btn))
        {
            return btn.Released && (raw || !btn.Consumed);
        }

        GameLogger.Error($"Couldn't find button of type {button}");
        return false;
    }


    [Flags]
    public enum SimpleMenuFlags
    {
        None,
        Clamp
    }


    public bool HorizontalMenu(ref int selectedOption, int length)
    {
        int move = 0;
        var axis = GetAxis(MurderInputAxis.Ui);
        if (axis.Pressed)
        {
            move = Math.Sign(axis.Value.X);
        }

        selectedOption = Calculator.WrapAround(selectedOption + move, 0, length - 1);

        return PressedAndConsume(MurderInputButtons.Submit);
    }

    public bool HorizontalMenu(ref MenuInfo currentInfo)
    {
        if (currentInfo.Disabled)
        {
            return false;
        }

        currentInfo.JustMoved = false;
        VirtualAxis axis = GetAxis(MurderInputAxis.Ui);

        // Check for vertical overflow
        currentInfo.OverflowY = axis.TickY ? axis.IntValue.Y : 0;

        return HorizontalOrVerticalMenu(ref currentInfo, input: axis.TickX ? axis.IntValue.X : null);
    }

    public bool VerticalMenu(ref MenuInfo currentInfo, SimpleMenuFlags flags = SimpleMenuFlags.None)
    {
        if (currentInfo.Disabled)
        {
            return false;
        }

        currentInfo.JustMoved = false;
        VirtualAxis axis = GetAxis(MurderInputAxis.Ui);

        // Check for horizontal overflow
        currentInfo.OverflowX = axis.TickX ? axis.IntValue.X : 0;

        return HorizontalOrVerticalMenu(ref currentInfo, axis.TickY ? axis.IntValue.Y : null, flags);
    }

    private bool HorizontalOrVerticalMenu(ref MenuInfo currentInfo, float? input, SimpleMenuFlags flags = SimpleMenuFlags.None)
    {
        bool pressed = false;
        if (Pressed(MurderInputButtons.Submit))
        {
            currentInfo.LastPressed = Game.NowUnscaled;
            pressed = true;
        }

        if (Pressed(MurderInputButtons.Cancel))
        {
            currentInfo.Cancel();
            currentInfo.Canceled = true;
        }
        else
        {
            currentInfo.Canceled = false;
        }

        if (currentInfo.Disabled || currentInfo.Options == null || currentInfo.Length == 0)
            return false;

        if (pressed)
        {
            Consume(MurderInputButtons.Submit);

            currentInfo.Press(Game.NowUnscaled);
        }

        if (input is not null)
        {
            // Fist check if we are clamping the menu
            if (flags.HasFlag(SimpleMenuFlags.Clamp))
            {
                if (input.Value < 0 && currentInfo.Selection == 0)
                {
                    return pressed;
                }

                if (input.Value > 0 && currentInfo.Selection == currentInfo.Length - 1)
                {
                    return pressed;
                }
            }

            // Pick the next option. However, we need to take into account options that can't be selected,
            // so this gets slightly trickier.
            int sign = Math.Sign(input.Value);

            int newOption = currentInfo.NextAvailableOption(currentInfo.Selection, sign);
            if (sign != 0)
            {
                currentInfo.Select(newOption, Game.NowUnscaled);
            }

            // == scroll ==
            if (newOption < currentInfo.Scroll)
            {
                currentInfo.Scroll = 0;
            }
            else if (newOption >= currentInfo.Scroll + currentInfo.VisibleItems)
            {
                currentInfo.Scroll = newOption - currentInfo.VisibleItems + 1;
            }
        }

        currentInfo.SmoothScroll = Calculator.LerpSmooth(currentInfo.SmoothScroll, currentInfo.Scroll, Game.UnscaledDeltaTime, 0.1f);
        return pressed;
    }

    public bool VerticalMenu<T>(ref GenericMenuInfo<T> currentInfo)
    {
        if (currentInfo.Disabled)
        {
            return false;
        }

        currentInfo.JustMoved = false;

        VirtualAxis axis = GetAxis(MurderInputAxis.Ui);
        return HorizontalOrVerticalMenu(ref currentInfo, axis.TickY ? axis.IntValue.Y : null,
            axis.PressedX ? axis.IntValue.X : 0);
    }

    private bool HorizontalOrVerticalMenu<T>(ref GenericMenuInfo<T> currentInfo, int? input, int overflow)
    {
        bool pressed = false;
        if (Pressed(MurderInputButtons.Submit))
        {
            currentInfo.LastPressed = Game.NowUnscaled;
            pressed = true;
        }

        bool canceled = false;
        if (Pressed(MurderInputButtons.Cancel))
        {
            canceled = true;
        }

        currentInfo.Canceled = canceled;
        currentInfo.OverflowX = overflow;

        if (currentInfo.Disabled || currentInfo.Options == null || currentInfo.Length == 0)
            return false;

        if (pressed)
        {
            Consume(MurderInputButtons.Submit);
        }

        if (input is not null)
        {
            // Pick the next option. However, we need to take into account options that can't be selected,
            // so this gets slightly trickier.
            int sign = Math.Sign(input.Value);

            int newOption = currentInfo.NextAvailableOption(currentInfo.Selection, sign);
            if (newOption != currentInfo.Selection)
            {
                if (newOption < currentInfo.Scroll)
                {
                    currentInfo.Scroll = newOption;
                }
                else if (newOption >= currentInfo.Scroll + currentInfo.VisibleItems)
                {
                    currentInfo.Scroll = newOption - currentInfo.VisibleItems + 1;
                }
                currentInfo.Select(newOption, Game.NowUnscaled);
            }
        }

        return pressed;
    }

    public bool SimpleVerticalMenu(ref int selectedOption, int length)
    {
        int move = 0;
        var axis = GetAxis(MurderInputAxis.Ui);
        if (axis.PressedY)
        {
            move = Math.Sign(axis.Value.Y);
        }

        selectedOption = Calculator.WrapAround(selectedOption + move, 0, length - 1);

        return PressedAndConsume(MurderInputButtons.Submit);
    }

    [Flags]
    public enum GridMenuFlags
    {
        None,
        ClampRight,
        ClampLeft,
        ClampTop,
        ClampBottom,
        ClampAllDirections,
        ClampSize,
        SelectDisabled,
        Rotate
    }

    public bool GridMenu(ref MenuInfo currentInfo, int width, int maxHeight, int size, GridMenuFlags gridMenuFlags = GridMenuFlags.None)
    {
        if (currentInfo.Disabled)
        {
            return false;
        }

        VirtualAxis rawAxis = GetAxis(MurderInputAxis.Ui);

        bool rotateInput = gridMenuFlags.HasFlag(GridMenuFlags.Rotate);

        // Horizontal / Vertical inputs
        bool horizontalPressed = rotateInput ? rawAxis.TickY : rawAxis.TickX;
        bool verticalPressed = rotateInput ? rawAxis.TickX : rawAxis.TickY;

        int horizontalValue = rotateInput ? rawAxis.IntValue.Y : rawAxis.IntValue.X;
        int verticalValue = rotateInput ? rawAxis.IntValue.X : rawAxis.IntValue.Y;

        float lastMoved = currentInfo.LastMoved;
        float lastPressed = currentInfo.LastPressed;

        // Recalculate height based on the size.
        int height = Calculator.CeilToInt((float)size / width);

        int lastRowWidth = width - (width * height - size);

        int lastSelection = currentInfo.Selection;

        int selectedOptionX = currentInfo.Selection % width;
        int selectedOptionY = Calculator.FloorToInt(currentInfo.Selection / width);
        int overflowX = 0;
        int overflowY = 0;

        int currentWidth = selectedOptionY == height - 1 ? lastRowWidth : width;
        if (horizontalPressed)
        {
            selectedOptionX += Math.Sign(horizontalValue);

            if (selectedOptionX >= currentWidth) // Is on last row and it has less than width.
            {
                overflowX = 1;
                if (gridMenuFlags.HasFlag(GridMenuFlags.ClampRight) || gridMenuFlags.HasFlag(GridMenuFlags.ClampAllDirections))
                {
                    selectedOptionX = currentWidth - 1;
                }
            }
            else if (selectedOptionX < 0)
            {
                overflowX = -1;
                if (gridMenuFlags.HasFlag(GridMenuFlags.ClampLeft) || gridMenuFlags.HasFlag(GridMenuFlags.ClampAllDirections))
                {
                    selectedOptionX = 0;
                }
            }

            selectedOptionX = Calculator.WrapAround(selectedOptionX, 0, currentWidth - 1);
            lastMoved = Game.NowUnscaled;
        }

        int currentHeight = selectedOptionX >= lastRowWidth ? height - 1 : height;
        if (verticalPressed)
        {
            selectedOptionY += Math.Sign(verticalValue);

            if (selectedOptionY >= currentHeight)
            {
                overflowY = 1;
                if (gridMenuFlags.HasFlag(GridMenuFlags.ClampBottom) || gridMenuFlags.HasFlag(GridMenuFlags.ClampAllDirections))
                {
                    selectedOptionY = currentHeight - 1;
                    currentInfo.Scroll = Math.Max(0, height - maxHeight);
                }
            }
            else if (selectedOptionY < 0)
            {
                overflowY = -1;
                if (gridMenuFlags.HasFlag(GridMenuFlags.ClampTop) || gridMenuFlags.HasFlag(GridMenuFlags.ClampAllDirections))
                {
                    selectedOptionY = 0;
                    currentInfo.Scroll = 0;
                }
            }

            if (selectedOptionY >= currentHeight)
            {
                // Select the last option
                selectedOptionY = currentHeight - 1;
                selectedOptionX = currentWidth;
            }

            selectedOptionY = Calculator.WrapAround(selectedOptionY, 0, currentHeight - 1);
            lastMoved = Game.NowUnscaled;
        }

        int selectedOptionIndex = selectedOptionX + selectedOptionY * width;

        if (gridMenuFlags.HasFlag(GridMenuFlags.ClampSize))
        {
            selectedOptionIndex = Math.Clamp(selectedOptionIndex, 0, currentInfo.Length - 1);
        }

        currentInfo.JustMoved = currentInfo.Selection != selectedOptionIndex;

        currentInfo.PreviousSelection = currentInfo.Selection;

        currentInfo.LastMoved = lastMoved;
        currentInfo.LastPressed = lastMoved;

        if (currentInfo.JustMoved)
        {
            bool isDisabled = selectedOptionIndex < 0 || selectedOptionIndex >= currentInfo.Length || !currentInfo.Options[selectedOptionIndex].Enabled;

            if (!isDisabled)
            {
                currentInfo.Select(selectedOptionIndex, lastMoved, false);
            }
            else
            {
                if (verticalPressed)
                {
                    int newOption = currentInfo.Selection;
                    int sign = Math.Sign(verticalValue) < 0 ? -1 : 1;
                    if (sign != 0)
                    {
                        (newOption, bool wrapped) = currentInfo.NextAvailableOptionVertical(selectedOptionIndex, width, sign);
                        if (wrapped)
                        {
                            currentInfo.OverflowY = sign;
                        }
                    }

                    currentInfo.Select(newOption, Game.NowUnscaled, false);
                }

                if (horizontalPressed)
                {
                    int newOption = currentInfo.Selection;
                    int sign = Math.Sign(horizontalValue) < 0 ? -1 : 1;
                    if (sign != 0)
                    {
                        (newOption, bool wrapped) = currentInfo.NextAvailableOptionHorizontal(selectedOptionIndex, width, sign);
                        if (wrapped)
                        {
                            currentInfo.OverflowX = sign;
                        }
                    }

                    currentInfo.Select(newOption, Game.NowUnscaled, false);
                }
            }
        }

        selectedOptionY = Calculator.FloorToInt(currentInfo.Selection / (float)width);
        if (selectedOptionY - currentInfo.Scroll >= maxHeight)
        {
            currentInfo.Scroll++;
        }
        if (selectedOptionY - currentInfo.Scroll < 0)
        {
            currentInfo.Scroll = Math.Max(0, currentInfo.Scroll - 1);
        }

        bool pressed = false;
        if (PressedAndConsume(MurderInputButtons.Submit))
        {
            lastPressed = Game.NowUnscaled;
            pressed = true;
        }

        bool canceled = false;
        if (Pressed(MurderInputButtons.Cancel))
        {
            canceled = true;
        }

        currentInfo.Canceled = canceled;
        currentInfo.OverflowX = overflowX;
        currentInfo.OverflowY = overflowY;
        return pressed;
    }

    public bool GridMenu<T>(ref GenericMenuInfo<T> currentInfo, int width, int size, GridMenuFlags gridMenuFlags = GridMenuFlags.None)
    {
        if (currentInfo.Disabled)
        {
            return false;
        }
        var rawAxis = GetAxis(MurderInputAxis.Ui);
        bool rotateInput = gridMenuFlags.HasFlag(GridMenuFlags.Rotate);

        // Horizontal / Vertical inputs
        bool horizontalPressed = rotateInput ? rawAxis.PressedY : rawAxis.PressedX;
        bool verticalPressed = rotateInput ? rawAxis.PressedX : rawAxis.PressedY;

        float horizontalValue = rotateInput ? rawAxis.Value.Y : rawAxis.Value.X;
        float verticalValue = rotateInput ? rawAxis.Value.X : rawAxis.Value.Y;

        float lastMoved = currentInfo.LastMoved;

        // Recalculate height based on the size.
        int height = Calculator.CeilToInt((float)size / width);
        int lastRowWidth = width - (width * height - size);

        int selectedOptionX = currentInfo.Selection % width;
        int selectedOptionY = Calculator.FloorToInt(currentInfo.Selection / width);
        int overflowX = 0;
        int overflowY = 0;
        if (horizontalPressed)
        {
            selectedOptionX += Math.Sign(horizontalValue);

            int currentWidth = selectedOptionY == height - 1 ? lastRowWidth : width;

            if (selectedOptionX >= currentWidth) // Is on last row and it has less than width.
            {
                overflowX = 1;
                if (gridMenuFlags.HasFlag(GridMenuFlags.ClampRight) || gridMenuFlags.HasFlag(GridMenuFlags.ClampAllDirections))
                {
                    selectedOptionX = currentWidth - 1;
                }
            }
            else if (selectedOptionX < 0)
            {
                overflowX = -1;
                if (gridMenuFlags.HasFlag(GridMenuFlags.ClampLeft) || gridMenuFlags.HasFlag(GridMenuFlags.ClampAllDirections))
                {
                    selectedOptionX = 0;
                }
            }

            selectedOptionX = Calculator.WrapAround(selectedOptionX, 0, currentWidth - 1);

            lastMoved = Game.NowUnscaled;
        }

        if (verticalPressed)
        {
            selectedOptionY += Math.Sign(verticalValue);

            int currentHeight = selectedOptionX >= lastRowWidth ? height - 1 : height;

            if (selectedOptionY >= currentHeight)
            {
                overflowY = 1;
                if (gridMenuFlags.HasFlag(GridMenuFlags.ClampBottom) || gridMenuFlags.HasFlag(GridMenuFlags.ClampAllDirections))
                {
                    selectedOptionY = currentHeight - 1;
                }
            }
            else if (selectedOptionY < 0)
            {
                overflowY = -1;
                if (gridMenuFlags.HasFlag(GridMenuFlags.ClampTop) || gridMenuFlags.HasFlag(GridMenuFlags.ClampAllDirections))
                {
                    selectedOptionY = 0;
                }
            }

            selectedOptionY = Calculator.WrapAround(selectedOptionY, 0, Math.Max(0, currentHeight - 1));

            lastMoved = Game.NowUnscaled;
        }

        int selectedOptionIndex = selectedOptionX + selectedOptionY * width;

        if (gridMenuFlags.HasFlag(GridMenuFlags.ClampSize))
        {
            selectedOptionIndex = Math.Clamp(selectedOptionIndex, 0, currentInfo.Length - 1);
        }

        bool pressed = false;
        if (PressedAndConsume(MurderInputButtons.Submit))
        {
            currentInfo.LastPressed = Game.NowUnscaled;
            pressed = true;
        }

        bool canceled = false;
        if (Pressed(MurderInputButtons.Cancel))
        {
            canceled = true;
        }

        int selectedOptionColumn = currentInfo.Selection / width;
        int visibleColumns = currentInfo.VisibleItems / width;
        if (selectedOptionColumn < currentInfo.Scroll)
        {
            currentInfo.Scroll = selectedOptionColumn;
        }
        else if (selectedOptionColumn >= currentInfo.Scroll + visibleColumns)
        {
            currentInfo.Scroll = selectedOptionColumn - visibleColumns + 1;
        }
        currentInfo.Select(selectedOptionIndex, lastMoved);
        currentInfo.SmoothScroll = Calculator.LerpSmooth(currentInfo.SmoothScroll, currentInfo.Scroll, Game.UnscaledDeltaTime, 0.1f);

        currentInfo.Canceled = canceled;
        currentInfo.OverflowX = overflowX;
        currentInfo.OverflowY = overflowY;

        return pressed;
    }

    private bool _registerKeyboardInputs = false;
    private int _maxCharacters = 32;

    private StringBuilder _userKeyboardInput = new();

    public void ClampText(int size)
    {
        if (size >= _userKeyboardInput.Length)
        {
            return;
        }

        _userKeyboardInput.Remove(size, _userKeyboardInput.Length - size);
    }

    public void ListenToKeyboardInput(bool enable, int maxCharacters = 32)
    {
        if (_registerKeyboardInputs == enable)
        {
            _userKeyboardInput = new();

            return;
        }

        if (enable)
        {
            TextInputEXT.StartTextInput();

            TextInputEXT.TextInput += OnDesktopTextInput;
        }
        else
        {
            TextInputEXT.StopTextInput();

            TextInputEXT.TextInput -= OnDesktopTextInput;
        }

        _userKeyboardInput = new();

        _registerKeyboardInputs = enable;
        _maxCharacters = maxCharacters;
    }

    public string GetKeyboardInput() => _userKeyboardInput.ToString();

    public void SetKeyboardInput(string value)
    {
        _userKeyboardInput.Clear();
        _userKeyboardInput.Append(value);
    }

    private void OnDesktopTextInput(char c)
    {
        if (c == (char)8 /* backspace */)
        {
            if (_userKeyboardInput.Length > 0)
            {
                _userKeyboardInput.Remove(_userKeyboardInput.Length - 1, 1);
            }

            return;
        }
        else if (c == (char)10 /* enter */ || c == (char)13 /* enter */ || c == (char)33 /* escape */)
        {
            return;
        }

        if (_userKeyboardInput.Length >= _maxCharacters)
        {
            return;
        }

        if (c < 32)
        {
            // This means this was a special character. Bypass the event.
            return;
        }

        _userKeyboardInput.Append(c);
    }

    public bool Down(Keys key)
    {
        var keyboardState = Keyboard.GetState();
        return keyboardState.IsKeyDown(key);
    }


    static readonly ImmutableArray<ButtonBindingsInfo>.Builder _bindingsInfoBuilder = ImmutableArray.CreateBuilder<ButtonBindingsInfo>();
    public void SaveCurrentToPreferences(GamePreferences gamePreferences)
    {
        if (_bindingsInfoBuilder.Any())
        {
            _bindingsInfoBuilder.Clear();
        }

        foreach (var button in _buttons)
        {
            var bindingsInfo = new ButtonBindingsInfo(button.Key, button.Value);
            _bindingsInfoBuilder.Add(bindingsInfo);
        }

        gamePreferences.SetButtonBindingsInfos(_bindingsInfoBuilder.DrainToImmutable());
    }

    public void LoadFromPreferences(GamePreferences gamePreferences)
    {
        if (Game.Data.GetAsset<InputProfileAsset>(Game.Profile.InputProfile) is not InputProfileAsset inputProfile)
        {
            GameLogger.Error("Input profile not found, did you set it in GameProfile?");
            return;
        }

        foreach (var button in inputProfile.Buttons)
        {
            bool loaded = false;
            foreach (var bindingsInfo in gamePreferences.ButtonBindingsInfos)
            {
                if (button.ButtonId == bindingsInfo.Key && button.AllowPlayerCustomization)
                {
                    _buttons[bindingsInfo.Key] = bindingsInfo.CreateVirtualButton();
                    loaded = true;
                    break;
                }
            }

            if (!loaded)
            {
                var virtualButton = GetOrCreateButton(button.ButtonId);
                virtualButton.ClearBinds();
                virtualButton.DeregisterAll();

                RegisterAll(virtualButton, button);
            }
        }

        foreach (var axis in inputProfile.Axis)
        {
            bool loaded = false;
            foreach (var bindingsInfo in gamePreferences.AxisBindingsInfos)
            {
                if (axis.AxisId == bindingsInfo.Key && axis.AllowPlayerCustomization)
                {
                    _axis[bindingsInfo.Key] = bindingsInfo.CreateVirtualAxis();
                    loaded = true;
                    break;
                }
            }

            if (!loaded)
            {
                var virtualAxis = GetOrCreateAxis(axis.AxisId);
                virtualAxis.DeregisterAll();
                RegisterAll(virtualAxis, axis);
            }
        }
    }

    public void ClearButton(int id)
    {
        var virtualButton = GetOrCreateButton(id);
        virtualButton.ClearBinds();
        virtualButton.DeregisterAll();
    }

    public void RestoreDefaults(int id)
    {
        if (Game.Data.GetAsset<InputProfileAsset>(Game.Profile.InputProfile) is not InputProfileAsset inputProfile)
        {
            GameLogger.Error("Input profile not found, did you set it in GameProfile?");
            return;
        }

        var virtualButton = GetOrCreateButton(id);
        virtualButton.ClearBinds();
        virtualButton.DeregisterAll();

        foreach (var button in inputProfile.Buttons)
        {
            if (button.ButtonId == id)
            {
                // We have a match
                RegisterAll(virtualButton, button);
                break;
            }
        }
    }

    public void RestoreAxisDefaults(int id)
    {
        if (Game.Data.GetAsset<InputProfileAsset>(Game.Profile.InputProfile) is not InputProfileAsset inputProfile)
        {
            GameLogger.Error("Input profile not found, did you set it in GameProfile?");
            return;
        }

        var virtualAxis = GetOrCreateAxis(id);
        virtualAxis.DeregisterAll();

        foreach (var axis in inputProfile.Axis)
        {
            if (axis.AxisId == id)
            {
                // We have a match
                RegisterAll(virtualAxis, axis);
                break;
            }
        }
    }

    private static void RegisterAll(VirtualButton virtualButton, InputInformation button)
    {
        foreach (var key in button.DefaultKeyboard)
        {
            virtualButton.Register([key]);
        }

        foreach (var gamepad in button.DefaultGamePadButtons)
        {
            virtualButton.Register([gamepad]);
        }

        foreach (var mouse in button.DefaultMouseButtons)
        {
            virtualButton.Register([mouse]);
        }
    }

    private static void RegisterAll(VirtualAxis virtualAxis, InputAxisInformation axis)
    {
        foreach (var analogue in axis.Analogue)
        {
            virtualAxis.Register([analogue]);
        }

        foreach (var digital in axis.Digital)
        {
            if (axis.Horizontal && axis.Vertical)
            {
                switch (digital[0].Source)
                {
                    case InputSource.Keyboard:
                        {
                            if (digital[0].Keyboard is Keys up && digital[1].Keyboard is Keys left &&
                                digital[2].Keyboard is Keys down && digital[3].Keyboard is Keys right)
                            {
                                virtualAxis.Register([new InputButtonAxis(up, left, down, right)]);
                            }
                        }
                        break;
                    case InputSource.Gamepad:
                        {
                            if (digital[0].Gamepad is Buttons up && digital[1].Gamepad is Buttons left &&
                                digital[2].Gamepad is Buttons down && digital[3].Gamepad is Buttons right)
                            {
                                virtualAxis.Register([new InputButtonAxis(up, left, down, right)]);
                            }
                        }
                        break;

                    default:
                        GameLogger.Warning("Not implemented yet!");
                        break;
                }
            }
            else
            {
                // Horizontal or vertical only, doesn't matter, duplicate the same value
                switch (digital[0].Source)
                {
                    case InputSource.Keyboard:
                        {
                            if (digital[0].Keyboard is Keys up && digital[1].Keyboard is Keys down)
                            {
                                virtualAxis.Register([new InputButtonAxis(up, up, down, down)]);
                            }
                        }
                        break;
                    case InputSource.Gamepad:
                        {
                            if (digital[0].Gamepad is Buttons up && digital[1].Gamepad is Buttons down)
                            {
                                virtualAxis.Register([new InputButtonAxis(up, up, down, down)]);
                            }
                        }
                        break;
                    default:
                        GameLogger.Warning("Not implemented yet!");
                        break;

                }
            }
        }

    }

    public void RestoreAllDefaults()
    {
        if (Game.Data.GetAsset<InputProfileAsset>(Game.Profile.InputProfile) is not InputProfileAsset inputProfile)
        {
            GameLogger.Warning("Skiping input config, InputProfile not found, did you set it in GameProfile?");
            return;
        }
        foreach (var button in inputProfile.Buttons)
        {
            var virtualButton = GetOrCreateButton(button.ButtonId);
            virtualButton.ClearBinds();
            virtualButton.DeregisterAll();

            RegisterAll(virtualButton, button);
        }

        foreach (var axis in inputProfile.Axis)
        {
            var virtualAxes = GetOrCreateAxis(axis.AxisId);


        }
    }

    public Buttons? GetAnyGamepadButton()
    {
        var gamepadState = GamePad.GetState(Microsoft.Xna.Framework.PlayerIndex.One);
        if (gamepadState.IsConnected)
        {
            foreach (var button in Enum.GetValues<Buttons>())
            {
                if (gamepadState.IsButtonDown(button))
                {
                    return button;
                }
            }
        }

        return null;
    }
}