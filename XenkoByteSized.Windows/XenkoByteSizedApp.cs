using Xenko.Engine;
using Xenko.Graphics;

namespace XenkoByteSized.Windows
{
    class XenkoByteSizedApp
    {
        static void Main(string[] args)
        {
            using (var game = new Game())
            {
                /* debugaloo */
                // game.GraphicsDeviceManager.DeviceCreationFlags |= DeviceCreationFlags.Debug;
                game.Run();
            }
        }
    }
}
