using KrokCompiler.Abstractions;
using KrokCompiler.Models;

/// <summary>
/// Реалізує IAstVisitor для виконання семантичного аналізу.
/// Перевіряє оголошення, типи, області видимості та ін.
/// </summary>
public class SemanticAnalyzer : IAstVisitor
{
    // === Внутрішній стан аналізатора ===

    // Стек областей видимості для ЗМІННИХ
    private readonly SymbolTable _variableScopes = new SymbolTable();

    // Глобальний словник для ФУНКЦІЙ (дозволяє виклик до оголошення)
    private readonly Dictionary<string, FunctionSignature> _functionSignatures = new();

    // "Канал" для повернення типу з рекурсивних викликів Visit...
    private KrokType _lastExpressionType = KrokType.Void;

    // Зберігає сигнатуру поточної функції (для перевірки 'return')
    private FunctionSignature? _currentFunctionSignature = null;

    // Зберігає, чи ми всередині циклу (для перевірки 'break')
    private int _loopDepth = 0;

    /// <summary>
    /// Допоміжний клас для зберігання сигнатур функцій
    /// </summary>
    private class FunctionSignature
    {
        public KrokType ReturnType { get; }
        public List<KrokType> ParamTypes { get; }

        public FunctionSignature(KrokType returnType, List<KrokType> paramTypes)
        {
            ReturnType = returnType;
            ParamTypes = paramTypes;
        }
    }

    // === Головні публічні методи ===

    /// <summary>
    /// Виконує повний семантичний аналіз AST.
    /// Виконує 2 проходи: 1-й для збору функцій, 2-й для аналізу.
    /// </summary>
    public void Analyze(ProgramNode node)
    {
        // --- ПЕРШИЙ ПРОХІД: Збір сигнатур функцій ---
        // Це дозволяє викликати функції до їх оголошення.
        CollectFunctionSignatures(node.Statements);

        if (!_functionSignatures.ContainsKey("main"))
        {
            // У нас немає токена для цієї помилки, тому кидаємо 
            // загальну помилку, посилаючись на початок файлу (рядок 1)
            throw new SemanticException("Undefined entry point: 'main' function was not found.",
                new Token(TokenType.Eof, "", null, 1, 1)); // (або null, якщо токен не потрібен)
        }
        // --- ДРУГИЙ ПРОХІД: Повний аналіз ---
        // (Ми також могли б зробити це за 1 прохід, 
        // але тоді Krok не підтримував би виклик функцій, 
        // оголошених пізніше)
        node.Accept(this);
    }

    private void CollectFunctionSignatures(List<Stmt> statements)
    {
        foreach (var stmt in statements)
        {
            if (stmt is FuncDeclStmt funcStmt)
            {
                var funcName = funcStmt.Name.Lexeme;
                if (_functionSignatures.ContainsKey(funcName))
                {
                    throw new SemanticException($"Function '{funcName}' is already declared.", funcStmt.Name);
                }

                var returnType = TokenTypeToKrokType(funcStmt.ReturnType);
                var paramTypes = funcStmt.Parameters
                    .Select(p => TokenTypeToKrokType(p.Type))
                    .ToList();

                var signature = new FunctionSignature(returnType, paramTypes);
                _functionSignatures.Add(funcName, signature);
            }
        }
    }

    // === Допоміжні методи ===

    private KrokType TokenTypeToKrokType(Token typeToken)
    {
        return typeToken.Type switch
        {
            TokenType.KwInt => KrokType.Int,
            TokenType.KwFloat64 => KrokType.Float64,
            TokenType.KwBool => KrokType.Bool,
            TokenType.KwString => KrokType.String,
            TokenType.KwVoid => KrokType.Void,
            _ => KrokType.Error
        };
    }

    /// <summary>
    /// Перевіряє, чи можна 'actualType' безпечно присвоїти 'expectedType'.
    /// (Дозволяє неявне приведення int -> float64).
    /// </summary>
    private void CheckTypeAssignment(KrokType expectedType, KrokType actualType, Token errorToken)
    {
        if (expectedType == actualType) return; // Ідеально

        if (expectedType == KrokType.Float64 && actualType == KrokType.Int)
        {
            return; // Дозволяємо неявне приведення
        }

        throw new SemanticException($"Cannot assign type '{actualType}' to expected type '{expectedType}'.", errorToken);
    }

    // === РЕАЛІЗАЦІЯ IAstVisitor (ДРУГИЙ ПРОХІД) ===

    public void VisitProgramNode(ProgramNode node)
    {
        foreach (var stmt in node.Statements)
        {
            stmt.Accept(this);
        }
    }

    public void VisitBlockStmt(BlockStmt stmt)
    {
        _variableScopes.BeginScope(); // <--- ПОЧАТОК ОБЛАСТІ ВИДИМОСТІ
        foreach (var statement in stmt.Statements)
        {
            statement.Accept(this);
        }
        _variableScopes.EndScope(); // <--- КІНЕЦЬ ОБЛАСТІ ВИДИМОСТІ
    }

    public void VisitFuncDeclStmt(FuncDeclStmt stmt)
    {
        // Сигнатуру ми вже зібрали на 1-му проході.
        // Тепер аналізуємо тіло.
        var signature = _functionSignatures[stmt.Name.Lexeme];
        _currentFunctionSignature = signature; // Встановлюємо контекст для 'return'

        _variableScopes.BeginScope(); // Нова область для параметрів і тіла

        // Оголошуємо параметри як змінні у цій новій області
        foreach (var p in stmt.Parameters)
        {
            var pType = TokenTypeToKrokType(p.Type);
            _variableScopes.Define(p.Name.Lexeme, pType); // (Тут вже немає помилок, бо парсер перевірив дублікати)
        }

        // Відвідуємо тіло
        stmt.Body.Accept(this);

        _variableScopes.EndScope(); // Завершуємо область
        _currentFunctionSignature = null; // Виходимо з функції
    }

    // Цей метод ніколи не викликається, 
    // оскільки параметри обробляються в 'VisitFuncDeclStmt'
    public void VisitParameterDeclStmt(ParameterDeclStmt stmt) { }

    public void VisitVarDeclStmt(VarDeclStmt stmt)
    {
        var type = TokenTypeToKrokType(stmt.Type);
        if (!_variableScopes.Define(stmt.Name.Lexeme, type))
        {
            throw new SemanticException($"Variable '{stmt.Name.Lexeme}' is already declared in this scope.", stmt.Name);
        }
    }

    public void VisitConstDeclStmt(ConstDeclStmt stmt)
    {
        // Перевіряємо праву частину (ініціалізатор)
        stmt.Initializer.Accept(this);
        var exprType = _lastExpressionType;

        // (Тут має бути перевірка, чи ініціалізатор - це *дійсно* константа.
        // Для простоти ми це опускаємо і довіряємо парсеру).

        if (!_variableScopes.Define(stmt.Name.Lexeme, exprType))
        {
            throw new SemanticException($"Constant '{stmt.Name.Lexeme}' is already declared in this scope.", stmt.Name);
        }
    }

    public void VisitExpressionStmt(ExpressionStmt stmt)
    {
        // Це для викликів void-функцій. Просто відвідуємо.
        stmt.Expression.Accept(this);
    }

    public void VisitAssignStmt(AssignStmt stmt)
    {
        // 1. Перевіряємо, чи існує змінна (ліва частина)
        var symbol = _variableScopes.Lookup(stmt.Name.Lexeme);
        if (symbol == null)
        {
            throw new SemanticException($"Undeclared variable '{stmt.Name.Lexeme}'.", stmt.Name);
        }

        // 2. Перевіряємо тип виразу (права частина)
        stmt.Value.Accept(this);
        var valueType = _lastExpressionType;

        // 3. Перевіряємо сумісність
        CheckTypeAssignment(symbol.Type, valueType, stmt.Name);
    }

    public void VisitIfStmt(IfStmt stmt)
    {
        stmt.Condition.Accept(this);
        if (_lastExpressionType != KrokType.Bool)
        {
            throw new SemanticException("If condition must be a boolean (true/false) value.", (stmt.Condition as BinaryExpr)?.Operator ?? (stmt.Condition as VariableExpr)?.Name);
        }

        stmt.ThenBranch.Accept(this);
        stmt.ElseBranch?.Accept(this);
    }

    public void VisitForStmt(ForStmt stmt)
    {
        _loopDepth++; // Ми увійшли в цикл
        _variableScopes.BeginScope(); // 'for' створює власну область видимості

        stmt.Initializer?.Accept(this);

        if (stmt.Condition != null)
        {
            stmt.Condition.Accept(this);
            if (_lastExpressionType != KrokType.Bool)
            {
                throw new SemanticException("For-loop condition must be a boolean value.", null); // TODO: Потрібен кращий токен
            }
        }

        stmt.Increment?.Accept(this);
        stmt.Body.Accept(this);

        _variableScopes.EndScope();
        _loopDepth--; // Ми вийшли з циклу
    }

    public void VisitReturnStmt(ReturnStmt stmt)
    {
        if (_currentFunctionSignature == null)
        {
            throw new SemanticException("'return' cannot be used outside of a function.", stmt.Keyword);
        }

        var expectedType = _currentFunctionSignature.ReturnType;

        if (stmt.Value == null) // 'return;'
        {
            if (expectedType != KrokType.Void)
            {
                throw new SemanticException($"Function must return a value of type '{expectedType}'.", stmt.Keyword);
            }
        }
        else // 'return expr;'
        {
            if (expectedType == KrokType.Void)
            {
                throw new SemanticException("A 'void' function cannot return a value.", stmt.Keyword);
            }

            stmt.Value.Accept(this);
            var actualType = _lastExpressionType;
            CheckTypeAssignment(expectedType, actualType, stmt.Keyword);
        }
    }

    public void VisitBreakStmt(BreakStmt stmt)
    {
        if (_loopDepth == 0)
        {
            throw new SemanticException("'break' can only be used inside a loop.", stmt.Keyword);
        }
    }

    public void VisitWriteStmt(WriteStmt stmt)
    {
        // 'write' може друкувати будь-які типи.
        // Ми просто відвідуємо аргументи, щоб перевірити, 
        // що вони є валідними виразами.
        foreach (var arg in stmt.Arguments)
        {
            arg.Accept(this);
            if (_lastExpressionType == KrokType.Void)
            {
                throw new SemanticException("'write' cannot print a 'void' value.", stmt.Keyword);
            }
        }
    }

    public void VisitReadStmt(ReadStmt stmt)
    {
        // Перевіряємо, що кожна змінна в read() оголошена
        foreach (var varToken in stmt.Variables)
        {
            var symbol = _variableScopes.Lookup(varToken.Lexeme);
            if (symbol == null)
            {
                throw new SemanticException($"Undeclared variable '{varToken.Lexeme}' in 'read' statement.", varToken);
            }
        }
    }

    // === Вирази ===

    public void VisitBinaryExpr(BinaryExpr expr)
    {
        expr.Left.Accept(this);
        var leftType = _lastExpressionType;

        expr.Right.Accept(this);
        var rightType = _lastExpressionType;

        switch (expr.Operator.Type)
        {
            case TokenType.OpAdd:
                if (leftType == KrokType.Int && rightType == KrokType.Int) _lastExpressionType = KrokType.Int;
                else if (leftType == KrokType.String && rightType == KrokType.String) _lastExpressionType = KrokType.String; // Конкатенація
                else if ((leftType == KrokType.Float64 && rightType == KrokType.Float64) ||
                         (leftType == KrokType.Float64 && rightType == KrokType.Int) ||
                         (leftType == KrokType.Int && rightType == KrokType.Float64)) _lastExpressionType = KrokType.Float64;
                else
                    throw new SemanticException($"Cannot add '{leftType}' and '{rightType}'.", expr.Operator);
                break;

            case TokenType.OpSub:
            case TokenType.OpMul:
            case TokenType.OpDiv:
                if (leftType == KrokType.Int && rightType == KrokType.Int) _lastExpressionType = (expr.Operator.Type == TokenType.OpDiv) ? KrokType.Float64 : KrokType.Int; // (Припускаємо, що / завжди повертає float)
                else if ((leftType == KrokType.Float64 && rightType == KrokType.Float64) ||
                        (leftType == KrokType.Float64 && rightType == KrokType.Int) ||
                        (leftType == KrokType.Int && rightType == KrokType.Float64)) _lastExpressionType = KrokType.Float64;
                else
                    throw new SemanticException($"Cannot perform '{expr.Operator.Lexeme}' on '{leftType}' and '{rightType}'.", expr.Operator);

                // (Тут треба додати перевірку ділення на нуль, якщо права частина - Literal(0))
                break;

            case TokenType.OpPow: // Піднесення до степеня
                if ((leftType == KrokType.Int || leftType == KrokType.Float64) &&
                   (rightType == KrokType.Int || rightType == KrokType.Float64))
                    _lastExpressionType = KrokType.Float64; // Завжди повертає float
                else
                    throw new SemanticException($"Cannot perform '^' on '{leftType}' and '{rightType}'.", expr.Operator);
                break;

            case TokenType.OpGt:
            case TokenType.OpGe:
            case TokenType.OpLt:
            case TokenType.OpLe:
                if ((leftType == KrokType.Int || leftType == KrokType.Float64) &&
                    (rightType == KrokType.Int || rightType == KrokType.Float64))
                    _lastExpressionType = KrokType.Bool;
                else
                    throw new SemanticException($"Cannot compare '{leftType}' and '{rightType}'.", expr.Operator);
                break;

            case TokenType.OpEq:
            case TokenType.OpNeq:
                // Дозволяємо порівняння всіх базових типів (крім void)
                if (leftType == KrokType.Void || rightType == KrokType.Void)
                    throw new SemanticException($"Cannot compare '{leftType}' and '{rightType}' for equality.", expr.Operator);

                if (leftType == rightType) _lastExpressionType = KrokType.Bool;
                else if ((leftType == KrokType.Float64 && rightType == KrokType.Int) ||
                         (leftType == KrokType.Int && rightType == KrokType.Float64)) _lastExpressionType = KrokType.Bool;
                else
                    // (Дозволяємо 'int == bool'?) Ні, заборонимо.
                    throw new SemanticException($"Cannot compare '{leftType}' and '{rightType}' for equality.", expr.Operator);
                break;
        }
    }

    public void VisitUnaryExpr(UnaryExpr expr)
    {
        expr.Right.Accept(this);
        var type = _lastExpressionType;

        if (expr.Operator.Type == TokenType.OpSub) // Тільки для '-'
        {
            if (type == KrokType.Int || type == KrokType.Float64)
            {
                _lastExpressionType = type; // Тип результату не змінюється
            }
            else
            {
                throw new SemanticException($"Unary '-' cannot be applied to type '{type}'.", expr.Operator);
            }
        }
        // Унарний '+' ігнорується і просто повертає тип виразу
        _lastExpressionType = type;
    }

    public void VisitCastExpr(CastExpr expr)
    {
        var targetType = TokenTypeToKrokType(expr.Type);
        expr.Expression.Accept(this);
        var originalType = _lastExpressionType;

        // (Тут можна додати логіку перевірки невалідних приведень, 
        // наприклад, 'string' в 'int')

        _lastExpressionType = targetType;
    }

    public void VisitCallExpr(CallExpr expr)
    {
        // Callee має бути VariableExpr (ім'я функції)
        if (expr.Callee is not VariableExpr varExpr)
        {
            throw new SemanticException("Cannot call a non-function.", (expr.Callee as BinaryExpr)?.Operator);
        }

        var funcName = varExpr.Name.Lexeme;

        // 1. Знаходимо сигнатуру функції (ми зібрали їх на 1-му проході)
        if (!_functionSignatures.TryGetValue(funcName, out var signature))
        {
            throw new SemanticException($"Undeclared function '{funcName}'.", varExpr.Name);
        }

        // 2. Перевіряємо кількість аргументів
        if (expr.Arguments.Count != signature.ParamTypes.Count)
        {
            throw new SemanticException($"Function '{funcName}' expects {signature.ParamTypes.Count} arguments, but got {expr.Arguments.Count}.", expr.Paren);
        }

        // 3. Перевіряємо типи аргументів
        for (int i = 0; i < expr.Arguments.Count; i++)
        {
            var argExpr = expr.Arguments[i];
            var expectedParamType = signature.ParamTypes[i];

            argExpr.Accept(this);
            var actualArgType = _lastExpressionType;

            // Перевіряємо сумісність (дозволяючи int -> float64)
            CheckTypeAssignment(expectedParamType, actualArgType, (argExpr as VariableExpr)?.Name ?? expr.Paren);
        }

        // Тип всього виклику - це тип повернення функції
        _lastExpressionType = signature.ReturnType;
    }

    public void VisitLiteralExpr(LiteralExpr expr)
    {
        if (expr.Value is int) _lastExpressionType = KrokType.Int;
        else if (expr.Value is double) _lastExpressionType = KrokType.Float64;
        else if (expr.Value is bool) _lastExpressionType = KrokType.Bool;
        else if (expr.Value is string) _lastExpressionType = KrokType.String;
        else _lastExpressionType = KrokType.Error;
    }

    public void VisitVariableExpr(VariableExpr expr)
    {
        var symbol = _variableScopes.Lookup(expr.Name.Lexeme);
        if (symbol == null)
        {
            throw new SemanticException($"Undeclared variable '{expr.Name.Lexeme}'.", expr.Name);
        }
        _lastExpressionType = symbol.Type;
    }

    // (Допоміжні вузли з AstPrinter)
    public void VisitNopStmt(NopStmt stmt) { /* Ігноруємо */ }
    public void VisitAstListNode(AstListNode node)
    {
        // Це лише для друку, аналізатор його ігнорує
        foreach (var child in node.Children)
        {
            child.Accept(this);
        }
    }
}