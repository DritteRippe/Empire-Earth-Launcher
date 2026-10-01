using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Empire_Earth_Launcher
{
    public partial class LauncherSettingsUserControl : UserControl
    {
        private IThemeService themeService;

        public LauncherSettingsUserControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Passes the services in. The control is created by the designer, which needs a parameterless
        /// constructor, so its owner calls this right after InitializeComponent.
        /// </summary>
        public void Initialize(IThemeService themeService)
        {
            if (themeService == null)
                throw new ArgumentNullException(nameof(themeService));
            this.themeService = themeService;
            themeService.Register(launcherKryptonPalette, this);

            LoadAvailableThemes();
        }

        private void LoadAvailableThemes()
        {
            foreach (string themeName in themeService.GetAvailableThemeNames())
                themeKryptonComboBox.Items.Add(themeName);
        }

        private void themeKryptonComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selected_theme_name = (string) themeKryptonComboBox.SelectedItem;

            if (themeKryptonComboBox.SelectedIndex > 0)
                themeService.ApplyTheme(selected_theme_name);
            else
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Theme File (*.xml)|*.xml|All files (*.*)|*.*";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    if (openFileDialog.FileName == null)
                        return;
                    themeService.ApplyThemeFile(openFileDialog.FileName);
                }
            }

        }
    }
}
