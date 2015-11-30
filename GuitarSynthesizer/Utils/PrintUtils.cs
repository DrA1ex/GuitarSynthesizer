using System;

namespace GuitarSynthesizer.Utils
{
    internal class PrintUtils
    {
        private static readonly int MaxBufferWidth = Console.LargestWindowWidth;
        private const int MinColumnsPerTrack = 30;

        private const string HeaderFirstSymbol = "┌";
        private const string HeaderPartSymbol = "┬";
        private const string HeaderLastSymbol = "┐";

        private const string RowFirstSymbol = "├";
        private const string RowPartSymbol = "┼";
        private const string RowLastSymbol = "┤";
        private const string RowLinePartSymbol = "│";

        private const string FooterFirstSymbol = "└";
        private const string FooterPartSymbol = "┴";
        private const string FooterLastSymbol = "┘";

        private const char HorizontalLineSymbol = '─';

        private static int TrackCount { get; set; }
        private static int ColumnsPerTrack { get; set; }

        private static string ContentPattern { get; set; }

        internal static void Init(int trackCount)
        {
            TrackCount = trackCount;
            ColumnsPerTrack = Math.Max(MinColumnsPerTrack, ComputeWidthPerColumn(trackCount, Console.BufferWidth));
            var newWidth = ComputeWidth(trackCount, ColumnsPerTrack);
            if(newWidth > MaxBufferWidth)
            {
                ColumnsPerTrack = ComputeWidthPerColumn(trackCount, MaxBufferWidth);
                newWidth = ComputeWidth(trackCount, ColumnsPerTrack);
            }
            Console.BufferWidth = newWidth;
            Console.WindowWidth = Console.BufferWidth;

            ContentPattern = $"{{0,-{ColumnsPerTrack}}}";
        }

        private static int ComputeWidthPerColumn(int trackCount, int consoleWidth) => (consoleWidth - trackCount - 1) / trackCount;
        private static int ComputeWidth(int trackCount, int widthPerTrack) => widthPerTrack * trackCount + trackCount + 2; // + dividers + first and last symbol + \r\n

        internal static void PrintHeaderOfTable()
        {
            PrintHeaderLine();
            for(int i = 0; i < TrackCount; i++)
            {
                AsyncConsole.Write(RowLinePartSymbol);
                AsyncConsole.Write(ContentPattern, String.Format("TRACK #{0}", i + 1));
            }
            AsyncConsole.WriteLine(RowLinePartSymbol);
        }

        private static void PrintHeaderLine()
        {
            PrintLine(HeaderFirstSymbol, HeaderPartSymbol, HeaderLastSymbol, HorizontalLineSymbol, ColumnsPerTrack);
        }

        internal static void PrintContentTable()
        {
            for(int i = 0; i < TrackCount; i++)
            {
                AsyncConsole.Write(RowLinePartSymbol);
                AsyncConsole.Write(ContentPattern, "");
            }

            AsyncConsole.Write(RowLinePartSymbol);
        }

        internal static void PrintContent(object obj, int position)
        {
            AsyncConsole.SetCursorLeft(1 + position * (ColumnsPerTrack + 1));
            AsyncConsole.Write(ContentPattern, obj);
        }

        internal static void PrintRowDividerTable()
        {
            PrintLine(RowFirstSymbol, RowPartSymbol, RowLastSymbol, HorizontalLineSymbol, ColumnsPerTrack);
        }

        internal static void PrintFooterOfTable()
        {
            PrintLine(FooterFirstSymbol, FooterPartSymbol, FooterLastSymbol, HorizontalLineSymbol, ColumnsPerTrack);
        }

        private static void PrintLine(string first, string part, string last, char fill, int length)
        {
            var line = new string(fill, length);
            AsyncConsole.Write(first);
            for(int i = 0; i < TrackCount; i++)
            {
                if(i > 0)
                {
                    AsyncConsole.Write(part);
                }

                AsyncConsole.Write(line);
            }
            AsyncConsole.WriteLine(last);
        }
    }
}
