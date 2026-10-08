using Spectre.Console;
using Spectre.Console.Rendering;
using System.Text;

namespace PowerDocker.UI;

public sealed class TerminalScreen : IDisposable
{
    private readonly IAnsiConsole _console;
    private readonly StringWriter _lineBuffer = new();
    private readonly AnsiWriter _encoder;
    private string[] _previousLines = [];
    private (int Width, int Height) _previousSize;

    public TerminalScreen(IAnsiConsole console)
    {
        _console = console;
        _encoder = new AnsiWriter(_lineBuffer, new AnsiCapabilities
        {
            Ansi = true,
            ColorSystem = console.Profile.Capabilities.ColorSystem,
            Links = console.Profile.Capabilities.Links
        });
    }

    public int Draw(IRenderable content, int width, int height)
    {
        var options = new RenderOptions(_console.Profile.Capabilities, new Size(width, height));
        var renderedLines = Segment.SplitLines(content.Render(options, width))
            .Take(Math.Max(1, height - 1)).ToArray();
        var lines = renderedLines.Select(EncodeLine).ToArray();
        var repaint = _previousSize != (width, height);
        var output = new StringBuilder();
        if (repaint) output.Append("\u001b[0m\u001b[2J");

        var changed = 0;
        for (var row = 0; row < lines.Length; row++)
        {
            if (!repaint && row < _previousLines.Length && lines[row] == _previousLines[row]) continue;
            output.Append($"\u001b[{row + 1};1H").Append(lines[row]);
            // At the right edge terminals keep the cursor in the last cell until
            // the next printable character; erasing here would delete that cell.
            if (renderedLines[row].CellCount() < width) output.Append("\u001b[K");
            changed++;
        }
        if (!repaint)
        {
            for (var row = lines.Length; row < _previousLines.Length; row++)
                output.Append($"\u001b[{row + 1};1H\u001b[0m\u001b[K");
        }

        if (output.Length > 0)
        {
            // One write per frame, without reprinting unchanged rows or scrolling.
            _console.Write(ControlCode.Create(_console.Profile.Capabilities,
                writer => writer.Write(output.ToString())));
        }
        _previousLines = lines;
        _previousSize = (width, height);
        return changed;
    }

    private string EncodeLine(SegmentLine line)
    {
        _lineBuffer.GetStringBuilder().Clear();
        foreach (var segment in line)
            _encoder.Write(segment.Text, segment.Style, segment.Link);
        // A row must not inherit selection colors from a separately repainted row.
        _encoder.ResetStyle();
        return _lineBuffer.ToString();
    }

    public void Dispose() => _lineBuffer.Dispose();
}
