using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace AtomicArt.Desktop.Services.Windowing;

internal static class WindowPlacementResolver
{
    public static Screen? FindTargetScreen(
        Window window,
        WindowPlacementState state)
    {
        if (state.X is not null && state.Y is not null)
        {
            PixelPoint position = new(state.X.Value, state.Y.Value);
            Screen? matchingScreen = window.Screens.All.FirstOrDefault(
                screen => Contains(screen.WorkingArea, position));

            if (matchingScreen is not null)
            {
                return matchingScreen;
            }
        }

        return window.Screens.Primary;
    }

    public static Size? ResolveSize(
        Window window,
        WindowPlacementState state,
        Screen? screen)
    {
        if (state.Width is null || state.Height is null)
        {
            return null;
        }

        double minimumWidth = GetValidMinimum(window.MinWidth);
        double minimumHeight = GetValidMinimum(window.MinHeight);
        double width = Math.Max(minimumWidth, state.Width.Value);
        double height = Math.Max(minimumHeight, state.Height.Value);

        if (screen is null)
        {
            return new Size(width, height);
        }

        double maximumWidth = Math.Max(
            minimumWidth,
            screen.WorkingArea.Width / screen.Scaling);
        double maximumHeight = Math.Max(
            minimumHeight,
            screen.WorkingArea.Height / screen.Scaling);

        return new Size(
            Math.Min(width, maximumWidth),
            Math.Min(height, maximumHeight));
    }

    public static PixelPoint? ResolvePosition(
        WindowPlacementState state,
        Size? restoredSize,
        Screen? screen)
    {
        if (state.X is null
            || state.Y is null
            || screen is null)
        {
            return null;
        }

        PixelPoint requestedPosition = new(
            state.X.Value,
            state.Y.Value);

        if (!Contains(screen.WorkingArea, requestedPosition))
        {
            return null;
        }

        if (restoredSize is not { } size)
        {
            return requestedPosition;
        }

        int pixelWidth = Math.Max(
            1,
            (int)Math.Ceiling(size.Width * screen.Scaling));
        int pixelHeight = Math.Max(
            1,
            (int)Math.Ceiling(size.Height * screen.Scaling));
        PixelRect workingArea = screen.WorkingArea;
        int maximumX = Math.Max(
            workingArea.X,
            workingArea.Right - pixelWidth);
        int maximumY = Math.Max(
            workingArea.Y,
            workingArea.Bottom - pixelHeight);

        return new PixelPoint(
            Math.Clamp(requestedPosition.X, workingArea.X, maximumX),
            Math.Clamp(requestedPosition.Y, workingArea.Y, maximumY));
    }

    public static Size? GetCurrentWindowSize(Window window)
    {
        return double.IsFinite(window.Width)
            && double.IsFinite(window.Height)
            && window.Width > 0d
            && window.Height > 0d
                ? new Size(window.Width, window.Height)
                : null;
    }

    public static bool HasSavedGeometry(WindowPlacementState state)
    {
        return state.X is not null
            || state.Y is not null
            || state.Width is not null
            || state.Height is not null;
    }

    private static bool Contains(
        PixelRect rectangle,
        PixelPoint point)
    {
        return point.X >= rectangle.X
            && point.X < rectangle.Right
            && point.Y >= rectangle.Y
            && point.Y < rectangle.Bottom;
    }

    private static double GetValidMinimum(double minimum)
    {
        return double.IsFinite(minimum) && minimum > 0d
            ? minimum
            : 1d;
    }
}
