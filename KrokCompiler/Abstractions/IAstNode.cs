namespace KrokCompiler.Abstractions
{
    public interface IAstNode 
    {
    }
    public abstract record Expr : IAstNode
    {
    }

    public abstract record Stmt : IAstNode
    {
    }
}
