using KrokCompiler.Abstractions;
using KrokCompiler.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrokCompiler.Parser
{
    public class Parser
    {
        private readonly List<Token> _tokens;
        private int _position = 0;

        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
        }

        // --- Головний метод ---
        // Program = { TopLevelDecl }
        public ProgramNode ParseProgram()
        {
            List<Stmt> statements = new List<Stmt>();
            while (!IsAtEnd())
            {
                // На верхньому рівні дозволені лише оголошення
                statements.Add(ParseTopLevelDecl());
            }
            return new ProgramNode(statements);
        }

        // TopLevelDecl = VarDecl | ConstDecl | FuncDecl
        private Stmt ParseTopLevelDecl()
        {
            if (Check(TokenType.KwFunc))
            {
                return ParseFuncDecl();
            }
            if (Check(TokenType.KwVar))
            {
                var stmt = ParseVarDecl();
                Match(TokenType.Semicolon, "Expected ';' after top-level var declaration.");
                return stmt;
            }
            if (Check(TokenType.KwConst))
            {
                var stmt = ParseConstDecl();
                Match(TokenType.Semicolon, "Expected ';' after top-level const declaration.");
                return stmt;
            }

            throw Error(CurrentToken, "Expected 'func', 'var', or 'const' at the top level.");
        }

        // --- Парсинг Інструкцій (Statements) ---

        // Statement = SimpleStatement ';' | BlockStatement
        private Stmt ParseStatement()
        {
            // Блокові інструкції (не потребують ';')
            if (Check(TokenType.KwIf)) return ParseIfStmt();
            if (Check(TokenType.KwFor)) return ParseForStmt();
            if (Check(TokenType.LBrace)) return ParseBlock();

            // Прості інструкції (потребують ';')
            Stmt stmt = ParseSimpleStatement();
            if (Check(TokenType.Semicolon))
            {
                Advance();
                return stmt;
            }

            throw Error(PreviousToken, "Expected ';' after statement.");
        }

        // SimpleStatement = VarDecl | ConstDecl | ...
        private Stmt ParseSimpleStatement()
        {
            if (Check(TokenType.KwVar))
            {
                // Обробка 'var':
                // 1. var i int (VarDeclStmt)
                // 2. var i = 0 (VarInitStmt - використаємо ConstDeclStmt)

                Match(TokenType.KwVar, "Expected 'var'.");
                Token name = Match(TokenType.Id, "Expected variable name.");

                if (Check(TokenType.OpAssign)) // Випадок 2: var i = 0
                {
                    Match(TokenType.OpAssign, "Expected '='.");
                    Expr value = ParseExpression();
                    // Повторно використовуємо вузол ConstDeclStmt для 'var i = 0'
                    return new ConstDeclStmt(name, value);
                }
                else // Випадок 1: var i int
                {
                    Token type = ParseType();
                    return new VarDeclStmt(name, type);
                }
            }
            if (Check(TokenType.KwVar)) return ParseVarDecl();
            if (Check(TokenType.KwConst)) return ParseConstDecl();
            if (Check(TokenType.KwReturn)) return ParseReturnStmt();
            if (Check(TokenType.KwBreak)) return ParseBreakStmt();
            if (Check(TokenType.KwRead)) return ParseReadStmt();
            if (Check(TokenType.KwWrite)) return ParseWriteStmt();

            // Це або AssignStmt, або FunctionCallStmt
            return ParseAssignOrCallStmt();
        }

        // Block = '{' { Statement } '}'
        private BlockStmt ParseBlock()
        {
            Match(TokenType.LBrace, "Expected '{' to start a block.");
            List<Stmt> statements = new List<Stmt>();

            while (!Check(TokenType.RBrace) && !IsAtEnd())
            {
                statements.Add(ParseStatement());
            }

            Match(TokenType.RBrace, "Expected '}' to end a block.");
            return new BlockStmt(statements);
        }

        // --- Конкретні інструкції ---

        private Stmt ParseVarDecl()
        {
            Token keyword = Match(TokenType.KwVar, "Expected 'var'.");
            Token name = Match(TokenType.Id, "Expected variable name.");
            Token type = ParseType();
            return new VarDeclStmt(name, type);
        }

        private Stmt ParseConstDecl()
        {
            Token keyword = Match(TokenType.KwConst, "Expected 'const'.");
            Token name = Match(TokenType.Id, "Expected constant name.");
            Match(TokenType.OpAssign, "Expected '=' in constant declaration.");
            Expr value = ParseExpression();
            return new ConstDeclStmt(name, value);
        }

        private Stmt ParseAssignOrCallStmt()
        {
            Token name = Match(TokenType.Id, "Expected identifier.");

            if (Check(TokenType.OpAssign)) // 1. Це Присвоєння (AssignStmt)
            {
                Match(TokenType.OpAssign, "Expected '='.");
                Expr value = ParseExpression();
                return new AssignStmt(name, value);
            }
            else if (Check(TokenType.LParen)) // 2. Це Виклик функції (FunctionCallStmt)
            {
                // Нам потрібно "повернути" `name` назад і розібрати
                // виклик функції як повний вираз.
                _position--; // "Відкочуємо" 'name'
                Expr callExpr = ParseFunctionCall();
                return new ExpressionStmt(callExpr);
            }

            throw Error(CurrentToken, $"Expected '=' or '(' after identifier '{name.Lexeme}'.");
        }

        // FuncDecl = 'func' ident '(' [ ParameterList ] ')' Type Block
        private Stmt ParseFuncDecl()
        {
            Match(TokenType.KwFunc, "Expected 'func'.");
            Token name = Match(TokenType.Id, "Expected function name.");
            Match(TokenType.LParen, "Expected '('.");

            List<ParameterDeclStmt> parameters = new List<ParameterDeclStmt>();
            if (!Check(TokenType.RParen))
            {
                do
                {
                    Token paramName = Match(TokenType.Id, "Expected parameter name.");
                    Token paramType = ParseType();
                    parameters.Add(new ParameterDeclStmt(paramName, paramType));
                } while (MatchAny(TokenType.Comma));
            }
            Match(TokenType.RParen, "Expected ')'.");

            Token returnType = ParseType(); // (int, float64, void...)
            BlockStmt body = ParseBlock();

            return new FuncDeclStmt(name, parameters, returnType, body);
        }

        // IfStmt = 'if' Expression Block [ 'else' ( IfStmt | Block ) ]
        private Stmt ParseIfStmt()
        {
            Match(TokenType.KwIf, "Expected 'if'.");
            Expr condition = ParseExpression();
            Stmt thenBranch = ParseBlock();
            Stmt? elseBranch = null;

            if (MatchAny(TokenType.KwElse))
            {
                if (Check(TokenType.KwIf))
                {
                    elseBranch = ParseIfStmt(); // 'else if'
                }
                else
                {
                    elseBranch = ParseBlock(); // 'else'
                }
            }
            return new IfStmt(condition, thenBranch, elseBranch);
        }

        // ForStmt = 'for' [ SimpleStmt ] ';' [ Expression ] ';' [ Expr ] Block
        private Stmt ParseForStmt()
        {
            Match(TokenType.KwFor, "Expected 'for'.");

            // --- GO-СТИЛЬ ---

            // Випадок 1: 'for {' (Нескінченний цикл)
            if (Check(TokenType.LBrace))
            {
                Stmt internalBody = ParseBlock();
                return new ForStmt(null, null, null, internalBody);
            }

            // --- C-СТИЛЬ (очікуємо крапки з комою) ---

            // 1. Ініціалізатор
            Stmt? initializer = null;
            if (!Check(TokenType.Semicolon))
            {
                initializer = ParseForSimpleStatement();
            }
            Match(TokenType.Semicolon, "Expected ';' after for-loop initializer.");

            // 2. Умова
            Expr? condition = null;
            if (!Check(TokenType.Semicolon))
            {
                condition = ParseExpression();
            }
            Match(TokenType.Semicolon, "Expected ';' after for-loop condition.");

            // 3. Інкремент
            Stmt? increment = null;
            if (!Check(TokenType.LBrace)) // Якщо це не початок блоку
            {
                // Інкремент - це також 'SimpleStatement', але без ';'
                increment = ParseForSimpleStatement();
            }

            // 4. Тіло
            Stmt body = ParseBlock();

            return new ForStmt(initializer, condition, increment, body);
        }

        /// <summary>
        /// Спеціальна версія парсера інструкцій для 'for',
        /// яка НЕ вимагає крапки з комою в кінці
        /// </summary>
        private Stmt ParseForSimpleStatement()
        {
            // 1. 'var i = 0' або 'var i int'
            if (Check(TokenType.KwVar))
            {
                Match(TokenType.KwVar, "Expected 'var'.");
                Token name = Match(TokenType.Id, "Expected variable name.");

                if (Check(TokenType.OpAssign)) // 'var i = 0'
                {
                    Match(TokenType.OpAssign, "Expected '='.");
                    Expr value = ParseExpression();
                    return new ConstDeclStmt(name, value);
                }
                else // 'var i int'
                {
                    Token type = ParseType();
                    return new VarDeclStmt(name, type);
                }
            }

            // 2. 'i = 0' або 'myFunc()'
            // Це або AssignStmt, або FunctionCallStmt
            return ParseAssignOrCallStmt();
        }

        // ReturnStmt = 'return' [Expression]
        private Stmt ParseReturnStmt()
        {
            Token keyword = Match(TokenType.KwReturn, "Expected 'return'.");
            Expr? value = null;
            if (!Check(TokenType.Semicolon))
            {
                value = ParseExpression();
            }
            return new ReturnStmt(keyword, value);
        }

        // BreakStmt = 'break'
        private Stmt ParseBreakStmt()
        {
            Token keyword = Match(TokenType.KwBreak, "Expected 'break'.");
            return new BreakStmt(keyword);
        }

        // ReadStmt = 'read' '(' IdentList ')'
        private Stmt ParseReadStmt()
        {
            Token keyword = Match(TokenType.KwRead, "Expected 'read'.");
            Match(TokenType.LParen, "Expected '(' after 'read'.");

            List<Token> variables = new List<Token>();
            do
            {
                variables.Add(Match(TokenType.Id, "Expected identifier in read list."));
            } while (MatchAny(TokenType.Comma));

            Match(TokenType.RParen, "Expected ')' after identifier list.");
            return new ReadStmt(keyword, variables);
        }

        // WriteStmt = 'write' '(' [ ExpressionList ] ')'
        private Stmt ParseWriteStmt()
        {
            Token keyword = Match(TokenType.KwWrite, "Expected 'write'.");
            Match(TokenType.LParen, "Expected '(' after 'write'.");

            List<Expr> arguments = new List<Expr>();
            if (!Check(TokenType.RParen))
            {
                do
                {
                    arguments.Add(ParseExpression());
                } while (MatchAny(TokenType.Comma));
            }

            Match(TokenType.RParen, "Expected ')' after expression list.");
            return new WriteStmt(keyword, arguments);
        }

        private Expr ParseExpression()
        {
            // 1. Розбираємо ліву частину (з вищим пріоритетом)
            Expr left = ParseAdditiveExpr();

            // 2. Перевіряємо, чи є оператор порівняння
            while (MatchAny(TokenType.OpEq, TokenType.OpNeq, TokenType.OpLt,
                             TokenType.OpLe, TokenType.OpGt, TokenType.OpGe))
            {
                // 3. Якщо є, створюємо вузол BinaryExpr
                Token op = PreviousToken;
                Expr right = ParseAdditiveExpr();
                left = new BinaryExpr(left, op, right);
            }

            // 4. Повертаємо весь вираз
            return left;
        }

        // AdditiveExpr = MultiplicativeExpr { AddOp MultiplicativeExpr }
        private Expr ParseAdditiveExpr()
        {
            Expr left = ParseMultiplicativeExpr();
            while (MatchAny(TokenType.OpAdd, TokenType.OpSub))
            {
                Token op = PreviousToken;
                Expr right = ParseMultiplicativeExpr();
                left = new BinaryExpr(left, op, right);
            }
            return left;
        }

        // MultiplicativeExpr = PowerExpr { MultOp PowerExpr }
        private Expr ParseMultiplicativeExpr()
        {
            Expr left = ParsePowerExpr();
            while (MatchAny(TokenType.OpMul, TokenType.OpDiv))
            {
                Token op = PreviousToken;
                Expr right = ParsePowerExpr();
                left = new BinaryExpr(left, op, right);
            }
            return left;
        }

        // PowerExpr = UnaryExpr [ '^' PowerExpr ] (Право-асоціативний)
        private Expr ParsePowerExpr()
        {
            Expr left = ParseUnaryExpr();
            if (MatchAny(TokenType.OpPow))
            {
                Token op = PreviousToken;
                Expr right = ParsePowerExpr(); // Рекурсія для правої асоціативності
                left = new BinaryExpr(left, op, right);
            }
            return left;
        }

        // UnaryExpr = [ Sign ] PrimaryExpr
        private Expr ParseUnaryExpr()
        {
            if (MatchAny(TokenType.OpAdd, TokenType.OpSub))
            {
                Token op = PreviousToken;
                Expr right = ParseUnaryExpr(); // Дозволяємо --x або -+x
                return new UnaryExpr(op, right);
            }
            return ParsePrimaryExpr();
        }

        // PrimaryExpr = ident | Const | '(' Expression ')' | FunctionCall | CastExpr
        private Expr ParsePrimaryExpr()
        {
            // 1. Перевірка на Літерали
            if (MatchAny(TokenType.IntConst, TokenType.RealConst,
                         TokenType.StringConst, TokenType.BoolConst))
            {
                return new LiteralExpr(PreviousToken.Value);
            }

            // 2. Перевірка на Явне Приведення Типів (Cast)
            if (Check(TokenType.KwInt) || Check(TokenType.KwFloat64) ||
                Check(TokenType.KwBool) || Check(TokenType.KwString))
            {
                // Ми бачимо тип. Перевіряємо, чи це приведення, "зазирнувши"
                // на наступний токен, не з'їдаючи його.
                if (_tokens[_position + 1].Type == TokenType.LParen)
                {
                    // Це дійсно приведення типу, напр. float64(counter)
                    Token typeToken = Advance(); // З'їдаємо 'float64'
                    Match(TokenType.LParen, "Expected '(' after type name for cast.");
                    Expr expressionToCast = ParseExpression();
                    Match(TokenType.RParen, "Expected ')' after cast expression.");
                    return new CastExpr(typeToken, expressionToCast);
                }
                // Якщо за типом НЕ йде '(', то це не вираз
                // (і, ймовірно, синтаксична помилка, яку зловить 
                // 'throw Error' в кінці)
            }

            // 3. Перевірка на Змінну або Виклик Функції
            if (Check(TokenType.Id))
            {
                // Зазираємо наперед: це 'id' чи 'id(...)' ?
                if (_tokens[_position + 1].Type == TokenType.LParen)
                {
                    return ParseFunctionCall();
                }

                // Це просто змінна
                return new VariableExpr(Advance());
            }

            // 4. Перевірка на Вираз в дужках
            if (MatchAny(TokenType.LParen))
            {
                Expr expr = ParseExpression();
                Match(TokenType.RParen, "Expected ')' after expression.");
                return expr;
            }

            // 5. Помилка
            throw Error(CurrentToken, "Expected an expression (variable, literal, '(', or type cast).");
        }

        // FunctionCall = ident '(' [ ArgumentList ] ')'
        private Expr ParseFunctionCall()
        {
            Expr callee = new VariableExpr(Match(TokenType.Id, "Expected function name."));
            Token paren = Match(TokenType.LParen, "Expected '(' after function name.");

            List<Expr> arguments = new List<Expr>();
            if (!Check(TokenType.RParen))
            {
                do
                {
                    arguments.Add(ParseExpression());
                } while (MatchAny(TokenType.Comma));
            }
            Match(TokenType.RParen, "Expected ')' after arguments.");

            return new CallExpr(callee, paren, arguments);
        }

        // Допоміжна функція для 'Type'
        private Token ParseType()
        {
            if (MatchAny(TokenType.KwInt, TokenType.KwFloat64,
                           TokenType.KwBool, TokenType.KwString, TokenType.KwVoid))
            {
                return PreviousToken;
            }
            throw Error(CurrentToken, "Expected a type (int, float64, bool, string, or void).");
        }

        // --- Обробка помилок і навігація ---

        private Token CurrentToken => _tokens[_position];
        private Token PreviousToken => _tokens[_position - 1];
        private bool IsAtEnd() => CurrentToken.Type == TokenType.Eof;

        private Token Advance()
        {
            if (!IsAtEnd()) _position++;
            return PreviousToken;
        }

        private bool Check(TokenType type) => CurrentToken.Type == type;

        private bool MatchAny(params TokenType[] types)
        {
            foreach (var type in types)
            {
                if (Check(type))
                {
                    Advance();
                    return true;
                }
            }
            return false;
        }

        private Token Match(TokenType type, string errorMessage)
        {
            if (Check(type)) return Advance();
            throw Error(CurrentToken, errorMessage);
        }

        private ParserException Error(Token token, string message)
        {
            // Ми використовуємо Token, щоб отримати рядок та колонку
            return new ParserException(
                $"[Line {token.Line}, Col {token.Column}] Error at '{token.Lexeme}': {message}"
            );
        }
    }
}
