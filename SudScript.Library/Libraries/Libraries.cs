namespace SudScript;

public static class Libraries
{
	private static readonly Dictionary<string, ILibrary> All = new(StringComparer.Ordinal);

	static Libraries()
	{
		Register(new IOLibrary());
		Register(new TimerLibrary());
		Register(new MathLibrary());
	}

	public static void Register(ILibrary library)
	{
		if (!All.TryAdd(library.Name, library))
		{
			throw new Exception($"Library '{library.Name}' is already registered.");
		}
	}

	public static bool Exists(string name) => All.ContainsKey(name);

	public static StructDeclaration CreateStruct(string name)
	{
		if (!All.TryGetValue(name, out var library))
		{
			throw new Exception($"Library '{name}' does not exist.");
		}

		return library.Create();
	}
}
