using KrokCompiler.Abstractions;
using KrokCompiler.Generator;
using KrokCompiler.Lexer;
using KrokCompiler.Models;
using KrokCompiler.Parser;

string filePath = "..\\..\\..\\..\\..\\demo.kr";
if (args.Length == 0)
{
    Utils.PrintError("Error: No path to the file .kr specified");
    Utils.PrintError("Example: .\\KrokLexer.exe my_program.kr");
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
ProgramNode? ast = null;
SemanticAnalyzer? analizer = null;
try
{
	Console.WriteLine("--- Staring Syntax Analisis ---");
	Parser parser = new Parser(tokens);
	ast = parser.ParseProgram();
    Utils.PrintSuccess("Syntax Analysis Successful");
	Console.WriteLine("--- Staring Semantic Analisis ---");
    analizer = new SemanticAnalyzer();
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
if(ast == null)
{
    Utils.PrintError($"Something went wrong. AST is empty.");
    return;
}
if (analizer == null)
{
    Utils.PrintError($"Something went wrong. SemanticAnalyzer result is empty.");
    return;
}
Console.WriteLine("--- Generating Code ---");
var generator = new CodeGenerator("prog", analizer.Functions);
var files = generator.Generate(ast);

foreach (var file in files)
{
    Console.WriteLine($"Writing file: {file.Key}");
    Directory.CreateDirectory("output");
    File.WriteAllText($"output/{file.Key}", file.Value);
}
Console.WriteLine("--- Compilation Complete ---");

Console.ReadKey();
