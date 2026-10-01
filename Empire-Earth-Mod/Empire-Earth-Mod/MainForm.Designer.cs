namespace Empire_Earth_Mod
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
            this.windowsVersionLabel = new System.Windows.Forms.Label();
            this.createModButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // windowsVersionLabel
            // 
            this.windowsVersionLabel.Location = new System.Drawing.Point(12, 9);
            this.windowsVersionLabel.Name = "windowsVersionLabel";
            this.windowsVersionLabel.Size = new System.Drawing.Size(212, 22);
            this.windowsVersionLabel.TabIndex = 0;
            this.windowsVersionLabel.Text = "You are using: ";
            // 
            // createModButton
            // 
            this.createModButton.Location = new System.Drawing.Point(176, 34);
            this.createModButton.Name = "createModButton";
            this.createModButton.Size = new System.Drawing.Size(75, 23);
            this.createModButton.TabIndex = 1;
            this.createModButton.Text = "Create Mod";
            this.createModButton.UseVisualStyleBackColor = true;
            this.createModButton.Click += new System.EventHandler(this.createModButton_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(263, 66);
            this.Controls.Add(this.createModButton);
            this.Controls.Add(this.windowsVersionLabel);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Empire Earth Mod";
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Button createModButton;

        private System.Windows.Forms.Label windowsVersionLabel;

        #endregion
    }
}