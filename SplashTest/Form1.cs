using System;
using System.Drawing;
using System.Windows.Forms;
using SplashLib;

namespace SplashTest
{
    public partial class Form1 : Form
    {
        private bool _firstActivated = true;

        public Form1()
        {
            InitializeComponent();
            ClientSize = new Size(800, 600);
            System.Threading.Thread.Sleep(1000);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (_firstActivated)
            {
                _firstActivated = false;
                SplashWindow.Current.Hide(this);
            }
        }
    }
}