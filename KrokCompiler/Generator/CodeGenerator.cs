using KrokCompiler.Abstractions;
using KrokCompiler.Models;

namespace KrokCompiler.Generator
{
    public class CodeGenerator : IAstVisitor
    {
        private readonly Dictionary<string, PsmModule> _modules = new();
        private PsmModule _currentModule;
        private int _labelCounter = 0;

        // Назва головного модуля (файлу)
        private string _mainModuleName;

        public CodeGenerator(string mainModuleName = "program")
        {
            _mainModuleName = mainModuleName;
            // Створюємо головний модуль
            _currentModule = new PsmModule(_mainModuleName);
            _modules.Add(_mainModuleName, _currentModule);
        }

        // Повертає словник: Ім'я файлу -> Вміст файлу
        public Dictionary<string, string> Generate(ProgramNode program)
        {
            program.Accept(this);

            var result = new Dictionary<string, string>();
            foreach (var mod in _modules.Values)
            {
                result.Add(mod.Name + ".postfix", mod.GenerateSource());
            }
            return result;
        }

        // Допоміжні методи емісії коду
        private void Emit(string lexeme, string token)
        {
            // Форматування: лексема зліва (20 символів), токен справа
            _currentModule.CodeSection.AppendLine($"    {lexeme,-20} {token}");
        }

        private void EmitLabel(string label)
        {
            Emit(label, "label");
            Emit(":", "colon");
        }

        private string NewLabel()
        {
            string lbl = "m" + (++_labelCounter);
            _currentModule.Labels.Add(lbl);
            return lbl;
        }

        private string FormatType(string krokType)
        {
            return krokType switch
            {
                "float64" => "float",
                _ => krokType
            };
        }

        // Visitor Methods
        public void VisitProgramNode(ProgramNode node)
        {
            // 1. Спочатку зберемо всі глобальні оголошення
            foreach (var stmt in node.Statements)
            {
                // Обробляємо глобальні змінні та константи
                if (stmt is VarDeclStmt || stmt is ConstDeclStmt)
                {
                    stmt.Accept(this);
                }
            }

            // 2. Обробляємо функції (вони створять нові модулі)
            foreach (var stmt in node.Statements.OfType<FuncDeclStmt>())
            {
                stmt.Accept(this);
            }

            // 3. PSM потрібен код у головному файлі.
            // Зазвичай Krok починається з main(), тому
            // додамо виклик main() у .code головного файлу.
            if (_modules.ContainsKey(_mainModuleName))
            {
                // Переключаємось на головний модуль, щоб дописати старт
                _currentModule = _modules[_mainModuleName];

                // Якщо є функція main, викликаємо її
                var mainFunc = node.Statements.OfType<FuncDeclStmt>()
                    .FirstOrDefault(f => f.Name.Lexeme == "main");

                if (mainFunc != null)
                {
                    Emit(_mainModuleName + "$main", "CALL");
                }
            }
        }

        public void VisitVarDeclStmt(VarDeclStmt stmt)
        {
            // Додаємо в секцію .vars поточного модуля
            string type = FormatType(stmt.Type.Lexeme);
            _currentModule.VarSection.AppendLine($"    {stmt.Name.Lexeme,-20} {type}");
        }

        public void VisitConstDeclStmt(ConstDeclStmt stmt)
        {
            // PSM не має поняття "константа" в пам'яті. 
            // Ми оголосимо це як змінну і одразу присвоїмо значення.

            string name = stmt.Name.Lexeme;

            // 1. Оголошуємо як змінну (але тип треба вгадати/отримати)
            // Для простоти візьмемо int
            // Оскільки у нас тут немає доступу до типів, 
            // припустимо, що це глобальна ініціалізація в коді.

            // ХАК: Оголошуємо як int/float/string базуючись на ініціалізаторі
            string type = "int";
            if (stmt.Initializer is LiteralExpr l)
            {
                if (l.Value is double) type = "float";
                if (l.Value is string) type = "string";
                if (l.Value is bool) type = "bool";
            }

            _currentModule.VarSection.AppendLine($"    {name,-20} {type}");

            // 2. Генеруємо код присвоєння (тільки якщо ми в середині функції або це глобальна ініціалізація)
            // Для глобальних констант в Krok код ініціалізації має йти на початку .code
            Emit(name, "l-val");
            stmt.Initializer.Accept(this);
            Emit("=", "assign_op");
        }

        public void VisitFuncDeclStmt(FuncDeclStmt stmt)
        {
            // Зберігаємо посилання на старий модуль
            var parentModule = _currentModule;

            // Створюємо новий модуль для функції
            string funcModuleName = parentModule.Name + "$" + stmt.Name.Lexeme;
            var funcModule = new PsmModule(funcModuleName);
            _modules.Add(funcModuleName, funcModule);

            // Переключаємо контекст
            _currentModule = funcModule;

            // Додаємо прототип у секцію .funcs батьківського модуля
            string retType = FormatType(stmt.ReturnType.Lexeme);
            parentModule.FuncSection.AppendLine($"    {funcModuleName,-30} {retType,-10} {stmt.Parameters.Count}");

            // Оголошуємо параметри як локальні змінні у новому модулі
            foreach (var param in stmt.Parameters)
            {
                _currentModule.VarSection.AppendLine($"    {param.Name.Lexeme,-20} {FormatType(param.Type.Lexeme)}");
            }

            // Генеруємо тіло функції
            stmt.Body.Accept(this);

            // Додаємо неявний return в кінці (для безпеки)
            Emit("RET", "ret_op");

            // Повертаємо контекст
            _currentModule = parentModule;
        }

        public void VisitBlockStmt(BlockStmt stmt)
        {
            foreach (var s in stmt.Statements)
            {
                s.Accept(this);
            }
        }

        public void VisitAssignStmt(AssignStmt stmt)
        {
            Emit(stmt.Name.Lexeme, "l-val");
            stmt.Value.Accept(this);
            Emit("=", "assign_op");
        }

        public void VisitWriteStmt(WriteStmt stmt)
        {
            foreach (var arg in stmt.Arguments)
            {
                arg.Accept(this); // Обчислюємо вираз на вершину стека
                Emit("OUT", "out_op");
            }
        }

        public void VisitReadStmt(ReadStmt stmt)
        {
            foreach (var varToken in stmt.Variables)
            {
                Emit("INP", "inp_op");
                Emit(varToken.Lexeme, "l-val"); // Куди писати
                Emit("SWAP", "stack_op");       // INP кладе значення, l-val кладе адресу. Треба: addr, val
                                                // У PSM assign_op: "Знімає l-value, знімає r-value". 
                                                // Отже стек має бути: [l-value, r-value].
                                                // INP дає [val]. l-val дає [val, addr]. 
                                                // Тому треба SWAP -> [addr, val].
                Emit("=", "assign_op");
            }
        }

        public void VisitIfStmt(IfStmt stmt)
        {
            string labelElse = NewLabel();
            string labelEnd = NewLabel();

            // Умова
            stmt.Condition.Accept(this);

            // Якщо хибно - стрибаємо на Else
            Emit(labelElse, "label");
            Emit("JF", "jf");

            // Тіло Then
            stmt.ThenBranch.Accept(this);
            Emit(labelEnd, "label");
            Emit("JUMP", "jump");

            // Мітка Else
            EmitLabel(labelElse);

            // Тіло Else
            if (stmt.ElseBranch != null)
            {
                stmt.ElseBranch.Accept(this);
            }

            // Мітка End
            EmitLabel(labelEnd);
        }

        public void VisitForStmt(ForStmt stmt)
        {
            string labelStart = NewLabel();
            string labelEnd = NewLabel();

            // 1. Ініціалізація
            stmt.Initializer?.Accept(this);

            // Мітка початку циклу
            EmitLabel(labelStart);

            // 2. Умова
            if (stmt.Condition != null)
            {
                stmt.Condition.Accept(this);
                Emit(labelEnd, "label");
                Emit("JF", "jf");
            }

            // 3. Тіло
            stmt.Body.Accept(this);

            // 4. Інкремент
            stmt.Increment?.Accept(this);

            // Стрибок на початок
            Emit(labelStart, "label");
            Emit("JUMP", "jump");

            // Мітка виходу
            EmitLabel(labelEnd);
        }

        public void VisitBreakStmt(BreakStmt stmt)
        {
            // Це спрощення. На скільки я розумію в реальному компіляторі треба зберігати стек циклів, щоб знати поточний 'labelEnd'.
            // В цій реалізації ми припустимо, що 'mX' - це остання згенерована мітка End. Для коректної роботи треба стек міток у класі CodeGenerator.
            // Поки що залишу цей коментар і дороблю, якщол встигну
            // Emit("JUMP_TO_END", "jump"); 
        }

        public void VisitReturnStmt(ReturnStmt stmt)
        {
            if (stmt.Value != null)
            {
                stmt.Value.Accept(this);
            }
            Emit("RET", "ret_op");
        }

        public void VisitExpressionStmt(ExpressionStmt stmt)
        {
            stmt.Expression.Accept(this);
            // Якщо вираз повертає значення, а ми його не використовуємо (наприклад, виклик int-функції),
            // треба очистити стек. Але якщо це void-функція, то нічого робити не треба.
            // Для простоти наразі припускаємо, що це void.
        }

        // --- Вирази ---

        public void VisitBinaryExpr(BinaryExpr expr)
        {
            expr.Left.Accept(this);
            expr.Right.Accept(this);

            string op = expr.Operator.Type switch
            {
                TokenType.OpAdd => "+",
                TokenType.OpSub => "-",
                TokenType.OpMul => "*",
                TokenType.OpDiv => "/",
                TokenType.OpPow => "^",
                TokenType.OpEq => "=",
                TokenType.OpNeq => "!=",
                TokenType.OpLt => "<",
                TokenType.OpLe => "<=",
                TokenType.OpGt => ">",
                TokenType.OpGe => ">=",
                _ => ""
            };

            string type = expr.Operator.Type switch
            {
                TokenType.OpEq or TokenType.OpNeq or
                TokenType.OpLt or TokenType.OpLe or
                TokenType.OpGt or TokenType.OpGe => "rel_op",
                _ => "math_op"
            };

            // Для конкатенації рядків в PSM є окрема команда CAT
            // Я про це не знав на етапі написання семантичного аналізатора,
            // тому ми не знаємо типів, тому генеруємо math_op, 
            // але якщо це рядки - PSM впаде. Це обмеження без повної типізації в AST
            // TODO: спробувати виправити

            Emit(op, type);
        }

        public void VisitUnaryExpr(UnaryExpr expr)
        {
            expr.Right.Accept(this);
            if (expr.Operator.Type == TokenType.OpSub)
            {
                Emit("NEG", "math_op");
            }
        }

        public void VisitLiteralExpr(LiteralExpr expr)
        {
            if (expr.Value is int i) Emit(i.ToString(), "int");
            else if (expr.Value is double d) Emit(d.ToString().Replace(',', '.'), "float");
            else if (expr.Value is bool b) Emit(b ? "TRUE" : "FALSE", "bool");
            else if (expr.Value is string s) Emit($"\"{s}\"", "string");
        }

        public void VisitVariableExpr(VariableExpr expr)
        {
            Emit(expr.Name.Lexeme, "r-val");
        }

        public void VisitCallExpr(CallExpr expr)
        {
            // 1. Аргументи кладуться на стек
            foreach (var arg in expr.Arguments)
            {
                arg.Accept(this);
            }

            // 2. Виклик (ім'я функції має бути повним, наприклад program$func)
            // Тут ми припускаємо, що викликаємо функцію з того ж модуля або вкладену (хоч зараз це і не підтримується).
            // Для простоти генеруємо: CurrentModule$FuncName
            string callee = ((VariableExpr)expr.Callee).Name.Lexeme;

            // Потрібна логіка дозволу імен. 
            // Припускаємо, що всі функції оголошені в головному файлі (program).
            string fullName = _mainModuleName + "$" + callee;

            Emit(fullName, "CALL");
        }

        public void VisitCastExpr(CastExpr expr)
        {
            expr.Expression.Accept(this);
            string convOp = expr.Type.Type switch
            {
                TokenType.KwFloat64 => "i2f", // Припускаємо int -> float
                TokenType.KwInt => "f2i",     // Припускаємо float -> int
                TokenType.KwString => "i2s",  // Припускаємо int -> string
                _ => ""
            };
            if (!string.IsNullOrEmpty(convOp))
            {
                Emit(convOp, "conv");
            }
        }

        // Заглушки
        public void VisitParameterDeclStmt(ParameterDeclStmt stmt) { }
        public void VisitNopStmt(NopStmt stmt) { }
        public void VisitAstListNode(AstListNode node) { }
    }
}
