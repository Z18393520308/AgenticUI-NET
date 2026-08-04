using System.Text.Json;

namespace AgenticUI;

public sealed class AgenticReplay
{
    private readonly AgenticCommandDispatcher _dispatcher;

    public AgenticReplay(AgenticCommandDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task<IReadOnlyList<AgenticCommandResult>> ReplayAsync(
        IEnumerable<AgenticCommand> commands,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<AgenticCommandResult>();
        foreach (var command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (delay is { } pause && pause > TimeSpan.Zero)
            {
                await Task.Delay(pause, cancellationToken).ConfigureAwait(false);
            }

            results.Add(await _dispatcher.DispatchAsync(command, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public static async Task<IReadOnlyList<AgenticCommand>> LoadCommandsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var commands = new List<AgenticCommand>();
        using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(line))
            {
                var command = JsonSerializer.Deserialize<AgenticCommand>(line, AgenticJson.Options);
                if (command is not null)
                {
                    commands.Add(command);
                }
            }
        }

        return commands;
    }
}
