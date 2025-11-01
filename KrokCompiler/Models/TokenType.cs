namespace KrokCompiler.Models
{
    public enum TokenType
    {
        // Літерали
        Id, IntConst, RealConst, StringConst, BoolConst,

        // Ключові слова
        KwVar, KwConst, KwFunc, KwIf, KwElse, KwFor,
        KwReturn, KwBreak, KwInt, KwFloat64, KwBool,
        KwString, KwVoid, KwRead, KwWrite,

        // Оператори
        OpAssign, OpAdd, OpSub, OpMul, OpDiv, OpPow,
        OpEq, OpNeq, OpLt, OpLe, OpGt, OpGe,

        // Роздільники
        LParen, RParen, LBrace, RBrace, Comma, Semicolon,

        // Службові
        Illegal, Eof
    }
}
