using Godot;
using PerformativeMail.App;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;

namespace PerformativeMail.Game;

public static class InputSampler
{
    public static MoveIntent Sample()
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

        return new MoveIntent(axisX, axisY, Yaw: 0, buttons);
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
