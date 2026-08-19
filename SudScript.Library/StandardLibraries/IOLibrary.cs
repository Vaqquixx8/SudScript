namespace SudScript;

public static class IOLibrary
{
	public static StructDeclaration Create()
	{
		return new StructDeclaration(
			"IO",
			new List<StructFieldDeclaration>(),
			new List<FunctionDeclaration>
			{
				new NativeFunctionDeclaration(
					"consoleWrite",
					args =>
					{
						foreach(Value value in args)
						{
							Console.Write(Interpreter.ToText(value));
						}

						return new VoidValue();
					}),

				new NativeFunctionDeclaration(
					"consoleWriteLine",
					args =>
					{
						foreach(Value value in args)
						{
							Console.Write(Interpreter.ToText(value));
						}

						Console.WriteLine();

						return new VoidValue();
					}),

				new NativeFunctionDeclaration(
					"consoleClear",
					args =>
					{
						Console.Clear();
						return new VoidValue();
					}),

				new NativeFunctionDeclaration(
					"consoleShowCursor",
					args =>
					{
						Console.CursorVisible = args[0];
						return new VoidValue();
					})
			});
	}
}
