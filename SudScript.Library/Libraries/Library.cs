namespace SudScript;

public interface ILibrary
{
	string Name {get;}
	StructDeclaration Create();
}
