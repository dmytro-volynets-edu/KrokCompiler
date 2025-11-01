using KrokCompiler.Interfaces;
using KrokCompiler.Lexer;
using KrokCompiler.Models;

string filePath = "..\\..\\..\\..\\..\\demo.kr";
if (args.Length == 0)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Error: No path to the file .kr specified");
    Console.WriteLine("Example: .\\KrokLexer.exe my_program.kr");
    Console.ResetColor();
    //return;
}
else
{
    filePath = args[0];
}

string sourceCode;

try
{
    sourceCode = await File.ReadAllTextAsync(filePath);
}
catch (FileNotFoundException)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error: File not found at path: {filePath}");
    Console.ResetColor();
    return;
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"An error occurred while reading the file: {ex.Message}");
    Console.ResetColor();
    return;
}

if (string.IsNullOrWhiteSpace(sourceCode))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Error: empty source file");
    Console.ResetColor();
    return;
}

IScanner scanner = new Scanner(sourceCode);

var lexer = new Lexer(scanner);

List<Token> tokens = lexer.Analyze();

Utils.PrintConstantsAndIdentifiers(tokens);

Console.ReadKey();
