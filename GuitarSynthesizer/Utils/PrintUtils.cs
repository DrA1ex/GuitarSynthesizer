using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuitarSynthesizer.Utils
{
    internal class PrintUtils
    {
        private const int FirstColumnLength = 32;
        private const int SecondColumnLength = 20;
        private const int ThirdColumnLength = 22;

        private const string ContentPattern = "{{{0},-{1}}}";
        private const string HeaderPattern = "┌{0}┬{1}┬{2}┐";
        private const string RowDividerPattern = "├{0}┼{1}┼{2}┤";
        private const string FooterPattern = "└{0}┴{1}┴{2}┘";
        private const char HorizontalLineSymbol = '─';

        private static readonly string ContentLinePattern =
            $"│{String.Format(ContentPattern, 0, FirstColumnLength)}" +
            $"│{String.Format(ContentPattern, 1, SecondColumnLength)}" +
            $"│{String.Format(ContentPattern, 2, ThirdColumnLength)}│";

        internal static void PrintHeaderOfTable()
        {
            Console.WriteLine(HeaderPattern
                , new string(HorizontalLineSymbol, FirstColumnLength)
                , new string(HorizontalLineSymbol, SecondColumnLength)
                , new string(HorizontalLineSymbol, ThirdColumnLength));
        }

        internal static void PrintContentTable(object first, object second, object third)
        {
            Console.WriteLine(ContentLinePattern, first, second, third);
        }


        internal static void PrintRowDividerTable()
        {
            Console.WriteLine(RowDividerPattern
                , new string(HorizontalLineSymbol, FirstColumnLength)
                , new string(HorizontalLineSymbol, SecondColumnLength)
                , new string(HorizontalLineSymbol, ThirdColumnLength));
        }

        internal static void PrintFooterOfTable()
        {
            Console.WriteLine(FooterPattern
                , new string(HorizontalLineSymbol, FirstColumnLength)
                , new string(HorizontalLineSymbol, SecondColumnLength)
                , new string(HorizontalLineSymbol, ThirdColumnLength));
        }
    }
}
