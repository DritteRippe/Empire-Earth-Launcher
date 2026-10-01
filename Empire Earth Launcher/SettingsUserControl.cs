using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Empire_Earth_Launcher
{
    public partial class SettingsUserControl : UserControl
    {
        private IThemeService themeService;

        public SettingsUserControl()
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
        }

        private void compatibilityWarningConfirmationKryptonButton_Click(object sender, EventArgs e)
        {
            compatibilityWarningKryptonPanel.Visible = false;
            // ShowDialog does not dispose the form; the using block releases it (and its theme registration).
            using (var dialog = new LauncherDialog(themeService, "Simple Question Dialog",
                       "This a very basic question blabla\nanother line here wow", MessageBoxButtons.OK))
            {
                dialog.ShowDialog(this);
            }
        }
    }
}
