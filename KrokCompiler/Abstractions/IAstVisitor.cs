namespace KrokCompiler.Abstractions
{
    public interface IAstVisitor
    {
        // Вирази
        void VisitBinaryExpr(BinaryExpr expr);
        void VisitUnaryExpr(UnaryExpr expr);
        void VisitLiteralExpr(LiteralExpr expr);
        void VisitVariableExpr(VariableExpr expr);
        void VisitCallExpr(CallExpr expr);
        void VisitCastExpr(CastExpr expr);

        // Інструкції
        void VisitProgramNode(ProgramNode node);
        void VisitBlockStmt(BlockStmt stmt);
        void VisitVarDeclStmt(VarDeclStmt stmt);
        void VisitConstDeclStmt(ConstDeclStmt stmt);
        void VisitExpressionStmt(ExpressionStmt stmt);
        void VisitAssignStmt(AssignStmt stmt);
        void VisitIfStmt(IfStmt stmt);
        void VisitForStmt(ForStmt stmt);
        void VisitReturnStmt(ReturnStmt stmt);
        void VisitBreakStmt(BreakStmt stmt);
        void VisitWriteStmt(WriteStmt stmt);
        void VisitReadStmt(ReadStmt stmt);
        void VisitFuncDeclStmt(FuncDeclStmt stmt);
        void VisitParameterDeclStmt(ParameterDeclStmt stmt);
        void VisitNopStmt(NopStmt stmt);
        void VisitAstListNode(AstListNode node);
    }
}
