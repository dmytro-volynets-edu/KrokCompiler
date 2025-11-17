using KrokCompiler.Abstractions;
using KrokCompiler.Lexer;
using KrokCompiler.Models;
using KrokCompiler.Parser;

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
    Utils.PrintError($"Error: File not found at path: {filePath}");
    return;
}
catch (Exception ex)
{
    Utils.PrintError($"An error occurred while reading the file: {ex.Message}");
    return;
}

if (string.IsNullOrWhiteSpace(sourceCode))
{
    Utils.PrintError("Error: empty source file");
    return;
}

IScanner scanner = new Scanner(sourceCode);

var lexer = new Lexer(scanner);

List<Token> tokens = lexer.Analyze();

Utils.PrintConstantsAndIdentifiers(tokens);
if (tokens.Count == 0) return;
Utils.PrintSuccess("Lexer: Lexical analysis completed successfully");

try
{
	Console.WriteLine("--- Staring Syntax Analisis ---");
	Parser parser = new Parser(tokens);
	ProgramNode ast = parser.ParseProgram();
    Utils.PrintSuccess("Syntax Analysis Successful");
	Console.WriteLine("--- Staring Semantic Analisis ---");
	SemanticAnalyzer analizer = new SemanticAnalyzer();
	analizer.Analyze(ast);
	Utils.PrintSuccess("----Abstract Syntax Tree----");
	var printer = new AstPrinter();
	string astString = printer.Print(ast);
	Console.WriteLine(astString);
	Utils.PrintSuccess("Semantic Analysis Successful: Program is valid!");
}
catch (ParserException e)
{
    Utils.PrintError($"Syntax Analysis Failed \n{e.Message}");
}
catch (SemanticException se)
{
	Utils.PrintError($"Semantic Analysis Failed \n{se.Message}");
}
catch (Exception)
{

    throw;
}

Console.ReadKey();
