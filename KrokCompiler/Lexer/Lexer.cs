namespace KrokCompiler.Lexer
{
    public class Lexer
    {
        // 1. Таблиці станів та токенів
        private Dictionary<(State, string), State> _stf; // Функція переходів (Діаграма станів)
        private HashSet<State> _F;                      // Множина заключних станів
        private HashSet<State> _Fstar;                  // Стани, що потребують повернення
        private HashSet<State> _Ferror;                 // Стани помилок

        private Dictionary<string, string> _keywordTable; // Таблиця ключових слів
        private Dictionary<State, string> _tokStateTable;  // Таблиця токенів за станом

        // 2. Стан аналізатора
        private State _state;          // Поточний стан (int у вашому прикладі)
        private int _numLine;          // Лічильник рядків
        private string _lexeme;        // Поточна лексема, що будується
        private char _char;            // Поточний символ

        private string _sourceCode;    // Весь вхідний код
        private int _numChar;          // Індекс поточного символу

        // 3. Вихідні таблиці (результат роботи)
        private List<SymbolTableEntry> _tableOfSymb;
        private Dictionary<string, int> _tableOfId;
        private Dictionary<string, (string, int)> _tableOfConst;

        public Lexer()
        {
            InitializeTables();
            _tableOfId = new Dictionary<string, int>();
            _tableOfConst = new Dictionary<string, (string, int)>();

        }

        public List<SymbolTableEntry> Analyze(string sourceCode)
        {
            if (string.IsNullOrWhiteSpace(sourceCode))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: empty source file");
                Console.ResetColor();
                throw new Exception();
            }
            _sourceCode = sourceCode;
            _numChar = -1;
            _numLine = 1;
            _state = State.q0;
            _lexeme = "";

            // Ініціалізуємо вихідні таблиці
            _tableOfSymb = new List<SymbolTableEntry>();
            _tableOfId = new Dictionary<string, int>();
            _tableOfConst = new Dictionary<string, (string, int)>();

            try
            {
                while (_numChar < _sourceCode.Length - 1)
                {
                    _char = NextChar();
                    string charClass = GetCharClass(_char);
                    _state = NextState(_state, charClass);

                    if (_F.Contains(_state)) // Якщо стан заключний
                    {
                        Processing();
                    }
                    else if (_state == State.q0) // Якщо стан стартовий (ігнор)
                    {
                        _lexeme = "";
                    }
                    else // Інакше - робочий стан
                    {
                        _lexeme += _char; // Додати символ до лексеми
                    }
                }
                PrintIdentifiers();
                PrintConstants();
                PrintLexerSuccess();
                return _tableOfSymb;
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
        
        private void PrintLexerSuccess()
        {            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Lexer: Lexical analysis completed successfully");
            Console.ResetColor();
        }

        private void PrintIdentifiers()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("-----------------------");
            Console.WriteLine("Identifiers:");
            Console.ResetColor();
            foreach (var item in _tableOfId)
            {
                Console.WriteLine($"{item.Key}  {item.Value}");
            }
        }
        private void PrintConstants()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("-----------------------");
            Console.WriteLine("Constants:");
            Console.ResetColor();
            foreach (var item in _tableOfConst)
            {
                Console.WriteLine($"{item.Key}  {item.Value}");
            }
        }

        private char NextChar()
        {
            _numChar++;
            return _sourceCode[_numChar];
        }

        private void PutCharBack()
        {
            _numChar--;
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

            // Якщо не знайдено нічого (помилка в самій STF),
            // повертаємо помилку (або кидаємо виняток)
            return State.q_ERR_F;
        }


        public class LexerException : Exception
        {
            public LexerException(string message) : base(message) { }
        }

        private void InitializeTables()
        {
            // Таблиця переходів
            _stf = new Dictionary<(State, string), State>
            {
                // --- q0 (Стартовий стан) ---
                { (State.q0, "ws"), State.q0 },             // Ігнорувати пробіл
                { (State.q0, "nl"), State.q_nl_F },         // Ігнорувати + лічильник
                { (State.q0, "other"), State.q_ERR_F },     // Помилка (нерозпізнаний символ)

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
                { (State.q_num_real_pre, "other"), State.q_ERR_F }, // Помилка: одинарна крапка

                { (State.q_num_real_post, "Digit"), State.q_num_real_post },
                { (State.q_num_real_post, "other"), State.q_real_F }, // Завершено real, Fstar
    
                // --- Розпізнавання рядка ---
                { (State.q_str, "quote"), State.q_str_F },      // Завершено string, F
                { (State.q_str, "nl"), State.q_ERR_F },        // Помилка: незакритий рядок
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
                { (State.q_not, "other"), State.q_ERR_F },    // Помилка: одинарний '!'

                { (State.q_lt, "assign_eq"), State.q_le_F },    // Завершено op_le, F
                { (State.q_lt, "other"), State.q_lt_F },      // Завершено op_lt, Fstar

                { (State.q_gt, "assign_eq"), State.q_ge_F },    // Завершено op_ge, F
                { (State.q_gt, "other"), State.q_gt_F }       // Завершено op_gt, Fstar
            };

            // Таблиця ключових слів
            _keywordTable = new Dictionary<string, string>
            {
                { "var", "kw_var" },
                { "const", "kw_const" },
                { "func", "kw_func" },
                { "if", "kw_if" },
                { "else", "kw_else" },
                { "for", "kw_for" },
                { "return", "kw_return" },
                { "break", "kw_break" },
                { "int", "kw_int" },
                { "float64", "kw_float64" },
                { "bool", "kw_bool" },
                { "string", "kw_string" },
                { "void", "kw_void" },
                { "read", "kw_read" },
                { "write", "kw_write" }
            };

            // Таблиця токенів за станом
            _tokStateTable = new Dictionary<State, string>
            {
                // Стани, що потребують повернення (Fstar)
                { State.q_id_F, "id" },
                { State.q_int_F, "int_const" },
                { State.q_real_F, "real_const" },
                { State.q_div_F, "op_div" },
                { State.q_assign_F, "op_assign" },
                { State.q_lt_F, "op_lt" },
                { State.q_gt_F, "op_gt" },

                // Звичайні заключні стани (без повернення)
                { State.q_str_F, "string_const" },
                { State.q_eq_F, "op_eq" },
                { State.q_le_F, "op_le" },
                { State.q_ge_F, "op_ge" },
                { State.q_neq_F, "op_neq" },
    
                // Односимвольні оператори (без повернення)
                { State.q_add_F, "op_add" },
                { State.q_sub_F, "op_sub" },
                { State.q_mul_F, "op_mul" },
                { State.q_pow_F, "op_pow" },
                { State.q_lparen_F, "lparen" },
                { State.q_rparen_F, "rparen" },
                { State.q_lbrace_F, "lbrace" },
                { State.q_rbrace_F, "rbrace" },
                { State.q_comma_F, "comma" },
                { State.q_semicolon_F, "semicolon" }
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
                State.q_ERR_F
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
            _Ferror = new HashSet<State> { State.q_ERR_F };
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

        private void Processing()
        {
            // 1. Обробка стану \n (лічильник рядків)
            if (_state == State.q_nl_F)
            {
                _numLine++;
                _state = State.q0;
                _lexeme = ""; // Очищуємо лексему (на випадок, якщо щось залишилось)
                return;
            }

            // 2. Обробка стану помилки
            if (_Ferror.Contains(_state))
            {
                Fail();
                return;
            }

            // 3. Визначення, чи потрібне повернення (retract)
            if (_Fstar.Contains(_state))
            {
                PutCharBack();
            }
            else
            {
                // Якщо стан НЕ Fstar, це значить, що поточний символ
                // є частиною лексеми (напр. "==", "!", '"').
                // Додаємо символ до лексеми.
                _lexeme += _char;
            }

            // 5. Отримання токена
            string token = GetToken(_state, _lexeme);

            // 6. Обробка таблиць ID та констант
            if (_state == State.q_id_F ||
                _state == State.q_int_F ||
                _state == State.q_real_F ||
                _state == State.q_str_F) // Рядки - це теж константи
            {
                // Перевіряємо, чи це не ключове слово, 
                // перш ніж додавати до таблиці ID
                if (token != "id" && token != "int_const" &&
                    token != "real_const" && token != "string_const")
                {
                    // Це ключове слово (напр. "kw_var"), індекс не потрібен
                    AddSymbolToTable(_lexeme, token, null);
                }
                else
                {
                    // Це id або константа, потрібен індекс
                    int? index = IndexIdConst(_lexeme, token);
                    AddSymbolToTable(_lexeme, token, index);
                }
            }
            else // Для решти токенів (оператори)
            {
                AddSymbolToTable(_lexeme, token, null);
            }

            // 7. Скидання для наступної лексеми
            _lexeme = "";
            _state = State.q0;
        }

        // Допоміжні методи, які викликає Processing()
        private string GetToken(State state, string lexeme)
        {
            // Спершу перевіряємо, чи це стан id, який може бути ключовим словом
            if (state == State.q_id_F)
            {
                if (_keywordTable.TryGetValue(lexeme, out string keywordToken))
                {
                    return keywordToken; // Це ключове слово (напр. "kw_var")
                }
            }

            // Інакше беремо токен зі станової таблиці
            if (_tokStateTable.TryGetValue(state, out string token))
            {
                return token; // Напр. "id", "int_const", "op_assign"
            }
            return "UNKNOWN";
        }

        /// <summary>
        /// Обробляє таблиці ідентифікаторів та констант.
        /// Перевіряє, чи лексема вже існує в таблиці.
        /// Якщо ні - додає її.
        /// Повертає індекс лексеми в її таблиці.
        /// </summary>
        private int IndexIdConst(string lexeme, string token)
        {
            if (token == "id")
            {
                // 1. Це Ідентифікатор
                if (_tableOfId.TryGetValue(lexeme, out int index))
                {
                    // Вже бачили цей id, повертаємо існуючий індекс
                    return index;
                }
                else
                {
                    // Новий id, додаємо його
                    int newIndex = _tableOfId.Count + 1; // Індекси зазвичай з 1
                    _tableOfId.Add(lexeme, newIndex);
                    return newIndex;
                }
            }
            else
            {
                // 2. Це Константа (int_const або real_const)
                if (_tableOfConst.TryGetValue(lexeme, out (string, int) entry))
                {
                    // Вже бачили цю константу
                    return entry.Item2; // Повертаємо її індекс
                }
                else
                {
                    // Нова константа
                    int newIndex = _tableOfConst.Count + 1;
                    // Зберігаємо (токен, індекс), напр. ("int_const", 1)
                    _tableOfConst.Add(lexeme, (token, newIndex));
                    return newIndex;
                }
            }
        }

        private void AddSymbolToTable(string lexeme, string token, int? index)
        {
            var entry = new SymbolTableEntry
            {
                LineNum = _numLine,
                Lexeme = lexeme,
                Token = token,
                IndexIdConst = index
            };
            _tableOfSymb.Add(entry);
            Console.WriteLine(entry.ToString());
        }

        // Обробка помилок
        private void Fail()
        {
            string displayableChar = Utils.GetDisplayableChar(_char);
            string message = $"Lexer: Line {_numLine} has unexpected symbol '{displayableChar}'";
            throw new LexerException(message);
        }
    }
}
