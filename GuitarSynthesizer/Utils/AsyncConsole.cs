using System;
using System.Collections.Concurrent;
using System.Threading;

namespace GuitarSynthesizer.Utils
{
    public static class AsyncConsole
    {
        private static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();
        private static readonly ManualResetEventSlim NewActionEvent = new ManualResetEventSlim(false);
        private static readonly ManualResetEventSlim ExitEvent = new ManualResetEventSlim(false);

        static AsyncConsole()
        {
            ThreadPool.QueueUserWorkItem(o => Loop());
            AppDomain.CurrentDomain.DomainUnload += (sender, args) =>
            {
                NewActionEvent.Set();
                ExitEvent.Set();
            };
        }

        public static void Write(string value) => AddAction(() => Console.Write(value));

        public static void Write(string format, params object[] args) => AddAction(() => Console.Write(format, args));

        public static void WriteLine() => AddAction(Console.WriteLine);

        public static void WriteLine(string value) => AddAction(() => Console.WriteLine(value));

        public static void WriteLine(string format, params object[] args)
            => AddAction(() => Console.WriteLine(format, args));

        public static void SetCursorPosition(int left, int top) => AddAction(() => Console.SetCursorPosition(left, top));

        public static void SetCursorLeft(int left)
            => AddAction(() => Console.SetCursorPosition(left, Console.CursorTop));

        private static void AddAction(Action action)
        {
            Queue.Enqueue(action);
            NewActionEvent.Set();
        }

        private static void Loop()
        {
            Action action;

            while(!ExitEvent.Wait(0))
            {
                NewActionEvent.Wait();

                if(Queue.TryDequeue(out action))
                {
                    action();
                }
                else
                {
                    NewActionEvent.Reset();
                }
            }

            NewActionEvent.Dispose();

            while(Queue.TryDequeue(out action))
            {
                action();
            }

            ExitEvent.Dispose();
        }
    }
}