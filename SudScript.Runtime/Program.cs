namespace SudScript.Runtime;

using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using SudScript;

public class Program
{
	static void Main(string[] args)
	{
		if (args.Length == 0)
		{
			Console.WriteLine("Usage: sud <command>");
			Console.WriteLine("Commands:");
			Console.WriteLine("  new <name>   Create a new Sud project");
			Console.WriteLine("  run     Run the current Sud project");
			Console.WriteLine("  build     Build the current Sud project");
			return;
		}

		string command = args[0];

		switch (command)
		{
			case "run":
				RunProject();
				break;

			case "build":
				BuildProject();
				break;

			case "new":
				NewProject(args);
				break;

			default:
				Console.WriteLine($"Unknown command: {command}");
				break;
		}
	}

	static void RunProject()
	{
		string manifestPath = FindManifest(
			Directory.GetCurrentDirectory());

		Manifest manifest = Manifest.Load(manifestPath);

		string projectRoot = Path.GetDirectoryName(manifestPath)!;

		string testScript = File.ReadAllText(
			Path.Combine(projectRoot, manifest.Entry!));

		Lexer lexer = new Lexer(testScript);
		Parser parser = new Parser(lexer.Tokenize());

		ProgramNode program = parser.ParseProgram();

		Interpreter interpreter = new();

		interpreter.SetModulesDirectory(Path.Combine(projectRoot, manifest.Modules!));
		interpreter.SetLibrariesDirectory(Path.Combine(projectRoot, manifest.Libraries!));

		interpreter.Initialize(program);
		interpreter.Execute();
	}

	static void NewProject(string[] args)
	{
		if (args.Length < 2)
		{
			Console.WriteLine("Usage: sud new <project-name>");
			return;
		}

		string projectName = args[1];

		string projectRoot = Path.Combine(
			Directory.GetCurrentDirectory(),
			projectName);

		if (Directory.Exists(projectRoot))
		{
			Console.WriteLine($"Project '{projectName}' already exists.");
			return;
		}

		// Create directories
		Directory.CreateDirectory(projectRoot);

		string srcDirectory = Path.Combine(
			projectRoot,
			"src");

		Directory.CreateDirectory(srcDirectory);

		// Create Modules Directory
		string modulesDirectory = Path.Combine(
			srcDirectory,
			"Modules");

		Directory.CreateDirectory(modulesDirectory);

		// Create Libraries Directory
		string librariesDirectory = Path.Combine(
			projectRoot,
			"Libraries");

		Directory.CreateDirectory(librariesDirectory);

		// Create sud.manifest
		string manifest =
	@$"
project = {projectName}
entry = ""src/Main.sud""
modules = ""src/Modules""
libraries = ""Libraries""
";

		File.WriteAllText(
			Path.Combine(projectRoot, "sud.manifest"),
			manifest.Trim());

		string mainScript =
@"need Sud:IO

let io = IO{}

func main()
{
	io:consoleWriteLine(""Hello World!"")
}
";

		File.WriteAllText(
			Path.Combine(srcDirectory, "Main.sud"),
			mainScript);

		Console.WriteLine($"Created Sud project '{projectName}'.");
	}

	public static string FindManifest(string startPath)
	{
		DirectoryInfo? dir;

		if (File.Exists(startPath))
		{
			dir = new DirectoryInfo(Path.GetDirectoryName(startPath)!);
		}
		else
		{
			dir = new DirectoryInfo(startPath);
		}

		while (dir != null)
		{
			string manifest = Path.Combine(dir.FullName, "sud.manifest");

			if (File.Exists(manifest))
			{
				return manifest;
			}

			dir = dir.Parent;
		}

		throw new FileNotFoundException("Could not locate sud.manifest.");
	}

	static void BuildProject()
	{
		string manifestPath = FindManifest(
			Directory.GetCurrentDirectory());

		Manifest manifest = Manifest.Load(manifestPath);

		string projectRoot = Path.GetDirectoryName(manifestPath)!;

		if (string.IsNullOrWhiteSpace(manifest.Project))
		{
			throw new Exception("Manifest is missing 'project'.");
		}

		if (string.IsNullOrWhiteSpace(manifest.Entry))
		{
			throw new Exception("Manifest is missing 'entry'.");
		}

		if (string.IsNullOrWhiteSpace(manifest.Modules))
		{
			throw new Exception("Manifest is missing 'modules'.");
		}

		if (string.IsNullOrWhiteSpace(manifest.Libraries))
		{
			throw new Exception("Manifest is missing 'libraries'.");
		}

		string entryPath = Path.GetFullPath(
			Path.Combine(projectRoot, manifest.Entry));

		string modulesPath = Path.GetFullPath(
			Path.Combine(projectRoot, manifest.Modules));

		string librariesPath = Path.GetFullPath(
			Path.Combine(projectRoot, manifest.Libraries));

		// ------------------------------------------------------------
		// Validate entry source
		// ------------------------------------------------------------

		if (!File.Exists(entryPath))
		{
			throw new FileNotFoundException(
				$"Entry file '{entryPath}' does not exist.");
		}

		string source = File.ReadAllText(entryPath);

		var lexer = new Lexer(source);
		var parser = new Parser(lexer.Tokenize());
		var program = parser.ParseProgram();

		// ------------------------------------------------------------
		// Build directories
		// ------------------------------------------------------------

		string buildDir = Path.Combine(
			projectRoot,
			"build");

		string genDir = Path.Combine(
			buildDir,
			"gen");

		string outputDir = Path.Combine(
			buildDir,
			"standalone");

		Directory.CreateDirectory(genDir);
		Directory.CreateDirectory(outputDir);

		// ------------------------------------------------------------
		// Collect files that need to be embedded
		//
		// These are stored as bytes so that both .sud files and
		// external .dll files can be embedded safely.
		// ------------------------------------------------------------

		var embeddedFiles = new Dictionary<string, byte[]>(
			StringComparer.Ordinal);

		// Entry script
		string entryRelative =
			Path.GetRelativePath(
				projectRoot,
				entryPath)
			.Replace('\\', '/');

		embeddedFiles[entryRelative] =
			Encoding.UTF8.GetBytes(source);

		// ------------------------------------------------------------
		// Collect modules
		// ------------------------------------------------------------

		if (Directory.Exists(modulesPath))
		{
			foreach (string file in Directory.EnumerateFiles(
				modulesPath,
				"*.sud",
				SearchOption.AllDirectories))
			{
				string relative =
					Path.GetRelativePath(
						projectRoot,
						file)
					.Replace('\\', '/');

				embeddedFiles[relative] =
					File.ReadAllBytes(file);
			}
		}

		// ------------------------------------------------------------
		// Collect external libraries
		//
		// Everything in Libraries/**/*.dll gets embedded.
		// This also allows a library to have dependency DLLs placed
		// in the same directory.
		// ------------------------------------------------------------

		if (Directory.Exists(librariesPath))
		{
			foreach (string file in Directory.EnumerateFiles(
				librariesPath,
				"*.dll",
				SearchOption.AllDirectories))
			{
				string relative =
					Path.GetRelativePath(
						projectRoot,
						file)
					.Replace('\\', '/');

				embeddedFiles[relative] =
					File.ReadAllBytes(file);
			}
		}

		// ------------------------------------------------------------
		// Generate host Program.cs
		// ------------------------------------------------------------

		var sb = new StringBuilder();

		sb.AppendLine("using System;");
		sb.AppendLine("using System.Collections.Generic;");
		sb.AppendLine("using System.IO;");
		sb.AppendLine("using SudScript;");
		sb.AppendLine();
		sb.AppendLine("class Program");
		sb.AppendLine("{");
		sb.AppendLine("    static void Main()");
		sb.AppendLine("    {");

		// Embedded files
		sb.AppendLine(
			"        var files = new Dictionary<string, string>");
		sb.AppendLine("        {");

		foreach (var kv in embeddedFiles)
		{
			string key = kv.Key;

			string b64 =
				Convert.ToBase64String(kv.Value);

			string escapedKey =
				key.Replace("\\", "\\\\")
				   .Replace("\"", "\\\"");

			sb.AppendLine(
				$"            [\"{escapedKey}\"] = \"{b64}\",");
		}

		sb.AppendLine("        };");
		sb.AppendLine();

		// ------------------------------------------------------------
		// Create temporary runtime directory
		// ------------------------------------------------------------

		sb.AppendLine(
			"        string tempDir = Path.Combine(");
		sb.AppendLine(
			"            Path.GetTempPath(),");
		sb.AppendLine(
			"            \"sud_\" + Guid.NewGuid().ToString(\"N\"));");

		sb.AppendLine(
			"        Directory.CreateDirectory(tempDir);");

		sb.AppendLine("        try");
		sb.AppendLine("        {");

		// ------------------------------------------------------------
		// Extract embedded files
		// ------------------------------------------------------------

		sb.AppendLine(
			"            foreach (var kv in files)");
		sb.AppendLine("            {");

		sb.AppendLine(
			"                string fullPath = Path.Combine(");
		sb.AppendLine(
			"                    tempDir,");
		sb.AppendLine(
			"                    kv.Key.Replace('/', Path.DirectorySeparatorChar));");

		sb.AppendLine(
			"                string? directory = Path.GetDirectoryName(fullPath);");

		sb.AppendLine(
			"                if (directory != null)");
		sb.AppendLine(
			"                {");
		sb.AppendLine(
			"                    Directory.CreateDirectory(directory);");
		sb.AppendLine(
			"                }");

		sb.AppendLine(
			"                File.WriteAllBytes(");
		sb.AppendLine(
			"                    fullPath,");
		sb.AppendLine(
			"                    Convert.FromBase64String(kv.Value));");

		sb.AppendLine("            }");

		sb.AppendLine();

		// ------------------------------------------------------------
		// Entry file
		// ------------------------------------------------------------

		string entryPathParts =
			string.Join(
				", ",
				entryRelative
					.Split('/', StringSplitOptions.RemoveEmptyEntries)
					.Select(part =>
						"\"" +
						part.Replace("\\", "\\\\")
							.Replace("\"", "\\\"") +
						"\""));

		sb.AppendLine(
			$"            string entryFile = Path.Combine(" +
			$"tempDir, {entryPathParts});");

		sb.AppendLine(
			"            string src = File.ReadAllText(entryFile);");

		sb.AppendLine(
			"            var lexer = new Lexer(src);");

		sb.AppendLine(
			"            var parser = new Parser(lexer.Tokenize());");

		sb.AppendLine(
			"            var program = parser.ParseProgram();");

		sb.AppendLine(
			"            var interpreter = new Interpreter();");

		sb.AppendLine();

		// ------------------------------------------------------------
		// Modules directory
		// ------------------------------------------------------------

		string modulesRelative =
			Path.GetRelativePath(
				projectRoot,
				modulesPath)
			.Replace('\\', '/');

		string modulesDirParts =
			string.Join(
				", ",
				modulesRelative
					.Split('/', StringSplitOptions.RemoveEmptyEntries)
					.Select(part =>
						"\"" +
						part.Replace("\\", "\\\\")
							.Replace("\"", "\\\"") +
						"\""));

		sb.AppendLine(
			$"            string modulesDir = Path.Combine(" +
			$"tempDir, {modulesDirParts});");

		sb.AppendLine(
			"            Directory.CreateDirectory(modulesDir);");

		sb.AppendLine(
			"            interpreter.SetModulesDirectory(modulesDir);");

		//sb.AppendLine(
		//	"            interpreter.SetLibrariesDirectory(Path.Combine(projectRoot, manifest.Libraries!));");

		sb.AppendLine();

		// ------------------------------------------------------------
		// Libraries directory
		// ------------------------------------------------------------

		string librariesRelative =
			Path.GetRelativePath(
				projectRoot,
				librariesPath)
			.Replace('\\', '/');

		string librariesDirParts =
			string.Join(
				", ",
				librariesRelative
					.Split('/', StringSplitOptions.RemoveEmptyEntries)
					.Select(part =>
						"\"" +
						part.Replace("\\", "\\\\")
							.Replace("\"", "\\\"") +
						"\""));

		sb.AppendLine(
			$"            string librariesDir = Path.Combine(" +
			$"tempDir, {librariesDirParts});");

		sb.AppendLine(
			"            Directory.CreateDirectory(librariesDir);");

		sb.AppendLine(
			"            interpreter.SetLibrariesDirectory(librariesDir);");

		sb.AppendLine();

		// ------------------------------------------------------------
		// Run interpreter
		// ------------------------------------------------------------

		sb.AppendLine(
			"            interpreter.Initialize(program);");

		sb.AppendLine(
			"            interpreter.Execute();");

		// ------------------------------------------------------------
		// Cleanup
		// ------------------------------------------------------------

		sb.AppendLine("        }");
		sb.AppendLine("        finally");
		sb.AppendLine("        {");
		sb.AppendLine(
			"            try");
		sb.AppendLine(
			"            {");
		sb.AppendLine(
			"                Directory.Delete(tempDir, true);");
		sb.AppendLine(
			"            }");
		sb.AppendLine(
			"            catch");
		sb.AppendLine(
			"            {");
		sb.AppendLine(
			"                // Ignore cleanup failures.");
		sb.AppendLine(
			"            }");
		sb.AppendLine("        }");

		sb.AppendLine("    }");
		sb.AppendLine("}");

		// ------------------------------------------------------------
		// Write generated host
		// ------------------------------------------------------------

		string generatedProgramPath =
			Path.Combine(
				genDir,
				"Program.cs");

		File.WriteAllText(
			generatedProgramPath,
			sb.ToString());

		// Locate SudScript runtime assembly
		string runtimeAssemblyPath = typeof(Interpreter).Assembly.Location;

		if (string.IsNullOrEmpty(runtimeAssemblyPath))
		{
			runtimeAssemblyPath = System.Environment.ProcessPath ?? string.Empty;
		}

		if (string.IsNullOrEmpty(runtimeAssemblyPath))
		{
			throw new Exception("Could not locate the SudScript runtime assembly.");
		}

		// ------------------------------------------------------------
		// Generate temporary .csproj
		// ------------------------------------------------------------

		string csproj = $@"
	<Project Sdk=""Microsoft.NET.Sdk"">
		<PropertyGroup>
			<OutputType>Exe</OutputType>
			<TargetFramework>net9.0</TargetFramework>
			<ImplicitUsings>enable</ImplicitUsings>
			<Nullable>enable</Nullable>
			<AssemblyName>{manifest.Project}</AssemblyName>
		</PropertyGroup>

		<ItemGroup>
			<Reference Include=""SudScript.Library"">
				<HintPath>{runtimeAssemblyPath}</HintPath>
			</Reference>
		</ItemGroup>
	</Project>";

		string csprojPath =
			Path.Combine(
				genDir,
				"SudScript.csproj");

		File.WriteAllText(
			csprojPath,
			csproj);

		// ------------------------------------------------------------
		// Publish
		// ------------------------------------------------------------

		string rid =
			RuntimeInformation.RuntimeIdentifier;

		var psi = new ProcessStartInfo
		{
			FileName = "dotnet",
			WorkingDirectory = projectRoot,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false
		};

		psi.ArgumentList.Add("publish");
		psi.ArgumentList.Add(csprojPath);
		psi.ArgumentList.Add("-c");
		psi.ArgumentList.Add("Release");
		psi.ArgumentList.Add("-r");
		psi.ArgumentList.Add(rid);
		psi.ArgumentList.Add("--self-contained");
		psi.ArgumentList.Add("true");
		psi.ArgumentList.Add("-p:PublishSingleFile=true");
		psi.ArgumentList.Add("-o");
		psi.ArgumentList.Add(outputDir);

		using var process = Process.Start(psi)
			?? throw new Exception(
				"Failed to start dotnet publish.");

		string stdout =
			process.StandardOutput.ReadToEnd();

		string stderr =
			process.StandardError.ReadToEnd();

		process.WaitForExit();

		if (process.ExitCode != 0)
		{
			throw new Exception(
				$"dotnet publish failed.\n\n{stdout}\n{stderr}");
		}

		// ------------------------------------------------------------
		// Verify output
		// ------------------------------------------------------------

		string exeName =
			manifest.Project +
			(OperatingSystem.IsWindows() ? ".exe" : "");

		string finalExe =
			Path.Combine(
				outputDir,
				exeName);

		if (!File.Exists(finalExe))
		{
			throw new Exception(
				$"Publish did not produce expected file: {finalExe}");
		}

		// Copy final executable to build/<project>
		string targetExe =
			Path.Combine(
				buildDir,
				exeName);

		File.Copy(
			finalExe,
			targetExe,
			true);

		Console.WriteLine($"Built {targetExe}");
	}
}
