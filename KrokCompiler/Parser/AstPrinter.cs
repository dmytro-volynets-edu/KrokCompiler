using KrokCompiler.Abstractions;
using System.Text;

/// <summary>
/// Друкує AST у вигляді "line-art" дерева.
/// Цей клас використовує пряму рекурсію для обходу дерева
/// </summary>
public class AstPrinter
{
    private StringBuilder _sb = new StringBuilder();

    // Головний публічний метод
    public string Print(IAstNode node)
    {
        _sb.Clear();
        PrintNodeRecursive(node, "", true);
        return _sb.ToString();
    }

    /// <summary>
    /// Рекурсивно друкує вузол та всіх його нащадків.
    /// </summary>
    /// <param name="node">Вузол для друку.</param>
    /// <param name="indent">Поточний префікс відступу (напр. "│   ").</param>
    /// <param name="isLast">Чи є цей вузол останнім у списку свого батька?</param>
    private void PrintNodeRecursive(IAstNode node, string indent, bool isLast)
    {
        // 1. Малюємо лінію для поточного вузла
        _sb.Append(indent);
        _sb.Append(isLast ? "└── " : "├── ");

        // 2. Визначаємо, який префікс передати дочірнім вузлам
        string childIndent = indent + (isLast ? "    " : "│   ");

        // 3. Використовуємо 'switch' для визначення, що це за вузол
        // і як його друкувати.
        switch (node)
        {
            // --- Вузли програми та блоків ---
            case ProgramNode n:
                _sb.AppendLine("Program");
                PrintChildren(n.Statements, childIndent);
                break;
            case BlockStmt n:
                _sb.AppendLine("Block");
                PrintChildren(n.Statements, childIndent);
                break;
            case FuncDeclStmt n:
                _sb.AppendLine($"FuncDecl {n.Name.Lexeme} (returns {n.ReturnType.Lexeme})");
                if (n.Parameters.Any())
                {
                    PrintNodeRecursive(new AstListNode("Parameters", n.Parameters), childIndent, false);
                }
                PrintNodeRecursive(n.Body, childIndent, true);
                break;
            case ParameterDeclStmt n:
                _sb.AppendLine($"Param {n.Name.Lexeme} ({n.Type.Lexeme})");
                break;

            // --- Інструкції ---
            case VarDeclStmt n:
                _sb.AppendLine($"VarDecl {n.Name.Lexeme} ({n.Type.Lexeme})");
                break;
            case ConstDeclStmt n:
                _sb.AppendLine($"ConstDecl {n.Name.Lexeme} =");
                PrintNodeRecursive(n.Initializer, childIndent, true);
                break;
            case ExpressionStmt n:
                _sb.AppendLine("ExprStmt");
                PrintNodeRecursive(n.Expression, childIndent, true);
                break;
            case AssignStmt n:
                _sb.AppendLine($"Assign {n.Name.Lexeme} =");
                PrintNodeRecursive(n.Value, childIndent, true);
                break;
            case IfStmt n:
                _sb.AppendLine("If");
                PrintNodeRecursive(n.Condition, childIndent, false);
                PrintNodeRecursive(n.ThenBranch, childIndent, n.ElseBranch == null);
                if (n.ElseBranch != null)
                {
                    PrintNodeRecursive(n.ElseBranch, childIndent, true);
                }
                break;
            case ForStmt n:
                _sb.AppendLine("For");
                PrintNodeRecursive(n.Initializer ?? new NopStmt("Initializer"), childIndent, false);
                PrintNodeRecursive(n.Condition ?? new LiteralExpr("(Empty Condition)"), childIndent, false);
                PrintNodeRecursive(n.Increment ?? new NopStmt("Increment"), childIndent, false);
                PrintNodeRecursive(n.Body, childIndent, true);
                break;
            case ReturnStmt n:
                _sb.AppendLine("Return");
                if (n.Value != null)
                {
                    PrintNodeRecursive(n.Value, childIndent, true);
                }
                break;
            case BreakStmt:
                _sb.AppendLine("Break");
                break;
            case WriteStmt n:
                _sb.AppendLine("write");
                PrintChildren(n.Arguments, childIndent);
                break;
            case ReadStmt n:
                _sb.AppendLine("read");
                // Перетворюємо список токенів на список вузлів AST для друку
                var varsAsNodes = n.Variables.Select(v => new VariableExpr(v));
                PrintChildren(varsAsNodes, childIndent);
                break;

            // --- Вирази ---
            case BinaryExpr n:
                _sb.AppendLine($"Binary {n.Operator.Lexeme}");
                PrintNodeRecursive(n.Left, childIndent, false);
                PrintNodeRecursive(n.Right, childIndent, true);
                break;
            case UnaryExpr n:
                _sb.AppendLine($"Unary {n.Operator.Lexeme}");
                PrintNodeRecursive(n.Right, childIndent, true);
                break;
            case CastExpr n:
                _sb.AppendLine($"Cast ({n.Type.Lexeme})");
                PrintNodeRecursive(n.Expression, childIndent, true);
                break;
            case CallExpr n:
                _sb.AppendLine("Call");
                PrintNodeRecursive(n.Callee, childIndent, false);
                PrintNodeRecursive(new AstListNode("Arguments", n.Arguments), childIndent, true);
                break;
            case LiteralExpr n:
                string val = n.Value is string s ? $"\"{s}\"" : (n.Value?.ToString() ?? "null");
                _sb.AppendLine($"Literal {val}");
                break;
            case VariableExpr n:
                _sb.AppendLine($"Var {n.Name.Lexeme}");
                break;

            // --- Спеціальні вузли для друку ---
            case AstListNode n:
                _sb.AppendLine(n.Name);
                PrintChildren(n.Children, childIndent);
                break;
            case NopStmt n:
                _sb.AppendLine($"(Empty {n.Name})");
                break;
        }
    }

    /// <summary>
    /// Допоміжний метод для друку списків (напр., тіло блоку або аргументи).
    /// </summary>
    private void PrintChildren(IEnumerable<IAstNode> children, string indent)
    {
        var childList = children.ToList();
        for (int i = 0; i < childList.Count; i++)
        {
            PrintNodeRecursive(childList[i], indent, i == childList.Count - 1);
        }
    }
}


