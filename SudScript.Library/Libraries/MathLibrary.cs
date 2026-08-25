namespace SudScript;

public sealed class MathLibrary : ILibrary
{
	public string Name => "Sud:Math";

	public StructDeclaration Create()
	{
		return new StructDeclaration(
			"MathState",
			new List<StructFieldDeclaration>(),
			new List<FunctionDeclaration>
			{
				// Basic
				new NativeFunctionDeclaration(
					"abs",
					args => new NumberValue(MathF.Abs(float.Parse(Interpreter.ToText(args[0])))),
					1),
				new NativeFunctionDeclaration(
					"floor",
					args => new NumberValue(MathF.Floor(float.Parse(Interpreter.ToText(args[0])))),
					1),
				new NativeFunctionDeclaration(
					"ceil",
					args => new NumberValue(MathF.Ceiling(float.Parse(Interpreter.ToText(args[0])))),
					1),
				new NativeFunctionDeclaration(
					"round",
					args => new NumberValue(MathF.Round(float.Parse(Interpreter.ToText(args[0])))),
					1),
				new NativeFunctionDeclaration(
					"sign",
					args => new NumberValue(MathF.Sign(float.Parse(Interpreter.ToText(args[0])))),
					1),

				// Min / Max / Clamp
				new NativeFunctionDeclaration(
					"min",
					args => new NumberValue(
						MathF.Min(
							float.Parse(Interpreter.ToText(args[0])),
							float.Parse(Interpreter.ToText(args[1]))
						)
					),
					2),

				new NativeFunctionDeclaration(
					"max",
					args => new NumberValue(
						MathF.Max(
							float.Parse(Interpreter.ToText(args[0])),
							float.Parse(Interpreter.ToText(args[1]))
						)
					),
					2),
				new NativeFunctionDeclaration(
					"clamp",
					args =>
					{
						float value = float.Parse(Interpreter.ToText(args[0]));
						float min = float.Parse(Interpreter.ToText(args[1]));
						float max = float.Parse(Interpreter.ToText(args[2]));

						return new NumberValue(Math.Clamp(value, min, max));
					},
					3),

				// Interpolation
				new NativeFunctionDeclaration(
					"lerp",
					args =>
					{
						float a = float.Parse(Interpreter.ToText(args[0]));
						float b = float.Parse(Interpreter.ToText(args[1]));
						float t = float.Parse(Interpreter.ToText(args[2]));

						return new NumberValue(a + (b - a) * t);
					},
					3),

				// Exponents
				new NativeFunctionDeclaration(
					"sqrt",
					args => new NumberValue(MathF.Sqrt(float.Parse(Interpreter.ToText(args[0])))),
					1),

				// Trigonometry
				new NativeFunctionDeclaration(
					"cos",
					args => new NumberValue(MathF.Cos(float.Parse(Interpreter.ToText(args[0])))),
					1),
				new NativeFunctionDeclaration(
					"sin",
					args => new NumberValue(MathF.Sin(float.Parse(Interpreter.ToText(args[0])))),
					1),
				new NativeFunctionDeclaration(
					"tan",
					args => new NumberValue(MathF.Tan(float.Parse(Interpreter.ToText(args[0])))),
					1),
				new NativeFunctionDeclaration(
					"acos",
					args => new NumberValue(MathF.Acos(float.Parse(Interpreter.ToText(args[0])))),
					1),
				new NativeFunctionDeclaration(
					"asin",
					args => new NumberValue(MathF.Asin(float.Parse(Interpreter.ToText(args[0])))),
					1),
				new NativeFunctionDeclaration(
					"atan",
					args => new NumberValue(MathF.Atan(float.Parse(Interpreter.ToText(args[0])))),
					1),
				new NativeFunctionDeclaration(
					"atan2",
					args => new NumberValue(
						MathF.Atan2(
							float.Parse(Interpreter.ToText(args[0])),
							float.Parse(Interpreter.ToText(args[1]))
						)
					),
					2),

				// Logarithms
				new NativeFunctionDeclaration(
					"log",
					args => new NumberValue(MathF.Log(float.Parse(Interpreter.ToText(args[0])))),
					1),
				new NativeFunctionDeclaration(
					"log10",
					args => new NumberValue(MathF.Log10(float.Parse(Interpreter.ToText(args[0])))),
					1),

				// Constants
				new NativeFunctionDeclaration(
					"pi",
					args => new NumberValue(MathF.PI),
					0),
				new NativeFunctionDeclaration(
					"e",
					args => new NumberValue(MathF.E),
					0),
			});
	}
}
