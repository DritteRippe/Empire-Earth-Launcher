using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Empire_Earth_Mod.Properties;
using Empire_Earth_Mod_Lib;

namespace Empire_Earth_Mod
{
    public partial class MainForm : Form
    {
        
        public MainForm()
        {
            InitializeComponent();
            windowsVersionLabel.Text += WindowsVersion.GetCurrentWindowsVersion().GetDescription();
        }

        private void createModButton_Click(object sender, EventArgs e)
        {
            ModCreatorForm modCreatorForm;
            try
            {
                modCreatorForm = new ModCreatorForm();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // The working directory below %LOCALAPPDATA% could not be created (disk full, no rights...).
                MessageBox.Show(this, string.Format(CultureInfo.CurrentCulture, Resources.ModCreatorNotStartedFormat,
                        ex.Message), Resources.ModCreatorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // ShowDialog does not dispose the form; the using block releases it and its resources.
            using (modCreatorForm)
            {
                modCreatorForm.ShowDialog(this);
            }
        }
    }
}