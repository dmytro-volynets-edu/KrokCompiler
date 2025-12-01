using KrokCompiler.Abstractions;
using KrokCompiler.Models;

/// <summary>
/// Реалізує IAstVisitor для виконання семантичного аналізу.
/// Перевіряє оголошення, типи, області видимості та ін.
/// </summary>
public class SemanticAnalyzer : IAstVisitor
{
    private readonly SymbolTable _variableScopes = new SymbolTable();

    // Словник для функцій. Тепер доступний публічно.
    private readonly Dictionary<string, FunctionSignature> _functionSignatures = new();
    public IReadOnlyDictionary<string, FunctionSignature> Functions => _functionSignatures;

    private KrokType _lastExpressionType = KrokType.Void;
    private FunctionSignature? _currentFunctionSignature = null;
    private int _loopDepth = 0;

    public void Analyze(ProgramNode node)
    {
        // 1. Збір сигнатур функцій
        CollectFunctionSignatures(node.Statements);

        // 2. Перевірка наявності main
        if (!_functionSignatures.ContainsKey("main"))
        {
            throw new SemanticException("Undefined entry point: 'main' function was not found.",
                new Token(TokenType.Eof, "", null, 1, 1));
        }

        // 3. Повний аналіз тіла програми
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

    // --- Допоміжні методи ---

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

    private void CheckTypeAssignment(KrokType expectedType, KrokType actualType, Token errorToken)
    {
        if (expectedType == actualType) return;
        if (expectedType == KrokType.Float64 && actualType == KrokType.Int) return; // Неявне приведення

        throw new SemanticException($"Cannot assign type '{actualType}' to expected type '{expectedType}'.", errorToken);
    }

    // --- Visitor Implementation ---
    public void VisitProgramNode(ProgramNode node)
    {
        foreach (var stmt in node.Statements) stmt.Accept(this);
    }

    public void VisitBlockStmt(BlockStmt stmt)
    {
        _variableScopes.BeginScope();
        foreach (var statement in stmt.Statements) statement.Accept(this);
        _variableScopes.EndScope();
    }

    public void VisitFuncDeclStmt(FuncDeclStmt stmt)
    {
        var signature = _functionSignatures[stmt.Name.Lexeme];
        _currentFunctionSignature = signature;
        _variableScopes.BeginScope();

        foreach (var p in stmt.Parameters)
        {
            var pType = TokenTypeToKrokType(p.Type);
            _variableScopes.Define(p.Name.Lexeme, pType);
        }

        stmt.Body.Accept(this);
        _variableScopes.EndScope();
        _currentFunctionSignature = null;
    }

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
        stmt.Initializer.Accept(this);
        var exprType = _lastExpressionType;
        if (!_variableScopes.Define(stmt.Name.Lexeme, exprType))
        {
            throw new SemanticException($"Constant '{stmt.Name.Lexeme}' is already declared in this scope.", stmt.Name);
        }
    }

    public void VisitExpressionStmt(ExpressionStmt stmt) => stmt.Expression.Accept(this);

    public void VisitAssignStmt(AssignStmt stmt)
    {
        var symbol = _variableScopes.Lookup(stmt.Name.Lexeme);
        if (symbol == null) throw new SemanticException($"Undeclared variable '{stmt.Name.Lexeme}'.", stmt.Name);

        stmt.Value.Accept(this);
        CheckTypeAssignment(symbol.Type, _lastExpressionType, stmt.Name);
    }

    public void VisitIfStmt(IfStmt stmt)
    {
        stmt.Condition.Accept(this);
        if (_lastExpressionType != KrokType.Bool)
            throw new SemanticException("If condition must be a boolean.", (stmt.Condition as VariableExpr)?.Name);

        stmt.ThenBranch.Accept(this);
        stmt.ElseBranch?.Accept(this);
    }

    public void VisitForStmt(ForStmt stmt)
    {
        _loopDepth++;
        _variableScopes.BeginScope();
        stmt.Initializer?.Accept(this);

        if (stmt.Condition != null)
        {
            stmt.Condition.Accept(this);
            if (_lastExpressionType != KrokType.Bool) throw new SemanticException("For condition must be boolean.", null);
        }

        stmt.Increment?.Accept(this);
        stmt.Body.Accept(this);
        _variableScopes.EndScope();
        _loopDepth--;
    }

    public void VisitReturnStmt(ReturnStmt stmt)
    {
        if (_currentFunctionSignature == null) throw new SemanticException("Return outside function.", stmt.Keyword);

        var expected = _currentFunctionSignature.ReturnType;
        if (stmt.Value == null)
        {
            if (expected != KrokType.Void) throw new SemanticException($"Function must return '{expected}'.", stmt.Keyword);
        }
        else
        {
            if (expected == KrokType.Void) throw new SemanticException("Void function cannot return value.", stmt.Keyword);
            stmt.Value.Accept(this);
            CheckTypeAssignment(expected, _lastExpressionType, stmt.Keyword);
        }
    }

    public void VisitBreakStmt(BreakStmt stmt)
    {
        if (_loopDepth == 0) throw new SemanticException("Break outside loop.", stmt.Keyword);
    }

    public void VisitWriteStmt(WriteStmt stmt)
    {
        foreach (var arg in stmt.Arguments)
        {
            arg.Accept(this);
            if (_lastExpressionType == KrokType.Void) throw new SemanticException("Cannot write void.", stmt.Keyword);
        }
    }

    public void VisitReadStmt(ReadStmt stmt)
    {
        foreach (var v in stmt.Variables)
        {
            if (_variableScopes.Lookup(v.Lexeme) == null) throw new SemanticException($"Undeclared variable '{v.Lexeme}'.", v);
        }
    }

    // --- Expressions ---
    public void VisitBinaryExpr(BinaryExpr expr)
    {
        expr.Left.Accept(this);
        var l = expr.Left.EvaluatedType;
        expr.Right.Accept(this);
        var r = expr.Right.EvaluatedType;

        switch (expr.Operator.Type)
        {
            case TokenType.OpAdd:
                if (l == KrokType.Int && r == KrokType.Int) _lastExpressionType = KrokType.Int;
                else if (l == KrokType.String && r == KrokType.String) expr.EvaluatedType = KrokType.String;
                else _lastExpressionType = KrokType.Float64;
                break;
            case TokenType.OpGt:
            case TokenType.OpLt:
            case TokenType.OpEq:
            case TokenType.OpNeq:
            case TokenType.OpGe:
            case TokenType.OpLe:
                _lastExpressionType = KrokType.Bool;
                break;
            case TokenType.OpDiv:
                expr.EvaluatedType = KrokType.Float64;
                _lastExpressionType = expr.EvaluatedType;
                break;
            default:
                // Для -, *, /, ^
                if (l == KrokType.Int && r == KrokType.Int && expr.Operator.Type != TokenType.OpDiv)
                    _lastExpressionType = KrokType.Int;
                else
                    _lastExpressionType = KrokType.Float64;
                break;
        }
    }

    public void VisitUnaryExpr(UnaryExpr expr)
    {
        expr.Right.Accept(this);
    }

    public void VisitCastExpr(CastExpr expr)
    {
        expr.Expression.Accept(this);
        _lastExpressionType = TokenTypeToKrokType(expr.TargetTypeToken);
        expr.EvaluatedType = _lastExpressionType;
    }

    public void VisitCallExpr(CallExpr expr)
    {
        if (expr.Callee is not VariableExpr v) throw new SemanticException("Call non-function.", null);

        if (!_functionSignatures.TryGetValue(v.Name.Lexeme, out var sig))
            throw new SemanticException($"Undeclared function '{v.Name.Lexeme}'.", v.Name);

        if (expr.Arguments.Count != sig.ParamTypes.Count)
            throw new SemanticException("Argument count mismatch.", v.Name);

        foreach (var arg in expr.Arguments) arg.Accept(this);

        expr.EvaluatedType = sig.ReturnType;
        _lastExpressionType = sig.ReturnType;
    }

    public void VisitLiteralExpr(LiteralExpr expr)
    {
        if (expr.Value is int) expr.EvaluatedType = KrokType.Int;
        else if (expr.Value is double) expr.EvaluatedType = KrokType.Float64;
        else if (expr.Value is bool) expr.EvaluatedType = KrokType.Bool;
        else if (expr.Value is string) expr.EvaluatedType = KrokType.String;
        else expr.EvaluatedType = KrokType.Error;
        _lastExpressionType = expr.EvaluatedType;
    }

    public void VisitVariableExpr(VariableExpr expr)
    {
        var symbol = _variableScopes.Lookup(expr.Name.Lexeme);
        if (symbol == null) throw new SemanticException($"Undeclared variable '{expr.Name.Lexeme}'.", expr.Name);
        expr.EvaluatedType = symbol.Type;
        _lastExpressionType = symbol.Type;
    }

    public void VisitNopStmt(NopStmt stmt) { }
    public void VisitAstListNode(AstListNode node) { }
}