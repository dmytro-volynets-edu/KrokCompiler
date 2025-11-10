using KrokCompiler.Models;

namespace KrokCompiler.Abstractions
{
    // --- Вузли Виразів (Expressions) ---
    public record BinaryExpr(Expr Left, Token Operator, Expr Right) : Expr
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitBinaryExpr(this);
    }

    public record UnaryExpr(Token Operator, Expr Right) : Expr
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitUnaryExpr(this);
    }

    public record LiteralExpr(object Value) : Expr
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitLiteralExpr(this);
    }

    public record VariableExpr(Token Name) : Expr
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitVariableExpr(this);
    }

    public record CallExpr(Expr Callee, Token Paren, List<Expr> Arguments) : Expr
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitCallExpr(this);
    }

    // --- Вузли Інструкцій (Statements) ---
    public record ProgramNode(List<Stmt> Statements) : IAstNode // Корінь дерева
    {
        public void Accept(IAstVisitor visitor) => visitor.VisitProgramNode(this);
    }

    public record BlockStmt(List<Stmt> Statements) : Stmt
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitBlockStmt(this);
    }

    public record VarDeclStmt(Token Name, Token Type) : Stmt
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitVarDeclStmt(this);
    }

    public record ConstDeclStmt(Token Name, Expr Initializer) : Stmt
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitConstDeclStmt(this);
    }

    public record ExpressionStmt(Expr Expression) : Stmt // Для викликів void-функцій
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitExpressionStmt(this);
    }

    public record AssignStmt(Token Name, Expr Value) : Stmt
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitAssignStmt(this);
    }

    public record CastExpr(Token Type, Expr Expression) : Expr
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitCastExpr(this);
    }

    public record IfStmt(Expr Condition, Stmt ThenBranch, Stmt? ElseBranch) : Stmt
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitIfStmt(this);
    }

    public record ForStmt(Stmt? Initializer, Expr? Condition, Stmt? Increment, Stmt Body) : Stmt
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitForStmt(this);
    }

    public record ReturnStmt(Token Keyword, Expr? Value) : Stmt
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitReturnStmt(this);
    }

    public record BreakStmt(Token Keyword) : Stmt
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitBreakStmt(this);
    }

    public record WriteStmt(Token Keyword, List<Expr> Arguments) : Stmt
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitWriteStmt(this);
    }

    public record ReadStmt(Token Keyword, List<Token> Variables) : Stmt
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitReadStmt(this);
    }

    public record FuncDeclStmt(Token Name, List<ParameterDeclStmt> Parameters, Token ReturnType, BlockStmt Body) : Stmt
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitFuncDeclStmt(this);
    }

    public record ParameterDeclStmt(Token Name, Token Type) : Stmt
    {
        public override void Accept(IAstVisitor visitor) => visitor.VisitParameterDeclStmt(this);
    }
}
