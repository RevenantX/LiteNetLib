#if WINRT && !UNITY_EDITOR
namespace LiteNetLib
{
    public struct ConsoleColor
    {
        public static readonly ConsoleColor Gray = new ConsoleColor();
        public static readonly ConsoleColor Yellow = new ConsoleColor();
        public static readonly ConsoleColor Cyan = new ConsoleColor();
        public static readonly ConsoleColor DarkCyan = new ConsoleColor();
        public static readonly ConsoleColor DarkGreen = new ConsoleColor();
        public static readonly ConsoleColor Blue = new ConsoleColor();
        public static readonly ConsoleColor DarkRed = new ConsoleColor();
        public static readonly ConsoleColor Red = new ConsoleColor();
        public static readonly ConsoleColor Green = new ConsoleColor();
    }
}
#endif
