namespace SudScript;

public class Environment(Environment? parent = null)
{
	readonly Dictionary<string, Value> values = new Dictionary<string, Value>();
	readonly Dictionary<string, FunctionDeclaration> functions = new Dictionary<string, FunctionDeclaration>();
	readonly Dictionary<string, StructDeclaration> structs = new Dictionary<string, StructDeclaration>();

	public Environment? Parent { get; } = parent;

	public void DefineVariable(string name, Value value)
	{
		values[name] = value;
	}

	public Value GetVariable(string name)
	{
		if (TryGetVariable(name, out var value))
		{
			return value;
		}

		throw new Exception($"Undefined variable '{name}'");
	}

	public bool TryGetVariable(string name, out Value value)
	{
		if (values.TryGetValue(name, out value!))
		{
			return true;
		}

		return Parent?.TryGetVariable(name, out value) ?? false;
	}


	public void AssignVariable(string name, Value value)
	{
		if (values.ContainsKey(name))
		{
			values[name] = value;
			return;
		}

		if (Parent != null)
		{
			Parent.AssignVariable(name, value);
			return;
		}

		throw new Exception($"Undefined variable '{name}'");
	}


	public void DefineFunction(string name, FunctionDeclaration function)
	{
		functions[name] = function;
	}

	public FunctionDeclaration GetFunction(string name)
	{
		if (TryGetFunction(name, out var function))
		{
			return function;
		}

		throw new Exception($"Undefined function '{name}'");
	}

	public bool TryGetFunction(string name, out FunctionDeclaration function)
	{
		if (functions.TryGetValue(name, out function!))
		{
			return true;
		}

		return Parent?.TryGetFunction(name, out function) ?? false;
	}


	public void DefineStruct(string name, StructDeclaration declaration)
	{
		structs[name] = declaration;
	}

	public StructDeclaration GetStruct(string name)
	{
		if (TryGetStruct(name, out var declaration))
		{
			return declaration;
		}

		throw new Exception($"Undefined struct '{name}'");
	}

	public bool TryGetStruct(string name, out StructDeclaration declaration)
	{
		if (structs.TryGetValue(name, out declaration!))
		{
			return true;
		}

		return Parent?.TryGetStruct(name, out declaration) ?? false;
	}
}