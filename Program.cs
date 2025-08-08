using Terminal.Gui;
using PowerDocker.UI;

Application.Init();

try
{
    var mainWindow = new MainWindow();
    var top = new Toplevel();
    top.Add(mainWindow);
    Application.Run(top);
}
catch (Exception ex)
{
    Application.Shutdown();
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Exception type: {ex.GetType().Name}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
        Console.WriteLine($"Inner exception type: {ex.InnerException.GetType().Name}");
    }
    Console.WriteLine("Make sure Docker is running and accessible.");
    Environment.Exit(1);
}
finally
{
    Application.Shutdown();
}
