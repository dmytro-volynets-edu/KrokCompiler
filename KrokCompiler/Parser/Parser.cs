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
        public void ParseProgram()
        {
            try
            {
                while (!IsAtEnd())
                {
                    ParseTopLevelDecl();
                }
            }
            catch (ParserException e)
            {
                // (Тут можна додати логіку для виводу помилки,
                // але ми просто дозволяємо їй "вилетіти" назовні)
                throw;
            }
        }

        // --- Допоміжні методи навігації ---

        private Token CurrentToken => _tokens[_position];
        private Token PreviousToken => _tokens[_position - 1];
        private bool IsAtEnd() => CurrentToken.Type == TokenType.Eof;

        // "З'їдає" поточний токен і повертає його
        private Token Advance()
        {
            if (!IsAtEnd()) _position++;
            return PreviousToken;
        }

        // Перевіряє, чи поточний токен має потрібний тип
        private bool Check(TokenType type) => CurrentToken.Type == type;

        // Перевіряє, чи поточний токен має один із типів у списку
        private bool MatchAny(params TokenType[] types)
        {
            foreach (var type in types)
            {
                if (Check(type))
                {
                    Advance(); // З'їдаємо токен, якщо він підійшов
                    return true;
                }
            }
            return false;
        }

        // "Вимагає" токен. Якщо токен не той - кидає помилку
        private Token Match(TokenType type, string errorMessage)
        {
            if (Check(type)) return Advance();
            throw Error(CurrentToken, errorMessage);
        }

        // --- Реалізація граматики (Методи рекурсивного спуску) ---

        // Program = { TopLevelDecl }
        private void ParseTopLevelDecl()
        {
            if (Check(TokenType.KwFunc))
            {
                ParseFuncDecl();
            }
            else if (Check(TokenType.KwVar))
            {
                ParseVarDecl();
            }
            else if (Check(TokenType.KwConst))
            {
                ParseConstDecl();
            }
            else
            {
                // Якщо ми не на початку оголошення, це помилка
                throw Error(CurrentToken, "Expected 'func', 'var', or 'const'.");
            }
        }

        // FuncDecl = 'func' ident '(' [ ParameterList ] ')' Type Block
        private void ParseFuncDecl()
        {
            Match(TokenType.KwFunc, "Expected 'func'.");
            Match(TokenType.Id, "Expected function name.");
            Match(TokenType.LParen, "Expected '('.");
            if (!Check(TokenType.RParen))
            {
                ParseParameterList();
            }
            Match(TokenType.RParen, "Expected ')'.");
            ParseType(); // Вимагаємо тип (int, float64, void...)
            ParseBlock(); // Тіло функції
        }

        // ParameterList = ParameterDecl { ',' ParameterDecl }
        private void ParseParameterList()
        {
            do
            {
                ParseParameterDecl();
            } while (MatchAny(TokenType.Comma));
        }

        // ParameterDecl = ident Type
        private void ParseParameterDecl()
        {
            Match(TokenType.Id, "Expected parameter name.");
            ParseType();
        }

        // VarDecl = 'var' ident Type
        private void ParseVarDecl()
        {
            Match(TokenType.KwVar, "Expected 'var'.");
            Match(TokenType.Id, "Expected variable name.");
            ParseType();
        }

        // ConstDecl = 'const' ident '=' Expression
        private void ParseConstDecl()
        {
            Match(TokenType.KwConst, "Expected 'const'.");
            Match(TokenType.Id, "Expected constant name.");
            Match(TokenType.OpAssign, "Expected '=' in constant declaration.");
            ParseExpression(); // Константа має бути ініціалізована
        }

        // Block = '{' { Statement } '}'
        private void ParseBlock()
        {
            Match(TokenType.LBrace, "Expected '{' to start a block.");
            while (!Check(TokenType.RBrace) && !IsAtEnd())
            {
                ParseStatement();
            }
            Match(TokenType.RBrace, "Expected '}' to end a block.");
        }

        // Statement = ... (велика логіка розгалуження)
        private void ParseStatement()
        {
            if (Check(TokenType.KwVar))
            {
                ParseVarDecl();
            }
            else if (Check(TokenType.KwConst))
            {
                ParseConstDecl();
            }
            else if (Check(TokenType.KwIf))
            {
                ParseIfStmt();
            }
            else if (Check(TokenType.KwFor))
            {
                ParseForStmt();
            }
            else if (Check(TokenType.KwReturn))
            {
                ParseReturnStmt();
            }
            else if (Check(TokenType.KwBreak))
            {
                Advance(); // З'їдаємо 'break'
            }
            else if (Check(TokenType.KwRead))
            {
                ParseReadStmt();
            }
            else if (Check(TokenType.KwWrite))
            {
                ParseWriteStmt();
            }
            else if (Check(TokenType.LBrace))
            {
                ParseBlock();
            }
            else
            {
                // Це або AssignStmt, або FunctionCallStmt
                ParseAssignOrCallStmt();
            }
        }

        // Специфічні інструкції
        private void ParseIfStmt()
        {
            Match(TokenType.KwIf, "Expected 'if'.");
            ParseExpression();
            ParseBlock();
            if (MatchAny(TokenType.KwElse))
            {
                if (Check(TokenType.KwIf))
                {
                    ParseIfStmt(); // для 'else if'
                }
                else
                {
                    ParseBlock(); // для 'else'
                }
            }
        }

        // ... (тут мають бути ParseForStmt, ParseReturnStmt, ParseReadStmt, ParseWriteStmt) ...
        // ... (вони схожі за логікою) ...

        // --- Парсинг виразів (Expression Parsing) ---
        // Це серце парсера. Ми реалізуємо ієрархію пріоритетів.

        // Expression = AdditiveExpr [ RelOp AdditiveExpr ]
        private void ParseExpression()
        {
            ParseAdditiveExpr();

            while (MatchAny(TokenType.OpEq, TokenType.OpNeq, TokenType.OpLt,
                             TokenType.OpLe, TokenType.OpGt, TokenType.OpGe))
            {
                // (Ми просто 'з'їдаємо' праву частину, оскільки не будуємо AST)
                ParseAdditiveExpr();
            }
        }

        // AdditiveExpr = MultiplicativeExpr { AddOp MultiplicativeExpr }
        private void ParseAdditiveExpr()
        {
            ParseMultiplicativeExpr();
            while (MatchAny(TokenType.OpAdd, TokenType.OpSub))
            {
                ParseMultiplicativeExpr();
            }
        }

        // MultiplicativeExpr = PowerExpr { MultOp PowerExpr }
        private void ParseMultiplicativeExpr()
        {
            ParsePowerExpr();
            while (MatchAny(TokenType.OpMul, TokenType.OpDiv))
            {
                ParsePowerExpr();
            }
        }

        // PowerExpr = UnaryExpr [ '^' PowerExpr ] (Право-асоціативний)
        private void ParsePowerExpr()
        {
            ParseUnaryExpr();
            if (MatchAny(TokenType.OpPow))
            {
                ParsePowerExpr(); // Рекурсія для правої асоціативності
            }
        }

        // UnaryExpr = [ Sign ] PrimaryExpr
        private void ParseUnaryExpr()
        {
            if (MatchAny(TokenType.OpAdd, TokenType.OpSub))
            {
                // Просто 'з'їдаємо' знак
            }
            ParsePrimaryExpr();
        }

        // PrimaryExpr = ident | Const | '(' Expression ')' | FunctionCall
        private void ParsePrimaryExpr()
        {
            if (MatchAny(TokenType.IntConst, TokenType.RealConst,
                         TokenType.StringConst, TokenType.BoolConst))
            {
                return; // Це літерал, успіх
            }

            if (Check(TokenType.Id) && _tokens[_position + 1].Type == TokenType.LParen)
            {
                ParseFunctionCall(); // Це виклик функції (напр. max(a, b))
            }
            else if (MatchAny(TokenType.Id))
            {
                return; // Це змінна, успіх
            }
            else if (MatchAny(TokenType.LParen))
            {
                ParseExpression(); // Вираз у дужках
                Match(TokenType.RParen, "Expected ')' after expression.");
            }
            else
            {
                throw Error(CurrentToken, "Expected an expression (variable, literal, or '('.");
            }
        }

        // Допоміжна функція для 'Type'
        private void ParseType()
        {
            if (!MatchAny(TokenType.KwInt, TokenType.KwFloat64,
                           TokenType.KwBool, TokenType.KwString, TokenType.KwVoid))
            {
                throw Error(CurrentToken, "Expected a type (int, float64, bool, string, or void).");
            }
        }

        // --- Обробка помилок ---
        private ParserException Error(Token token, string message)
        {
            return new ParserException(
                $"[Line {token.Line}, Col {token.Column}] Error at '{token.Lexeme}': {message}"
            );
        }


        // IdentList = ident { ',' ident }
        private void ParseIdentList()
        {
            Match(TokenType.Id, "Expected at least one identifier in list.");
            while (MatchAny(TokenType.Comma))
            {
                Match(TokenType.Id, "Expected identifier after comma.");
            }
        }

        // ArgumentList = Expression { ',' Expression }
        // (Також використовується для ExpressionList у 'write')
        private void ParseArgumentList()
        {
            // Дозволяємо порожній список (напр. 'write()')
            if (Check(TokenType.RParen))
            {
                return;
            }

            do
            {
                ParseExpression();
            } while (MatchAny(TokenType.Comma));
        }

        // ReadStmt = 'read' '(' IdentList ')'
        private void ParseReadStmt()
        {
            Match(TokenType.KwRead, "Expected 'read'.");
            Match(TokenType.LParen, "Expected '(' after 'read'.");
            ParseIdentList();
            Match(TokenType.RParen, "Expected ')' after identifier list.");
        }

        // WriteStmt = 'write' '(' [ ExpressionList ] ')'
        private void ParseWriteStmt()
        {
            Match(TokenType.KwWrite, "Expected 'write'.");
            Match(TokenType.LParen, "Expected '(' after 'write'.");
            ParseArgumentList(); // Використовуємо той самий парсер списку, що й для функцій
            Match(TokenType.RParen, "Expected ')' after expression list.");
        }

        // SimpleStmt = AssignStmt | VarDecl 
        // (Спрощена версія, що НЕ споживає крапку з комою)
        private void ParseSimpleStmt()
        {
            if (Check(TokenType.KwVar))
            {
                // 'var' ident Type
                Advance(); // З'їдаємо 'var'
                Match(TokenType.Id, "Expected variable name in for-loop declaration.");
                ParseType();
            }
            else if (Check(TokenType.Id))
            {
                // 'ident' '=' Expression
                Advance(); // З'їдаємо 'ident'
                Match(TokenType.OpAssign, "Expected '=' in for-loop assignment.");
                ParseExpression();
            }
            else
            {
                // Помилка, якщо тут щось інше (окрім ';')
                throw Error(CurrentToken, "Expected assignment or variable declaration in for-loop.");
            }
        }

        // ForStmt = 'for' [ SimpleStmt ] ';' [ Expression ] ';' [ SimpleStmt ] Block
        private void ParseForStmt()
        {
            Match(TokenType.KwFor, "Expected 'for'.");

            // 1. Ініціалізація (InitStmt)
            if (!Check(TokenType.Semicolon))
            {
                ParseSimpleStmt();
            }
            Match(TokenType.Semicolon, "Expected ';' after for-loop init statement.");

            // 2. Умова (Condition)
            if (!Check(TokenType.Semicolon))
            {
                ParseExpression();
            }
            Match(TokenType.Semicolon, "Expected ';' after for-loop condition.");

            // 3. Пост-ітерація (PostStmt)
            if (!Check(TokenType.LBrace)) // Якщо це не початок блоку
            {
                ParseSimpleStmt();
            }

            // 4. Тіло (Block)
            ParseBlock();
        }

        // FunctionCall = ident '(' [ ArgumentList ] ')'
        private void ParseFunctionCall()
        {
            Match(TokenType.Id, "Expected function name.");
            Match(TokenType.LParen, "Expected '(' after function name.");
            ParseArgumentList();
            Match(TokenType.RParen, "Expected ')' after arguments.");
        }

        // Обробляє інструкції, що починаються з 'Id':
        // AssignStmt = ident '=' Expression
        // FunctionCallStmt = FunctionCall
        private void ParseAssignOrCallStmt()
        {
            // Ми вже знаємо, що CurrentToken - це Id (з 'ParseStatement')

            // Зазираємо на 1 токен вперед
            Token nextToken = _tokens[_position + 1];

            if (nextToken.Type == TokenType.OpAssign) // Це присвоєння
            {
                // Parse AssignStmt
                Match(TokenType.Id, "Expected variable name.");
                Match(TokenType.OpAssign, "Expected '=' in assignment.");
                ParseExpression();
            }
            else if (nextToken.Type == TokenType.LParen) // Це виклик функції
            {
                // Parse FunctionCallStmt
                ParseFunctionCall();
            }
            else
            {
                throw Error(CurrentToken, $"Expected '=' or '(' after identifier '{CurrentToken.Lexeme}', but got '{nextToken.Lexeme}'.");
            }
        }

        // ReturnStmt = 'return' [Expression]
        private void ParseReturnStmt()
        {
            Match(TokenType.KwReturn, "Expected 'return'.");

            // Перевіряємо, чи є вираз для повернення.
            // Якщо наступний токен - крапка з комою, 
            // це означає, що виразу немає (як у void-функції).
            if (!Check(TokenType.Semicolon))
            {
                // Якщо це не кінець інструкції, значить, має бути вираз
                ParseExpression();
            }

            // Примітка: крапка з комою (;) буде перевірена 
            // в головному методі ParseStatement() після повернення звідси.
        }
    }
}
