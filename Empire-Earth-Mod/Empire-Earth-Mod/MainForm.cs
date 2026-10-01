using System;
using System.Windows.Forms;
using Empire_Earth_Mod_Lib;

namespace Empire_Earth_Mod
{
    public partial class MainForm : Form
    {
        
        public MainForm()
        {
            InitializeComponent();
            windowsVersionLabel.Text += WindowsVersion.GetWindowsVersionName(WindowsVersion.GetCurrentWindowsVersion());
        }

        private void createModButton_Click(object sender, EventArgs e)
        {
            // ShowDialog does not dispose the form; the using block releases it and its resources.
            using (var modCreatorForm = new ModCreatorForm())
            {
                modCreatorForm.ShowDialog(this);
            }
        }
    }
}