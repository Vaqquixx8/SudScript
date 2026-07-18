namespace SudScript;

public class Program
{
	static readonly string sourcePath = "/mnt/HardDrive/Projects/SudScript/src";

	static void Main()
	{
		string testScript = File.ReadAllText(Path.Combine(sourcePath, "Main.sud"));

		Lexer lexer = new Lexer(testScript);
		List<Token> tokens = lexer.Tokenize();

		Parser parser = new Parser(tokens);
		ProgramNode prgm = parser.ParseProgram();

		Interpreter interpreter = new Interpreter();

		interpreter.SetModulesDirectory(Path.Combine(sourcePath, "Modules"));

		interpreter.Initialize(prgm);
		interpreter.Execute();
	}
}