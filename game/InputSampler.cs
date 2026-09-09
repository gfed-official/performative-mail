using Godot;
using PerformativeMail.App;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;

namespace PerformativeMail.Game;

public static class InputSampler
{
    public const int HotbarSlots = 8;
    public const int DefaultHotbarSlot = 1;

    public static MoveIntent Sample(in FirstPersonLookState look)
    {
        sbyte axisX = Axis(Key.D, Key.Right, Key.A, Key.Left);
        sbyte axisY = Axis(Key.W, Key.Up, Key.S, Key.Down);
        var buttons = InputButtons.None;
        if (Input.IsPhysicalKeyPressed(Key.Shift))
            buttons |= InputButtons.Sprint;
        if (Input.IsPhysicalKeyPressed(Key.E))
            buttons |= InputButtons.Interact;
        for (int id = 0; id < 8; id++)
        {
            if (Input.IsJoyButtonPressed(id, JoyButton.X))
                buttons |= InputButtons.Interact;
        }

        return new MoveIntent(axisX, axisY, look.Yaw, buttons);
    }

    public static bool MenuHeld()
    {
        if (Input.IsPhysicalKeyPressed(Key.Escape))
            return true;

        for (int id = 0; id < 8; id++)
        {
            if (Input.IsJoyButtonPressed(id, JoyButton.Start))
                return true;
        }

        return false;
    }

    public static bool MapHeld()
    {
        if (Input.IsPhysicalKeyPressed(Key.M))
            return true;

        for (int id = 0; id < 8; id++)
        {
            if (Input.IsJoyButtonPressed(id, JoyButton.Back))
                return true;
        }

        return false;
    }

    public static bool BuildHeld()
    {
        if (Input.IsPhysicalKeyPressed(Key.B))
            return true;

        for (int id = 0; id < 8; id++)
        {
            if (Input.IsJoyButtonPressed(id, JoyButton.LeftShoulder))
                return true;
        }

        return false;
    }

    public static bool RotateHeld()
    {
        if (Input.IsPhysicalKeyPressed(Key.R))
            return true;

        for (int id = 0; id < 8; id++)
        {
            if (Input.IsJoyButtonPressed(id, JoyButton.RightShoulder))
                return true;
        }

        return false;
    }

    public static bool PipetteHeld() => Input.IsPhysicalKeyPressed(Key.Q);

    public static bool PlaceHeld() => Input.IsMouseButtonPressed(MouseButton.Left);

    public static bool TryHotbarSlot(InputEvent @event, out int slot)
    {
        slot = -1;
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return false;

        slot = key.PhysicalKeycode switch
        {
            Key.Key1 or Key.Kp1 => 0,
            Key.Key2 or Key.Kp2 => 1,
            Key.Key3 or Key.Kp3 => 2,
            Key.Key4 or Key.Kp4 => 3,
            Key.Key5 or Key.Kp5 => 4,
            Key.Key6 or Key.Kp6 => 5,
            Key.Key7 or Key.Kp7 => 6,
            Key.Key8 or Key.Kp8 => 7,
            _ => -1,
        };
        return slot >= 0;
    }

    public static bool TryHotbarWheel(InputEvent @event, out int delta)
    {
        delta = 0;
        if (@event is not InputEventMouseButton { Pressed: true } mouse)
            return false;

        switch (mouse.ButtonIndex)
        {
            case MouseButton.WheelUp:
                delta = -1;
                return true;
            case MouseButton.WheelDown:
                delta = 1;
                return true;
            default:
                return false;
        }
    }

    public static int WrapHotbarSlot(int slot)
    {
        int wrapped = slot % HotbarSlots;
        return wrapped < 0 ? wrapped + HotbarSlots : wrapped;
    }

    private static sbyte Axis(Key positiveA, Key positiveB, Key negativeA, Key negativeB)
    {
        int value = 0;
        if (Input.IsPhysicalKeyPressed(positiveA) || Input.IsPhysicalKeyPressed(positiveB))
            value += MovementStep.AxisFull;
        if (Input.IsPhysicalKeyPressed(negativeA) || Input.IsPhysicalKeyPressed(negativeB))
            value -= MovementStep.AxisFull;
        return (sbyte)value;
    }
}
