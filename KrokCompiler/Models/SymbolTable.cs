using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrokCompiler.Models
{
    public class Symbol
    {
        public string Name { get; }
        public KrokType Type { get; }

        public Symbol(string name, KrokType type)
        {
            Name = name;
            Type = type;
        }
    }

    public class SymbolTable
    {
        // Стек областей видимості. Верхній словник - поточна область.
        private readonly Stack<Dictionary<string, Symbol>> _scopes = new();

        public SymbolTable()
        {
            // Починаємо з глобальної області видимості
            BeginScope();
        }

        /// <summary>
        /// Починає нову область видимості (при вході в '{' або в функцію)
        /// </summary>
        public void BeginScope()
        {
            _scopes.Push(new Dictionary<string, Symbol>());
        }

        /// <summary>
        /// Завершує поточну область видимості (при виході з '}')
        /// </summary>
        public void EndScope()
        {
            _scopes.Pop();
        }

        /// <summary>
        /// Оголошує новий символ у *поточній* області видимості
        /// Кидає помилку, якщо символ вже існує в цій області
        /// </summary>
        public bool Define(string name, KrokType type)
        {
            var currentScope = _scopes.Peek();
            if (currentScope.ContainsKey(name))
            {
                return false; // Помилка: Повторне оголошення
            }

            currentScope[name] = new Symbol(name, type);
            return true;
        }

        /// <summary>
        /// Шукає символ, проходячи від поточної області видимості вгору до глобальної
        /// </summary>
        public Symbol? Lookup(string name)
        {
            // Проходимо по стеку згори донизу
            foreach (var scope in _scopes)
            {
                if (scope.TryGetValue(name, out Symbol symbol))
                {
                    return symbol; // Знайшли
                }
            }
            return null; // Не знайшли (неоголошено)
        }
    }
}
