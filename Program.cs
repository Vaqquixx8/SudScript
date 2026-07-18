namespace SudScript;

public class Program
{
	static readonly string testPath = "/mnt/HardDrive/Projects/SudScript/src/ImportTest.sud";

	static void Main()
	{
		string testScript = File.ReadAllText(testPath);

		Lexer lexer = new Lexer(testScript);
		List<Token> tokens = lexer.Tokenize();

		Parser parser = new Parser(tokens);
		ProgramNode prgm = parser.ParseProgram();

		Interpreter interpreter = new Interpreter();

		// Set the modules directory before initializing
		interpreter.SetModulesDirectory("/mnt/HardDrive/Projects/SudScript/src/modules");

		interpreter.Initialize(prgm);
		interpreter.Execute();
	}
}