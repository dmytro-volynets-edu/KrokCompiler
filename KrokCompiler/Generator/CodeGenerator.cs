using KrokCompiler.Abstractions;
using KrokCompiler.Models;

namespace KrokCompiler.Generator
{
    public class CodeGenerator : IAstVisitor
    {
        private readonly Dictionary<string, PsmModule> _modules = new();
        private PsmModule _currentModule;
        private int _labelCounter = 0;
        private readonly string _mainModuleName;
        private readonly IReadOnlyDictionary<string, FunctionSignature> _functions;

        private Dictionary<string, string> _variableTypes = new();

        public CodeGenerator(string mainModuleName, IReadOnlyDictionary<string, FunctionSignature> functions)
        {
            _mainModuleName = mainModuleName;
            _functions = functions;
            _currentModule = new PsmModule(_mainModuleName);
            _modules.Add(_mainModuleName, _currentModule);
        }

        public Dictionary<string, string> Generate(ProgramNode program)
        {
            program.Accept(this);
            var result = new Dictionary<string, string>();
            foreach (var mod in _modules.Values)
            {
                result.Add(mod.Name + ".postfix", mod.GenerateSource());
            }
            return result;
        }

        // --- Emitters ---

        private void Emit(string lexeme, string token)
        {
            // Додаємо інструкцію в список
            _currentModule.CodeInstructions.Add($"    {lexeme,-20} {token}");
        }

        private void EmitLabel(string label)
        {
            // 1. Записуємо поточну адресу (індекс наступної інструкції)
            // Поточна кількість інструкцій = індекс наступної.
            // Але ми зараз додамо ще 2 інструкції (label і colon).
            // Тому реальна інструкція, на яку ми стрибаємо, буде через 2 позиції.
            // index: label (current)
            // index+1: colon
            // index+2: <TARGET INSTRUCTION>

            int address = _currentModule.CodeInstructions.Count + 2;
            _currentModule.LabelAddresses[label] = address;

            // 2. Емітимо маркери мітки (якщо PSM їх не видаляє, вони займають місце)
            Emit(label, "label");
            Emit(":", "colon");
        }

        private string NewLabel()
        {
            string lbl = "m" + (++_labelCounter);
            _currentModule.LabelNames.Add(lbl);
            return lbl;
        }

        private string FormatType(string krokType) => krokType == "float64" ? "float" : krokType;
        private string KrokTypeToString(KrokType type) => type switch { KrokType.Int => "int", KrokType.Float64 => "float", KrokType.Bool => "bool", KrokType.String => "string", _ => "void" };

        // --- Visitor ---

        public void VisitProgramNode(ProgramNode node)
        {
            foreach (var stmt in node.Statements)
            {
                if (stmt is VarDeclStmt || stmt is ConstDeclStmt) stmt.Accept(this);
            }

            foreach (var stmt in node.Statements.OfType<FuncDeclStmt>())
            {
                if (stmt.Name.Lexeme == "main") continue;
                stmt.Accept(this);
            }

            var mainFunc = node.Statements.OfType<FuncDeclStmt>().FirstOrDefault(f => f.Name.Lexeme == "main");
            if (mainFunc != null)
            {
                mainFunc.Body.Accept(this);
                Emit("RET", "ret_op");
            }
        }

        public void VisitFuncDeclStmt(FuncDeclStmt stmt)
        {
            var parentModule = _currentModule;
            var parentLocals = new Dictionary<string, string>(_variableTypes);

            string funcModuleName = _mainModuleName + "$" + stmt.Name.Lexeme;
            _currentModule = new PsmModule(funcModuleName);
            _modules.Add(funcModuleName, _currentModule);

            _variableTypes = new Dictionary<string, string>(parentLocals);

            foreach (var param in stmt.Parameters)
            {
                string type = FormatType(param.Type.Lexeme);
                _currentModule.VarSection.AppendLine($"    {param.Name.Lexeme,-20} {type}");
                _variableTypes[param.Name.Lexeme] = type;
            }

            stmt.Body.Accept(this);
            Emit("RET", "ret_op");

            _currentModule = parentModule;
            _variableTypes = parentLocals;
        }

        public void VisitVarDeclStmt(VarDeclStmt stmt)
        {
            string type = FormatType(stmt.Type.Lexeme);
            _currentModule.VarSection.AppendLine($"    {stmt.Name.Lexeme,-20} {type}");
            _variableTypes[stmt.Name.Lexeme] = type;
        }

        public void VisitConstDeclStmt(ConstDeclStmt stmt)
        {
            string type = "int";
            if (stmt.Initializer is LiteralExpr l)
            {
                if (l.Value is double) type = "float";
                if (l.Value is string) type = "string";
                if (l.Value is bool) type = "bool";
            }

            _currentModule.VarSection.AppendLine($"    {stmt.Name.Lexeme,-20} {type}");
            _variableTypes[stmt.Name.Lexeme] = type;

            Emit(stmt.Name.Lexeme, "l-val");
            stmt.Initializer.Accept(this);
            Emit("=", "assign_op");
        }

        public void VisitBlockStmt(BlockStmt stmt) { foreach (var s in stmt.Statements) s.Accept(this); }

        public void VisitAssignStmt(AssignStmt stmt)
        {
            Emit(stmt.Name.Lexeme, "l-val");
            stmt.Value.Accept(this);
            Emit("=", "assign_op");
        }

        public void VisitReadStmt(ReadStmt stmt)
        {
            foreach (var v in stmt.Variables)
            {
                Emit("INP", "inp_op");
                if (_variableTypes.TryGetValue(v.Lexeme, out string type))
                {
                    if (type == "int") Emit("s2i", "conv");
                    else if (type == "float") Emit("s2f", "conv");
                }
                Emit(v.Lexeme, "l-val");
                Emit("SWAP", "stack_op");
                Emit("=", "assign_op");
            }
        }

        public void VisitWriteStmt(WriteStmt stmt)
        {
            foreach (var arg in stmt.Arguments) { arg.Accept(this); Emit("OUT", "out_op"); }
        }

        public void VisitIfStmt(IfStmt stmt)
        {
            string labelElse = NewLabel();
            string labelEnd = NewLabel();
            stmt.Condition.Accept(this);
            Emit(labelElse, "label"); Emit("JF", "jf");
            stmt.ThenBranch.Accept(this);
            Emit(labelEnd, "label"); Emit("JUMP", "jump");
            EmitLabel(labelElse);
            stmt.ElseBranch?.Accept(this);
            EmitLabel(labelEnd);
        }

        public void VisitForStmt(ForStmt stmt)
        {
            string labelStart = NewLabel();
            string labelEnd = NewLabel();
            stmt.Initializer?.Accept(this);
            EmitLabel(labelStart);
            if (stmt.Condition != null)
            {
                stmt.Condition.Accept(this);
                Emit(labelEnd, "label"); Emit("JF", "jf");
            }
            stmt.Body.Accept(this);
            stmt.Increment?.Accept(this);
            Emit(labelStart, "label"); Emit("JUMP", "jump");
            EmitLabel(labelEnd);
        }

        public void VisitReturnStmt(ReturnStmt stmt) { if (stmt.Value != null) stmt.Value.Accept(this); Emit("RET", "ret_op"); }
        public void VisitBreakStmt(BreakStmt stmt) { /* Потрібен стек міток */ }
        public void VisitExpressionStmt(ExpressionStmt stmt) { stmt.Expression.Accept(this); }

        public void VisitCallExpr(CallExpr expr)
        {
            foreach (var arg in expr.Arguments) arg.Accept(this);

            string funcName = ((VariableExpr)expr.Callee).Name.Lexeme;

            string retType = "void";
            if (_functions.TryGetValue(funcName, out var sig))
            {
                retType = KrokTypeToString(sig.ReturnType);
            }

            string callName = funcName;

            string funcDecl = $"    {callName,-30} {retType,-10} {expr.Arguments.Count}";
            if (!_currentModule.DeclaredFuncs.Contains(callName))
            {
                _currentModule.FuncSection.AppendLine(funcDecl);
                _currentModule.DeclaredFuncs.Add(callName);
            }

            Emit(callName, "CALL");
        }

        public void VisitBinaryExpr(BinaryExpr expr)
        {
            expr.Left.Accept(this);
            expr.Right.Accept(this);

            if (expr.Operator.Type == TokenType.OpAdd && expr.EvaluatedType == KrokType.String)
            {
                Emit("CAT", "cat_op");
                return;
            }

            string op = expr.Operator.Type switch
            {
                TokenType.OpAdd => "+",
                TokenType.OpSub => "-",
                TokenType.OpMul => "*",
                TokenType.OpDiv => "/",
                TokenType.OpPow => "^",
                TokenType.OpEq => "==",
                TokenType.OpNeq => "!=",
                TokenType.OpLt => "<",
                TokenType.OpLe => "<=",
                TokenType.OpGt => ">",
                TokenType.OpGe => ">=",
                _ => ""
            };
            string type = (op == "==" || op == "!=" || op == "<" || op == "<=" || op == ">" || op == ">=") ? "rel_op" : "math_op";
            Emit(op, type);
        }

        public void VisitUnaryExpr(UnaryExpr expr) { expr.Right.Accept(this); if (expr.Operator.Type == TokenType.OpSub) Emit("NEG", "math_op"); }

        public void VisitLiteralExpr(LiteralExpr expr)
        {
            if (expr.Value is int i) Emit(i.ToString(), "int");
            else if (expr.Value is double d) Emit(d.ToString().Replace(',', '.'), "float");
            else if (expr.Value is bool b) Emit(b ? "TRUE" : "FALSE", "bool");
            else if (expr.Value is string s) Emit($"\"{s}\"", "string");
        }

        public void VisitVariableExpr(VariableExpr expr)
        {
            if (_currentModule.Name != _mainModuleName)
            {
                if (!_currentModule.VarSection.ToString().Contains(expr.Name.Lexeme))
                {
                    if (!_currentModule.GlobVarSection.ToString().Contains(expr.Name.Lexeme))
                        _currentModule.GlobVarSection.AppendLine($"    {expr.Name.Lexeme}");
                }
            }
            Emit(expr.Name.Lexeme, "r-val");
        }

        public void VisitCastExpr(CastExpr expr)
        {
            expr.Expression.Accept(this);
            string convOp = "";
            switch (expr.TargetTypeToken.Type)
            {
                case TokenType.KwFloat64: convOp = "i2f"; break;
                case TokenType.KwInt: convOp = "f2i"; break;
                case TokenType.KwString:
                    if (expr.Expression.EvaluatedType == KrokType.Float64) convOp = "f2s";
                    else convOp = "i2s";
                    break;
            }
            if (convOp != "") Emit(convOp, "conv");
        }

        public void VisitParameterDeclStmt(ParameterDeclStmt stmt) { }
        public void VisitNopStmt(NopStmt stmt) { }
        public void VisitAstListNode(AstListNode node) { }
    }
}