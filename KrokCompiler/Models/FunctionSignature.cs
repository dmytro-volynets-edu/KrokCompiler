namespace KrokCompiler.Models
{

    /// <summary>
    /// Допоміжний клас для зберігання сигнатур функцій
    /// </summary>
    public class FunctionSignature
    {
        public KrokType ReturnType { get; }
        public List<KrokType> ParamTypes { get; }

        public FunctionSignature(KrokType returnType, List<KrokType> paramTypes)
        {
            ReturnType = returnType;
            ParamTypes = paramTypes;
        }
    }
}
