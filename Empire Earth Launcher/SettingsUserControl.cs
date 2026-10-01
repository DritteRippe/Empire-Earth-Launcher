using System;
using System.Windows.Forms;
using Empire_Earth_Launcher.Properties;

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
            using (var dialog = new LauncherDialog(themeService, Resources.CompatibilityHintTitle,
                       Resources.CompatibilityHintMessage, MessageBoxButtons.OK))
            {
                dialog.ShowDialog(this);
            }
        }
    }
}
