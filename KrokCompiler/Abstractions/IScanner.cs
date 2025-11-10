namespace KrokCompiler.Abstractions
{
    public interface IScanner
    {
        int Column { get; }
        bool IsAtEnd { get; }
        int Line { get; }

        char Advance();
        char Peek();
        void Retract();
    }
}