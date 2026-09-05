namespace SudScript;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public class Environment(Environment? parent = null)
{
	Dictionary<string, Value>? values;
	Dictionary<string, FunctionDeclaration>? functions;
	Dictionary<string, StructDeclaration>? structs;
	List<Environment>? imports;

	public Environment? Parent { get; } = parent;

	public void AddImport(Environment imported)
	{
		(imports ??= new List<Environment>()).Add(imported);
	}

	// -------------------------------------------------------------
	// Variables
	// -------------------------------------------------------------

	public void DefineVariable(string name, Value value)
	{
		(values ??= new Dictionary<string, Value>())[name] = value;
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
		Environment? env = this;
		while (env != null)
		{
			if (env.values != null && env.values.TryGetValue(name, out value!))
			{
				return true;
			}
			env = env.Parent;
		}

		return TryGetImportedVariable(name, out value);
	}

	bool TryGetImportedVariable(string name, out Value value)
	{
		if (Parent != null && Parent.TryGetImportedVariable(name, out value))
		{
			return true;
		}

		if (imports != null)
		{
			foreach (var import in imports)
			{
				if (import.TryGetOwnVariable(name, out value!))
				{
					return true;
				}
			}
		}

		value = default!;
		return false;
	}

	public bool TryGetOwnVariable(string name, out Value value)
	{
		if (values != null && values.TryGetValue(name, out value!))
		{
			return true;
		}
		value = default!;
		return false;
	}

	public void AssignVariable(string name, Value value)
	{
		Environment? env = this;
		while (env != null)
		{
			if (env.values != null)
			{
				ref Value slot = ref CollectionsMarshal.GetValueRefOrNullRef(env.values, name);
				if (!Unsafe.IsNullRef(ref slot))
				{
					slot = value;
					return;
				}
			}
			env = env.Parent;
		}
		throw new Exception($"Undefined variable '{name}'");
	}

	// -------------------------------------------------------------
	// Functions
	// -------------------------------------------------------------

	public void DefineFunction(string name, FunctionDeclaration function)
	{
		(functions ??= new Dictionary<string, FunctionDeclaration>())[name] = function;
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
		Environment? env = this;
		while (env != null)
		{
			if (env.functions != null && env.functions.TryGetValue(name, out function!))
			{
				return true;
			}
			env = env.Parent;
		}

		return TryGetImportedFunction(name, out function);
	}

	bool TryGetImportedFunction(string name, out FunctionDeclaration function)
	{
		if (Parent != null && Parent.TryGetImportedFunction(name, out function))
		{
			return true;
		}

		if (imports != null)
		{
			foreach (var import in imports)
			{
				if (import.TryGetOwnFunction(name, out function!))
				{
					return true;
				}
			}
		}

		function = default!;
		return false;
	}

	public bool TryGetOwnFunction(string name, out FunctionDeclaration function)
	{
		if (functions != null && functions.TryGetValue(name, out function!))
		{
			return true;
		}
		function = default!;
		return false;
	}

	// -------------------------------------------------------------
	// Structs
	// -------------------------------------------------------------

	public void DefineStruct(string name, StructDeclaration declaration)
	{
		(structs ??= new Dictionary<string, StructDeclaration>())[name] = declaration;
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
		Environment? env = this;
		while (env != null)
		{
			if (env.structs != null && env.structs.TryGetValue(name, out declaration!))
			{
				return true;
			}
			env = env.Parent;
		}

		return TryGetImportedStruct(name, out declaration);
	}

	bool TryGetImportedStruct(string name, out StructDeclaration declaration)
	{
		if (Parent != null && Parent.TryGetImportedStruct(name, out declaration))
		{
			return true;
		}

		if (imports != null)
		{
			foreach (var import in imports)
			{
				if (import.TryGetOwnStruct(name, out declaration!))
				{
					return true;
				}
			}
		}

		declaration = default!;
		return false;
	}

	public bool TryGetOwnStruct(string name, out StructDeclaration declaration)
	{
		if (structs != null && structs.TryGetValue(name, out declaration!))
		{
			return true;
		}
		declaration = default!;
		return false;
	}

	public void ResetForReuse()
	{
		values?.Clear();
		functions?.Clear();
		structs?.Clear();
		imports?.Clear();
	}
}
