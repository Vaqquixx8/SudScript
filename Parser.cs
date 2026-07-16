namespace SudScript;

public class Parser(List<Token> _tokens)
{
	readonly List<Token> tokens = _tokens;

	int position = 0;

	Token Current => position < tokens.Count ? tokens[position] : tokens[^1];

	Token Consume()
	{
		return tokens[position++];
	}

	bool Match(TokenType type)
	{
		if (Current.type != type)
		{
			return false;
		}

		Consume();
		return true;
	}

	Token Peek(int offset = 1)
	{
		int index = position + offset;

		if (index >= tokens.Count)
		{
			return tokens[^1];
		}

		return tokens[index];
	}

	Token Expect(TokenType type)
	{
		if (Current.type != type)
		{
			throw new Exception($"Expected {type} at {Current.line}:{Current.column} got {Current.type} instead.");
		}

		return Consume();
	}

	public ProgramNode ParseProgram()
	{
		List<Statement> body = new List<Statement>();

		while(Current.type != TokenType.EndOfFile)
		{
			body.Add(ParseStatement());
		}

		return new ProgramNode(body);
	}

	Statement ParseStatement()
	{
		return Current.type switch
		{
			TokenType.Let => ParseVariableDeclaration(),
			TokenType.If => ParseIfStatement(),
			TokenType.LeftBrace => ParseBlockStatement(),
			TokenType.Func => ParseFunctionDeclaration(),
			TokenType.Return => ParseReturnStatement(),
			TokenType.While => ParseWhileStatement(),
			TokenType.Break => ParseBreakStatement(),
			TokenType.Continue => ParseContinueStatement(),
			TokenType.Struct => ParseStructDeclaration(),
			_ => ParseExpressionStatement(),
		};
	}

	VariableDeclaration ParseVariableDeclaration()
	{
		Consume();

		Token nameToken = Expect(TokenType.Identifier);
		Expect(TokenType.Equals);
		Expression value = ParseExpression();

		return new VariableDeclaration(nameToken.value, value);
	}

	IfStatement ParseIfStatement()
	{
		Consume();

		Expect(TokenType.LeftParen);
		Expression condition = ParseExpression();
		Expect(TokenType.RightParen);

		Statement thenBlock = ParseBlockStatement();

		Statement? elseBlock = null;

		if (Match(TokenType.Else))
		{
			if (Current.type == TokenType.If)
			{
				elseBlock = ParseIfStatement();
			}
			else
			{
				elseBlock = ParseBlockStatement();
			}
		}

		return new IfStatement(condition, thenBlock, elseBlock);
	}

	BlockStatement ParseBlockStatement()
	{
		Expect(TokenType.LeftBrace);
		List<Statement> body = new List<Statement>();

		while(Current.type != TokenType.RightBrace && Current.type != TokenType.EndOfFile)
		{
			body.Add(ParseStatement());
		}
		Expect(TokenType.RightBrace);

		return new BlockStatement(body);
	}

	FunctionDeclaration ParseFunctionDeclaration(bool isShared = false)
	{
		Consume();

		Token nameToken = Expect(TokenType.Identifier);

		Expect(TokenType.LeftParen);

		List<string> parameters = new List<string>();

		if(Current.type != TokenType.RightParen)
		{
			do
			{
				Token param = Expect(TokenType.Identifier);
				parameters.Add(param.value);
			}while(Match(TokenType.Comma));
		}

		Expect(TokenType.RightParen);

		BlockStatement body = ParseBlockStatement();

		return new FunctionDeclaration(nameToken.value, parameters, body, isShared);
	}

	ReturnStatement ParseReturnStatement()
	{
		Consume();

		if (Current.type == TokenType.RightBrace || Current.type == TokenType.EndOfFile)
		{
			return new ReturnStatement(new VoidExpression());
		}

		Expression value = ParseExpression();

		return new ReturnStatement(value);
	}

	WhileStatement ParseWhileStatement()
	{
		Consume();

		Expect(TokenType.LeftParen);
		Expression condition = ParseExpression();
		Expect(TokenType.RightParen);

		Statement block = ParseBlockStatement();

		return new WhileStatement(condition, block);
	}

	BreakStatement ParseBreakStatement()
	{
		Consume();
		return new BreakStatement();
	}

	ContinueStatement ParseContinueStatement()
	{
		Consume();
		return new ContinueStatement();
	}

	StructDeclaration ParseStructDeclaration()
	{
		Consume();

		Token nameToken = Expect(TokenType.Identifier);
		Expect(TokenType.LeftBrace);

		List<StructFieldDeclaration> fields = new List<StructFieldDeclaration>();
		List<FunctionDeclaration> methods = new List<FunctionDeclaration>();

		while(Current.type != TokenType.RightBrace)
		{
			if(Current.type == TokenType.Let)
			{
				Consume();
				Token fieldName = Expect(TokenType.Identifier);
				Expect(TokenType.Equals);
				Expression value = ParseExpression();
				fields.Add(new StructFieldDeclaration(fieldName.value, value));
			}
			else if(Current.type == TokenType.Shared)
			{
				Consume();
				methods.Add(ParseFunctionDeclaration(isShared : true));
			}
			else if(Current.type == TokenType.Func)
			{
				methods.Add(ParseFunctionDeclaration());
			}
			else
			{
				throw new Exception($"Unexpected token {Current.type} in struct body.");
			}
		}
		Expect(TokenType.RightBrace);
		return new StructDeclaration(nameToken.value, fields, methods);
	}

	ExpressionStatement ParseExpressionStatement()
	{
		Expression expression = ParseExpression();
		return new ExpressionStatement(expression);
	}

	Expression ParseExpression()
	{
		return ParseAssignment();
	}

	Expression ParseAssignment()
	{
		Expression left = ParseLogicalOr();

		if (Match(TokenType.Equals))
		{
			Expression value = ParseAssignment();

			return left switch
			{
				IdentifierExpression id => new AssignmentExpression(id, value),
				MemberAccessExpression member => new AssignmentExpression(member, value),
				_ => throw new Exception("Invalid assignment target.")
			};
		}

		return left;
	}

	Expression ParseLogicalOr()
	{
		Expression left = ParseLogicalAnd();

		while (Current.type == TokenType.OrOr)
		{
			TokenType op = Consume().type;
			Expression right = ParseLogicalAnd();
			left = new BinaryExpression(left, op, right);
		}

		return left;
	}

	Expression ParseLogicalAnd()
	{
		Expression left = ParseEquality();

		while (Current.type == TokenType.AndAnd)
		{
			TokenType op = Consume().type;
			Expression right = ParseEquality();
			left = new BinaryExpression(left, op, right);
		}

		return left;
	}

	Expression ParseEquality()
	{
		Expression left = ParseComparison();

		while(Current.type == TokenType.EqualsEquals || Current.type == TokenType.NotEquals)
		{
			TokenType op = Consume().type;
			Expression right = ParseComparison();
			left = new BinaryExpression(left, op, right);
		}

		return left;
	}

	Expression ParseComparison()
	{
		Expression left = ParseAdditive();

		while(Current.type == TokenType.Lesser || Current.type == TokenType.LEqual || Current.type == TokenType.Greater || Current.type == TokenType.GEqual)
		{
			TokenType op = Consume().type;
			Expression right = ParseAdditive();
			left =  new BinaryExpression(left, op, right);
		}

		return left;
	}

	Expression ParseAdditive()
	{
		Expression left = ParseMultiplicitive();
		while(Current.type == TokenType.Plus || Current.type == TokenType.Minus)
		{
			TokenType op = Consume().type;

			Expression right = ParseMultiplicitive();

			left = new BinaryExpression(left, op, right);
		}

		return left;
	}

	Expression ParseMultiplicitive()
	{
		Expression left = ParseExponential();

		while (Current.type == TokenType.Star || Current.type == TokenType.Slash || Current.type == TokenType.Modulo)
		{
			TokenType op = Consume().type;

			Expression right = ParseExponential();

			left = new BinaryExpression(left, op, right);
		}

		return left;
	}

	Expression ParseExponential()
	{
		Expression left = ParseUnary();

		while (Current.type == TokenType.Carat)
		{
			TokenType op = Consume().type;

			Expression right = ParseExponential();

			left = new BinaryExpression(left, op, right);
		}

		return left;
	}

	Expression ParseUnary()
	{
		if (Current.type == TokenType.Minus || Current.type == TokenType.Exclamation)
		{
			TokenType op = Consume().type;

			Expression right = ParseUnary();

			return new UnaryExpression(right, op);
		}

		return ParsePrimary();
	}

	FunctionCallExpression ParseFunctionCall(string name)
	{
		Expect(TokenType.LeftParen);

		List<Expression> arguments = new List<Expression>();

		if (Current.type != TokenType.RightParen)
		{
			do
			{
				arguments.Add(ParseExpression());

			} while (Match(TokenType.Comma));
		}

		Expect(TokenType.RightParen);

		return new FunctionCallExpression(name, arguments);
	}

	StructLiteralExpression ParseStructLiteral(string name)
	{
		Consume();
		List<(string Name, Expression Value)> fields = new List<(string Name, Expression Value)>();

		if(Current.type != TokenType.RightBrace)
		{
			do
			{
				Token fieldName = Expect(TokenType.Identifier);
				Expect(TokenType.Equals);
				Expression value = ParseExpression();
				fields.Add((fieldName.value, value));
			}while(Match(TokenType.Comma));
		}
		Expect(TokenType.RightBrace);
		return new StructLiteralExpression(name, fields);
	}

	ListExpression ParseListLiteral()
	{
		Consume();

		List<Expression> elements = new List<Expression>();

		if (Current.type != TokenType.RightBracket)
		{
			do
			{
				elements.Add(ParseExpression());
			}
			while (Match(TokenType.Comma));
		}

		Expect(TokenType.RightBracket);

		return new ListExpression(elements);
	}

	Expression ParsePrimary()
	{
		Expression expr = ParsePrimaryCore();

		while (Match(TokenType.Colon))
		{
			Token member = Expect(TokenType.Identifier);

			if(Current.type == TokenType.LeftParen)
			{
				Consume();

				List<Expression> args = new List<Expression>();

				if (Current.type != TokenType.RightParen)
				{
					do
					{
						args.Add(ParseExpression());
					} while (Match(TokenType.Comma));
				}
				Expect(TokenType.RightParen);

				expr = new MethodCallExpression(expr, member.value, args);
			}
			else
			{
				expr = new MemberAccessExpression(expr, member.value);
			}
			
		}
		return expr;
	}

	Expression ParsePrimaryCore()
	{
		switch (Current.type)
		{
			case TokenType.NumericLiteral:
				float floatValue = float.Parse(Consume().value);
				return new NumericExpression(floatValue);

			case TokenType.StringLiteral:
				return new StringExpression(Consume().value);

			case TokenType.BooleanLiteral:
				bool boolValue = bool.Parse(Consume().value);
				return new BooleanExpression(boolValue);

			case TokenType.LeftParen:
				Consume();
				Expression expr = ParseExpression();
				Expect(TokenType.RightParen);
				return expr;

			case TokenType.LeftBracket:
				return ParseListLiteral();

			case TokenType.Identifier:
			{
				Token nameToken = Consume();

				if(Current.type == TokenType.LeftParen)
				{
					return ParseFunctionCall(nameToken.value);
				}

				if(Current.type == TokenType.LeftBrace)
				{
					return ParseStructLiteral(nameToken.value);
				}

				return new IdentifierExpression(nameToken.value);
			}

			default:
				throw new Exception($"Unexpected token {Current}.");
		}
	}
}