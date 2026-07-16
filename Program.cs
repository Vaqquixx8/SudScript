namespace SudScript;

public class Program
{
	static readonly string fibonacciTestPath = "/mnt/HardDrive/Projects/SudScript/ScriptTests/Fibonacci.sud";
	static readonly string structTestPath = "/mnt/HardDrive/Projects/SudScript/ScriptTests/StructTest.sud";
	static void Main()
	{
		string testScript = File.ReadAllText(structTestPath);

		Lexer lexer = new Lexer(testScript);
		List<Token> tokens = lexer.Tokenize();

		Parser parser = new Parser(tokens);
		ProgramNode prgm = parser.ParseProgram();

		Interpreter interpreter = new Interpreter();

		interpreter.Initialize(prgm);

		interpreter.Execute();
	}
}