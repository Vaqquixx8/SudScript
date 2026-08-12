namespace SudScript;

public class ModuleLoader(string _baseDirectory)
{
	string baseDirectory = _baseDirectory;
	HashSet<string> loadedModules = new HashSet<string>();

	readonly Dictionary<string, string> groupIndex = new Dictionary<string, string>();

	public void BuildGroupIndex()
	{
		groupIndex.Clear();
		loadedModules.Clear();

		foreach (string filePath in Directory.EnumerateFiles(baseDirectory, "*.sud", SearchOption.AllDirectories))
		{
			string source = File.ReadAllText(filePath);

			Lexer lexer = new Lexer(source);
			List<Token> tokens = lexer.Tokenize();

			Parser parser = new Parser(tokens);
			ProgramNode program = parser.ParseProgram();

			if (program.Body.Count == 0)
			{
				continue;
			}

			if (program.Body[0] is not GroupDeclaration group)
			{
				continue;
			}

			if (groupIndex.TryGetValue(group.Name, out string? existingPath))
			{
				throw new Exception(
					$"Duplicate group declaration '{group.Name}':\n" +
					$"  {existingPath}\n" +
					$"  {filePath}"
				);
			}

			groupIndex[group.Name] = filePath;
		}
	}

	public List<StructDeclaration> LoadModule(List<string> path)
	{
		string groupName = string.Join(":", path);

		List<StructDeclaration> structs = new List<StructDeclaration>();

		foreach (string matchedGroup in FindGroups(groupName))
		{
			structs.AddRange(LoadGroup(matchedGroup));
		}

		return structs;
	}

	IEnumerable<string> FindGroups(string groupName)
	{
		string prefix = groupName + ":";

		return groupIndex
			.Keys
			.Where(name => name == groupName || name.StartsWith(prefix, StringComparison.Ordinal))
			.OrderBy(name => name);
	}

	List<StructDeclaration> LoadGroup(string groupName)
	{
		// Prevent loading the same group multiple times.
		if (!loadedModules.Add(groupName))
		{
			return new List<StructDeclaration>();
		}

		if (!groupIndex.TryGetValue(groupName, out string? filePath))
		{
			throw new Exception($"Group '{groupName}' was not found.");
		}

		string source = File.ReadAllText(filePath);

		Lexer lexer = new Lexer(source);
		List<Token> tokens = lexer.Tokenize();

		Parser parser = new Parser(tokens);
		ProgramNode program = parser.ParseProgram();

		List<StructDeclaration> structs = new List<StructDeclaration>();

		foreach (Statement statement in program.Body)
		{
			if (statement is NeedImportStatement import)
			{
				structs.AddRange(LoadModule(import.Path));
			}
			else if (statement is StructDeclaration structDecl)
			{
				structs.Add(structDecl);
			}
		}

		return structs;
	}

	string ResolveFilePath(List<string> path)
	{
		string relative = string.Join("/", path) + ".sud";
		string fullPath = Path.GetFullPath(Path.Combine(baseDirectory, relative));

		if (!File.Exists(fullPath))
		{
			throw new FileNotFoundException(
				$"Module file not found: {fullPath}\n" +
				$"Base directory: {baseDirectory}\n" +
				$"Relative path: {relative}"
			);
		}

		return fullPath;
	}
}