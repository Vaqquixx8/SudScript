namespace SudScript;

public sealed class IOLibrary : ILibrary
{
	public string Name => "Sud:IO";

	public StructDeclaration Create()
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
						return new StringValue(key.Key.ToString());
					},
					0),
				new NativeFunctionDeclaration(
					"consoleResetColor",
					args =>
					{
						Console.ResetColor();
						return new VoidValue();
					},
					0),
				new NativeFunctionDeclaration(
				    "consoleSetTextColor",
				    args =>
				    {
				        string colorName = Interpreter.ToText(args[0]);

				        if (!Enum.TryParse<ConsoleColor>(colorName, true, out var color))
				        {
				            throw new Exception($"{colorName} is not a valid Console color.");
				        }

				        Console.ForegroundColor = color;
				        return new VoidValue();
				    },
				    1),
				new NativeFunctionDeclaration(
				    "consoleSetBackgroundColor",
				    args =>
				    {
				        string colorName = Interpreter.ToText(args[0]);

				        if (!Enum.TryParse<ConsoleColor>(colorName, true, out var color))
				        {
				            throw new Exception($"{colorName} is not a valid Console color.");
				        }

				        Console.BackgroundColor = color;
				        return new VoidValue();
				    },
				    1)
			}
		);
	}
}
