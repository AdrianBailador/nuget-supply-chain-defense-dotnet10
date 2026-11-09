using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

ILogger logger = NullLogger.Instance;

logger.LogInformation(
    "DefensiveApp started. Package source mapping, a committed lock file, and " +
    "central package management are the only differences between this project " +
    "and VictimApp.");

Console.WriteLine("DefensiveApp built and ran successfully.");
