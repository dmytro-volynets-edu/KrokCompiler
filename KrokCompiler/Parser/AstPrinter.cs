using KrokCompiler.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrokCompiler.Parser
{
    public class AstPrinter : IAstVisitor
    {
        private StringBuilder _sb = new StringBuilder();
        private int _indent = 0;

        public string Print(ProgramNode node)
        {
            _sb.Clear();
            node.Accept(this); // Запускаємо обхід дерева
            return _sb.ToString();
        }

        private void Indent() => _sb.Append(' ', _indent * 2);

        private void BeginBlock(string name)
        {
            Indent();
            _sb.AppendLine($"({name}");
            _indent++;
        }

        private void EndBlock()
        {
            _indent--;
            Indent();
            _sb.AppendLine(")");
        }

        // --- РЕАЛІЗАЦІЯ ІНТЕРФЕЙСУ IAstVisitor ---

        public void VisitProgramNode(ProgramNode node)
        {
            BeginBlock("Program");
            foreach (var stmt in node.Statements)
            {
                stmt.Accept(this);
            }
            EndBlock();
        }

        public void VisitBlockStmt(BlockStmt stmt)
        {
            BeginBlock("Block");
            foreach (var statement in stmt.Statements)
            {
                statement.Accept(this);
            }
            EndBlock();
        }

        public void VisitFuncDeclStmt(FuncDeclStmt stmt)
        {
            BeginBlock($"FuncDecl {stmt.Name.Lexeme} (returns {stmt.ReturnType.Lexeme})");
            foreach (var param in stmt.Parameters)
            {
                param.Accept(this);
            }
            stmt.Body.Accept(this);
            EndBlock();
        }

        public void VisitParameterDeclStmt(ParameterDeclStmt stmt)
        {
            Indent();
            _sb.AppendLine($"(Param {stmt.Name.Lexeme} {stmt.Type.Lexeme})");
        }

        public void VisitVarDeclStmt(VarDeclStmt stmt)
        {
            Indent();
            _sb.AppendLine($"(VarDecl {stmt.Name.Lexeme} {stmt.Type.Lexeme})");
        }

        public void VisitConstDeclStmt(ConstDeclStmt stmt)
        {
            BeginBlock($"ConstDecl {stmt.Name.Lexeme} =");
            stmt.Initializer.Accept(this);
            EndBlock();
        }

        public void VisitExpressionStmt(ExpressionStmt stmt)
        {
            // Це "обгортка" для таких речей, як виклики void-функцій
            BeginBlock("ExprStmt");
            stmt.Expression.Accept(this);
            EndBlock();
        }

        public void VisitAssignStmt(AssignStmt stmt)
        {
            BeginBlock($"Assign {stmt.Name.Lexeme} =");
            stmt.Value.Accept(this);
            EndBlock();
        }

        public void VisitIfStmt(IfStmt stmt)
        {
            BeginBlock("If");
            Indent(); _sb.AppendLine("(Condition"); _indent++;
            stmt.Condition.Accept(this);
            _indent--; Indent(); _sb.AppendLine(")");

            Indent(); _sb.AppendLine("(Then"); _indent++;
            stmt.ThenBranch.Accept(this);
            _indent--; Indent(); _sb.AppendLine(")");

            if (stmt.ElseBranch != null)
            {
                Indent(); _sb.AppendLine("(Else"); _indent++;
                stmt.ElseBranch.Accept(this);
                _indent--; Indent(); _sb.AppendLine(")");
            }
            EndBlock();
        }

        public void VisitForStmt(ForStmt stmt)
        {
            BeginBlock("For");

            Indent(); _sb.AppendLine("(Init"); _indent++;
            stmt.Initializer?.Accept(this);
            _indent--; Indent(); _sb.AppendLine(")");

            Indent(); _sb.AppendLine("(Condition"); _indent++;
            stmt.Condition?.Accept(this);
            _indent--; Indent(); _sb.AppendLine(")");

            Indent(); _sb.AppendLine("(Increment"); _indent++;
            stmt.Increment?.Accept(this);
            _indent--; Indent(); _sb.AppendLine(")");

            Indent(); _sb.AppendLine("(Body"); _indent++;
            stmt.Body.Accept(this);
            _indent--; Indent(); _sb.AppendLine(")");

            EndBlock();
        }

        public void VisitReturnStmt(ReturnStmt stmt)
        {
            if (stmt.Value == null)
            {
                Indent(); _sb.AppendLine("(Return)");
            }
            else
            {
                BeginBlock("Return");
                stmt.Value.Accept(this);
                EndBlock();
            }
        }

        public void VisitBreakStmt(BreakStmt stmt)
        {
            Indent();
            _sb.AppendLine("(Break)");
        }

        public void VisitWriteStmt(WriteStmt stmt)
        {
            BeginBlock("write");
            foreach (var arg in stmt.Arguments)
            {
                arg.Accept(this);
            }
            EndBlock();
        }

        public void VisitReadStmt(ReadStmt stmt)
        {
            BeginBlock("read");
            foreach (var var in stmt.Variables)
            {
                Indent();
                _sb.AppendLine($"(Var {var.Lexeme})");
            }
            EndBlock();
        }

        // --- Вирази ---

        public void VisitBinaryExpr(BinaryExpr expr)
        {
            BeginBlock(expr.Operator.Lexeme); // Напр. (+)
            expr.Left.Accept(this);
            expr.Right.Accept(this);
            EndBlock();
        }

        public void VisitUnaryExpr(UnaryExpr expr)
        {
            BeginBlock($"(Unary {expr.Operator.Lexeme})"); // Напр. (Unary -)
            expr.Right.Accept(this);
            EndBlock();
        }

        public void VisitCastExpr(CastExpr expr)
        {
            BeginBlock($"(Cast {expr.Type.Lexeme})"); // Напр. (Cast float64)
            expr.Expression.Accept(this);
            EndBlock();
        }

        public void VisitLiteralExpr(LiteralExpr expr)
        {
            Indent();
            if (expr.Value is string s)
                _sb.AppendLine($"\"{s}\""); // Рядки беремо в лапки
            else
                _sb.AppendLine(expr.Value.ToString());
        }

        public void VisitVariableExpr(VariableExpr expr)
        {
            Indent();
            _sb.AppendLine($"(Var {expr.Name.Lexeme})");
        }

        public void VisitCallExpr(CallExpr expr)
        {
            BeginBlock("Call");
            expr.Callee.Accept(this);
            foreach (var arg in expr.Arguments)
            {
                arg.Accept(this);
            }
            EndBlock();
        }
    }
}
