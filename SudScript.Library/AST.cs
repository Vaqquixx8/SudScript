namespace SudScript;

public abstract record Statement;
public abstract record Expression;
public abstract record Value;

public record ProgramNode(List<Statement> Body);

// =======================================================
// Values
// =======================================================

public record NumberValue(float Value) : Value;
public record StringValue(string Value) : Value;
public record BooleanValue(bool Value) : Value;
public record ListValue(List<Value> Values) : Value;

public record StructInstanceValue(
	string TypeName,
	Dictionary<string, Value> Fields
) : Value;

public record VoidValue() : Value;

// =======================================================
// Misc
// =======================================================

public record StructFieldDeclaration(string Name, Expression DefaultValue);
public record StructDeclarationValue(
	string Name,
	Dictionary<string, Expression> FieldDefaults,
	Dictionary<string, FunctionDeclaration> Methods
);

// =======================================================
// Statements
// =======================================================

public record ExpressionStatement(Expression Expression) : Statement;

public record VariableDeclaration(string Name, Expression Value) : Statement;

public record FunctionDeclaration(
	string Name,
	List<string> Params,
	BlockStatement Block,
	bool IsShared
) : Statement;

public record StructDeclaration(
	string Name,
	List<StructFieldDeclaration> Fields,
	List<FunctionDeclaration> Methods
) : Statement;

public record IfStatement(
	Expression Condition,
	Statement ThenBlock,
	Statement? ElseBlock
) : Statement;

public record WhileStatement(
	Expression Condition,
	Statement Block
) : Statement;

public record BlockStatement(List<Statement> Body) : Statement;

public record ReturnStatement(Expression Value) : Statement;
public record BreakStatement() : Statement;
public record ContinueStatement() : Statement;

public record NeedImportStatement(
	List<string> Path,
	string? Alias
) : Statement;

public record GroupDeclaration(string Name) : Statement;

// =======================================================
// Expressions
// =======================================================

public record IdentifierExpression(string Name) : Expression;

public record NumericExpression(float Value) : Expression;

public record StringExpression(string Value) : Expression;

public record BooleanExpression(bool Value) : Expression;

public record StructLiteralExpression(
	string Name,
	List<(string Name, Expression Value)> Fields
) : Expression;

public record MemberAccessExpression(
	Expression Target,
	string Member
) : Expression;

public record VoidExpression() : Expression;

public record ListExpression(List<Expression> Elements) : Expression;

public record AssignmentExpression(
	Expression Target,
	Expression Value,
	TokenType Operator = TokenType.Equals
) : Expression;

public record BinaryExpression(
	Expression Left,
	TokenType Op,
	Expression Right
) : Expression;

public record UnaryExpression(
	Expression Right,
	TokenType Op
) : Expression;

public record PostfixExpression(
	Expression Target,
	TokenType Operator
) : Expression;

public record FunctionCallExpression(
	string Name,
	List<Expression> Args
) : Expression;

public record MethodCallExpression(
	Expression Target,
	string Method,
	List<Expression> Args
) : Expression;

// =======================================================
// Flow Control
// =======================================================

public enum FlowType
{
	None,
	Return,
	Break,
	Continue
}

public readonly struct ExecutionResult(FlowType type, Value? value = null)
{
	public readonly FlowType Type = type;
	public readonly Value? Value = value;

	public static readonly ExecutionResult None = new(FlowType.None);
	public static readonly ExecutionResult Break = new(FlowType.Break);
	public static readonly ExecutionResult Continue = new(FlowType.Continue);

	public static ExecutionResult Return(Value value)
		=> new(FlowType.Return, value);
}
