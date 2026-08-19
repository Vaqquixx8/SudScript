namespace SudScript;

public static class StandardLibraries
{
	public static readonly Dictionary<string, Func<StructDeclaration>> All =
		new(StringComparer.Ordinal)
		{
			["Sud:IO"] = IOLibrary.Create,
			["Sud:Time"] = TimerLibrary.Create,
			["Sud:Math"] = MathLibrary.Create,
		};

	public static bool Exists(string name)
	{
		return All.ContainsKey(name);
	}

	public static StructDeclaration CreateStruct(string name)
	{
		if(!All.TryGetValue(name, out var factory))
		{
			throw new Exception($"Standard library '{name}' does not exist.");
		}

		return factory();
	}
}
