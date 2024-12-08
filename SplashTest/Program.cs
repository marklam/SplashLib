using System;
using System.Drawing;
using System.Windows.Forms;
using SplashLib;

namespace SplashTest
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            // Choose *one* of the following splash screen launchers... not both!
            launchNormalSplashScreen();
            //launchCustomEventHandlerSplashScreen();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }

        private static void launchCustomEventHandlerSplashScreen()
        {
            SplashWindow.Current.Image = new Bitmap(typeof (Form1), "splash.jpg");
            SplashWindow.Current.ShowShadow = true;
            SplashWindow.Current.MinimumDuration = 3000;
            SplashWindow.Current.SetCustomizer(CustomEventHandler);
            SplashWindow.Current.Show();
        }

        private static void launchNormalSplashScreen()
        {
            SplashWindow.Current.Image = new Bitmap(typeof (Form1), "splash.jpg");
            SplashWindow.Current.ShowShadow = true;
            SplashWindow.Current.MinimumDuration = 3000;
            SplashWindow.Current.Show();
        }

        private static void CustomEventHandler(SplashScreenSurface surface)
        {
            Graphics graphics = surface.Graphics;
            Rectangle bounds = surface.Bounds;

            graphics.DrawString("Welcome to the Application!",
                                new Font("Impact", 32),
                                new SolidBrush(Color.Red),
                                new PointF(bounds.Left + 20, bounds.Top + 150));
        }
    }
}