namespace SudScript;

using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

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

		interpreter.SetModulesDirectory(
			Path.Combine(projectRoot, manifest.Modules!));

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

		// Create sud.manifest
		string manifest =
	@$"
project = {projectName}
entry = ""src/Main.sud""
modules = ""src/Modules""
";

		File.WriteAllText(
			Path.Combine(projectRoot, "sud.manifest"),
			manifest.Trim());


		string mainScript =
@"func main()
{
	let helloWorld = ""Hello World!""
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
	    string manifestPath = FindManifest(Directory.GetCurrentDirectory());
	    Manifest manifest = Manifest.Load(manifestPath);
	    string projectRoot = Path.GetDirectoryName(manifestPath)!;

	    string entryPath = Path.Combine(projectRoot, manifest.Entry!);
	    string modulesPath = Path.Combine(projectRoot, manifest.Modules!);

	    // Validate the entry source before doing anything else.
	    string source = File.ReadAllText(entryPath);
	    var lexer = new Lexer(source);
	    var parser = new Parser(lexer.Tokenize());
	    var program = parser.ParseProgram();

	    string buildDir = Path.Combine(projectRoot, "build");
	    string genDir = Path.Combine(buildDir, "gen");
	    Directory.CreateDirectory(genDir);

	    // Collect all .sud files that must be embedded.
	    var embeddedFiles = new Dictionary<string, string>();

	    string entryRelative = Path.GetRelativePath(projectRoot, entryPath)
	        .Replace('\\', '/');
	    embeddedFiles[entryRelative] = source;

	    if (Directory.Exists(modulesPath))
	    {
	        foreach (string file in Directory.EnumerateFiles(
	                     modulesPath, "*.sud", SearchOption.AllDirectories))
	        {
	            string relative = Path.GetRelativePath(projectRoot, file)
	                .Replace('\\', '/');
	            embeddedFiles[relative] = File.ReadAllText(file);
	        }
	    }

	    // Generate the host Program.cs.
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
	    sb.AppendLine("        var files = new Dictionary<string, string>");
	    sb.AppendLine("        {");

	    foreach (var kv in embeddedFiles)
	    {
	        string key = kv.Key;
	        string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(kv.Value));
	        sb.AppendLine($"            [\"{key}\"] = \"{b64}\",");
	    }

	    sb.AppendLine("        };");
	    sb.AppendLine();
	    sb.AppendLine("        string tempDir = Path.Combine(Path.GetTempPath(), \"sud_\" + Guid.NewGuid().ToString(\"N\"));");
	    sb.AppendLine("        Directory.CreateDirectory(tempDir);");
	    sb.AppendLine("        try");
	    sb.AppendLine("        {");
	    sb.AppendLine("            foreach (var kv in files)");
	    sb.AppendLine("            {");
	    sb.AppendLine("                string fullPath = Path.Combine(tempDir, kv.Key.Replace('/', Path.DirectorySeparatorChar));");
	    sb.AppendLine("                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);");
	    sb.AppendLine("                File.WriteAllBytes(fullPath, Convert.FromBase64String(kv.Value));");
	    sb.AppendLine("            }");
	    sb.AppendLine();

	    string entryPathParts = string.Join(", ",
	        entryRelative.Split('/').Select(part => "\"" + part + "\""));
	    sb.AppendLine($"        string entryFile = Path.Combine(tempDir, {entryPathParts});");
	    sb.AppendLine("        string src = File.ReadAllText(entryFile);");
	    sb.AppendLine("        var lexer = new Lexer(src);");
	    sb.AppendLine("        var parser = new Parser(lexer.Tokenize());");
	    sb.AppendLine("        var program = parser.ParseProgram();");
	    sb.AppendLine("        var interpreter = new Interpreter();");
	    sb.AppendLine();

	    string modulesRelative = Path.GetRelativePath(projectRoot, modulesPath)
	        .Replace('\\', '/');
	    string modulesDirParts = string.Join(", ",
	        modulesRelative.Split('/').Select(part => "\"" + part + "\""));
	    sb.AppendLine($"        string modulesDir = Path.Combine(tempDir, {modulesDirParts});");
	    sb.AppendLine("        Directory.CreateDirectory(modulesDir);");
	    sb.AppendLine("        interpreter.SetModulesDirectory(modulesDir);");
	    sb.AppendLine("        interpreter.Initialize(program);");
	    sb.AppendLine("        interpreter.Execute();");
	    sb.AppendLine("        }");
	    sb.AppendLine("        finally");
	    sb.AppendLine("        {");
	    sb.AppendLine("            try { Directory.Delete(tempDir, true); } catch {}");
	    sb.AppendLine("        }");
	    sb.AppendLine("    }");
	    sb.AppendLine("}");

	    File.WriteAllText(Path.Combine(genDir, "Program.cs"), sb.ToString());

	    // Generate a .csproj that references the SudScript runtime assembly.
	    string runtimeAssemblyPath = typeof(Interpreter).Assembly.Location;

	    // If the assembly is loaded from a single-file bundle, Location may be empty.
	    // Fallback to System.Environment.ProcessPath (the executable itself).
	    if (string.IsNullOrEmpty(runtimeAssemblyPath))
	    {
	        runtimeAssemblyPath = System.Environment.ProcessPath ?? string.Empty;
	    }

	    if (string.IsNullOrEmpty(runtimeAssemblyPath))
	    {
	        throw new Exception("Could not locate the SudScript runtime assembly.");
	    }

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
	    <Reference Include=""SudScript"">
	      <HintPath>{runtimeAssemblyPath}</HintPath>
	    </Reference>
	  </ItemGroup>
		</Project>";

	    File.WriteAllText(Path.Combine(genDir, "SudScript.csproj"), csproj);

	    // 4. Publish as a self-contained single-file executable.
	    string rid = RuntimeInformation.RuntimeIdentifier;
	    string outputDir = Path.Combine(buildDir, "standalone");
	    Directory.CreateDirectory(outputDir);

	    var psi = new ProcessStartInfo
	    {
	        FileName = "dotnet",
	        WorkingDirectory = projectRoot,
	        RedirectStandardOutput = true,
	        RedirectStandardError = true,
	        UseShellExecute = false
	    };

	    psi.ArgumentList.Add("publish");
	    psi.ArgumentList.Add(Path.Combine(genDir, "SudScript.csproj"));
	    psi.ArgumentList.Add("-c");
	    psi.ArgumentList.Add("Release");
	    psi.ArgumentList.Add("-r");
	    psi.ArgumentList.Add(rid);
	    psi.ArgumentList.Add("--self-contained");
	    psi.ArgumentList.Add("true");
	    psi.ArgumentList.Add("-p:PublishSingleFile=true");
	    psi.ArgumentList.Add("-o");
	    psi.ArgumentList.Add(outputDir);

	    using var process = Process.Start(psi)!;
	    string stdout = process.StandardOutput.ReadToEnd();
		string stderr = process.StandardError.ReadToEnd();

		process.WaitForExit();

	    string exeName = manifest.Project + (OperatingSystem.IsWindows() ? ".exe" : "");
	    string finalExe = Path.Combine(outputDir, exeName);

	    if (!File.Exists(finalExe))
	    {
	        throw new Exception($"Publish did not produce expected file: {finalExe}");
	    }

	    string targetExe = Path.Combine(buildDir, exeName);
	    File.Copy(finalExe, targetExe, true);

	    Console.WriteLine($"Built {targetExe}");
	}
}
