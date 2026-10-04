using System.Diagnostics;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Shouldly;

namespace IngaCookBook.PlaywrightTests;

public sealed partial class EmailFreeAccountWorkflowTests
{
    private static async Task<string> IssueResetLinkAsync(DistributedApplication app, string projectPath, string userId, CancellationToken cancellationToken)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var testOutput = new DirectoryInfo(AppContext.BaseDirectory);
        // Both projects use the repository's standard bin/<configuration>/<TFM> outputs.
        var assembly = Path.Combine(projectDirectory, "bin", testOutput.Parent!.Name, testOutput.Name, "IngaCookBook.dll");
        File.Exists(assembly).ShouldBeTrue("Build the server with the same configuration as the browser tests.");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = projectDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { assembly, "account-recovery", "--user-id", userId, "--base-url", app.GetEndpoint("ingacookbook", "https").ToString() })
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["Email__Provider"] = "None";
        start.Environment["ConnectionStrings__ingacookbookdb"] = await app.GetConnectionStringAsync("ingacookbookdb", cancellationToken);
        start.Environment["ConnectionStrings__recipephotos"] = await app.GetConnectionStringAsync("recipephotos", cancellationToken);
        using var process = Process.Start(start)!;
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            // Do not include reset links or configuration in test output.
            process.ExitCode.ShouldBe(0, "The trusted operator command should exit successfully without starting a web server.");
            (await errors).ShouldBeEmpty();
            return (await output).Split(Environment.NewLine).Single(line => line.StartsWith("https://", StringComparison.Ordinal));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }
}
