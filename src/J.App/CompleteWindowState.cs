namespace J.App;

public readonly record struct CompleteWindowState(
    FormWindowState WindowState,
    RectangleF UnscaledRestoreBounds,
    PointF UnscaledLocation,
    SizeF UnscaledSize
)
{
    public static CompleteWindowState Save(Form form)
    {
        var scale = form.DeviceDpi / 96d;
        return new CompleteWindowState(
            WindowState: form.WindowState,
            UnscaledRestoreBounds: new RectangleF(
                (float)(form.RestoreBounds.X / scale),
                (float)(form.RestoreBounds.Y / scale),
                (float)(form.RestoreBounds.Width / scale),
                (float)(form.RestoreBounds.Height / scale)
            ),
            UnscaledLocation: new PointF((float)(form.Location.X / scale), (float)(form.Location.Y / scale)),
            UnscaledSize: new SizeF((float)(form.Size.Width / scale), (float)(form.Size.Height / scale))
        );
    }

    public void Restore(Form form)
    {
        if (UnscaledLocation == default && UnscaledSize == default && UnscaledRestoreBounds == default)
            return;

        var scale = form.DeviceDpi / 96d;
        Point scaledLocation = new((int)(UnscaledLocation.X * scale), (int)(UnscaledLocation.Y * scale));
        Size scaledSize = new((int)(UnscaledSize.Width * scale), (int)(UnscaledSize.Height * scale));
        Rectangle scaledRestoreBounds =
            new(
                (int)(UnscaledRestoreBounds.X * scale),
                (int)(UnscaledRestoreBounds.Y * scale),
                (int)(UnscaledRestoreBounds.Width * scale),
                (int)(UnscaledRestoreBounds.Height * scale)
            );

        // Ensure the window will be visible on at least one screen
        var screens = Screen.AllScreens;
        var isVisible = false;
        foreach (var screen in screens)
        {
            // Check if at least part of the window will be visible
            if (screen.WorkingArea.IntersectsWith(scaledRestoreBounds))
            {
                isVisible = true;
                break;
            }
        }

        if (!isVisible)
        {
            // If window would be off-screen, center it on the primary screen
            var screen = Screen.PrimaryScreen!;
            scaledLocation = new Point(
                (screen.WorkingArea.Width - scaledSize.Width) / 2,
                (screen.WorkingArea.Height - scaledSize.Height) / 2
            );
        }

        // Set the initial size and location
        if (WindowState == FormWindowState.Normal)
        {
            form.Location = scaledLocation;
            form.Size = scaledSize;
        }
        else
        {
            // For Maximized state, set the RestoreBounds first
            form.Location = scaledRestoreBounds.Location;
            form.Size = scaledRestoreBounds.Size;
        }

        // Set the window state last
        form.WindowState = WindowState;
    }
}
