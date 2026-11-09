namespace SupplyChainDefense.Tests;

/// <summary>
/// Both tests shell out to the real `dotnet` CLI against an isolated copy of
/// the repo — this is deliberately not a unit test of application code. The
/// thing under test is NuGet/MSBuild behavior itself: does an unmapped
/// project execute a dependency's build-time logic, and does a mapped one
/// refuse to even restore it.
/// </summary>
public sealed class SupplyChainTests : IClassFixture<LabEnvironmentFixture>
{
    private readonly LabEnvironmentFixture _lab;

    public SupplyChainTests(LabEnvironmentFixture lab) => _lab = lab;

    [Fact]
    public void VictimApp_without_source_mapping_executes_the_dependency_build_logic()
    {
        var markerPath = Path.Combine(_lab.VictimAppDir, "supply-chain-poc-marker.txt");
        File.Delete(markerPath);

        var build = _lab.RunDotnet(_lab.VictimAppDir, "build");

        Assert.Equal(0, build.ExitCode);
        Assert.True(File.Exists(markerPath),
            "Expected EvilPkg.Benign's buildTransitive target to run during " +
            "`dotnet build` and drop a marker file — it didn't, which means " +
            "the attack precondition for this test no longer holds.");

        var markerContent = File.ReadAllText(markerPath);
        Assert.Contains("supply-chain-poc executed", markerContent);
    }

    [Fact]
    public void DefensiveApp_with_source_mapping_refuses_to_restore_the_same_package()
    {
        var csprojPath = Path.Combine(_lab.DefensiveAppDir, "DefensiveApp.csproj");
        var original = File.ReadAllText(csprojPath);

        // Simulate the same incident as the victim: something adds a
        // dependency on EvilPkg.Benign. No --source override, no shortcuts —
        // this is the same PackageReference shape a real PR would add.
        // CPM forbids a plain Version attribute (NU1008) — VersionOverride is
        // the documented escape hatch, so that's the shape a real PR would use.
        var withMaliciousReference = original.Replace(
            "<PackageReference Include=\"Microsoft.Extensions.Logging.Abstractions\" />",
            "<PackageReference Include=\"Microsoft.Extensions.Logging.Abstractions\" />\n" +
            "    <PackageReference Include=\"EvilPkg.Benign\" VersionOverride=\"1.0.0\" />");

        Assert.NotEqual(original, withMaliciousReference);
        File.WriteAllText(csprojPath, withMaliciousReference);

        var markerPath = Path.Combine(_lab.DefensiveAppDir, "supply-chain-poc-marker.txt");
        File.Delete(markerPath);

        try
        {
            var restore = _lab.RunDotnet(_lab.DefensiveAppDir, "restore");

            Assert.NotEqual(0, restore.ExitCode);
            Assert.Contains("NU1100", restore.CombinedOutput);
            Assert.Contains("PackageSourceMapping", restore.CombinedOutput);
            Assert.False(File.Exists(markerPath),
                "Restore was blocked, so build-time logic from the malicious " +
                "package must never have had a chance to run.");
        }
        finally
        {
            File.WriteAllText(csprojPath, original);
        }
    }
}
