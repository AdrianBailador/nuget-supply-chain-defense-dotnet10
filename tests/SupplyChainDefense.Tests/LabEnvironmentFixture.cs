using System.Diagnostics;

namespace SupplyChainDefense.Tests;

/// <summary>
/// Copies the repo into an isolated temp directory with its own NuGet global
/// packages cache, then packs EvilPkg.Benign fresh into a local-only feed.
/// Nothing here ever touches the developer's real NuGet cache or nuget.org,
/// and nothing is published anywhere — restore only ever sees a folder on
/// local disk.
/// </summary>
public sealed class LabEnvironmentFixture : IDisposable
{
    public string RepoRoot { get; }
    public string VictimAppDir { get; }
    public string DefensiveAppDir { get; }
    public string LocalLabFeedDir { get; }
    private readonly string _nugetPackagesCache;

    public LabEnvironmentFixture()
    {
        var sourceRoot = FindRepoRoot(AppContext.BaseDirectory);

        RepoRoot = Directory.CreateTempSubdirectory("supply-chain-lab-").FullName;
        CopyDirectory(sourceRoot, RepoRoot);

        VictimAppDir = Path.Combine(RepoRoot, "src", "VictimApp");
        DefensiveAppDir = Path.Combine(RepoRoot, "src", "DefensiveApp");
        LocalLabFeedDir = Path.Combine(RepoRoot, "local-lab-feed");
        _nugetPackagesCache = Path.Combine(RepoRoot, "_isolated-nuget-cache");

        Directory.CreateDirectory(LocalLabFeedDir);
        Directory.CreateDirectory(_nugetPackagesCache);

        var pack = RunDotnet(
            RepoRoot,
            "pack", Path.Combine("src", "EvilPkg.Benign", "EvilPkg.Benign.csproj"),
            "-c", "Release",
            "-o", LocalLabFeedDir);

        if (pack.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to pack the lab PoC package. Output:\n{pack.StdOut}\n{pack.StdErr}");
        }
    }

    public DotnetResult RunDotnet(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        // Isolated cache: a package that ended up here from one scenario must
        // never silently satisfy a different scenario's restore.
        startInfo.Environment["NUGET_PACKAGES"] = _nugetPackagesCache;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet process.");

        var stdOut = process.StandardOutput.ReadToEnd();
        var stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new DotnetResult(process.ExitCode, stdOut, stdErr);
    }

    private static string FindRepoRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            if (current.EnumerateFiles("*.slnx").Any() || current.EnumerateFiles("*.sln").Any())
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException($"Could not locate repo root above '{startDirectory}'.");
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var sourceInfo = new DirectoryInfo(sourceDir);

        foreach (var directory in sourceInfo.GetDirectories())
        {
            if (directory.Name is "bin" or "obj" or ".git")
            {
                continue;
            }

            var destinationSubDir = Path.Combine(destinationDir, directory.Name);
            Directory.CreateDirectory(destinationSubDir);
            CopyDirectory(directory.FullName, destinationSubDir);
        }

        foreach (var file in sourceInfo.GetFiles())
        {
            file.CopyTo(Path.Combine(destinationDir, file.Name), overwrite: true);
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(RepoRoot, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup — a lingering temp dir isn't worth failing the test run over.
        }
    }
}

public sealed record DotnetResult(int ExitCode, string StdOut, string StdErr)
{
    public string CombinedOutput => StdOut + Environment.NewLine + StdErr;
}
