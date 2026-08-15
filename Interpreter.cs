namespace SudScript;

using System.Threading;
using System.Diagnostics;

public class Interpreter
{
	Environment environment = new Environment();
	ProgramNode? program;
	string modulesDirectory = null!;
	readonly Dictionary<string, Func<List<Value>, Value>> builtins = new Dictionary<string, Func<List<Value>, Value>>();

	Stopwatch stopwatch = new Stopwatch();

	public void SetModulesDirectory(string path)
	{
		modulesDirectory = path;
	}

	public void Initialize(ProgramNode _program)
	{
		program = _program;
		environment = new Environment();
		stopwatch = Stopwatch.StartNew();
		InitializeBuiltins();

		string baseDir;
		if (modulesDirectory == null)
		{
			throw new Exception($"No modules directory set.");
		}
		else
		{
			baseDir = modulesDirectory;
		}

		if (!Path.IsPathRooted(baseDir))
		{
			baseDir = Path.GetFullPath(baseDir);
		}

		var loader = new ModuleLoader(baseDir);
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

	void InitializeBuiltins()
	{
		Random random = new Random();

		builtins["random"] = args =>
		{
			int min = (int)((NumberValue)args[0]).Value;
			int max = (int)((NumberValue)args[1]).Value;

			return new NumberValue(random.Next(min, max + 1));
		};
		builtins["consoleSay"] = args =>
		{
			foreach(Value value in args)
			{
				Console.Write(ToText(value));
			}
			Console.Write('\n');
			return new VoidValue();
		};
		builtins["consoleClear"] = args =>
		{
			Console.Clear();
			return new VoidValue();
		};
		builtins["stringToNumber"] = args =>
		{
			return new NumberValue(float.Parse(ToText(args[0])));
		};
		builtins["stringToBool"] = args =>
		{
			return new BooleanValue(bool.Parse(ToText(args[0])));
		};
		builtins["toString"] = args =>
		{
			return new StringValue(ToText(args[0]));
		};
		builtins["sqrt"] = args =>
		{
			return new NumberValue(MathF.Sqrt(float.Parse(ToText(args[0]))));
		};
		builtins["sin"] = args =>
		{
			return new NumberValue(MathF.Sin(float.Parse(ToText(args[0]))));
		};
		builtins["cos"] = args =>
		{
			return new NumberValue(MathF.Cos(float.Parse(ToText(args[0]))));
		};
		builtins["tan"] = args =>
		{
			return new NumberValue(MathF.Tan(float.Parse(ToText(args[0]))));
		};
		builtins["atan"] = args =>
		{
			return new NumberValue(MathF.Atan(float.Parse(ToText(args[0]))));
		};
		builtins["atan2"] = args =>
		{
			return new NumberValue(MathF.Atan2(float.Parse(ToText(args[0])), float.Parse(ToText(args[1]))));
		};
		builtins["floor"] = args =>
		{
			return new NumberValue(MathF.Floor(float.Parse(ToText(args[0]))));
		};
		builtins["ceil"] = args =>
		{
			return new NumberValue(MathF.Ceiling(float.Parse(ToText(args[0]))));
		};
		builtins["round"] = args =>
		{
			return new NumberValue(MathF.Round(float.Parse(ToText(args[0]))));
		};
		builtins["min"] = args =>
		{
		    float a = float.Parse(ToText(args[0]));
		    float b = float.Parse(ToText(args[1]));

		    return new NumberValue(MathF.Min(a, b));
		};
		builtins["max"] = args =>
		{
		    float a = float.Parse(ToText(args[0]));
		    float b = float.Parse(ToText(args[1]));

		    return new NumberValue(MathF.Max(a, b));
		};
		builtins["clamp"] = args =>
		{
		    float a = float.Parse(ToText(args[0]));
		    float b = float.Parse(ToText(args[1]));
		    float c = float.Parse(ToText(args[2]));

		    return new NumberValue(Math.Clamp(a, b, c));
		};
		builtins["wait"] = args =>
		{
		    float seconds = float.Parse(ToText(args[0]));

		    if (seconds > 0)
			{
				Thread.Sleep((int)(seconds * 1000));
			}

		    return new VoidValue();
		};
		builtins["time"] = args =>
		{
		    return new NumberValue((float)stopwatch.Elapsed.TotalSeconds);
		};
	}

	public void Execute()
	{
		if (program == null)
		{
			throw new Exception("Interpreter has not been initialized.");
		}

		if (!environment.TryGetFunction("main", out var main))
		{
			throw new Exception("Program entry point 'main' was not found.");
		}

		if (main.Params.Count != 0)
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
			foreach (Statement statement in block.Body)
			{
				var result = ExecuteStatement(statement);

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
		while (IsTruthy(EvaluateExpression(whileStatement.Condition)))
		{
			var result = ExecuteStatement(whileStatement.Block);

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

	Value CallFunction(FunctionCallExpression call)
	{
		if(builtins.TryGetValue(call.Name, out var builtin))
		{
			List<Value> arguments = new List<Value>();

			foreach(Expression arg in call.Args)
			{
				arguments.Add(EvaluateExpression(arg));
			}
			return builtin(arguments);
		}

		FunctionDeclaration function = environment.GetFunction(call.Name);

		if (call.Args.Count != function.Params.Count)
		{
			throw new Exception($"Function '{call.Name}' expects {function.Params.Count} arguments, but got {call.Args.Count} instead.");
		}

		Environment previous = environment;

		environment = new Environment(environment);

		try
		{
			for (int i = 0; i < function.Params.Count; i++)
			{
				Value argument = EvaluateExpression(call.Args[i]);
				environment.DefineVariable(function.Params[i], argument);
			}

			var result = ExecuteStatements(function.Block.Body);

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

	Value CallMethod(MethodCallExpression call)
	{
		List<Value> args = call.Args.Select(EvaluateExpression).ToList();

		if(call.Target is IdentifierExpression id && environment.TryGetStruct(id.Name, out var decl))
		{
			return CallStructSharedMethod(decl!, call.Method, args);
		}

		Value target = EvaluateExpression(call.Target);

		return target switch
		{
			ListValue list => CallListMethod(list, call.Method, args),
			StructInstanceValue instance => CallStructInstanceMethod(instance, call.Method, args),
			_ => throw new Exception($"{target.GetType().Name} has not method '{call.Method}'."),
		};
	}

	static Value CallListMethod(ListValue list, string method, List<Value> args)
	{
		switch (method)
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
				return new BooleanValue(list.Values.Any(v => AreEqual(v, args[0])));
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

			default:
				throw new Exception($"Unknown list method '{method}'.");
		}
	}

	Value CallStructInstanceMethod(StructInstanceValue instance, string method, List<Value> args)
	{
		StructDeclaration decl = environment.GetStruct(instance.TypeName);

		FunctionDeclaration function = decl.Methods.FirstOrDefault(m => !m.IsShared && m.Name == method) ?? throw new Exception($"Struct '{instance.TypeName}' has no instance method {method}.");

		if(function.Params.Count != args.Count)
		{
			throw new Exception($"Method '{method}' expects {function.Params.Count} arguments but got {args.Count} instead.");
		}
		Environment previous = environment;
		environment = new Environment(environment);

		try
		{
			environment.DefineVariable("self", instance);
			for(int i = 0; i< function.Params.Count; ++i)
			{
				environment.DefineVariable(function.Params[i], args[i]);
			}
			var result = ExecuteStatements(function.Block.Body);

			if(result.Type == FlowType.Return)
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

	Value CallStructSharedMethod(StructDeclaration decl, string method, List<Value> args)
	{
		FunctionDeclaration function = decl.Methods.FirstOrDefault(m => m.IsShared && m.Name == method) ?? throw new Exception($"Struct '{decl.Name}' has no shared method '{method}'.");

		if (function.Params.Count != args.Count)
		{
			throw new Exception($"Shared method '{method}' expects {function.Params.Count} arguments but got {args.Count} instead.");
		}

		Environment previous = environment;
		environment = new Environment(environment);

		try
		{
			for (int i = 0; i < function.Params.Count; ++i)
			{
				environment.DefineVariable(function.Params[i], args[i]);
			}

			var result = ExecuteStatements(function.Block.Body);

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
		foreach (var statement in statements)
		{
			var result = ExecuteStatement(statement);

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
				return new BooleanValue(boolean.Value);

			case VoidExpression:
				return new VoidValue();

			case ListExpression list:
			{
				List<Value> values = new List<Value>();
				foreach(Expression expr in list.Elements)
				{
					values.Add(EvaluateExpression(expr));
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
					TokenType.Exclamation when Right is BooleanValue n => new BooleanValue(!IsTruthy(Right)),
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
						return new BooleanValue(false);
					}
					return new BooleanValue(IsTruthy(EvaluateExpression(binary.Right)));
				}

				if (binary.Op == TokenType.OrOr)
				{
					Value left = EvaluateExpression(binary.Left);

					if (IsTruthy(left))
					{
						return new BooleanValue(true);
					}

					return new BooleanValue(IsTruthy(EvaluateExpression(binary.Right)));
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

			            throw new Exception(
			                $"{target.GetType().Name} does not support member assignment.");
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

				Dictionary<string, Value> fields = new Dictionary<string, Value>();

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
					throw new Exception($"Operator {postfix.Operator} reqquires a number.");
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
			(TokenType.EqualsEquals, _, _) => new BooleanValue(AreEqual(left, right)),
			(TokenType.NotEquals, _, _) => new BooleanValue(!AreEqual(left, right)),

			// Numeric comparisons
			(TokenType.Lesser, NumberValue a, NumberValue b) => new BooleanValue(a.Value < b.Value),
			(TokenType.LEqual, NumberValue a, NumberValue b) => new BooleanValue(a.Value <= b.Value),
			(TokenType.Greater, NumberValue a, NumberValue b) => new BooleanValue(a.Value > b.Value),
			(TokenType.GEqual, NumberValue a, NumberValue b) => new BooleanValue(a.Value >= b.Value),

			// String comparisons
			(TokenType.Lesser, StringValue a, StringValue b) => new BooleanValue(string.CompareOrdinal(a.Value, b.Value) < 0),
			(TokenType.LEqual, StringValue a, StringValue b) => new BooleanValue(string.CompareOrdinal(a.Value, b.Value) <= 0),
			(TokenType.Greater, StringValue a, StringValue b) => new BooleanValue(string.CompareOrdinal(a.Value, b.Value) > 0),
			(TokenType.GEqual, StringValue a, StringValue b) => new BooleanValue(string.CompareOrdinal(a.Value, b.Value) >= 0),

			_ => throw new Exception(
				$"Unsupported operator {op} for {left.GetType().Name} and {right.GetType().Name}.")
		};
	}

	static bool AreEqual(Value left, Value Right)
	{
		return (left, Right) switch
		{
			(NumberValue a, NumberValue b) => a.Value == b.Value,
			(StringValue a, StringValue b) => a.Value == b.Value,
			(BooleanValue a, BooleanValue b) => a.Value == b.Value,
			(ListValue a, ListValue b) =>
				a.Values.Count == b.Values.Count &&
				a.Values.Zip(b.Values).All(pair => AreEqual(pair.First, pair.Second)),
			(StructInstanceValue a, StructInstanceValue b) =>
				a.TypeName == b.TypeName &&
				a.Fields.Keys.All(k  => AreEqual(a.Fields[k], b.Fields[k])),
			_ => false
		};
	}

	static string ToText(Value Value)
	{
		return Value switch
		{
			NumberValue n => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
			StringValue s => s.Value,
			BooleanValue b => b.Value ? "true" : "false",
			VoidValue => "void",
			ListValue list => "[" + string.Join(", ", list.Values.Select(ToText)) + "]",
			StructInstanceValue sv => $"{sv.TypeName} {{ {string.Join(", ", sv.Fields.Select(kv => $"{kv.Key}: {ToText(kv.Value)}"))} }}",
			_ => Value.ToString() ?? ""
		};
	}


	static bool IsTruthy(Value Value)
	{
		return Value switch
		{
			BooleanValue b => b.Value,
			NumberValue n => n.Value > 0f,
			StringValue s => !string.IsNullOrEmpty(s.Value),

			_ => throw new Exception($"Cannot use {Value.GetType().Name} as a Condition.")
		};
	}
}
