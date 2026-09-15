using System.Runtime.ExceptionServices;

namespace Faqra.App.Tests;

/// <summary>Runs WPF code on a dedicated STA thread, rethrowing any failure on the test thread.</summary>
public static class StaThread
{
    public static void Run(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
