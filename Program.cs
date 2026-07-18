namespace SudScript;

public class Program
{
	static void Main(string[] args)
	{
		if (args.Length == 0)
		{
			Console.WriteLine("Usage: sud <command>");
			Console.WriteLine("Commands:");
			Console.WriteLine("  run     Run the current Sud project");
			return;
		}

		string command = args[0];

		switch (command)
		{
			case "run":
				RunProject();
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

	public static string FindManifest(string startPath)
	{
		DirectoryInfo dir;

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