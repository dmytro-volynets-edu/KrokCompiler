using KrokCompiler.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrokCompiler.Lexer
{
    public static class Utils
    {
        /// <summary>
        /// Function to escape characters
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static string GetDisplayableChar(char c)
        {
            switch (c)
            {
                case '\n':
                    return "\\n"; 
                case '\r':
                    return "\\r";
                case '\t':
                    return "\\t";
                default:
                    return c.ToString();
            }
        }

        /// <summary>
        /// Function to print tables of unique constants and identifiers
        /// </summary>
        /// <param name="tokens"></param>
        public static void PrintConstantsAndIdentifiers(List<Token> tokens)
        {
            void PrintIdentifiers(Dictionary<string, int> tableOfId)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("-----------------------");
                Console.WriteLine("Identifiers:");
                Console.ResetColor();
                foreach (var item in tableOfId)
                {
                    Console.WriteLine($"{item.Key,-20}  {item.Value}");
                }
            }

            void PrintConstants(Dictionary<string, (string, int)> tableOfConst)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("-----------------------");
                Console.WriteLine("Constants:");
                Console.ResetColor();
                foreach (var item in tableOfConst)
                {
                    Console.WriteLine($"{item.Key}  {item.Value}");
                }
            }

            if (tokens.Count > 0)
            {
                // 3. Аналіз успішний. 
                // ТЕПЕР ми будуємо таблиці ID та Констант

                var tableOfId = new Dictionary<string, int>();
                var tableOfConst = new Dictionary<string, (string, int)>();

                // Проходимо по списку токенів, щоб заповнити таблиці
                foreach (var token in tokens)
                {
                    // Додаємо ID
                    if (token.Type == TokenType.Id)
                    {
                        if (!tableOfId.ContainsKey(token.Lexeme))
                        {
                            // Додаємо, лише якщо бачимо цей ID вперше
                            tableOfId.Add(token.Lexeme, tableOfId.Count + 1);
                        }
                    }
                    // Додаємо Константи
                    else if (token.Type == TokenType.IntConst ||
                             token.Type == TokenType.RealConst ||
                             token.Type == TokenType.StringConst)
                    {
                        if (!tableOfConst.ContainsKey(token.Lexeme))
                        {
                            tableOfConst.Add(token.Lexeme,
                                (token.Type.ToString(), tableOfConst.Count + 1));
                        }
                    }
                }

                PrintIdentifiers(tableOfId);
                PrintConstants(tableOfConst);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("-----------------------");
                Console.WriteLine("No tokens found");
                Console.ResetColor();
            }
        }

    }
}
