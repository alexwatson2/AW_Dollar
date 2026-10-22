using System.Threading;
using System.Windows.Forms;

namespace AW_Dollar;

internal static class Program
{
    /// <summary>
    /// App entry point. Runs an ApplicationContext that owns a small tray icon plus the
    /// borderless floating price card (<see cref="PricePanelForm"/>) showing USD/EUR.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        // Only one instance should run at a time, otherwise duplicate tray icons appear.
        using var singleInstanceMutex = new Mutex(initiallyOwned: true, "AW_Dollar.SingleInstance.Mutex", out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
