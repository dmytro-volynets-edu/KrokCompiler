using KrokCompiler.Abstractions;

namespace KrokCompiler.Lexer
{
    public class Scanner : IScanner
    {
        private readonly string _source;
        private int _position = 0;
        private int _line = 1;
        private int _column = 1;

        public Scanner(string source) { _source = source; }

        public int Line => _line;
        public int Column => _column;
        public bool IsAtEnd => _position >= _source.Length; // true якщо сканер досяг кінця файлу

        public char Advance()
        {
            if (IsAtEnd) return '\0';

            char c = _source[_position++];

            if (c == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
            return c;
        }

        // "Зазирнути" на наступний символ, не з'їдаючи його
        public char Peek()
        {
            if (_position >= _source.Length) return '\0';
            return _source[_position];
        }

        // Повернути символ назад (для Fstar)
        public void Retract()
        {
            _position--;
            // (Тут треба обережно обробляти `\n`, якщо він був)
        }

    }
}
