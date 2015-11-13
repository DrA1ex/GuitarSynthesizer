﻿using System;

namespace GuitarSynthesizer.Utils
{
    internal class PrintUtils
    {
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
            ColumnsPerTrack = Math.Max(MinColumnsPerTrack, (Console.BufferWidth - trackCount - 1) / trackCount);
            Console.BufferWidth = ColumnsPerTrack * trackCount + trackCount + 2; // + dividers + first and last symbol + \r\n
            Console.WindowWidth = Console.BufferWidth;

            ContentPattern = $"{{0,-{ColumnsPerTrack}}}";
        }

        internal static void PrintHeaderOfTable()
        {
            PrintHeaderLine();
            for(int i = 0; i < TrackCount; i++)
            {
                Console.Write(RowLinePartSymbol);
                Console.Write(ContentPattern, String.Format("TRACK #{0}", i + 1));
            }
            Console.WriteLine(RowLinePartSymbol);
        }

        private static void PrintHeaderLine()
        {
            PrintLine(HeaderFirstSymbol, HeaderPartSymbol, HeaderLastSymbol, HorizontalLineSymbol, ColumnsPerTrack);
        }

        internal static void PrintContentTable()
        {
            for(int i = 0; i < TrackCount; i++)
            {
                Console.Write(RowLinePartSymbol);
                Console.Write(ContentPattern, "");
            }

            Console.Write(RowLinePartSymbol);
        }

        internal static void PrintContent(object obj, int position)
        {
            Console.SetCursorPosition(1 + position * (ColumnsPerTrack + 1), Console.CursorTop);
            Console.Write(ContentPattern, obj);
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
            Console.Write(first);
            for(int i = 0; i < TrackCount; i++)
            {
                if(i > 0)
                {
                    Console.Write(part);
                }

                Console.Write(line);
            }
            Console.WriteLine(last);
        }
    }
}
