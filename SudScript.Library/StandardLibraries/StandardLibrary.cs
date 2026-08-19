namespace SudScript;

public sealed class StandardLibrary(string name, Func<StructDeclaration> createStruct)
{
	public string Name { get; } = name;

	public StructDeclaration CreateStruct()
	{
		return createStruct();
	}
}
