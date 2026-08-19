using System.Diagnostics;
using System.Threading;

namespace SudScript;

public static class TimerLibrary
{
	public static StructDeclaration Create()
	{
		Stopwatch stopwatch = new Stopwatch();

		return new StructDeclaration(
			"TimerState",
			new List<StructFieldDeclaration>(),
			new List<FunctionDeclaration>
			{
				new NativeFunctionDeclaration(
					"wait",
					args =>
					{
						float seconds = float.Parse(Interpreter.ToText(args[0]));

						if (seconds > 0)
						{
							Thread.Sleep(TimeSpan.FromSeconds(seconds));
						}

						return new VoidValue();
					},
					1),
				new NativeFunctionDeclaration(
					"currentTime",
					args =>
					{
						return new NumberValue((float)stopwatch.Elapsed.TotalSeconds);
					},
					0),
				new NativeFunctionDeclaration(
					"start",
					args =>
					{
						stopwatch.Start();
						return new VoidValue();
					},
					0),
				new NativeFunctionDeclaration(
					"stop",
					args =>
					{
						stopwatch.Stop();
						return new NumberValue((float)stopwatch.Elapsed.TotalSeconds);
					},
					0),
				new NativeFunctionDeclaration(
					"reset",
					args =>
					{
						stopwatch.Reset();
						return new VoidValue();
					},
					0),
				new NativeFunctionDeclaration(
					"restart",
					args =>
					{
						stopwatch.Restart();
						return new VoidValue();
					},
					0)
			});
	}
}
