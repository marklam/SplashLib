using System.Drawing;

namespace SplashLib
{
    public class SplashScreenSurface
    {
        private readonly Rectangle _bounds;
        private readonly Graphics _graphics;

        internal SplashScreenSurface(Graphics g, Rectangle r)
        {
            _graphics = g;
            _bounds = r;
        }

        public Rectangle Bounds
        {
            get { return _bounds; }
        }

        public Graphics Graphics
        {
            get { return _graphics; }
        }
    }
}