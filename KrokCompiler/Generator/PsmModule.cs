using System.Text;

namespace KrokCompiler.Generator
{
    public class PsmModule
    {
        public string Name { get; }
        public StringBuilder VarSection { get; } = new();
        public StringBuilder FuncSection { get; } = new();
        public StringBuilder CodeSection { get; } = new();
        public List<string> Labels { get; } = new();

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

            if (FuncSection.Length > 0)
            {
                sb.AppendLine(".funcs(");
                sb.Append(FuncSection);
                sb.AppendLine(")");
                sb.AppendLine();
            }

            if (Labels.Count > 0)
            {
                sb.AppendLine(".labels(");
                foreach (var lbl in Labels) sb.AppendLine($"    {lbl} 1"); // Фіктивна адреса, PSM перерахує
                sb.AppendLine(")");
                sb.AppendLine();
            }

            sb.AppendLine(".code(");
            sb.Append(CodeSection);
            sb.AppendLine(")");

            return sb.ToString();
        }
    }
}
