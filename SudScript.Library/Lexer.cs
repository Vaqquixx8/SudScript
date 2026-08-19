namespace SudScript;

public enum TokenType
{
	Identifier,
	NumericLiteral, BooleanLiteral, StringLiteral, NullLiteral,

	Plus, Minus,
	Star, Slash, Modulo,
	Carat,
	Exclamation,

	Increment, Decrement,
	PlusEquals, MinusEquals,
	DivideEquals, TimesEquals,

	Let, If, Else, While, Break, Continue,
	Equals,
	Func, Is, Return,
	Struct, Shared,
	Need, As,
	Group,

	EqualsEquals, LEqual, GEqual, Lesser, Greater, NotEquals,
	AndAnd, OrOr,

	LeftParen, RightParen,
	LeftBrace, RightBrace,
	LeftBracket, RightBracket,

	Colon,
	Comma,

	EndOfFile
};

public struct Token(TokenType _type, string _value, int _line, int _column)
{
	public TokenType type = _type;
	public string value = _value;

	public int line = _line;
	public int column = _column;

	public override readonly string ToString()
	{
		return $"{type} | {value} ({line}:{column})";
	}
}

public class Lexer(string _source)
{
	readonly string source = _source;

	int position = 0;

	int line = 1;
	int column = 1;

	char Current => position >= source.Length ? '\0' : source[position];

	char Peek(int offset = 1) =>  position + offset >= source.Length ? '\0' : source[position + offset];

	readonly Dictionary<string, TokenType> keywords = new Dictionary<string, TokenType>()
	{
		{"let", TokenType.Let},
		{"if", TokenType.If},
		{"while", TokenType.While},
		{"break", TokenType.Break},
		{"else", TokenType.Else},
		{"null", TokenType.NullLiteral},
		{"true", TokenType.BooleanLiteral},
		{"false", TokenType.BooleanLiteral},
		{"func", TokenType.Func},
		{"is", TokenType.Is},
		{"return", TokenType.Return},
		{"continue", TokenType.Continue},
		{"struct", TokenType.Struct},
		{"shared", TokenType.Shared},
		{"need", TokenType.Need},
		{"as", TokenType.As},
		{"group", TokenType.Group},
	};

	void Advance()
	{
		if (Current == '\n')
		{
			line++;
			column = 1;
		}
		else
		{
			column++;
		}

		position++;
	}

	public List<Token> Tokenize()
	{
		List<Token> tokens = new List<Token>();

		while(Current != '\0')
		{
			if (char.IsWhiteSpace(Current))
			{
				Advance();
				continue;
			}

			// Numberic Literals
			if (char.IsDigit(Current))
			{
				int start = position;
				int startLine = line;
				int startColumn = column;

				bool seenDot = false;

				while (char.IsDigit(Current) || Current == '.')
				{
					if (Current == '.')
					{
						if (seenDot)
						{
							throw new Exception($"Numeric literal cannot contain multiple dots at {line}:{column}.");
						}

						seenDot = true;
					}

					Advance();
				}

				string number = source[start..position];

				tokens.Add(new Token(TokenType.NumericLiteral, number, startLine, startColumn));
				continue;
			}

			// Identifiers and Keywords
			if (char.IsLetter(Current) || Current == '_')
			{
				int start = position;
				int startLine = line;
				int startColumn = column;
				while (char.IsLetterOrDigit(Current) || Current == '_')
				{
					Advance();
				}

				string word = source[start..position];

				if(keywords.TryGetValue(word, out TokenType type))
				{
					tokens.Add(new Token(type, word, startLine, startColumn));
					continue;
				}

				tokens.Add(new Token(TokenType.Identifier, word, startLine, startColumn));
				continue;
			}

			// String Literals
			if (Current == '"')
			{
				int startLine = line;
				int startColumn = column;

				Advance();

				int start = position;

				while (Current != '"' && Current != '\0')
				{
					Advance();
				}

				if (Current == '\0')
				{
					throw new Exception($"Unterminated string literal at {startLine}:{startColumn}.");
				}

				string value = source[start..position];

				Advance();

				tokens.Add(new Token(TokenType.StringLiteral, value, startLine, startColumn));
				continue;
			}

			// Misc Tokens, 1 and 2 chars
			switch (Current)
			{
				case '+':
					if(Peek() == '=')
					{
						tokens.Add(new Token(TokenType.PlusEquals, "+=", line, column));
						Advance();
						Advance();
						break;
					}
					if(Peek() == '+')
					{
						tokens.Add(new Token(TokenType.Increment, "++", line, column));
						Advance();
						Advance();
						break;
					}
					tokens.Add(new Token(TokenType.Plus, "+", line, column));
					Advance();
					break;
				case '-':
					if(Peek() == '=')
					{
						tokens.Add(new Token(TokenType.MinusEquals, "-=", line, column));
						Advance();
						Advance();
						break;
					}
					if(Peek() == '-')
					{
						tokens.Add(new Token(TokenType.Decrement, "--", line, column));
						Advance();
						Advance();
						break;
					}
					tokens.Add(new Token(TokenType.Minus, "-", line, column));
					Advance();
					break;

				case '*':
					if(Peek() == '=')
					{
						tokens.Add(new Token(TokenType.TimesEquals, "*=", line, column));
						Advance();
						Advance();
						break;
					}
					tokens.Add(new Token(TokenType.Star, "*", line, column));
					Advance();
					break;

				case '/':
					if (Peek() == '/')
					{
						while(Current != '\n' && Current != '\r')
						{
							Advance();
						}
						break;
					}
					if(Peek() == '=')
					{
						tokens.Add(new Token(TokenType.DivideEquals, "/=", line, column));
						Advance();
						Advance();
						break;
					}
					tokens.Add(new Token(TokenType.Slash, "/", line, column));
					Advance();
					break;

				case '%':
					tokens.Add(new Token(TokenType.Modulo, "%", line, column));
					Advance();
					break;

				case '^':
					tokens.Add(new Token(TokenType.Carat, "^", line, column));
					Advance();
					break;

				case '(':
					tokens.Add(new Token(TokenType.LeftParen, "(", line, column));
					Advance();
					break;

				case ')':
					tokens.Add(new Token(TokenType.RightParen, ")", line, column));
					Advance();
					break;

				case '[':
					tokens.Add(new Token(TokenType.LeftBracket, "[", line, column));
					Advance();
					break;

				case ']':
					tokens.Add(new Token(TokenType.RightBracket, "]", line, column));
					Advance();
					break;

				case '{':
					tokens.Add(new Token(TokenType.LeftBrace, "{", line, column));
					Advance();
					break;

				case '}':
					tokens.Add(new Token(TokenType.RightBrace, "}", line, column));
					Advance();
					break;

				case ':':
					tokens.Add(new Token(TokenType.Colon, ":", line, column));
					Advance();
					break;

				case '=':
					if(Peek() == '=')
					{
						tokens.Add(new Token(TokenType.EqualsEquals, "==", line, column));
						Advance();
						Advance();
						break;
					}
					tokens.Add(new Token(TokenType.Equals, "=", line, column));
					Advance();
					break;

				case '<':
					if (Peek() == '=')
					{
						tokens.Add(new Token(TokenType.LEqual, "<=", line, column));
						Advance();
						Advance();
						break;
					}
					tokens.Add(new Token(TokenType.Lesser, "<", line, column));
					Advance();
					break;

				case '>':
					if (Peek() == '=')
					{
						tokens.Add(new Token(TokenType.GEqual, ">=", line, column));
						Advance();
						Advance();
						break;
					}
					tokens.Add(new Token(TokenType.Greater, ">", line, column));
					Advance();
					break;

				case '!':
					if (Peek() == '=')
					{
						tokens.Add(new Token(TokenType.NotEquals, "!=", line, column));
						Advance();
						Advance();
						break;
					}
					tokens.Add(new Token(TokenType.Exclamation, "!", line, column));
					Advance();
					break;

				case ',':
					tokens.Add(new Token(TokenType.Comma, ",", line, column));
					Advance();
					break;

				case '&':
					if (Peek() == '&')
					{
						tokens.Add(new Token(TokenType.AndAnd, "&&", line, column));
						Advance();
						Advance();
						break;
					}
					break;
				case '|':
					if (Peek() == '|')
					{
						tokens.Add(new Token(TokenType.OrOr, "||", line, column));
						Advance();
						Advance();
						break;
					}
					break;

				default:
					throw new Exception($"Unexpected character '{Current}' at {line}:{column}.");
			}
		}
		tokens.Add(new Token(TokenType.EndOfFile, "", line, column));

		return tokens;
	}
}
