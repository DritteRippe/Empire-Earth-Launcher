
namespace Empire_Earth_Launcher
{

    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.launcherKryptonPalette = new Krypton.Toolkit.KryptonPalette(this.components);
            this.playKryptonCheckButton = new Krypton.Toolkit.KryptonCheckButton();
            this.settingsKryptonCheckButton = new Krypton.Toolkit.KryptonCheckButton();
            this.launcherKryptonCheckButton = new Krypton.Toolkit.KryptonCheckButton();
            this.generalUserControl = new Empire_Earth_Launcher.GeneralUserControl();
            this.launcherSettingsUserControl = new Empire_Earth_Launcher.LauncherSettingsUserControl();
            this.settingsUserControl = new Empire_Earth_Launcher.SettingsUserControl();
            this.SuspendLayout();
            // 
            // launcherKryptonPalette
            // 
            this.launcherKryptonPalette.ButtonStyles.ButtonInputControl.StateCommon.Back.Image = ((System.Drawing.Image)(resources.GetObject("resource.Image")));
            this.launcherKryptonPalette.ButtonStyles.ButtonInputControl.StateCommon.Back.ImageStyle = Krypton.Toolkit.PaletteImageStyle.Stretch;
            this.launcherKryptonPalette.ButtonStyles.ButtonInputControl.StatePressed.Back.Image = ((System.Drawing.Image)(resources.GetObject("resource.Image1")));
            this.launcherKryptonPalette.ButtonStyles.ButtonInputControl.StatePressed.Back.ImageStyle = Krypton.Toolkit.PaletteImageStyle.Stretch;
            this.launcherKryptonPalette.ButtonStyles.ButtonListItem.StateCommon.Content.LongText.Color1 = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.launcherKryptonPalette.ButtonStyles.ButtonListItem.StateCommon.Content.LongText.ColorStyle = Krypton.Toolkit.PaletteColorStyle.Solid;
            this.launcherKryptonPalette.ButtonStyles.ButtonListItem.StateCommon.Content.ShortText.Color1 = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.launcherKryptonPalette.ButtonStyles.ButtonListItem.StateCommon.Content.ShortText.ColorStyle = Krypton.Toolkit.PaletteColorStyle.Solid;
            this.launcherKryptonPalette.ButtonStyles.ButtonListItem.StateTracking.Back.Color1 = System.Drawing.Color.White;
            this.launcherKryptonPalette.ButtonStyles.ButtonListItem.StateTracking.Back.ColorStyle = Krypton.Toolkit.PaletteColorStyle.Solid;
            this.launcherKryptonPalette.ButtonStyles.ButtonListItem.StateTracking.Border.DrawBorders = ((Krypton.Toolkit.PaletteDrawBorders)((((Krypton.Toolkit.PaletteDrawBorders.Top | Krypton.Toolkit.PaletteDrawBorders.Bottom) 
            | Krypton.Toolkit.PaletteDrawBorders.Left) 
            | Krypton.Toolkit.PaletteDrawBorders.Right)));
            this.launcherKryptonPalette.ButtonStyles.ButtonListItem.StateTracking.Border.Rounding = 0F;
            this.launcherKryptonPalette.ButtonStyles.ButtonStandalone.OverrideDefault.Back.Color1 = System.Drawing.Color.White;
            this.launcherKryptonPalette.ButtonStyles.ButtonStandalone.OverrideDefault.Back.Color2 = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(230)))), ((int)(((byte)(230)))));
            this.launcherKryptonPalette.ButtonStyles.ButtonStandalone.OverrideDefault.Back.ColorStyle = Krypton.Toolkit.PaletteColorStyle.Dashed;
            this.launcherKryptonPalette.ButtonStyles.ButtonStandalone.StateCommon.Back.Color1 = System.Drawing.Color.White;
            this.launcherKryptonPalette.ButtonStyles.ButtonStandalone.StateCommon.Back.Color2 = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(230)))), ((int)(((byte)(230)))));
            this.launcherKryptonPalette.ButtonStyles.ButtonStandalone.StateCommon.Back.ColorStyle = Krypton.Toolkit.PaletteColorStyle.Dashed;
            this.launcherKryptonPalette.ButtonStyles.ButtonStandalone.StateCommon.Content.LongText.Color1 = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.launcherKryptonPalette.ButtonStyles.ButtonStandalone.StateCommon.Content.ShortText.Color1 = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.launcherKryptonPalette.ControlStyles.ControlClient.StateCommon.Back.Color1 = System.Drawing.Color.White;
            this.launcherKryptonPalette.ControlStyles.ControlClient.StateCommon.Back.ColorStyle = Krypton.Toolkit.PaletteColorStyle.Solid;
            this.launcherKryptonPalette.ControlStyles.ControlGroupBox.StateCommon.Back.Color1 = System.Drawing.Color.White;
            this.launcherKryptonPalette.ControlStyles.ControlGroupBox.StateCommon.Back.ColorStyle = Krypton.Toolkit.PaletteColorStyle.Solid;
            this.launcherKryptonPalette.FormStyles.FormCommon.StateCommon.Back.Color1 = System.Drawing.Color.White;
            this.launcherKryptonPalette.FormStyles.FormCommon.StateCommon.Back.ColorStyle = Krypton.Toolkit.PaletteColorStyle.Solid;
            this.launcherKryptonPalette.FormStyles.FormCommon.StateCommon.Border.Rounding = 8F;
            this.launcherKryptonPalette.HeaderStyles.HeaderCommon.StateCommon.Back.Color1 = System.Drawing.Color.White;
            this.launcherKryptonPalette.HeaderStyles.HeaderCommon.StateCommon.Back.ColorStyle = Krypton.Toolkit.PaletteColorStyle.Solid;
            this.launcherKryptonPalette.HeaderStyles.HeaderForm.StateCommon.Content.LongText.Color1 = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.launcherKryptonPalette.HeaderStyles.HeaderForm.StateCommon.Content.ShortText.Color1 = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.launcherKryptonPalette.Images.CheckBox.CheckedDisabled = ((System.Drawing.Image)(resources.GetObject("resource.CheckedDisabled")));
            this.launcherKryptonPalette.Images.CheckBox.CheckedNormal = ((System.Drawing.Image)(resources.GetObject("resource.CheckedNormal")));
            this.launcherKryptonPalette.Images.CheckBox.CheckedPressed = ((System.Drawing.Image)(resources.GetObject("resource.CheckedPressed")));
            this.launcherKryptonPalette.Images.CheckBox.CheckedTracking = ((System.Drawing.Image)(resources.GetObject("resource.CheckedTracking")));
            this.launcherKryptonPalette.Images.CheckBox.Common = ((System.Drawing.Image)(resources.GetObject("resource.Common")));
            this.launcherKryptonPalette.Images.CheckBox.UncheckedDisabled = ((System.Drawing.Image)(resources.GetObject("resource.UncheckedDisabled")));
            this.launcherKryptonPalette.Images.CheckBox.UncheckedPressed = ((System.Drawing.Image)(resources.GetObject("resource.UncheckedPressed")));
            this.launcherKryptonPalette.Images.RadioButton.CheckedDisabled = ((System.Drawing.Image)(resources.GetObject("resource.CheckedDisabled1")));
            this.launcherKryptonPalette.Images.RadioButton.CheckedNormal = ((System.Drawing.Image)(resources.GetObject("resource.CheckedNormal1")));
            this.launcherKryptonPalette.Images.RadioButton.CheckedPressed = ((System.Drawing.Image)(resources.GetObject("resource.CheckedPressed1")));
            this.launcherKryptonPalette.Images.RadioButton.CheckedTracking = ((System.Drawing.Image)(resources.GetObject("resource.CheckedTracking1")));
            this.launcherKryptonPalette.Images.RadioButton.Common = ((System.Drawing.Image)(resources.GetObject("resource.Common1")));
            this.launcherKryptonPalette.Images.RadioButton.UncheckedDisabled = ((System.Drawing.Image)(resources.GetObject("resource.UncheckedDisabled1")));
            this.launcherKryptonPalette.Images.RadioButton.UncheckedPressed = ((System.Drawing.Image)(resources.GetObject("resource.UncheckedPressed1")));
            this.launcherKryptonPalette.InputControlStyles.InputControlStandalone.StateCommon.Back.Color1 = System.Drawing.Color.White;
            this.launcherKryptonPalette.InputControlStyles.InputControlStandalone.StateCommon.Back.ColorStyle = Krypton.Toolkit.PaletteColorStyle.Solid;
            this.launcherKryptonPalette.InputControlStyles.InputControlStandalone.StateCommon.Border.DrawBorders = ((Krypton.Toolkit.PaletteDrawBorders)((((Krypton.Toolkit.PaletteDrawBorders.Top | Krypton.Toolkit.PaletteDrawBorders.Bottom) 
            | Krypton.Toolkit.PaletteDrawBorders.Left) 
            | Krypton.Toolkit.PaletteDrawBorders.Right)));
            this.launcherKryptonPalette.InputControlStyles.InputControlStandalone.StateCommon.Border.Rounding = 4F;
            this.launcherKryptonPalette.InputControlStyles.InputControlStandalone.StateCommon.Content.LongText.Color1 = System.Drawing.Color.Black;
            this.launcherKryptonPalette.InputControlStyles.InputControlStandalone.StateCommon.Content.ShortText.Color1 = System.Drawing.Color.Black;
            this.launcherKryptonPalette.LabelStyles.LabelCommon.StateCommon.LongText.Color1 = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.launcherKryptonPalette.LabelStyles.LabelCommon.StateCommon.ShortText.Color1 = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.launcherKryptonPalette.PanelStyles.PanelCommon.StateCommon.Color1 = System.Drawing.Color.White;
            this.launcherKryptonPalette.PanelStyles.PanelCommon.StateCommon.ColorStyle = Krypton.Toolkit.PaletteColorStyle.Solid;
            // 
            // playKryptonCheckButton
            // 
            resources.ApplyResources(this.playKryptonCheckButton, "playKryptonCheckButton");
            this.playKryptonCheckButton.Name = "playKryptonCheckButton";
            this.playKryptonCheckButton.OverrideDefault.Border.DrawBorders = ((Krypton.Toolkit.PaletteDrawBorders)((((Krypton.Toolkit.PaletteDrawBorders.Top | Krypton.Toolkit.PaletteDrawBorders.Bottom) 
            | Krypton.Toolkit.PaletteDrawBorders.Left) 
            | Krypton.Toolkit.PaletteDrawBorders.Right)));
            this.playKryptonCheckButton.OverrideDefault.Border.Rounding = 0F;
            this.playKryptonCheckButton.Palette = this.launcherKryptonPalette;
            this.playKryptonCheckButton.PaletteMode = Krypton.Toolkit.PaletteMode.Custom;
            this.playKryptonCheckButton.StateCommon.Border.DrawBorders = ((Krypton.Toolkit.PaletteDrawBorders)((((Krypton.Toolkit.PaletteDrawBorders.Top | Krypton.Toolkit.PaletteDrawBorders.Bottom) 
            | Krypton.Toolkit.PaletteDrawBorders.Left) 
            | Krypton.Toolkit.PaletteDrawBorders.Right)));
            this.playKryptonCheckButton.StateCommon.Border.Rounding = 0F;
            this.playKryptonCheckButton.Values.Text = resources.GetString("playKryptonCheckButton.Values.Text");
            this.playKryptonCheckButton.Click += new System.EventHandler(this.navigationKryptonCheckButton_Click);
            // 
            // settingsKryptonCheckButton
            // 
            resources.ApplyResources(this.settingsKryptonCheckButton, "settingsKryptonCheckButton");
            this.settingsKryptonCheckButton.Name = "settingsKryptonCheckButton";
            this.settingsKryptonCheckButton.OverrideDefault.Border.DrawBorders = ((Krypton.Toolkit.PaletteDrawBorders)((((Krypton.Toolkit.PaletteDrawBorders.Top | Krypton.Toolkit.PaletteDrawBorders.Bottom) 
            | Krypton.Toolkit.PaletteDrawBorders.Left) 
            | Krypton.Toolkit.PaletteDrawBorders.Right)));
            this.settingsKryptonCheckButton.OverrideDefault.Border.Rounding = 0F;
            this.settingsKryptonCheckButton.Palette = this.launcherKryptonPalette;
            this.settingsKryptonCheckButton.PaletteMode = Krypton.Toolkit.PaletteMode.Custom;
            this.settingsKryptonCheckButton.StateCommon.Border.DrawBorders = ((Krypton.Toolkit.PaletteDrawBorders)((((Krypton.Toolkit.PaletteDrawBorders.Top | Krypton.Toolkit.PaletteDrawBorders.Bottom) 
            | Krypton.Toolkit.PaletteDrawBorders.Left) 
            | Krypton.Toolkit.PaletteDrawBorders.Right)));
            this.settingsKryptonCheckButton.StateCommon.Border.Rounding = 0F;
            this.settingsKryptonCheckButton.Values.Text = resources.GetString("settingsKryptonCheckButton.Values.Text");
            this.settingsKryptonCheckButton.Click += new System.EventHandler(this.navigationKryptonCheckButton_Click);
            // 
            // launcherKryptonCheckButton
            // 
            resources.ApplyResources(this.launcherKryptonCheckButton, "launcherKryptonCheckButton");
            this.launcherKryptonCheckButton.Name = "launcherKryptonCheckButton";
            this.launcherKryptonCheckButton.Palette = this.launcherKryptonPalette;
            this.launcherKryptonCheckButton.PaletteMode = Krypton.Toolkit.PaletteMode.Custom;
            this.launcherKryptonCheckButton.StateCommon.Border.DrawBorders = ((Krypton.Toolkit.PaletteDrawBorders)((((Krypton.Toolkit.PaletteDrawBorders.Top | Krypton.Toolkit.PaletteDrawBorders.Bottom) 
            | Krypton.Toolkit.PaletteDrawBorders.Left) 
            | Krypton.Toolkit.PaletteDrawBorders.Right)));
            this.launcherKryptonCheckButton.StateCommon.Border.Rounding = 0F;
            this.launcherKryptonCheckButton.Values.Text = resources.GetString("launcherKryptonCheckButton.Values.Text");
            this.launcherKryptonCheckButton.Click += new System.EventHandler(this.navigationKryptonCheckButton_Click);
            // 
            // generalUserControl
            // 
            this.generalUserControl.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.generalUserControl, "generalUserControl");
            this.generalUserControl.Name = "generalUserControl";
            // 
            // launcherSettingsUserControl
            // 
            this.launcherSettingsUserControl.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.launcherSettingsUserControl, "launcherSettingsUserControl");
            this.launcherSettingsUserControl.Name = "launcherSettingsUserControl";
            // 
            // settingsUserControl
            // 
            resources.ApplyResources(this.settingsUserControl, "settingsUserControl");
            this.settingsUserControl.Name = "settingsUserControl";
            // 
            // MainForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.Controls.Add(this.generalUserControl);
            this.Controls.Add(this.settingsUserControl);
            this.Controls.Add(this.launcherSettingsUserControl);
            this.Controls.Add(this.launcherKryptonCheckButton);
            this.Controls.Add(this.settingsKryptonCheckButton);
            this.Controls.Add(this.playKryptonCheckButton);
            this.Name = "MainForm";
            this.Palette = this.launcherKryptonPalette;
            this.PaletteMode = Krypton.Toolkit.PaletteMode.Custom;
            this.StateCommon.Border.DrawBorders = ((Krypton.Toolkit.PaletteDrawBorders)((((Krypton.Toolkit.PaletteDrawBorders.Top | Krypton.Toolkit.PaletteDrawBorders.Bottom) 
            | Krypton.Toolkit.PaletteDrawBorders.Left) 
            | Krypton.Toolkit.PaletteDrawBorders.Right)));
            this.ResumeLayout(false);

        }

        #endregion
        private Krypton.Toolkit.KryptonCheckButton playKryptonCheckButton;
        private Krypton.Toolkit.KryptonCheckButton settingsKryptonCheckButton;
        private Krypton.Toolkit.KryptonCheckButton launcherKryptonCheckButton;
        private Krypton.Toolkit.KryptonPalette launcherKryptonPalette;
        private LauncherSettingsUserControl launcherSettingsUserControl;
        private GeneralUserControl generalUserControl;
        private SettingsUserControl settingsUserControl;
    }
}

