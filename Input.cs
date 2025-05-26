using Silk.NET.SDL;

namespace TheAdventure;

public unsafe class Input
{
    private readonly Sdl _sdl;
    private bool _escapePressed = false;
    private bool _lastEscapeState = false;
    private bool _lastEKeyState = false;
    private bool _lastRKeyState = false;

    public EventHandler<(int x, int y)>? OnMouseClick;

    public Input(Sdl sdl)
    {
        _sdl = sdl;
    }

    public bool IsLeftPressed()
    {
        ReadOnlySpan<byte> keyboardState = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
        return keyboardState[(int)KeyCode.Left] == 1;
    }

    public bool IsRightPressed()
    {
        ReadOnlySpan<byte> keyboardState = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
        return keyboardState[(int)KeyCode.Right] == 1;
    }

    public bool IsUpPressed()
    {
        ReadOnlySpan<byte> keyboardState = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
        return keyboardState[(int)KeyCode.Up] == 1;
    }

    public bool IsDownPressed()
    {
        ReadOnlySpan<byte> keyboardState = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
        return keyboardState[(int)KeyCode.Down] == 1;
    }

    public bool IsKeyAPressed()
    {
        ReadOnlySpan<byte> _keyboardState = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
        return _keyboardState[(int)KeyCode.A] == 1;
    }

    public bool IsKeyBPressed()
    {
        ReadOnlySpan<byte> _keyboardState = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
        return _keyboardState[(int)KeyCode.B] == 1;
    }

    public bool IsKeyRPressed()
    {
        ReadOnlySpan<byte> keyboardState = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
        return keyboardState[(int)KeyCode.R] == 1;
    }

    public bool IsKeyEPressed()
    {
        ReadOnlySpan<byte> keyboardState = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
        return keyboardState[(int)KeyCode.E] == 1;
    }

    public bool IsEscapePressed()
    {
        ReadOnlySpan<byte> keyboardState = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
        bool currentEscapeState = keyboardState[(int)KeyCode.Escape] == 1;
        
        // Detect key press (not held down)
        bool wasJustPressed = currentEscapeState && !_lastEscapeState;
        _lastEscapeState = currentEscapeState;
        
        return wasJustPressed;
    }

    public bool ProcessInput()
    {
        Event ev = new Event();
        while (_sdl.PollEvent(ref ev) != 0)
        {
            if (ev.Type == (uint)EventType.Quit)
            {
                return true;
            }

            switch (ev.Type)
            {
                case (uint)EventType.Keydown:
                {
                    // Handle ESC key specifically to prevent it from triggering other actions
                    if (ev.Key.Keysym.Sym == (uint)KeyCode.Escape)
                    {
                        // ESC key handled separately in IsEscapePressed(), don't process further
                        break;
                    }
                    break;
                }
                    
                case (uint)EventType.Windowevent:
                {
                    switch (ev.Window.Event)
                    {
                        case (byte)WindowEventID.Shown:
                        case (byte)WindowEventID.Exposed:
                        {
                            break;
                        }
                        case (byte)WindowEventID.Hidden:
                        {
                            break;
                        }
                        case (byte)WindowEventID.Moved:
                        {
                            break;
                        }
                        case (byte)WindowEventID.SizeChanged:
                        {
                            break;
                        }
                        case (byte)WindowEventID.Minimized:
                        case (byte)WindowEventID.Maximized:
                        case (byte)WindowEventID.Restored:
                            break;
                        case (byte)WindowEventID.Enter:
                        {
                            break;
                        }
                        case (byte)WindowEventID.Leave:
                        {
                            break;
                        }
                        case (byte)WindowEventID.FocusGained:
                        {
                            break;
                        }
                        case (byte)WindowEventID.FocusLost:
                        {
                            break;
                        }
                        case (byte)WindowEventID.Close:
                        {
                            break;
                        }
                        case (byte)WindowEventID.TakeFocus:
                        {
                            _sdl.SetWindowInputFocus(_sdl.GetWindowFromID(ev.Window.WindowID));
                            break;
                        }
                    }

                    break;
                }

                case (uint)EventType.Fingermotion:
                {
                    break;
                }

                case (uint)EventType.Mousemotion:
                {
                    break;
                }

                case (uint)EventType.Fingerdown:
                {
                    break;
                }
                case (uint)EventType.Mousebuttondown:
                {
                    if (ev.Button.Button == (byte)MouseButton.Primary)
                    {
                        OnMouseClick?.Invoke(this, (ev.Button.X, ev.Button.Y));
                    }

                    break;
                }

                case (uint)EventType.Fingerup:
                {
                    break;
                }

                case (uint)EventType.Mousebuttonup:
                {
                    break;
                }

                case (uint)EventType.Mousewheel:
                {
                    break;
                }

                case (uint)EventType.Keyup:
                {
                    break;
                }
            }
        }

        return false;
    }
}