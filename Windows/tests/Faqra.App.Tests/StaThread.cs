using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace Faqra.App.Tests;

/// <summary>
/// One STA thread shared by every UI test. WPF freezes theme brushes onto the thread that created
/// the Application, so a thread per test would fail with "cannot access Freezable across threads".
/// </summary>
public static class StaThread
{
    private static readonly Lazy<Dispatcher> UiDispatcher = new(CreateDispatcher, LazyThreadSafetyMode.ExecutionAndPublication);

    public static void Run(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        UiDispatcher.Value.Invoke(() =>
        {
            try
            {
                EnsureApplication();
                action();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        failure?.Throw();
    }

    /// <summary>
    /// Every UI surface resolves its fonts, styles and theme brushes from Application.Current, so the
    /// app object has to exist before any control is constructed, whichever test runs first.
    /// </summary>
    private static void EnsureApplication()
    {
        if (Application.Current is not null)
        {
            return;
        }
        var app = new App();
        app.InitializeComponent();
        // Startup never runs in tests, so apply the theme the way OnStartup would.
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
            Wpf.Ui.Appearance.ApplicationTheme.Dark, Wpf.Ui.Controls.WindowBackdropType.None, updateAccent: true);
    }

    private static Dispatcher CreateDispatcher()
    {
        var ready = new TaskCompletionSource<Dispatcher>();
        var thread = new Thread(() =>
        {
            ready.SetResult(Dispatcher.CurrentDispatcher);
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "Faqra UI tests",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return ready.Task.GetAwaiter().GetResult();
    }
}
