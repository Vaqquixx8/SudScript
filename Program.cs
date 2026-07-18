namespace SudScript;

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
			return;
		}

		string command = args[0];

		switch (command)
		{
			case "run":
				RunProject();
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
@"
// Main.sud
func main()
{
	say(""Hello World!"")
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
}