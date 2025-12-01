using System.Text;

namespace KrokCompiler.Generator
{
    public class PsmModule
    {
        public string Name { get; }

        // Секції для генерації
        public StringBuilder VarSection { get; } = new();
        public StringBuilder GlobVarSection { get; } = new();
        public StringBuilder FuncSection { get; } = new();

        // Список для інструкцій (для розрахунку адрес міток)
        public List<string> CodeInstructions { get; } = new();

        // Зберігаємо імена міток
        public List<string> LabelNames { get; } = new();

        // Словник: Мітка -> Адреса (індекс інструкції)
        public Dictionary<string, int> LabelAddresses { get; } = new();

        // Щоб не дублювати оголошення функцій у .funcs
        public HashSet<string> DeclaredFuncs { get; } = new();

        public PsmModule(string name)
        {
            Name = name;
        }

        public string GenerateSource()
        {
            var sb = new StringBuilder();
            sb.AppendLine(".target: Postfix Machine");
            sb.AppendLine(".version: 0.3");
            sb.AppendLine();

            if (VarSection.Length > 0)
            {
                sb.AppendLine(".vars(");
                sb.Append(VarSection);
                sb.AppendLine(")");
                sb.AppendLine();
            }

            if (GlobVarSection.Length > 0)
            {
                sb.AppendLine(".globVarList(");
                sb.Append(GlobVarSection);
                sb.AppendLine(")");
                sb.AppendLine();
            }

            if (FuncSection.Length > 0)
            {
                sb.AppendLine(".funcs(");
                sb.Append(FuncSection);
                sb.AppendLine(")");
                sb.AppendLine();
            }

            if (LabelNames.Count > 0)
            {
                sb.AppendLine(".labels(");
                foreach (var lbl in LabelNames)
                {
                    int addr = LabelAddresses.ContainsKey(lbl) ? LabelAddresses[lbl] : 0;
                    sb.AppendLine($"    {lbl,-20} {addr}");
                }
                sb.AppendLine(")");
                sb.AppendLine();
            }

            sb.AppendLine(".code(");
            foreach (var line in CodeInstructions)
            {
                sb.AppendLine(line);
            }
            sb.AppendLine(")");

            return sb.ToString();
        }
    }
}