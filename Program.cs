using PowerDocker.UI;

try
{
    var mainWindow = new MainWindow();
    await mainWindow.RunAsync();
}
catch (Exception ex)
{
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
