namespace KrokCompiler.Models
{
    public class SemanticException : Exception
    {
        public SemanticException(string message, Token token)
            : base($"[Line {token.Line}, Col {token.Column}] Semantic Error: {message}")
        {
        }
    }
}
