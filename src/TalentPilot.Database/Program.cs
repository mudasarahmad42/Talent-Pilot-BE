using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

return await SqlScriptRunnerApp.RunAsync(args);

internal static class SqlScriptRunnerApp
{
    private static readonly Regex GoLineRegex = new(@"^\s*GO\s*(?:--.*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = RunnerOptions.Parse(args);
            if (options.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            var scriptsRoot = ResolveScriptsRoot(options.ScriptsRoot);
            var connection = ResolveConnectionString(options.ConnectionString);
            var scripts = FindScripts(scriptsRoot);

            Console.WriteLine("TalentPilot.Database SQL runner");
            Console.WriteLine($"Scripts root: {scriptsRoot}");
            Console.WriteLine($"Connection source: {connection.Source}");
            Console.WriteLine($"Connection target: {DescribeConnectionTarget(connection.Value)}");
            Console.WriteLine($"Scripts found: {scripts.Count}");
            Console.WriteLine();

            await using var sqlConnection = new SqlConnection(connection.Value);
            await sqlConnection.OpenAsync();

            foreach (var script in scripts)
            {
                await ExecuteScriptAsync(sqlConnection, script);
            }

            Console.WriteLine();
            Console.WriteLine("Database scripts completed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Database script run failed.");
            Console.Error.WriteLine(ex.Message);

            var sqlException = ex as SqlException ?? ex.InnerException as SqlException;
            if (sqlException is not null)
            {
                foreach (SqlError error in sqlException.Errors)
                {
                    Console.Error.WriteLine($"SQL {error.Number}, line {error.LineNumber}: {error.Message}");
                }
            }

            return 1;
        }
    }

    private static async Task ExecuteScriptAsync(SqlConnection connection, SqlScript script)
    {
        Console.WriteLine($"Running {script.RelativePath}");

        var sql = await File.ReadAllTextAsync(script.FullPath);
        var batches = SplitBatches(sql);

        for (var index = 0; index < batches.Count; index++)
        {
            var batchNumber = index + 1;
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = batches[index];
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed while running {script.RelativePath}, batch {batchNumber}.", ex);
            }
        }

        Console.WriteLine($"Completed {script.RelativePath} ({batches.Count} batches)");
    }

    private static List<string> SplitBatches(string sql)
    {
        var batches = new List<string>();
        var current = new StringBuilder();

        using var reader = new StringReader(sql);
        while (reader.ReadLine() is { } line)
        {
            if (GoLineRegex.IsMatch(line))
            {
                AddBatchIfNotEmpty(batches, current);
                continue;
            }

            current.AppendLine(line);
        }

        AddBatchIfNotEmpty(batches, current);
        return batches;
    }

    private static void AddBatchIfNotEmpty(List<string> batches, StringBuilder current)
    {
        var text = current.ToString();
        current.Clear();

        if (!string.IsNullOrWhiteSpace(text))
        {
            batches.Add(text);
        }
    }

    private static IReadOnlyList<SqlScript> FindScripts(string scriptsRoot)
    {
        string[] folders = ["schema", "migrations", "seed", "stored-procedures"];
        var scripts = new List<SqlScript>();

        foreach (var folder in folders)
        {
            var folderPath = Path.Combine(scriptsRoot, folder);
            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"Required script folder was not found: {folderPath}");
            }

            var files = Directory
                .EnumerateFiles(folderPath, "*.sql", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            scripts.AddRange(files.Select(path => new SqlScript(path, Path.GetRelativePath(scriptsRoot, path))));
        }

        if (scripts.Count == 0)
        {
            throw new InvalidOperationException($"No SQL scripts were found under {scriptsRoot}.");
        }

        return scripts;
    }

    private static string ResolveScriptsRoot(string? explicitScriptsRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitScriptsRoot))
        {
            var fullPath = Path.GetFullPath(explicitScriptsRoot);
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException($"The provided scripts path does not exist: {fullPath}");
            }

            return fullPath;
        }

        foreach (var start in CandidateStartDirectories())
        {
            foreach (var directory in WalkUp(start))
            {
                var scripts = Path.Combine(directory, "scripts");
                if (Directory.Exists(scripts))
                {
                    return scripts;
                }
            }
        }

        throw new DirectoryNotFoundException("Could not find the scripts folder. Run from Application Code/Backend Code or pass --scripts <path>.");
    }

    private static ConnectionStringResult ResolveConnectionString(string? explicitConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(explicitConnectionString))
        {
            return new ConnectionStringResult(explicitConnectionString, "--connection");
        }

        var fromEnvironment = Environment.GetEnvironmentVariable("ConnectionStrings__TalentPilot");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return new ConnectionStringResult(fromEnvironment, "ConnectionStrings__TalentPilot");
        }

        foreach (var appsettingsPath in FindAppSettingsFiles())
        {
            var value = TryReadConnectionString(appsettingsPath);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return new ConnectionStringResult(value, appsettingsPath);
            }
        }

        throw new InvalidOperationException("Connection string missing. Pass --connection, set ConnectionStrings__TalentPilot, or add ConnectionStrings:TalentPilot to appsettings.");
    }

    private static string? TryReadConnectionString(string appsettingsPath)
    {
        try
        {
            using var stream = File.OpenRead(appsettingsPath);
            using var document = JsonDocument.Parse(stream);

            if (!document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings))
            {
                return null;
            }

            if (!connectionStrings.TryGetProperty("TalentPilot", out var talentPilot))
            {
                return null;
            }

            return talentPilot.ValueKind == JsonValueKind.String ? talentPilot.GetString() : null;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Could not parse appsettings file: {appsettingsPath}", ex);
        }
    }

    private static IEnumerable<string> FindAppSettingsFiles()
    {
        var relativePaths = new[]
        {
            "appsettings.Development.json",
            "appsettings.json",
            Path.Combine("src", "TalentPilot.Database", "appsettings.Development.json"),
            Path.Combine("src", "TalentPilot.Database", "appsettings.json"),
            Path.Combine("src", "TalentPilot.Api", "appsettings.Development.json"),
            Path.Combine("src", "TalentPilot.Api", "appsettings.json"),
            Path.Combine("src", "TalentPilot.Worker", "appsettings.Development.json"),
            Path.Combine("src", "TalentPilot.Worker", "appsettings.json")
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var start in CandidateStartDirectories())
        {
            foreach (var directory in WalkUp(start))
            {
                foreach (var relativePath in relativePaths)
                {
                    var path = Path.GetFullPath(Path.Combine(directory, relativePath));
                    if (seen.Add(path) && File.Exists(path))
                    {
                        yield return path;
                    }
                }
            }
        }
    }

    private static IEnumerable<string> CandidateStartDirectories()
    {
        yield return Directory.GetCurrentDirectory();
        yield return AppContext.BaseDirectory;
    }

    private static IEnumerable<string> WalkUp(string start)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(start));

        while (directory is not null)
        {
            yield return directory.FullName;
            directory = directory.Parent;
        }
    }

    private static string DescribeConnectionTarget(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var database = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "(default database)" : builder.InitialCatalog;
            return $"{builder.DataSource} / {database}";
        }
        catch
        {
            return "(connection string target could not be parsed)";
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("TalentPilot.Database");
        Console.WriteLine();
        Console.WriteLine("Runs SQL scripts in schema, migrations, seed, stored-procedures order.");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project src/TalentPilot.Database -- --connection \"<connection string>\"");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --connection <value>  SQL Server connection string. Overrides env/appsettings.");
        Console.WriteLine("  --scripts <path>      Optional path to the scripts folder.");
        Console.WriteLine("  --help                Show this help.");
    }
}

internal sealed record RunnerOptions(string? ConnectionString, string? ScriptsRoot, bool ShowHelp)
{
    public static RunnerOptions Parse(string[] args)
    {
        string? connectionString = null;
        string? scriptsRoot = null;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-h", StringComparison.OrdinalIgnoreCase))
            {
                showHelp = true;
                continue;
            }

            if (TryReadOptionValue(args, ref i, "--connection", arg, out var connection))
            {
                connectionString = connection;
                continue;
            }

            if (TryReadOptionValue(args, ref i, "--scripts", arg, out var scripts))
            {
                scriptsRoot = scripts;
                continue;
            }

            throw new ArgumentException($"Unknown argument: {arg}");
        }

        return new RunnerOptions(connectionString, scriptsRoot, showHelp);
    }

    private static bool TryReadOptionValue(string[] args, ref int index, string optionName, string currentArg, out string? value)
    {
        value = null;

        if (currentArg.StartsWith(optionName + "=", StringComparison.OrdinalIgnoreCase))
        {
            value = currentArg[(optionName.Length + 1)..];
            return true;
        }

        if (!currentArg.Equals(optionName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{optionName} requires a value.");
        }

        index++;
        value = args[index];
        return true;
    }
}

internal sealed record ConnectionStringResult(string Value, string Source);

internal sealed record SqlScript(string FullPath, string RelativePath);
