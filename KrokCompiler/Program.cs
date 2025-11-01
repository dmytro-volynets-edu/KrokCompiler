using KrokCompiler.Lexer;

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


var lexer = new Lexer();

var tokens = lexer.Analyze(sourceCode);

Console.ReadKey();
