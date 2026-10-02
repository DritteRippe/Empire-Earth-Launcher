using Krypton.Toolkit;
using System;
using System.Drawing;
using System.Windows.Forms;
using Empire_Earth_Launcher.Properties;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// Message box in the style of the launcher. Show it with ShowDialog in a using block; the result is the
    /// DialogResult of the clicked button.
    /// </summary>
    public partial class LauncherDialog : KryptonForm
    {
        /// <param name="themeService">Theme of the launcher.</param>
        /// <param name="title">Text of the title bar.</param>
        /// <param name="message">Message, may contain line breaks.</param>
        /// <param name="buttons">Buttons to show: <see cref="MessageBoxButtons.OK"/> or
        /// <see cref="MessageBoxButtons.YesNo"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="buttons"/> is another combination.</exception>
        public LauncherDialog(IThemeService themeService, string title, string message, MessageBoxButtons buttons)
        {
            if (themeService == null)
                throw new ArgumentNullException(nameof(themeService));
            InitializeComponent();
            themeService.Register(launcherKryptonPalette, this);

            // All corners use the top left image, mirrored as needed.
            bottomRightCornerPictureBox.Image.RotateFlip(RotateFlipType.Rotate180FlipNone);
            topRightCornerPictureBox.Image.RotateFlip(RotateFlipType.RotateNoneFlipX);
            bottomLeftCornerPictureBox.Image.RotateFlip(RotateFlipType.Rotate180FlipX);

            ApplyTexts(title, message);
            ShowButtons(buttons);
        }

        /// <summary>
        /// Sets the texts of the dialog: title and message as given by the caller (already in the UI language), the
        /// buttons from the resources (ADR 0009). The designer texts are placeholders.
        /// </summary>
        private void ApplyTexts(string title, string message)
        {
            Text = title;
            titleKryptonLabel.Values.Text = title;
            messageKryptonLabel.Values.Text = message;
            okKryptonButton.Values.Text = Resources.DialogOk;
            yesKryptonButton.Values.Text = Resources.DialogYes;
            noKryptonButton.Values.Text = Resources.DialogNo;
        }

        /// <summary>
        /// Shows the buttons of <paramref name="buttons"/>; their DialogResult (set in the designer) ends the
        /// dialog. Enter and Escape choose the default and the cancel button.
        /// </summary>
        private void ShowButtons(MessageBoxButtons buttons)
        {
            switch (buttons)
            {
                case MessageBoxButtons.OK:
                    okKryptonButton.Visible = true;
                    yesKryptonButton.Visible = false;
                    noKryptonButton.Visible = false;
                    AcceptButton = okKryptonButton;
                    CancelButton = okKryptonButton;
                    break;
                case MessageBoxButtons.YesNo:
                    okKryptonButton.Visible = false;
                    yesKryptonButton.Visible = true;
                    noKryptonButton.Visible = true;
                    AcceptButton = yesKryptonButton;
                    CancelButton = noKryptonButton;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(buttons), buttons,
                        "The launcher dialog supports OK and YesNo.");
            }
        }
    }
}
