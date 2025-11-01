namespace KrokCompiler.Models
{
    public class Token
    {
        public TokenType Type { get; }
        public string Lexeme { get; }
        public object Value { get; } // Напр., 123 (int), 3.14 (double), "hello" (string)
        public int Line { get; }
        public int Column { get; } // Для кращої діагностики помилок

        public Token(TokenType type, string lexeme, object value, int line, int col)
        {
            Type = type;
            Lexeme = lexeme;
            Value = value;
            Line = line;
            Column = col;
        }

        public override string ToString() => $"{Line,-5} {Column,-5} {Type,-15} {Lexeme,-20} {Value}";
    }
}
