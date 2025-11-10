using KrokCompiler.Abstractions;
using KrokCompiler.Models;

namespace KrokCompiler.Lexer
{
    public class Lexer
    {
        private readonly IScanner _scanner;
        private List<Token> _tokens = new List<Token>();     // результат роботи

        // 1. Таблиці станів та токенів
        private Dictionary<(State, string), State> _stf; // Функція переходів (Діаграма станів)
        private HashSet<State> _F;                      // Множина заключних станів
        private HashSet<State> _Fstar;                  // Стани, що потребують повернення
        private HashSet<State> _Ferror;                 // Стани помилок

        private Dictionary<string, TokenType> _keywordTable; // Таблиця ключових слів
        private Dictionary<State, TokenType> _tokStateTable;  // Таблиця токенів за станом

        // 2. Стан аналізатора
        private State _state;          // Поточний стан (int у вашому прикладі)
        private string _lexeme;        // Поточна лексема, що будується
        private int _lexemeStartLine;
        private int _lexemeStartColumn;


        public Lexer(IScanner scanner)
        {
            _scanner = scanner;
            _lexeme = "";
            _state = State.q0;
            InitializeTables();
        }

        public List<Token> Analyze()
        {
            _tokens.Clear();

            try
            {
                while (!_scanner.IsAtEnd)
                {
                    char currentSymbol = _scanner.Advance();
                    string charClass = GetCharClass(currentSymbol);
                    _state = NextState(_state, charClass);

                    if (_F.Contains(_state)) // Якщо стан заключний
                    {
                        Processing(currentSymbol);
                    }
                    else if (_state == State.q0) // Якщо стан стартовий (ігнор)
                    {
                        _lexeme = "";
                        _lexemeStartLine = _scanner.Line;
                        _lexemeStartColumn = _scanner.Column;
                    }
                    else // Інакше - робочий стан
                    {
                        _lexeme += currentSymbol; // Додати символ до лексеми
                    }
                }
                _tokens.Add(new Token(TokenType.Eof, "", null, _scanner.Line, _scanner.Column));
                return _tokens;
            }
            catch (LexerException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Lexer: Emergency termination of the program. \n {e.Message}");
                Console.ResetColor();
                Environment.Exit(1);
                throw;
            }
        }

        private State NextState(State currentState, string charClass)
        {
            // Намагаємося знайти прямий перехід
            if (_stf.TryGetValue((currentState, charClass), out State nextState))
            {
                return nextState;
            }

            // Якщо прямого переходу немає, шукаємо перехід по 'other'
            if (_stf.TryGetValue((currentState, "other"), out State otherState))
            {
                return otherState;
            }

            // Якщо не знайдено нічого (помилка в самій STF):
            return State.q_ERR_IllegalChar;
        }

        private void InitializeTables()
        {
            // Таблиця переходів
            _stf = new Dictionary<(State, string), State>
            {
                // --- q0 (Стартовий стан) ---
                { (State.q0, "ws"), State.q0 },             // Ігнорувати пробіл
                { (State.q0, "nl"), State.q_nl_F },         // Ігнорувати + лічильник
                { (State.q0, "other"), State.q_ERR_IllegalChar },     // Помилка (нерозпізнаний символ)

                // Початки токенів
                { (State.q0, "Letter"), State.q_id },
                { (State.q0, "Digit"), State.q_num_int },
                { (State.q0, "dot"), State.q_num_real_pre },
                { (State.q0, "quote"), State.q_str },
                { (State.q0, "slash"), State.q_slash },
                { (State.q0, "assign_eq"), State.q_assign },
                { (State.q0, "not"), State.q_not },
                { (State.q0, "lt"), State.q_lt },
                { (State.q0, "gt"), State.q_gt },

                // Односимвольні оператори
                { (State.q0, "+"), State.q_add_F },
                { (State.q0, "-"), State.q_sub_F },
                { (State.q0, "*"), State.q_mul_F },
                { (State.q0, "^"), State.q_pow_F },
                { (State.q0, "("), State.q_lparen_F },
                { (State.q0, ")"), State.q_rparen_F },
                { (State.q0, "{"), State.q_lbrace_F },
                { (State.q0, "}"), State.q_rbrace_F },
                { (State.q0, ","), State.q_comma_F },
                { (State.q0, ";"), State.q_semicolon_F },
    
                // --- q_id (Розпізнавання ідентифікатора) ---
                { (State.q_id, "Letter"), State.q_id },
                { (State.q_id, "Digit"), State.q_id },
                { (State.q_id, "underscore"), State.q_id },
                { (State.q_id, "other"), State.q_id_F },    // Завершено, Fstar

                // --- Розпізнавання чисел ---
                { (State.q_num_int, "Digit"), State.q_num_int },
                { (State.q_num_int, "dot"), State.q_num_real_post },
                { (State.q_num_int, "other"), State.q_int_F },  // Завершено int, Fstar

                { (State.q_num_real_pre, "Digit"), State.q_num_real_post },
                { (State.q_num_real_pre, "other"), State.q_ERR_InvalidNumber }, // Помилка: одинарна крапка

                { (State.q_num_real_post, "Digit"), State.q_num_real_post },
                { (State.q_num_real_post, "other"), State.q_real_F }, // Завершено real, Fstar
    
                // --- Розпізнавання рядка ---
                { (State.q_str, "quote"), State.q_str_F },      // Завершено string, F
                { (State.q_str, "nl"), State.q_ERR_UnclosedString },        // Помилка: незакритий рядок
                { (State.q_str, "other"), State.q_str },      // Будь-який інший символ

                // --- Розпізнавання коментаря та ділення ---
                { (State.q_slash, "slash"), State.q_comment },
                { (State.q_slash, "other"), State.q_div_F },    // Завершено op_div, Fstar

                { (State.q_comment, "nl"), State.q_nl_F },          // Кінець коментаря, перехід у стан обробки /n
                { (State.q_comment, "other"), State.q_comment },// "Їмо" коментар

                // --- Розпізнавання операторів порівняння ---
                { (State.q_assign, "assign_eq"), State.q_eq_F }, // Завершено op_eq, F
                { (State.q_assign, "other"), State.q_assign_F }, // Завершено op_assign, Fstar
    
                { (State.q_not, "assign_eq"), State.q_neq_F }, // Завершено op_neq, F
                { (State.q_not, "other"), State.q_ERR_ExpectedEquals },    // Помилка: одинарний '!'

                { (State.q_lt, "assign_eq"), State.q_le_F },    // Завершено op_le, F
                { (State.q_lt, "other"), State.q_lt_F },      // Завершено op_lt, Fstar

                { (State.q_gt, "assign_eq"), State.q_ge_F },    // Завершено op_ge, F
                { (State.q_gt, "other"), State.q_gt_F }       // Завершено op_gt, Fstar
            };

            // Таблиця ключових слів
            _keywordTable = new Dictionary<string, TokenType>
            {
                { "var", TokenType.KwVar },
                { "const", TokenType.KwConst },
                { "func", TokenType.KwFunc },
                { "if", TokenType.KwIf },
                { "else", TokenType.KwElse },
                { "for", TokenType.KwFor },
                { "return", TokenType.KwReturn },
                { "break", TokenType.KwBreak },
                { "int", TokenType.KwInt },
                { "float64", TokenType.KwFloat64 },
                { "bool", TokenType.KwBool },
                { "string", TokenType.KwString },
                { "void", TokenType.KwVoid },
                { "read", TokenType.KwRead },
                { "write", TokenType.KwWrite }
            };

            // Таблиця токенів за станом
            _tokStateTable = new Dictionary<State, TokenType>
            {
                // Стани, що потребують повернення (Fstar)
                { State.q_id_F, TokenType.Id },
                { State.q_int_F, TokenType.IntConst },
                { State.q_real_F, TokenType.RealConst },
                { State.q_div_F, TokenType.OpDiv },
                { State.q_assign_F, TokenType.OpAssign },
                { State.q_lt_F, TokenType.OpLt },
                { State.q_gt_F, TokenType.OpGt },

                // Звичайні заключні стани (без повернення)
                { State.q_str_F, TokenType.StringConst },
                { State.q_eq_F, TokenType.OpEq },
                { State.q_le_F, TokenType.OpLe },
                { State.q_ge_F, TokenType.OpGe },
                { State.q_neq_F, TokenType.OpNeq },
    
                // Односимвольні оператори (без повернення)
                { State.q_add_F, TokenType.OpAdd },
                { State.q_sub_F, TokenType.OpSub },
                { State.q_mul_F, TokenType.OpMul },
                { State.q_pow_F, TokenType.OpPow },
                { State.q_lparen_F, TokenType.LParen },
                { State.q_rparen_F, TokenType.RParen },
                { State.q_lbrace_F, TokenType.LBrace },
                { State.q_rbrace_F, TokenType.RBrace },
                { State.q_comma_F, TokenType.Comma },
                { State.q_semicolon_F, TokenType.Semicolon }
            };

            // Множини заключних станів
            _F = new HashSet<State>
            {
                // Fstar (з поверненням символу)
                State.q_id_F,
                State.q_int_F,
                State.q_real_F,
                State.q_div_F,
                State.q_assign_F,
                State.q_lt_F,
                State.q_gt_F,
    
                // F (без повернення, звичайні)
                State.q_str_F,
                State.q_eq_F,
                State.q_le_F,
                State.q_ge_F,
                State.q_neq_F,

                // F (односимвольні оператори)
                State.q_add_F,
                State.q_sub_F,
                State.q_mul_F,
                State.q_pow_F,
                State.q_lparen_F,
                State.q_rparen_F,
                State.q_lbrace_F,
                State.q_rbrace_F,
                State.q_comma_F,
                State.q_semicolon_F,
                State.q_nl_F,
    
                // Ferror (стан помилки)
                State.q_ERR_IllegalChar,
                State.q_ERR_UnclosedString,
                State.q_ERR_InvalidNumber,
                State.q_ERR_ExpectedEquals
            };
            // Множина заключних станів, що потребують повернення (F*)
            _Fstar = new HashSet<State>
            {
                State.q_id_F,       // Напр., "myVar" + пробіл
                State.q_int_F,      // Напр., "123" + пробіл
                State.q_real_F,     // Напр., "1.23" + пробіл
                State.q_div_F,      // Напр., "/" + пробіл (шлях q0 -> q_slash -> q_div_F)
                State.q_assign_F,   // Напр., "=" + пробіл
                State.q_lt_F,       // Напр., "<" + пробіл
                State.q_gt_F        // Напр., ">" + пробіл
            };
            _Ferror = new HashSet<State> 
            { 
                State.q_ERR_IllegalChar,
                State.q_ERR_UnclosedString,
                State.q_ERR_InvalidNumber,
                State.q_ERR_ExpectedEquals,
            };
        }

        private string GetCharClass(char c)
        {
            if (c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z') return "Letter";
            if (c >= '0' && c <= '9') return "Digit";

            switch (c)
            {
                case ' ': return "ws";
                case '\t': return "ws";
                case '\n': return "nl";
                case '\r': return "ws"; // вважаємо повернення картеки просто пробілом щоб виправити нумерацію рядків

                case '.': return "dot";
                case '_': return "underscore";
                case '"': return "quote";
                case '/': return "slash";
                case '=': return "assign_eq";
                case '!': return "not";
                case '<': return "lt";
                case '>': return "gt";

                case '+':
                case '-':
                case '*':
                case '^':
                case '(':
                case ')':
                case '{':
                case '}':
                case ',':
                case ';':
                    return c.ToString();

                default:
                    return "other";
            }
        }

        private void Processing(char currentSymbol)
        {
            // 1. Обробка стану \n (лічильник рядків)
            if (_state == State.q_nl_F)
            {
                _state = State.q0;
                _lexeme = ""; // Очищуємо лексему (на випадок, якщо щось залишилось)
                return;
            }

            // 2. Обробка стану помилки
            if (_Ferror.Contains(_state))
            {
                Fail(currentSymbol);
                return;
            }

            // 3. Обробка повернення (Fstar)
            if (_Fstar.Contains(_state))
            {
                _scanner.Retract(); // Використовуємо сканер
            }
            else
            {
                // Якщо не Fstar, поточний символ є частиною лексеми
                _lexeme += currentSymbol;
            }

            // 4. Отримання ТИПУ токена
            TokenType tokenType = GetTokenType(_state, _lexeme);

            // 5. Створення "Значення" (Value)
            object value = ConvertLexemeToValue(_lexeme, tokenType);

            // 6. Створення токена
            AddToken(tokenType, value);

            // 7. Скидання
            _lexeme = "";
            _state = State.q0;
        }


        // Допоміжні методи, які викликає Processing()
        private TokenType GetTokenType(State state, string lexeme)
        {
            if (state == State.q_id_F)
            {
                // Перевіряємо, чи це ключове слово
                if (_keywordTable.TryGetValue(lexeme, out TokenType kwType))
                {
                    return kwType;
                }
                return TokenType.Id; // Це звичайний ID
            }
            // Беремо зі станової таблиці
            return _tokStateTable[state];
        }

        /// <summary>
        /// Обробляє таблиці ідентифікаторів та констант.
        /// Перевіряє, чи лексема вже існує в таблиці.
        /// Якщо ні - додає її.
        /// Повертає індекс лексеми в її таблиці.
        /// </summary>
        private object ConvertLexemeToValue(string lexeme, TokenType type)
        {
            switch (type)
            {
                case TokenType.IntConst:
                    return int.Parse(lexeme); // Або Int64.Parse
                case TokenType.RealConst:
                    return double.Parse(lexeme, System.Globalization.CultureInfo.InvariantCulture);
                case TokenType.StringConst:
                    // Видаляємо лапки
                    return lexeme.Substring(1, lexeme.Length - 2);
                case TokenType.BoolConst:
                    return lexeme == "true";
                default:
                    return null; // Для ID, операторів тощо value не потрібне
            }
        }
        private void AddToken(TokenType type, object value)
        {
            _tokens.Add(new Token(type, _lexeme, value, _lexemeStartLine, _lexemeStartColumn));
            // (Друк в консоль тепер можна робити тут,
            // викликавши _tokens.Last().ToString())
            Console.WriteLine(_tokens.Last().ToString());
        }

        // Обробка помилок
        private void Fail(char currentSymbol)
        {
            string displayableChar = Utils.GetDisplayableChar(currentSymbol);
            string errorMessage = _state switch
            {
                State.q_ERR_UnclosedString =>
                    "Unclosed string literal. String literals cannot span multiple lines.",

                State.q_ERR_InvalidNumber =>
                    $"Invalid number format. Expected a digit after '.', but got '{displayableChar}'.",

                State.q_ERR_ExpectedEquals =>
                    $"Invalid operator. Expected '=' after '!', but got '{displayableChar}'.",

                State.q_ERR_IllegalChar or _ =>
                    $"Illegal or unexpected character: '{displayableChar}'"
            };
            string fullMessage = $"Lexer Error: (Line {_lexemeStartLine}, Column {_lexemeStartColumn}) -> {errorMessage}";
            throw new LexerException(fullMessage);
        }
    }
}
