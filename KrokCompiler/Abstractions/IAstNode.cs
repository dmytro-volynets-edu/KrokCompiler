namespace KrokCompiler.Abstractions
{
    public interface IAstNode 
    {
        void Accept(IAstVisitor visitor);
    }
    public abstract record Expr : IAstNode
    {
        public abstract void Accept(IAstVisitor visitor);
    }

    public abstract record Stmt : IAstNode
    {
        public abstract void Accept(IAstVisitor visitor);
    }
}
