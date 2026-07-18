namespace SudScript;

public class ModuleLoader(string _baseDirectory)
{
	string baseDirectory = _baseDirectory;
	HashSet<string> loadedModules = new HashSet<string>();

	public List<StructDeclaration> LoadModule(List<string> path)
	{
		string moduleKey = string.Join(":", path);

		if (!loadedModules.Add(moduleKey))
		{
			return new List<StructDeclaration>();
		}

		string filePath = ResolveFilePath(path);

		string source = File.ReadAllText(filePath);

		Lexer lexer = new Lexer(source);
		List<Token> tokens = lexer.Tokenize();

		Parser parser = new Parser(tokens);
		ProgramNode prgm = parser.ParseProgram();

		List<StructDeclaration> structs = new List<StructDeclaration>();
		foreach(Statement statement in prgm.Body)
		{
			if(statement is NeedImportStatement import)
			{
				var imported = LoadModule(import.Path);
				structs.AddRange(imported);
			}
			else if(statement is StructDeclaration structDecl)
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