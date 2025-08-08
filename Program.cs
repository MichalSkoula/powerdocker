using Terminal.Gui;
using PowerDocker.UI;

Application.Init();

// Set default terminal colors (white on black)
Colors.TopLevel.Normal = new Terminal.Gui.Attribute(Color.White, Color.Black);
Colors.TopLevel.Focus = new Terminal.Gui.Attribute(Color.Black, Color.Gray);
Colors.TopLevel.HotNormal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black);
Colors.TopLevel.HotFocus = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Gray);
Colors.TopLevel.Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black);

Colors.Base.Normal = new Terminal.Gui.Attribute(Color.White, Color.Black);
Colors.Base.Focus = new Terminal.Gui.Attribute(Color.Black, Color.Gray);
Colors.Base.HotNormal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black);
Colors.Base.HotFocus = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Gray);
Colors.Base.Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black);

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
