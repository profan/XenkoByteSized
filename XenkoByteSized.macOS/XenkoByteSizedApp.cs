using Xenko.Engine;

namespace XenkoByteSized.macOS
{
    class XenkoByteSizedApp
    {
        static void Main(string[] args)
        {
            using (var game = new Game())
            {
                game.Run();
            }
        }
    }
}
