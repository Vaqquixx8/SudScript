namespace SudScript;

using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Globalization;

public class Interpreter
{
	Environment environment = new Environment();
	ProgramNode? program;
	string modulesDirectory = null!;
	string librariesDirectory = null!;

	readonly Dictionary<(StructDeclaration Decl, string Method, bool Shared), FunctionDeclaration> methodCache = new Dictionary<(StructDeclaration, string, bool), FunctionDeclaration>();
	readonly Dictionary<string, Func<List<Value>, Value>> builtins = new Dictionary<string, Func<List<Value>, Value>>();

	public void SetModulesDirectory(string path)
	{
		modulesDirectory = path;
	}
	public void SetLibrariesDirectory(string path)
	{
		librariesDirectory = path;
	}

	public void Initialize(ProgramNode _program)
	{
		program = _program;
		environment = new Environment();
		methodCache.Clear();

		if (modulesDirectory == null)
		{
			throw new Exception($"No modules directory set.");
		}
		if(librariesDirectory == null)
		{
			throw new Exception($"No libraries directory set.");
		}

		string modulesDir = Path.GetFullPath(modulesDirectory);
		string librariesDir = Path.GetFullPath(librariesDirectory);

		ExternalLibraryLoader.LoadDirectory(librariesDir);

		var loader = new ModuleLoader(modulesDir);
		loader.BuildGroupIndex();

		foreach (var stmt in program.Body)
		{
			if (stmt is NeedImportStatement import)
			{
				try
				{
					foreach (var moduleEnv in loader.LoadModule(import.Path))
					{
						environment.AddImport(moduleEnv);
					}
				}
				catch (Exception ex)
				{
					throw new Exception($"Failed to import module '{string.Join(":", import.Path)}': {ex.Message}");
				}
			}
		}

		foreach (var stmt in program.Body)
		{
			if (stmt is StructDeclaration s)
			{
				if (environment.TryGetOwnStruct(s.Name, out _))
				{
					throw new Exception($"Struct '{s.Name}' is already defined.");
				}

				environment.DefineStruct(s.Name, s);
			}
		}

		// Register Global Functions
		foreach (Statement statement in program.Body)
		{
			if (statement is FunctionDeclaration function)
			{
				environment.DefineFunction(function.Name, function);
			}
		}

		// Register Global Variables
		foreach (Statement statement in program.Body)
		{
			if (statement is VariableDeclaration variable)
			{
				environment.DefineVariable(variable.Name, EvaluateExpression(variable.Value));
			}
		}
	}

	public void Execute()
	{
		if(program == null)
		{
			throw new Exception("Interpreter has not been initialized.");
		}

		if(!environment.TryGetFunction("main", out var main))
		{
			throw new Exception("Program entry point 'main' was not found.");
		}

		if(main is not UserFunctionDeclaration userMain)
		{
			throw new Exception("'main' must be a user-defined function.");
		}

		if(userMain.Params.Count != 0)
		{
			throw new Exception("'main' cannot have parameters.");
		}

		CallFunction(new FunctionCallExpression("main", new List<Expression>()));
	}

	ExecutionResult ExecuteStatement(Statement statement)
	{
		switch (statement)
		{
			case VariableDeclaration variable:
				ExecuteVariableDeclaration(variable);
				return ExecutionResult.None;

			case ExpressionStatement expression:
				EvaluateExpression(expression.Expression);
				return ExecutionResult.None;

			case BlockStatement block:
				return ExecuteBlock(block);

			case IfStatement ifStatement:
				return ExecuteIf(ifStatement);

			case WhileStatement whileStatement:
				return ExecuteWhile(whileStatement);

			case FunctionDeclaration function:
				ExecuteFunctionDeclaration(function);
				return ExecutionResult.None;

			case ReturnStatement ret:
				return ExecutionResult.Return(
				EvaluateExpression(ret.Value!)
			);

			case BreakStatement:
				return ExecutionResult.Break;

			case ContinueStatement:
				return ExecutionResult.Continue;

			default:
				throw new Exception($"Unknown statement {statement.GetType().Name}.");
		}
	}

	void ExecuteVariableDeclaration(VariableDeclaration declaration)
	{
		Value value = EvaluateExpression(declaration.Value);

		environment.DefineVariable(declaration.Name, value);
	}

	ExecutionResult ExecuteBlock(BlockStatement block)
	{
		Environment previous = environment;

		environment = new Environment(environment);

		try
		{
			var body = block.Body;
			for (int i = 0; i < body.Count; ++i)
			{
				var result = ExecuteStatement(body[i]);

				if (result.Type != FlowType.None)
				{
					return result;
				}
			}

			return ExecutionResult.None;
		}
		finally
		{
			environment = previous;
		}
	}

	ExecutionResult ExecuteIf(IfStatement ifStatement)
	{
		Value condition = EvaluateExpression(ifStatement.Condition);

		if(IsTruthy(condition))
		{
			return ExecuteStatement(ifStatement.ThenBlock);
		}
		else if (ifStatement.ElseBlock != null)
		{
			return ExecuteStatement(ifStatement.ElseBlock);
		}

		return ExecutionResult.None;
	}

	ExecutionResult ExecuteWhile(WhileStatement whileStatement)
	{
		Environment? loopEnvironment = null;

		while (IsTruthy(EvaluateExpression(whileStatement.Condition)))
		{
			ExecutionResult result;

			if (whileStatement.Block is BlockStatement block)
			{
				loopEnvironment ??= new Environment(environment);
				loopEnvironment.ResetForReuse();

				Environment previous = environment;
				environment = loopEnvironment;

				try
				{
					result = ExecuteStatements(block.Body);
				}
				finally
				{
					environment = previous;
				}
			}
			else
			{
				result = ExecuteStatement(whileStatement.Block);
			}

			switch (result.Type)
			{
				case FlowType.Return:
					return result;

				case FlowType.Break:
					return ExecutionResult.None;

				case FlowType.Continue:
					continue;
			}

		}
		return ExecutionResult.None;
	}

	void ExecuteFunctionDeclaration(FunctionDeclaration function)
	{
		environment.DefineFunction(function.Name, function);
	}

	List<Value> EvaluateArguments(List<Expression> args)
	{
		var result = new List<Value>(args.Count);
		for (int i = 0; i < args.Count; ++i)
		{
			result.Add(EvaluateExpression(args[i]));
		}
		return result;
	}

	Value CallFunction(FunctionCallExpression call)
	{
		if(builtins.TryGetValue(call.Name, out var builtin))
		{
			return builtin(EvaluateArguments(call.Args));
		}

		FunctionDeclaration function = environment.GetFunction(call.Name);
		List<Value> args = EvaluateArguments(call.Args);

		return Invoke(function, args, call.Name, self: null);
	}

	Value CallMethod(MethodCallExpression call)
	{
		List<Value> args = EvaluateArguments(call.Args);

		if(call.Target is IdentifierExpression id && environment.TryGetStruct(id.Name, out var decl))
		{
			return CallStructSharedMethod(decl!, call.Method, args);
		}

		Value target = EvaluateExpression(call.Target);

		return target switch
		{
			StringValue str => CallStringMethod(str, call.Method, args),
			ListValue list => CallListMethod(list, call.Method, args),
			StructInstanceValue instance => CallStructInstanceMethod(instance, call.Method, args),
			_ => throw new Exception($"{target.GetType().Name} has not method '{call.Method}'."),
		};
	}

	static Value CallStringMethod(StringValue str, string method, List<Value> args)
	{
		switch (method)
		{
			case "length":
			{
				return new NumberValue(str.Value.Length);
			}
			case "get":
			{
				int index = (int)((NumberValue)args[0]).Value;
				return new StringValue(str.Value[index].ToString());
			}
			case "contains":
			{
				string value = ((StringValue)args[0]).Value;
				return BooleanValue.Of(str.Value.Contains(value));
			}
			case "startsWith":
			{
				string value = ((StringValue)args[0]).Value;
				return BooleanValue.Of(str.Value.StartsWith(value));
			}
			case "endsWith":
			{
				string value = ((StringValue)args[0]).Value;
				return BooleanValue.Of(str.Value.EndsWith(value));
			}
			case "indexOf":
			{
				string value = ((StringValue)args[0]).Value;
				return new NumberValue(str.Value.IndexOf(value));
			}
			case "lastIndexOf":
			{
				string value = ((StringValue)args[0]).Value;
				return new NumberValue(str.Value.LastIndexOf(value));
			}
			case "substring":
			{
				int start = (int)((NumberValue)args[0]).Value;

				if (args.Count > 1)
				{
					int length = (int)((NumberValue)args[1]).Value;
					return new StringValue(str.Value.Substring(start, length));
				}

				return new StringValue(str.Value.Substring(start));
			}
			case "toUpper":
			{
				return new StringValue(str.Value.ToUpper());
			}
			case "toLower":
			{
				return new StringValue(str.Value.ToLower());
			}
			case "trim":
			{
				return new StringValue(str.Value.Trim());
			}
			case "replace":
			{
				string oldValue = ((StringValue)args[0]).Value;
				string newValue = ((StringValue)args[1]).Value;

				return new StringValue(str.Value.Replace(oldValue, newValue));
			}
			case "split":
			{
				string separator = ((StringValue)args[0]).Value;

				var parts = str.Value.Split(separator);
				var values = new List<Value>(parts.Length);
				for (int i = 0; i < parts.Length; ++i)
				{
					values.Add(new StringValue(parts[i]));
				}

				return new ListValue(values);
			}
			case "reverse":
			{
				return new StringValue(new string(str.Value.Reverse().ToArray()));
			}
			case "repeat":
			{
				int count = (int)((NumberValue)args[0]).Value;
				return new StringValue(string.Concat(Enumerable.Repeat(str.Value, count)));
			}
			default:
				throw new Exception($"Unknown string method '{method}'.");
		}
	}

	static Value CallListMethod(ListValue list, string method, List<Value> args)
	{
		switch(method)
		{
			case "get":
			{
				int index = (int)((NumberValue)args[0]).Value;
				return list.Values[index];
			}
			case "set":
			{
				int index = (int)((NumberValue)args[0]).Value;
				list.Values[index] = args[1];
				return args[1];
			}
			case "add":
			{
				list.Values.Add(args[0]);
				return new VoidValue();
			}
			case "remove":
			{
				int index = (int)((NumberValue)args[0]).Value;
				Value removed = list.Values[index];
				list.Values.RemoveAt(index);
				return removed;
			}
			case "length":
			{
				return new NumberValue(list.Values.Count);
			}
			case "contains":
			{
				return BooleanValue.Of(list.Values.Any(v => AreEqual(v, args[0])));
			}
			case "fill":
			{
				int size = list.Values.Count;

				if(args.Count > 1)
				{
					size = (int)((NumberValue)args[1]).Value;
					list.Values.Clear();

					for(int i = 0; i < size; ++i)
					{
						list.Values.Add(args[0]);
					}
				}
				else
				{
					for(int i = 0; i < size; ++i)
					{
						list.Values[i] = args[0];
					}
				}
				return new VoidValue();
			}
			case "clear":
			{
				list.Values.Clear();
				return new VoidValue();
			}
			case "indexOf":
			{
				for (int i = 0; i < list.Values.Count; ++i)
				{
					if (AreEqual(list.Values[i], args[0]))
					{
						return new NumberValue(i);
					}
				}

				return new NumberValue(-1);
			}

			default:
				throw new Exception($"Unknown list method '{method}'.");
		}
	}

	FunctionDeclaration ResolveMethod(StructDeclaration decl, string method, bool shared)
	{
		var key = (decl, method, shared);

		if (methodCache.TryGetValue(key, out var cached))
		{
			return cached;
		}

		FunctionDeclaration? found = null;
		foreach (var m in decl.Methods)
		{
			if (m.IsShared == shared && m.Name == method)
			{
				found = m;
				break;
			}
		}

		if (found == null)
		{
			throw new Exception(shared
				? $"Struct '{decl.Name}' has no shared method '{method}'."
				: $"Struct '{decl.Name}' has no instance method '{method}'.");
		}

		methodCache[key] = found;
		return found;
	}

	Value CallStructInstanceMethod(StructInstanceValue instance, string method, List<Value> args)
	{
		StructDeclaration decl = environment.GetStruct(instance.TypeName);
		FunctionDeclaration function = ResolveMethod(decl, method, shared: false);

		return Invoke(function, args, method, self: instance);
	}

	Value CallStructSharedMethod(StructDeclaration decl, string method, List<Value> args)
	{
		FunctionDeclaration function = ResolveMethod(decl, method, shared: true);

		return Invoke(function, args, method, self: null);
	}

	Value Invoke(FunctionDeclaration function, List<Value> args, string name, StructInstanceValue? self)
	{
		if (function is NativeFunctionDeclaration native)
		{
			if (native.ArgumentCount.HasValue && native.ArgumentCount.Value != args.Count)
			{
				throw new Exception(
					$"Function '{name}' expects " +
					$"{native.ArgumentCount.Value} arguments, " +
					$"but got {args.Count} instead.");
			}
			return native.Implementation(args);
		}

		if (function is not UserFunctionDeclaration userFunction)
		{
			throw new Exception($"Unknown function type '{function.GetType().Name}'.");
		}

		if (userFunction.Params.Count != args.Count)
		{
			throw new Exception(
				$"Function '{name}' expects " +
				$"{userFunction.Params.Count} arguments, " +
				$"but got {args.Count} instead.");
		}

		Environment previous = environment;

		environment = new Environment(environment);

		try
		{
			if (self != null)
			{
				environment.DefineVariable("self", self);
			}

			for (int i = 0; i < userFunction.Params.Count; ++i)
			{
				environment.DefineVariable(userFunction.Params[i], args[i]);
			}

			var result = ExecuteStatements(userFunction.Block.Body);

			if (result.Type == FlowType.Return)
			{
				return result.Value!;
			}

			return new VoidValue();
		}
		finally
		{
			environment = previous;
		}
	}

	ExecutionResult ExecuteStatements(List<Statement> statements)
	{
		for (int i = 0; i < statements.Count; ++i)
		{
			var result = ExecuteStatement(statements[i]);

			if (result.Type != FlowType.None)
			{
				return result;
			}
		}

		return ExecutionResult.None;
	}

	Value EvaluateExpression(Expression expression)
	{
		switch (expression)
		{
			case NumericExpression number:
				return new NumberValue(number.Value);

			case StringExpression str:
				return new StringValue(str.Value);

			case BooleanExpression boolean:
				return BooleanValue.Of(boolean.Value);

			case VoidExpression:
				return new VoidValue();
			case NullExpression:
				return new NullValue();

			case ListExpression list:
			{
				var values = new List<Value>(list.Elements.Count);
				for (int i = 0; i < list.Elements.Count; ++i)
				{
					values.Add(EvaluateExpression(list.Elements[i]));
				}
				return new ListValue(values);
			}

			case IdentifierExpression identifier:
				return environment.GetVariable(identifier.Name);

			case UnaryExpression unary:
			{
				Value Right = EvaluateExpression(unary.Right);

				return unary.Op switch
				{
					TokenType.Minus when Right is NumberValue n => new NumberValue(-n.Value),
					TokenType.Exclamation when Right is BooleanValue n => BooleanValue.Of(!IsTruthy(Right)),
					_ => throw new Exception($"Unknown unary operator {unary.Op}."),
				};
			}

			case BinaryExpression binary:
			{
				if(binary.Op == TokenType.AndAnd)
				{
					Value left = EvaluateExpression(binary.Left);

					if (!IsTruthy(left))
					{
						return BooleanValue.Of(false);
					}
					return BooleanValue.Of(IsTruthy(EvaluateExpression(binary.Right)));
				}

				if (binary.Op == TokenType.OrOr)
				{
					Value left = EvaluateExpression(binary.Left);

					if (IsTruthy(left))
					{
						return BooleanValue.Of(true);
					}

					return BooleanValue.Of(IsTruthy(EvaluateExpression(binary.Right)));
				}

				Value leftValue = EvaluateExpression(binary.Left);
				Value rightValue = EvaluateExpression(binary.Right);

				return EvaluateBinary(leftValue, binary.Op, rightValue);
			}

			case AssignmentExpression assignment:
			{
				Value value;

				if (assignment.Operator == TokenType.Equals)
				{
					value = EvaluateExpression(assignment.Value);
				}
				else
				{
					Value currentValue = EvaluateExpression(assignment.Target);
					Value rightValue = EvaluateExpression(assignment.Value);

					TokenType binaryOperator = assignment.Operator switch
					{
						TokenType.PlusEquals => TokenType.Plus,
						TokenType.MinusEquals => TokenType.Minus,
						TokenType.TimesEquals => TokenType.Star,
						TokenType.DivideEquals => TokenType.Slash,

						_ => throw new Exception($"Unknown assignment operator {assignment.Operator}.")
					};

					value = EvaluateBinary(currentValue, binaryOperator, rightValue);
				}

				switch (assignment.Target)
				{
					case IdentifierExpression id:
						environment.AssignVariable(id.Name, value);
						return value;

					case MemberAccessExpression member:
					{
						Value target = EvaluateExpression(member.Target);

						if (target is StructInstanceValue instance)
						{
							instance.Fields[member.Member] = value;
							return value;
						}

						throw new Exception($"{target.GetType().Name} does not support member assignment.");
					}

					default:
						throw new Exception("Invalid assignment target.");
				}
			}

			case FunctionCallExpression call:
				return CallFunction(call);

			case MethodCallExpression method:
				return CallMethod(method);

			case StructLiteralExpression literal:
			{
				StructDeclaration decl = environment.GetStruct(literal.Name);

				Dictionary<string, Value> fields = new Dictionary<string, Value>(decl.Fields.Count + literal.Fields.Count);

				foreach(var field in decl.Fields)
				{
					fields[field.Name] = EvaluateExpression(field.DefaultValue);
				}

				foreach (var pair in literal.Fields)
				{
					fields[pair.Name] = EvaluateExpression(pair.Value);
				}

				return new StructInstanceValue(literal.Name, fields);
			}

			case MemberAccessExpression member:
			{
				Value target = EvaluateExpression(member.Target);

				return target switch
				{
					StructInstanceValue instance when instance.Fields.TryGetValue(member.Member, out var val) => val,
					_ => throw new Exception($"{target.GetType().Name} has no member '{member.Member}'.")
				};
			}

			case PostfixExpression postfix:
			{
				Value oldValue = EvaluateExpression(postfix.Target);

				if(oldValue is not NumberValue number)
				{
					throw new Exception($"Operator {postfix.Operator} requires a number.");
				}
				float newValue = postfix.Operator switch
				{
					TokenType.Increment => number.Value + 1,
					TokenType.Decrement => number.Value - 1,

					_ => throw new Exception($"Unknown postfix operator {postfix.Operator}.")
				};
				Value result = new NumberValue(newValue);
				switch(postfix.Target)
				{
					case IdentifierExpression id:
						environment.AssignVariable(id.Name, result);
						break;
					case MemberAccessExpression member:
					{
						Value target = EvaluateExpression(member.Target);
						if(target is StructInstanceValue instance)
						{
							instance.Fields[member.Member] = result;
							break;
						}
						throw new Exception($"{target.GetType().Name} does not support member assignment.");
					}
					default:
						throw new Exception("Invalid increment/decrement target.");
				}
				return oldValue;
			}

			default:
				throw new Exception($"Unknown expression {expression.GetType().Name}.");
		}
	}

	static Value EvaluateBinary(Value left, TokenType op, Value right)
	{
		return (op, left, right) switch
		{
			// Numeric arithmetic
			(TokenType.Plus, NumberValue a, NumberValue b) => new NumberValue(a.Value + b.Value),
			(TokenType.Minus, NumberValue a, NumberValue b) => new NumberValue(a.Value - b.Value),
			(TokenType.Star, NumberValue a, NumberValue b) => new NumberValue(a.Value * b.Value),
			(TokenType.Slash, NumberValue a, NumberValue b) => new NumberValue(a.Value / b.Value),
			(TokenType.Modulo, NumberValue a, NumberValue b) => new NumberValue(a.Value % b.Value),
			(TokenType.Carat, NumberValue a, NumberValue b) => new NumberValue(MathF.Pow(a.Value, b.Value)),

			// String concatenation
			(TokenType.Plus, _, StringValue)
				or (TokenType.Plus, StringValue, _)
				=> new StringValue(ToText(left) + ToText(right)),

			// Equality
			(TokenType.EqualsEquals, _, _) => BooleanValue.Of(AreEqual(left, right)),
			(TokenType.NotEquals, _, _) => BooleanValue.Of(!AreEqual(left, right)),

			// Numeric comparisons
			(TokenType.Lesser, NumberValue a, NumberValue b) => BooleanValue.Of(a.Value < b.Value),
			(TokenType.LEqual, NumberValue a, NumberValue b) => BooleanValue.Of(a.Value <= b.Value),
			(TokenType.Greater, NumberValue a, NumberValue b) => BooleanValue.Of(a.Value > b.Value),
			(TokenType.GEqual, NumberValue a, NumberValue b) => BooleanValue.Of(a.Value >= b.Value),

			// String comparisons
			(TokenType.Lesser, StringValue a, StringValue b) => BooleanValue.Of(string.CompareOrdinal(a.Value, b.Value) < 0),
			(TokenType.LEqual, StringValue a, StringValue b) => BooleanValue.Of(string.CompareOrdinal(a.Value, b.Value) <= 0),
			(TokenType.Greater, StringValue a, StringValue b) => BooleanValue.Of(string.CompareOrdinal(a.Value, b.Value) > 0),
			(TokenType.GEqual, StringValue a, StringValue b) => BooleanValue.Of(string.CompareOrdinal(a.Value, b.Value) >= 0),

			_ => throw new Exception(
				$"Unsupported operator {op} for {left.GetType().Name} and {right.GetType().Name}.")
		};
	}

	public static bool AreEqual(Value left, Value right)
	{
		return (left, right) switch
		{
			(NumberValue a, NumberValue b) => a.Value == b.Value,
			(StringValue a, StringValue b) => a.Value == b.Value,
			(BooleanValue a, BooleanValue b) => a.Value == b.Value,

			(NullValue, NullValue) => true,
			(VoidValue, VoidValue) => true,

			(ListValue a, ListValue b) =>
				a.Values.Count == b.Values.Count &&
				a.Values.Zip(b.Values).All(pair => AreEqual(pair.First, pair.Second)),

			(StructInstanceValue a, StructInstanceValue b) =>
				a.TypeName == b.TypeName &&
				a.Fields.Count == b.Fields.Count &&
				a.Fields.Keys.All(k => a.Fields.ContainsKey(k) && AreEqual(a.Fields[k], b.Fields[k])),

			_ => false
		};
	}

	public static string ToText(Value Value)
	{
		return Value switch
		{
			NumberValue n => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
			StringValue s => s.Value,
			BooleanValue b => b.Value ? "true" : "false",
			NullValue => "null",
			VoidValue => "void",
			ListValue list => "[" + string.Join(", ", list.Values.Select(ToText)) + "]",
			StructInstanceValue sv => $"{sv.TypeName} {{ {string.Join(", ", sv.Fields.Select(kv => $"{kv.Key}: {ToText(kv.Value)}"))} }}",
			_ => Value.ToString() ?? ""
		};
	}

	public static int ToInt(Value value)
	{
		return value switch
		{
			NumberValue n => checked((int)n.Value),
			StringValue s => int.Parse(s.Value, CultureInfo.InvariantCulture),
			_ => throw new InvalidOperationException(
				$"Expected a number, got {value.GetType().Name}.")
		};
	}

	public static byte ToByte(Value value)
	{
		return value switch
		{
			NumberValue n => checked((byte)n.Value),
			StringValue s => byte.Parse(s.Value, CultureInfo.InvariantCulture),
			_ => throw new InvalidOperationException(
				$"Expected a number, got {value.GetType().Name}.")
		};
	}

	static bool IsTruthy(Value Value)
	{
		return Value switch
		{
			BooleanValue b => b.Value,
			NumberValue n => n.Value != 0f,
			StringValue s => !string.IsNullOrEmpty(s.Value),
			NullValue => false,

			_ => throw new Exception($"Cannot use {Value.GetType().Name} as a Condition.")
		};
	}
}
