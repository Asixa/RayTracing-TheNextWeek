using System;
using System.Windows.Forms;
using Random = ALight.Render.Mathematics.Random;

namespace ALight
{
    public partial class Form1 : Form
    {
        public static Form1 main;
        private readonly Render.Renderer renderer=new Render.Renderer();
        private int seconds;
        public Form1()
        {
            main = this;
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            renderer.Init();

        }
        

        private int samples;
        public void SetSPP()
        {
            SPP.Text = "已采样" + (++samples) + "次";
        }

        private void button2_Click(object sender, EventArgs e)
        {
            renderer.Save();
            
        }
    }
}
