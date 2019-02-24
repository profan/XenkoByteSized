using Xenko.Engine;

namespace XenkoByteSized.Linux
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
