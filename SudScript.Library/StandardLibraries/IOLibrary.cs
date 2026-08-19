namespace SudScript;

public static class IOLibrary
{
	public static StructDeclaration Create()
	{
		return new StructDeclaration(
			"IOState",
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
					},
					0),
				new NativeFunctionDeclaration(
					"consoleShowCursor",
					args =>
					{
						Console.CursorVisible = bool.Parse(Interpreter.ToText(args[0]));
						return new VoidValue();
					},
					1),
				new NativeFunctionDeclaration(
					"consoleReadKey",
					args =>
					{
						if (!Console.KeyAvailable)
						{
							return new NullValue();
						}

						ConsoleKeyInfo key = Console.ReadKey(true);
						return new StringValue(key.KeyChar.ToString());
					},
					0)
			});
	}
}
