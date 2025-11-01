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
    }
}
