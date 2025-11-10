using KrokCompiler.Models;

namespace KrokCompiler.Abstractions
{
    // --- Вузли Виразів (Expressions) ---
    public record BinaryExpr(Expr Left, Token Operator, Expr Right) : Expr
    {
    }

    public record UnaryExpr(Token Operator, Expr Right) : Expr
    {
    }

    public record LiteralExpr(object Value) : Expr
    {
    }

    public record VariableExpr(Token Name) : Expr
    {
    }

    public record CallExpr(Expr Callee, Token Paren, List<Expr> Arguments) : Expr
    {
    }

    // --- Вузли Інструкцій (Statements) ---
    public record ProgramNode(List<Stmt> Statements) : IAstNode // Корінь дерева
    {
    }

    public record BlockStmt(List<Stmt> Statements) : Stmt
    {
    }

    public record VarDeclStmt(Token Name, Token Type) : Stmt
    {
    }

    public record ConstDeclStmt(Token Name, Expr Initializer) : Stmt
    {
    }

    public record ExpressionStmt(Expr Expression) : Stmt // Для викликів void-функцій
    {
    }

    public record AssignStmt(Token Name, Expr Value) : Stmt
    {
    }

    public record CastExpr(Token Type, Expr Expression) : Expr
    {
    }

    public record IfStmt(Expr Condition, Stmt ThenBranch, Stmt? ElseBranch) : Stmt
    {
    }

    public record ForStmt(Stmt? Initializer, Expr? Condition, Stmt? Increment, Stmt Body) : Stmt
    {
    }

    public record ReturnStmt(Token Keyword, Expr? Value) : Stmt
    {
    }

    public record BreakStmt(Token Keyword) : Stmt
    {
    }

    public record WriteStmt(Token Keyword, List<Expr> Arguments) : Stmt
    {
    }

    public record ReadStmt(Token Keyword, List<Token> Variables) : Stmt
    {
    }

    public record FuncDeclStmt(Token Name, List<ParameterDeclStmt> Parameters, Token ReturnType, BlockStmt Body) : Stmt
    {
    }

    public record ParameterDeclStmt(Token Name, Token Type) : Stmt
    {
    }

    /// <summary>
    /// Допоміжний вузол "No Operation", використовується 
    /// для друку порожніх частин циклу 'for'.
    /// </summary>
    internal record NopStmt(string Name) : Stmt
    {
    }

    /// <summary>
    /// Допоміжний вузол для друку списків (напр. "Arguments").
    /// </summary>
    internal record AstListNode(string Name, List<IAstNode> Children) : IAstNode
    {
        // Для сумісності з 'List<IAstNode>':
        public AstListNode(string name, List<Expr> children)
            : this(name, children.Cast<IAstNode>().ToList()) { }

        public AstListNode(string name, List<Stmt> children)
            : this(name, children.Cast<IAstNode>().ToList()) { }

        public AstListNode(string name, List<ParameterDeclStmt> children)
            : this(name, children.Cast<IAstNode>().ToList()) { }
    }
}
