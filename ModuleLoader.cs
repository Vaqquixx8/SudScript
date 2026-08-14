namespace SudScript;
public class ModuleLoader(string _baseDirectory)
{
	string baseDirectory = _baseDirectory;

	readonly Dictionary<string, string> groupIndex = new Dictionary<string, string>();
	readonly Dictionary<string, Environment> moduleEnvironments = new Dictionary<string, Environment>();

	public void BuildGroupIndex()
	{
		groupIndex.Clear();
		moduleEnvironments.Clear();

		foreach(string filePath in Directory.EnumerateFiles(baseDirectory, "*.sud", SearchOption.AllDirectories))
		{
			string source = File.ReadAllText(filePath);

			Lexer lexer = new Lexer(source);
			List<Token> tokens = lexer.Tokenize();

			Parser parser = new Parser(tokens);
			ProgramNode program = parser.ParseProgram();

			if(program.Body.Count == 0)
			{
				continue;
			}

			if(program.Body[0] is not GroupDeclaration group)
			{
				continue;
			}

			if(groupIndex.TryGetValue(group.Name, out string? existingPath))
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

	public List<Environment> LoadModule(List<string> path)
	{
		string groupName = string.Join(":", path);

		var environments = new List<Environment>();

		foreach(string matchedGroup in FindGroups(groupName))
		{
			environments.Add(LoadGroupEnvironment(matchedGroup));
		}

		return environments;
	}

	IEnumerable<string> FindGroups(string groupName)
	{
		string prefix = groupName + ":";

		return groupIndex
			.Keys
			.Where(name => name == groupName || name.StartsWith(prefix, StringComparison.Ordinal))
			.OrderBy(name => name);
	}

	Environment LoadGroupEnvironment(string groupName)
	{
		if(moduleEnvironments.TryGetValue(groupName, out var cached))
		{
			return cached;
		}

		if(!groupIndex.TryGetValue(groupName, out string? filePath))
		{
			throw new Exception($"Group '{groupName}' was not found.");
		}

		var moduleEnv = new Environment();
		moduleEnvironments[groupName] = moduleEnv;

		string source = File.ReadAllText(filePath);

		Lexer lexer = new Lexer(source);
		List<Token> tokens = lexer.Tokenize();

		Parser parser = new Parser(tokens);
		ProgramNode program = parser.ParseProgram();

		foreach(Statement statement in program.Body)
		{
			if(statement is NeedImportStatement import)
			{
				foreach(var importedEnv in LoadModule(import.Path))
				{
					moduleEnv.AddImport(importedEnv);
				}
			}
			else if(statement is StructDeclaration structDecl)
			{
				if(moduleEnv.TryGetOwnStruct(structDecl.Name, out _))
				{
					throw new Exception(
						$"Struct '{structDecl.Name}' is already defined in group '{groupName}'."
					);
				}

				moduleEnv.DefineStruct(structDecl.Name, structDecl);
			}
		}

		return moduleEnv;
	}
}
