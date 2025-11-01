namespace KrokCompiler.Lexer
{
    public struct SymbolTableEntry
    {
        public int LineNum;
        public string Lexeme;
        public string Token;
        public int? IndexIdConst; // Nullable int, бо індекс є не для всіх

        public override string ToString() =>
            $"{LineNum,-5} {Lexeme,-20} {Token,-15} {IndexIdConst?.ToString() ?? ""}";
    }
}
