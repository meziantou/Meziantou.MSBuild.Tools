using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using Microsoft.Build.Logging.StructuredLogger;

var rootCommand = new RootCommand();
DetectDoubleWritesCommand(rootCommand);
return await rootCommand.InvokeAsync(args);

static void DetectDoubleWritesCommand(RootCommand rootCommand)
{
    var binlogPath = new Option<string>("--path", description: "Path of the binlog file") { IsRequired = true };

    var command = new Command("detect-double-writes");
    command.AddOption(binlogPath);

    command.SetHandler((InvocationContext ctx) =>
    {
        DetectDoubleWrites(ctx, ctx.ParseResult.GetValueForOption(binlogPath)!);
        return System.Threading.Tasks.Task.CompletedTask;
    });

    rootCommand.AddCommand(command);

    static void DetectDoubleWrites(InvocationContext ctx, string path)
    {
        path = Path.GetFullPath(path);
        try
        {
            var doubleWritesCount = 0;
            var build = Serialization.Read(path);
            if (!build.Succeeded)
            {
                ctx.Console.Error.WriteLine($"Build failed for '{path}'");
                ctx.ExitCode = 1;
                return;
            }

            foreach (var doubleWrite in DoubleWritesAnalyzer.GetDoubleWrites(build))
            {
                doubleWritesCount++;
                ctx.Console.Error.WriteLine($"Multiple writes to {doubleWrite.Key}");
                foreach (var source in doubleWrite.Value)
                {
                    ctx.Console.Error.WriteLine("- " + source);
                }
            }

            ctx.ExitCode = -doubleWritesCount;
        }
        catch (Exception ex)
        {
            ctx.Console.Error.WriteLine($"Error processing binary log file: {ex.Message}");
            ctx.ExitCode = 2;
        }
    }
}
