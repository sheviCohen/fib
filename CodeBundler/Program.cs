using System.CommandLine;
using System.IO;

Option<string> languageOption = new("--language", "-l")
{
    Description = "The programming language(s) to include.",
    Required = true
};
Option<string> outputOption = new("--output", "-o")
{
    Description = "The path of the output file."
};
Option<bool> noteOption = new("--note", "-n")
{
    Description = "Include each source file's name and relative path as a comment."
};
Option<bool> removeEmptyLinesOption = new("--remove-empty-lines", "-r")
{
    Description = "Remove empty and whitespace-only lines from source files."
};
Option<string> authorOption = new("--author", "-a")
{
    Description = "The author's name to include at the top of the output file."
};
Option<string> sortOption = new("--sort", "-s")
{
    Description = "Sort files by name or by type (name/type)."
};

RootCommand rootCommand = new("CodeBundler - combines source files into one file.");

Command bundleCommand = new("bundle", "Bundles source files from the current directory.");

bundleCommand.Options.Add(languageOption);
bundleCommand.Options.Add(outputOption);
bundleCommand.Options.Add(noteOption);
bundleCommand.Options.Add(removeEmptyLinesOption);
bundleCommand.Options.Add(authorOption);
bundleCommand.Options.Add(sortOption);


rootCommand.Subcommands.Add(bundleCommand);


Command createRspCommand = new(
    "create-rsp",
    "Creates a response file interactively.");

createRspCommand.Aliases.Add("cr");

createRspCommand.SetAction(parseResult =>
{
    string ReadRequiredValue(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            string? value = Console.ReadLine();

            if (value is null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();

            Console.WriteLine("A value is required.");
        }
    }

    string ReadOptionalValue(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    bool AskYesNo(string prompt)
    {
        while (true)
        {
            Console.Write($"{prompt} [y/n, default n]: ");
            string? answer = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(answer))
                return false;

            switch (answer.Trim().ToLowerInvariant())
            {
                case "y":
                case "yes":
                    return true;

                case "n":
                case "no":
                    return false;

                default:
                    Console.WriteLine("Please enter y or n.");
                    break;
            }
        }
    }

    string QuoteResponseToken(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    Console.WriteLine("=== CodeBundler response-file creator ===");

    string languages = ReadRequiredValue(
        "Programming language(s), e.g. cs,cpp or all: ");

    if (string.IsNullOrWhiteSpace(languages))
    {
        Console.Error.WriteLine("ERROR: A language is required.");
        return;
    }

    string output = ReadOptionalValue(
        "Output file path (blank = bundle.txt): ");

    bool includeNotes = AskYesNo(
        "Include source file name and relative path comments?");

    string sortMode;

    while (true)
    {
        string sortInput = ReadOptionalValue(
            "Sort mode: name or type (default name): ");

        sortMode = string.IsNullOrWhiteSpace(sortInput)
            ? "name"
            : sortInput.ToLowerInvariant();

        if (sortMode == "name" || sortMode == "type")
            break;

        Console.WriteLine("Invalid sort mode. Enter name or type.");
    }

    bool removeEmptyLines = AskYesNo(
        "Remove empty lines?");

    string author = ReadOptionalValue(
        "Author name (blank = no author): ");

    string responseFileName = ReadOptionalValue(
        "Response file name (default bundle.rsp): ");

    if (string.IsNullOrWhiteSpace(responseFileName))
        responseFileName = "bundle.rsp";

    if (!responseFileName.EndsWith(
        ".rsp", StringComparison.OrdinalIgnoreCase))
    {
        responseFileName += ".rsp";
    }

    try
    {
        string? assemblyPath =
            System.Reflection.Assembly.GetEntryAssembly()?.Location;

        if (string.IsNullOrWhiteSpace(assemblyPath) ||
            !File.Exists(assemblyPath))
        {
            Console.Error.WriteLine(
                "ERROR: Could not locate the running CodeBundler assembly.");
            return;
        }

        string depsPath = Path.ChangeExtension(
            assemblyPath, ".deps.json")!;

        string runtimeConfigPath = Path.ChangeExtension(
            assemblyPath, ".runtimeconfig.json")!;

        if (!File.Exists(depsPath) ||
            !File.Exists(runtimeConfigPath))
        {
            Console.Error.WriteLine(
                "ERROR: Dependency or runtime configuration file not found. Build the project first.");
            return;
        }

        List<string> responseLines = new()
        {
            "exec",
            "--depsfile",
            QuoteResponseToken(depsPath),
            "--runtimeconfig",
            QuoteResponseToken(runtimeConfigPath),
            QuoteResponseToken(assemblyPath),
            "bundle",
            "--language",
            QuoteResponseToken(languages),
            "--sort",
            QuoteResponseToken(sortMode)
        };

        if (!string.IsNullOrWhiteSpace(output))
        {
            responseLines.Add("--output");
            responseLines.Add(QuoteResponseToken(output));
        }

        if (includeNotes)
            responseLines.Add("--note");

        if (removeEmptyLines)
            responseLines.Add("--remove-empty-lines");

        if (!string.IsNullOrWhiteSpace(author))
        {
            responseLines.Add("--author");
            responseLines.Add(QuoteResponseToken(author));
        }

        string responsePath = Path.GetFullPath(responseFileName);

        File.WriteAllLines(responsePath, responseLines);

        Console.WriteLine();
        Console.WriteLine(
            $"Response file created successfully: {responsePath}");

        Console.WriteLine("Run it with:");
        Console.WriteLine($"dotnet \"@{responsePath}\"");
    }
    catch (Exception ex) when (
        ex is IOException ||
        ex is UnauthorizedAccessException ||
        ex is ArgumentException ||
        ex is NotSupportedException ||
        ex is System.Security.SecurityException)
    {
        Console.Error.WriteLine(
            $"ERROR: Could not create response file: {ex.Message}");
    }
});

rootCommand.Subcommands.Add(createRspCommand);

bundleCommand.SetAction(parseResult =>
{
    string? language = parseResult.GetValue(languageOption);
    string? output = parseResult.GetValue(outputOption);
    bool includeNotes = parseResult.GetValue(noteOption);
    bool removeEmptyLines = parseResult.GetValue(removeEmptyLinesOption);
    string? author = parseResult.GetValue(authorOption);
    string? sort = parseResult.GetValue(sortOption);

    try
    {
        string currentDirectory = Directory.GetCurrentDirectory();

        string outputPath = output is null
            ? Path.Combine(currentDirectory, "bundle.txt")
            : Path.GetFullPath(output);

        string sortMode = string.IsNullOrWhiteSpace(sort)
            ? "name"
            : sort.Trim().ToLowerInvariant();

        if (sortMode != "name" && sortMode != "type")
        {
            Console.Error.WriteLine(
                $"ERROR: Invalid sort mode '{sort}'. Use 'name' or 'type'.");

            return;
        }

        var supportedLanguages =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["cs"] = new[] { ".cs" },
                ["c"] = new[] { ".c", ".h" },
                ["cpp"] = new[] { ".cpp", ".cc", ".cxx", ".h", ".hpp", ".hh", ".hxx" },
                ["java"] = new[] { ".java" },
                ["py"] = new[] { ".py" },
                ["js"] = new[] { ".js", ".jsx" },
                ["ts"] = new[] { ".ts", ".tsx" },
                ["go"] = new[] { ".go" },
                ["rs"] = new[] { ".rs" },
                ["php"] = new[] { ".php" },
                ["rb"] = new[] { ".rb" }
            };

        string[] requestedLanguages = (language ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (requestedLanguages.Length == 0)
        {
            Console.Error.WriteLine("ERROR: Please specify a language.");
            return;
        }

        bool includeAll = requestedLanguages.Contains(
            "all", StringComparer.OrdinalIgnoreCase);

        string[] unknownLanguages = requestedLanguages
            .Where(name =>
                !string.Equals(name, "all", StringComparison.OrdinalIgnoreCase) &&
                !supportedLanguages.ContainsKey(name))
            .ToArray();

        if (unknownLanguages.Length > 0)
        {
            Console.Error.WriteLine(
                $"ERROR: Unsupported language(s): {string.Join(", ", unknownLanguages)}");

            Console.Error.WriteLine(
                $"Supported languages: {string.Join(", ", supportedLanguages.Keys.OrderBy(name => name))}, all");

            return;
        }

        HashSet<string> extensions = includeAll
            ? supportedLanguages.Values
                .SelectMany(ext => ext)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : requestedLanguages
                .SelectMany(name => supportedLanguages[name])
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var excludedDirectories = new HashSet<string>(
            new[] { "bin", "obj", "Debug", "Release" },
            StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> EnumerateSourceFiles(string directory)
        {
            foreach (string file in Directory.EnumerateFiles(directory))
            {
                yield return file;
            }

            foreach (string subdirectory in Directory.EnumerateDirectories(directory))
            {
                if (excludedDirectories.Contains(Path.GetFileName(subdirectory)))
                {
                    continue;
                }

                foreach (string file in EnumerateSourceFiles(subdirectory))
                {
                    yield return file;
                }
            }
        }

        HashSet<string> allSupportedExtensions = supportedLanguages.Values
            .SelectMany(ext => ext)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(outputPath) &&
            allSupportedExtensions.Contains(Path.GetExtension(outputPath)))
        {
            Console.Error.WriteLine(
                "ERROR: The output path points to an existing source-code file. Choose a different output path.");

            return;
        }

        IEnumerable<string> matchingSourceFiles =
            EnumerateSourceFiles(currentDirectory)
                .Where(file =>
                    !string.Equals(
                        Path.GetFullPath(file),
                        outputPath,
                        StringComparison.OrdinalIgnoreCase))
                .Where(file =>
                    extensions.Contains(Path.GetExtension(file)));

        string[] sourceFiles;

        if (sortMode == "name")
        {
            sourceFiles = matchingSourceFiles
                .OrderBy(
                    file => Path.GetFileName(file),
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    file => Path.GetRelativePath(currentDirectory, file),
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        else
        {
            sourceFiles = matchingSourceFiles
                .OrderBy(
                    file => Path.GetExtension(file),
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    file => Path.GetFileName(file),
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    file => Path.GetRelativePath(currentDirectory, file),
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        using (StreamWriter writer = new(outputPath, false))
        {
            if (!string.IsNullOrWhiteSpace(author))
            {
                string authorCommentPrefix =
                    requestedLanguages.All(name =>
                        name.Equals("py", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("rb", StringComparison.OrdinalIgnoreCase))
                        ? "#"
                        : "//";

                writer.WriteLine($"{authorCommentPrefix} Author: {author.Trim()}");
                writer.WriteLine();
            }
            for (int i = 0; i < sourceFiles.Length; i++)
            {
                string fileContent = File.ReadAllText(sourceFiles[i]);
                if (removeEmptyLines)
                {
                    fileContent = string.Join(
                        Environment.NewLine,
                        fileContent
                            .Split(
                                new[] { "\r\n", "\n", "\r" },
                                StringSplitOptions.None)
                            .Where(line => !string.IsNullOrWhiteSpace(line)));
                }
                if (includeNotes)
                {
                    string relativePath = Path.GetRelativePath(
                        currentDirectory, sourceFiles[i]);

                    string fileName = Path.GetFileName(sourceFiles[i]);
                    string extension = Path.GetExtension(sourceFiles[i]);

                    string commentPrefix =
                        extension.Equals(".py", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".rb", StringComparison.OrdinalIgnoreCase)
                            ? "#"
                            : "//";

                    writer.WriteLine(
                        $"{commentPrefix} Source file: {fileName} | Relative path: {relativePath}");
                }
                if (fileContent.Length > 0)
                {
                    writer.Write(fileContent);

                    if (!fileContent.EndsWith('\n') &&
                        !fileContent.EndsWith('\r'))
                    {
                        writer.WriteLine();
                    }
                }

                if (i < sourceFiles.Length - 1)
                {
                    writer.WriteLine();
                }
            }
        }

        Console.WriteLine(
            $"Selected language(s): {string.Join(", ", requestedLanguages)}");

        Console.WriteLine($"Files found: {sourceFiles.Length}");

        foreach (string file in sourceFiles)
        {
            Console.WriteLine(
                $" - {Path.GetRelativePath(currentDirectory, file)}");
        }

        Console.WriteLine($"Output file created successfully: {outputPath}");
    }
    catch (Exception ex) when (
        ex is IOException ||
        ex is UnauthorizedAccessException ||
        ex is ArgumentException ||
        ex is NotSupportedException ||
        ex is System.Security.SecurityException)
    {
        Console.Error.WriteLine(
        $"ERROR: Could not process files: {ex.Message}");
    }
});

rootCommand.Parse(args).Invoke();
