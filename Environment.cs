namespace SudScript;

public class Environment(Environment? parent = null)
{
	readonly Dictionary<string, Value> values = new Dictionary<string, Value>();
	readonly Dictionary<string, FunctionDeclaration> functions = new Dictionary<string, FunctionDeclaration>();
	readonly Dictionary<string, StructDeclaration> structs = new Dictionary<string, StructDeclaration>();

	readonly List<Environment> imports = new List<Environment>();

	public Environment? Parent { get; } = parent;

	public void AddImport(Environment imported)
	{
		imports.Add(imported);
	}

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

		if (Parent != null && Parent.TryGetVariable(name, out value))
		{
			return true;
		}

		foreach (var import in imports)
		{
			if (import.TryGetOwnVariable(name, out value))
			{
				return true;
			}
		}

		value = default!;
		return false;
	}

	public bool TryGetOwnVariable(string name, out Value value)
	{
		return values.TryGetValue(name, out value!);
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

		if (Parent != null && Parent.TryGetFunction(name, out function))
		{
			return true;
		}

		foreach (var import in imports)
		{
			if (import.TryGetOwnFunction(name, out function))
			{
				return true;
			}
		}

		function = default!;
		return false;
	}

	public bool TryGetOwnFunction(string name, out FunctionDeclaration function)
	{
		return functions.TryGetValue(name, out function!);
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

		if (Parent != null && Parent.TryGetStruct(name, out declaration))
		{
			return true;
		}

		foreach (var import in imports)
		{
			if (import.TryGetOwnStruct(name, out declaration))
			{
				return true;
			}
		}

		declaration = default!;
		return false;
	}

	public bool TryGetOwnStruct(string name, out StructDeclaration declaration)
	{
		return structs.TryGetValue(name, out declaration!);
	}
}
