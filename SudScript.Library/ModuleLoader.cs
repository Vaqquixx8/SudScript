namespace SudScript;

public class ModuleLoader(string _baseDirectory)
{
	string baseDirectory = _baseDirectory;

	readonly Dictionary<string, List<string>> groupIndex = new Dictionary<string, List<string>>();
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

			if(!groupIndex.TryGetValue(group.Name, out var paths))
			{
				paths = new List<string>();
				groupIndex[group.Name] = paths;
			}

			paths.Add(filePath);
		}
	}

	public List<Environment> LoadModule(List<string> path)
	{
		string groupName = string.Join(":", path);

		var environments = new List<Environment>();

		foreach(string filePath in FindGroups(groupName))
		{
			environments.Add(LoadGroupEnvironment(filePath));
		}

		return environments;
	}

	IEnumerable<string> FindGroups(string groupName)
	{
		string prefix = groupName + ":";

		return groupIndex
			.Where(pair => pair.Key == groupName || pair.Key.StartsWith(prefix, StringComparison.Ordinal))
			.OrderBy(pair => pair.Key)
			.SelectMany(pair => pair.Value);
	}

	Environment LoadGroupEnvironment(string filePath)
	{
		if(moduleEnvironments.TryGetValue(filePath, out var cached))
		{
			return cached;
		}

		var moduleEnv = new Environment();
		moduleEnvironments[filePath] = moduleEnv;

		string source = File.ReadAllText(filePath);

		Lexer lexer = new Lexer(source);
		List<Token> tokens = lexer.Tokenize();

		Parser parser = new Parser(tokens);
		ProgramNode program = parser.ParseProgram();

		string groupName = program.Body.FirstOrDefault() is GroupDeclaration group ? group.Name : filePath;

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
					throw new Exception($"Struct '{structDecl.Name}' is already defined in group '{groupName}'.");
				}

				moduleEnv.DefineStruct(structDecl.Name, structDecl);
			}
		}

		return moduleEnv;
	}
}
