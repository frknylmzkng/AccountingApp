using Win32Exception = System.ComponentModel.Win32Exception;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

internal static class ResponsiveTestWindow
{
    // Layout fixtures need a logical viewport, even when a headless runner has
    // a smaller desktop. Form.Size and WM_WINDOWPOSCHANGING otherwise cap the
    // request at SystemInformation.MaxWindowTrackSize. Only these test-owned,
    // transparent windows use the fallback; desktop settings are not changed.
    private const uint NoMove = 0x0002, NoZOrder = 0x0004, NoActivate = 0x0010, NoSendChanging = 0x0400;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    internal static void Show(Form form, Size logicalContentSize)
    {
        form.Show();
        var content = form.LogicalToDeviceUnits(logicalContentSize);
        int chrome = form.Controls.OfType<ToolStrip>().Where(c => c.Visible).Sum(c => c.Height);
        var requested = new Size(content.Width + form.Width - form.ClientSize.Width,
            content.Height + chrome + form.Height - form.ClientSize.Height);
        ResizeDevice(form, requested);
        TestContext.Progress.WriteLine($"{form.GetType().Name}: viewport={form.Size}, native track limit={SystemInformation.MaxWindowTrackSize}, DPI={form.DeviceDpi}");
    }

    internal static void Resize(Form form, Size logicalSize) => ResizeDevice(form, form.LogicalToDeviceUnits(logicalSize));

    internal static void ResizeDevice(Form form, Size requested)
    {
        Assert.That(form.WindowState, Is.EqualTo(FormWindowState.Normal));
        Assert.That(requested.Width, Is.GreaterThanOrEqualTo(form.MinimumSize.Width));
        Assert.That(requested.Height, Is.GreaterThanOrEqualTo(form.MinimumSize.Height));
        form.Size = requested;
        if (form.Size != requested)
        {
            // Keep native WM_SIZE/layout notifications, but skip the desktop
            // track-size restriction. Do not weaken any layout assertion.
            if (!SetWindowPos(form.Handle, 0, 0, 0, requested.Width, requested.Height,
                NoMove | NoZOrder | NoActivate | NoSendChanging))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        form.PerformLayout(); Application.DoEvents();
        Assert.That(form.Size, Is.EqualTo(requested),
            $"Logical viewport was clamped; native track limit={SystemInformation.MaxWindowTrackSize}");
    }
}

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Appearance")]
public class ResponsiveTestWindowTests
{
    [Test]
    public void LargeViewportIsRealAndCanShrinkAgainWithoutChangingFields()
    {
        using var form = new Form { Opacity = 0, ShowInTaskbar = false };
        var field = new TextBox { Dock = DockStyle.Fill, Multiline = true, Text = "Only an example layout value" };
        form.Controls.Add(field); ResponsiveTestWindow.Show(form, new Size(800, 450));
        ResponsiveTestWindow.Resize(form, new Size(2400, 1200));
        Assert.That(field.Bounds.Size, Is.EqualTo(form.ClientSize));
        ResponsiveTestWindow.Resize(form, new Size(560, 420));
        Assert.That(field.Bounds.Size, Is.EqualTo(form.ClientSize));
        Assert.That(field.Text, Is.EqualTo("Only an example layout value"));
    }
}
