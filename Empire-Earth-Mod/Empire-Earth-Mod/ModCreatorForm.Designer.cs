using System.ComponentModel;

namespace Empire_Earth_Mod
{
    partial class ModCreatorForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private IContainer components = null;

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
            this.wizardTabControl = new System.Windows.Forms.TabControl();
            this.generalTabPage = new System.Windows.Forms.TabPage();
            this.variantsKryptonDataGridView = new Krypton.Toolkit.KryptonDataGridView();
            this.variantNameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.variantIdColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.variantsKryptonLabel = new Krypton.Toolkit.KryptonLabel();
            this.removeVariantKryptonButton = new Krypton.Toolkit.KryptonButton();
            this.variantKryptonTextBox = new Krypton.Toolkit.KryptonTextBox();
            this.addVariantKryptonButton = new Krypton.Toolkit.KryptonButton();
            this.authorsKryptonLabel = new Krypton.Toolkit.KryptonLabel();
            this.authorsKryptonTextBox = new Krypton.Toolkit.KryptonTextBox();
            this.contactKryptonLabel = new Krypton.Toolkit.KryptonLabel();
            this.contactKryptonTextBox = new Krypton.Toolkit.KryptonTextBox();
            this.versionKryptonTextBox = new Krypton.Toolkit.KryptonTextBox();
            this.versionKryptonLabel = new Krypton.Toolkit.KryptonLabel();
            this.descriptionKryptonTextBox = new Krypton.Toolkit.KryptonTextBox();
            this.descriptionKryptonLabel = new Krypton.Toolkit.KryptonLabel();
            this.nameKryptonLabel = new Krypton.Toolkit.KryptonLabel();
            this.nameKryptonTextBox = new Krypton.Toolkit.KryptonTextBox();
            this.imagesTabPage = new System.Windows.Forms.TabPage();
            this.bannersKryptonGroupBox = new Krypton.Toolkit.KryptonGroupBox();
            this.bannerCounterKryptonLabel = new Krypton.Toolkit.KryptonLabel();
            this.removeBannerKryptonButton = new Krypton.Toolkit.KryptonButton();
            this.addBannerKryptonButton = new Krypton.Toolkit.KryptonButton();
            this.prevBannerKryptonButton = new Krypton.Toolkit.KryptonButton();
            this.nextBannerKryptonButton = new Krypton.Toolkit.KryptonButton();
            this.bannersPictureBox = new System.Windows.Forms.PictureBox();
            this.bannersVariantsKryptonComboBox = new Krypton.Toolkit.KryptonComboBox();
            this.selectIconKryptonButton = new Krypton.Toolkit.KryptonButton();
            this.iconKryptonLabel = new Krypton.Toolkit.KryptonLabel();
            this.iconPictureBox = new System.Windows.Forms.PictureBox();
            this.filesTabPage = new System.Windows.Forms.TabPage();
            this.updateVariantsFilesKryptonButton = new Krypton.Toolkit.KryptonButton();
            this.filesKryptonComboBox = new Krypton.Toolkit.KryptonComboBox();
            this.variantFilesKryptonDataGridView = new Krypton.Toolkit.KryptonDataGridView();
            this.infoColumn = new System.Windows.Forms.DataGridViewImageColumn();
            this.variantColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.fileNameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.productColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.fileTypeColumn = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.nextKryptonButton = new Krypton.Toolkit.KryptonButton();
            this.backKryptonButton = new Krypton.Toolkit.KryptonButton();
            this.buildTabPage = new System.Windows.Forms.TabPage();
            this.generalStepPictureBox = new System.Windows.Forms.PictureBox();
            this.imagesStepPictureBox = new System.Windows.Forms.PictureBox();
            this.filesStepPictureBox = new System.Windows.Forms.PictureBox();
            this.archiveStepPictureBox = new System.Windows.Forms.PictureBox();
            this.buildStatusLabel = new System.Windows.Forms.Label();
            this.generalStepLabel = new System.Windows.Forms.Label();
            this.imagesStepLabel = new System.Windows.Forms.Label();
            this.filesStepLabel = new System.Windows.Forms.Label();
            this.archiveStepLabel = new System.Windows.Forms.Label();
            this.wizardTabControl.SuspendLayout();
            this.generalTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.variantsKryptonDataGridView)).BeginInit();
            this.imagesTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bannersKryptonGroupBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bannersKryptonGroupBox.Panel)).BeginInit();
            this.bannersKryptonGroupBox.Panel.SuspendLayout();
            this.bannersKryptonGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bannersPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bannersVariantsKryptonComboBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.iconPictureBox)).BeginInit();
            this.filesTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.filesKryptonComboBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.variantFilesKryptonDataGridView)).BeginInit();
            this.buildTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.generalStepPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.imagesStepPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.filesStepPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.archiveStepPictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // wizardTabControl
            // 
            this.wizardTabControl.Controls.Add(this.generalTabPage);
            this.wizardTabControl.Controls.Add(this.imagesTabPage);
            this.wizardTabControl.Controls.Add(this.filesTabPage);
            this.wizardTabControl.Controls.Add(this.buildTabPage);
            this.wizardTabControl.Location = new System.Drawing.Point(12, 12);
            this.wizardTabControl.Name = "wizardTabControl";
            this.wizardTabControl.SelectedIndex = 0;
            this.wizardTabControl.Size = new System.Drawing.Size(645, 317);
            this.wizardTabControl.TabIndex = 0;
            // 
            // generalTabPage
            // 
            this.generalTabPage.Controls.Add(this.variantsKryptonDataGridView);
            this.generalTabPage.Controls.Add(this.variantsKryptonLabel);
            this.generalTabPage.Controls.Add(this.removeVariantKryptonButton);
            this.generalTabPage.Controls.Add(this.variantKryptonTextBox);
            this.generalTabPage.Controls.Add(this.addVariantKryptonButton);
            this.generalTabPage.Controls.Add(this.authorsKryptonLabel);
            this.generalTabPage.Controls.Add(this.authorsKryptonTextBox);
            this.generalTabPage.Controls.Add(this.contactKryptonLabel);
            this.generalTabPage.Controls.Add(this.contactKryptonTextBox);
            this.generalTabPage.Controls.Add(this.versionKryptonTextBox);
            this.generalTabPage.Controls.Add(this.versionKryptonLabel);
            this.generalTabPage.Controls.Add(this.descriptionKryptonTextBox);
            this.generalTabPage.Controls.Add(this.descriptionKryptonLabel);
            this.generalTabPage.Controls.Add(this.nameKryptonLabel);
            this.generalTabPage.Controls.Add(this.nameKryptonTextBox);
            this.generalTabPage.Location = new System.Drawing.Point(4, 22);
            this.generalTabPage.Name = "generalTabPage";
            this.generalTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.generalTabPage.Size = new System.Drawing.Size(637, 291);
            this.generalTabPage.TabIndex = 0;
            this.generalTabPage.Text = "General";
            this.generalTabPage.UseVisualStyleBackColor = true;
            // 
            // variantsKryptonDataGridView
            // 
            this.variantsKryptonDataGridView.AllowUserToAddRows = false;
            this.variantsKryptonDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.variantNameColumn, this.variantIdColumn });
            this.variantsKryptonDataGridView.Location = new System.Drawing.Point(279, 79);
            this.variantsKryptonDataGridView.MultiSelect = false;
            this.variantsKryptonDataGridView.Name = "variantsKryptonDataGridView";
            this.variantsKryptonDataGridView.RowHeadersVisible = false;
            this.variantsKryptonDataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.variantsKryptonDataGridView.Size = new System.Drawing.Size(326, 174);
            this.variantsKryptonDataGridView.TabIndex = 22;
            // 
            // variantNameColumn
            // 
            this.variantNameColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.variantNameColumn.HeaderText = "Name";
            this.variantNameColumn.Name = "variantNameColumn";
            // 
            // variantIdColumn
            // 
            this.variantIdColumn.HeaderText = "ID";
            this.variantIdColumn.Name = "variantIdColumn";
            this.variantIdColumn.ReadOnly = true;
            this.variantIdColumn.Width = 230;
            // 
            // variantsKryptonLabel
            // 
            this.variantsKryptonLabel.Location = new System.Drawing.Point(410, 27);
            this.variantsKryptonLabel.Name = "variantsKryptonLabel";
            this.variantsKryptonLabel.Size = new System.Drawing.Size(54, 20);
            this.variantsKryptonLabel.TabIndex = 21;
            this.variantsKryptonLabel.Values.Text = "Variants";
            // 
            // removeVariantKryptonButton
            // 
            this.removeVariantKryptonButton.Location = new System.Drawing.Point(508, 53);
            this.removeVariantKryptonButton.Name = "removeVariantKryptonButton";
            this.removeVariantKryptonButton.Size = new System.Drawing.Size(24, 20);
            this.removeVariantKryptonButton.TabIndex = 20;
            this.removeVariantKryptonButton.Values.Text = "-";
            this.removeVariantKryptonButton.Click += new System.EventHandler(this.removeVariantKryptonButton_Click);
            // 
            // variantKryptonTextBox
            // 
            this.variantKryptonTextBox.Location = new System.Drawing.Point(344, 53);
            this.variantKryptonTextBox.Name = "variantKryptonTextBox";
            this.variantKryptonTextBox.Size = new System.Drawing.Size(133, 23);
            this.variantKryptonTextBox.TabIndex = 19;
            // 
            // addVariantKryptonButton
            // 
            this.addVariantKryptonButton.Location = new System.Drawing.Point(483, 53);
            this.addVariantKryptonButton.Name = "addVariantKryptonButton";
            this.addVariantKryptonButton.Size = new System.Drawing.Size(24, 20);
            this.addVariantKryptonButton.TabIndex = 18;
            this.addVariantKryptonButton.Values.Text = "+";
            this.addVariantKryptonButton.Click += new System.EventHandler(this.addVariantKryptonButton_Click);
            // 
            // authorsKryptonLabel
            // 
            this.authorsKryptonLabel.Location = new System.Drawing.Point(34, 246);
            this.authorsKryptonLabel.Name = "authorsKryptonLabel";
            this.authorsKryptonLabel.Size = new System.Drawing.Size(53, 20);
            this.authorsKryptonLabel.TabIndex = 10;
            this.authorsKryptonLabel.Values.Text = "Authors";
            // 
            // authorsKryptonTextBox
            // 
            this.authorsKryptonTextBox.Location = new System.Drawing.Point(91, 243);
            this.authorsKryptonTextBox.Name = "authorsKryptonTextBox";
            this.authorsKryptonTextBox.Size = new System.Drawing.Size(125, 23);
            this.authorsKryptonTextBox.TabIndex = 9;
            this.authorsKryptonTextBox.Text = "author name";
            // 
            // contactKryptonLabel
            // 
            this.contactKryptonLabel.Location = new System.Drawing.Point(34, 217);
            this.contactKryptonLabel.Name = "contactKryptonLabel";
            this.contactKryptonLabel.Size = new System.Drawing.Size(53, 20);
            this.contactKryptonLabel.TabIndex = 8;
            this.contactKryptonLabel.Values.Text = "Contact";
            // 
            // contactKryptonTextBox
            // 
            this.contactKryptonTextBox.Location = new System.Drawing.Point(91, 214);
            this.contactKryptonTextBox.Name = "contactKryptonTextBox";
            this.contactKryptonTextBox.Size = new System.Drawing.Size(125, 23);
            this.contactKryptonTextBox.TabIndex = 7;
            // 
            // versionKryptonTextBox
            // 
            this.versionKryptonTextBox.Location = new System.Drawing.Point(91, 185);
            this.versionKryptonTextBox.Name = "versionKryptonTextBox";
            this.versionKryptonTextBox.Size = new System.Drawing.Size(125, 23);
            this.versionKryptonTextBox.TabIndex = 6;
            this.versionKryptonTextBox.Text = "1.0.0.0";
            // 
            // versionKryptonLabel
            // 
            this.versionKryptonLabel.Location = new System.Drawing.Point(34, 188);
            this.versionKryptonLabel.Name = "versionKryptonLabel";
            this.versionKryptonLabel.Size = new System.Drawing.Size(51, 20);
            this.versionKryptonLabel.TabIndex = 5;
            this.versionKryptonLabel.Values.Text = "Version";
            // 
            // descriptionKryptonTextBox
            // 
            this.descriptionKryptonTextBox.Location = new System.Drawing.Point(34, 79);
            this.descriptionKryptonTextBox.Multiline = true;
            this.descriptionKryptonTextBox.Name = "descriptionKryptonTextBox";
            this.descriptionKryptonTextBox.Size = new System.Drawing.Size(182, 100);
            this.descriptionKryptonTextBox.TabIndex = 4;
            // 
            // descriptionKryptonLabel
            // 
            this.descriptionKryptonLabel.Location = new System.Drawing.Point(34, 53);
            this.descriptionKryptonLabel.Name = "descriptionKryptonLabel";
            this.descriptionKryptonLabel.Size = new System.Drawing.Size(73, 20);
            this.descriptionKryptonLabel.TabIndex = 3;
            this.descriptionKryptonLabel.Values.Text = "Description";
            // 
            // nameKryptonLabel
            // 
            this.nameKryptonLabel.Location = new System.Drawing.Point(34, 24);
            this.nameKryptonLabel.Name = "nameKryptonLabel";
            this.nameKryptonLabel.Size = new System.Drawing.Size(43, 20);
            this.nameKryptonLabel.TabIndex = 1;
            this.nameKryptonLabel.Values.Text = "Name";
            // 
            // nameKryptonTextBox
            // 
            this.nameKryptonTextBox.Location = new System.Drawing.Point(83, 21);
            this.nameKryptonTextBox.Name = "nameKryptonTextBox";
            this.nameKryptonTextBox.Size = new System.Drawing.Size(133, 23);
            this.nameKryptonTextBox.TabIndex = 0;
            // 
            // imagesTabPage
            // 
            this.imagesTabPage.Controls.Add(this.bannersKryptonGroupBox);
            this.imagesTabPage.Controls.Add(this.selectIconKryptonButton);
            this.imagesTabPage.Controls.Add(this.iconKryptonLabel);
            this.imagesTabPage.Controls.Add(this.iconPictureBox);
            this.imagesTabPage.Location = new System.Drawing.Point(4, 22);
            this.imagesTabPage.Name = "imagesTabPage";
            this.imagesTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.imagesTabPage.Size = new System.Drawing.Size(637, 291);
            this.imagesTabPage.TabIndex = 1;
            this.imagesTabPage.Text = "Icon & Banners";
            this.imagesTabPage.UseVisualStyleBackColor = true;
            // 
            // bannersKryptonGroupBox
            // 
            this.bannersKryptonGroupBox.Location = new System.Drawing.Point(198, 20);
            this.bannersKryptonGroupBox.Name = "bannersKryptonGroupBox";
            // 
            // bannersKryptonGroupBox.Panel
            // 
            this.bannersKryptonGroupBox.Panel.Controls.Add(this.bannerCounterKryptonLabel);
            this.bannersKryptonGroupBox.Panel.Controls.Add(this.removeBannerKryptonButton);
            this.bannersKryptonGroupBox.Panel.Controls.Add(this.addBannerKryptonButton);
            this.bannersKryptonGroupBox.Panel.Controls.Add(this.prevBannerKryptonButton);
            this.bannersKryptonGroupBox.Panel.Controls.Add(this.nextBannerKryptonButton);
            this.bannersKryptonGroupBox.Panel.Controls.Add(this.bannersPictureBox);
            this.bannersKryptonGroupBox.Panel.Controls.Add(this.bannersVariantsKryptonComboBox);
            this.bannersKryptonGroupBox.Size = new System.Drawing.Size(397, 247);
            this.bannersKryptonGroupBox.TabIndex = 28;
            this.bannersKryptonGroupBox.Values.Heading = "Banners";
            // 
            // bannerCounterKryptonLabel
            // 
            this.bannerCounterKryptonLabel.AutoSize = false;
            this.bannerCounterKryptonLabel.Location = new System.Drawing.Point(85, 35);
            this.bannerCounterKryptonLabel.Name = "bannerCounterKryptonLabel";
            this.bannerCounterKryptonLabel.Size = new System.Drawing.Size(222, 20);
            this.bannerCounterKryptonLabel.StateCommon.LongText.TextH = Krypton.Toolkit.PaletteRelativeAlign.Center;
            this.bannerCounterKryptonLabel.StateCommon.ShortText.TextH = Krypton.Toolkit.PaletteRelativeAlign.Center;
            this.bannerCounterKryptonLabel.TabIndex = 42;
            this.bannerCounterKryptonLabel.Values.Text = "Banner";
            // 
            // removeBannerKryptonButton
            // 
            this.removeBannerKryptonButton.Location = new System.Drawing.Point(85, 182);
            this.removeBannerKryptonButton.Name = "removeBannerKryptonButton";
            this.removeBannerKryptonButton.Size = new System.Drawing.Size(75, 23);
            this.removeBannerKryptonButton.TabIndex = 41;
            this.removeBannerKryptonButton.Values.Text = "Remove";
            this.removeBannerKryptonButton.Click += new System.EventHandler(this.removeBannerKryptonButton_Click);
            // 
            // addBannerKryptonButton
            // 
            this.addBannerKryptonButton.Location = new System.Drawing.Point(232, 182);
            this.addBannerKryptonButton.Name = "addBannerKryptonButton";
            this.addBannerKryptonButton.Size = new System.Drawing.Size(75, 23);
            this.addBannerKryptonButton.TabIndex = 40;
            this.addBannerKryptonButton.Values.Text = "Add new";
            this.addBannerKryptonButton.Click += new System.EventHandler(this.addBannerKryptonButton_Click);
            // 
            // prevBannerKryptonButton
            // 
            this.prevBannerKryptonButton.Location = new System.Drawing.Point(73, 99);
            this.prevBannerKryptonButton.Name = "prevBannerKryptonButton";
            this.prevBannerKryptonButton.Size = new System.Drawing.Size(16, 41);
            this.prevBannerKryptonButton.TabIndex = 39;
            this.prevBannerKryptonButton.Values.Text = "<";
            this.prevBannerKryptonButton.Click += new System.EventHandler(this.prevBannerKryptonButton_Click);
            // 
            // nextBannerKryptonButton
            // 
            this.nextBannerKryptonButton.Location = new System.Drawing.Point(304, 99);
            this.nextBannerKryptonButton.Name = "nextBannerKryptonButton";
            this.nextBannerKryptonButton.Size = new System.Drawing.Size(16, 41);
            this.nextBannerKryptonButton.TabIndex = 37;
            this.nextBannerKryptonButton.Values.Text = ">";
            this.nextBannerKryptonButton.Click += new System.EventHandler(this.nextBannerKryptonButton_Click);
            // 
            // bannersPictureBox
            // 
            this.bannersPictureBox.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.bannersPictureBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.bannersPictureBox.Location = new System.Drawing.Point(85, 56);
            this.bannersPictureBox.Name = "bannersPictureBox";
            this.bannersPictureBox.Size = new System.Drawing.Size(222, 129);
            this.bannersPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.bannersPictureBox.TabIndex = 38;
            this.bannersPictureBox.TabStop = false;
            // 
            // bannersVariantsKryptonComboBox
            // 
            this.bannersVariantsKryptonComboBox.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawVariable;
            this.bannersVariantsKryptonComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.bannersVariantsKryptonComboBox.DropDownWidth = 121;
            this.bannersVariantsKryptonComboBox.IntegralHeight = false;
            this.bannersVariantsKryptonComboBox.Location = new System.Drawing.Point(138, 10);
            this.bannersVariantsKryptonComboBox.Name = "bannersVariantsKryptonComboBox";
            this.bannersVariantsKryptonComboBox.Size = new System.Drawing.Size(121, 21);
            this.bannersVariantsKryptonComboBox.TabIndex = 36;
            this.bannersVariantsKryptonComboBox.SelectedIndexChanged += new System.EventHandler(this.bannersVariantsKryptonComboBox_SelectedIndexChanged);
            // 
            // selectIconKryptonButton
            // 
            this.selectIconKryptonButton.Location = new System.Drawing.Point(58, 184);
            this.selectIconKryptonButton.Name = "selectIconKryptonButton";
            this.selectIconKryptonButton.Size = new System.Drawing.Size(75, 23);
            this.selectIconKryptonButton.TabIndex = 24;
            this.selectIconKryptonButton.Values.Text = "Select";
            this.selectIconKryptonButton.Click += new System.EventHandler(this.selectIconKryptonButton_Click);
            // 
            // iconKryptonLabel
            // 
            this.iconKryptonLabel.AutoSize = false;
            this.iconKryptonLabel.Location = new System.Drawing.Point(45, 52);
            this.iconKryptonLabel.Name = "iconKryptonLabel";
            this.iconKryptonLabel.Size = new System.Drawing.Size(100, 20);
            this.iconKryptonLabel.StateCommon.ShortText.TextH = Krypton.Toolkit.PaletteRelativeAlign.Center;
            this.iconKryptonLabel.TabIndex = 26;
            this.iconKryptonLabel.Values.Text = "Icon";
            // 
            // iconPictureBox
            // 
            this.iconPictureBox.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.iconPictureBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.iconPictureBox.Location = new System.Drawing.Point(45, 78);
            this.iconPictureBox.Name = "iconPictureBox";
            this.iconPictureBox.Size = new System.Drawing.Size(100, 100);
            this.iconPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.iconPictureBox.TabIndex = 25;
            this.iconPictureBox.TabStop = false;
            // 
            // filesTabPage
            // 
            this.filesTabPage.Controls.Add(this.updateVariantsFilesKryptonButton);
            this.filesTabPage.Controls.Add(this.filesKryptonComboBox);
            this.filesTabPage.Controls.Add(this.variantFilesKryptonDataGridView);
            this.filesTabPage.Location = new System.Drawing.Point(4, 22);
            this.filesTabPage.Name = "filesTabPage";
            this.filesTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.filesTabPage.Size = new System.Drawing.Size(637, 291);
            this.filesTabPage.TabIndex = 2;
            this.filesTabPage.Text = "Files";
            this.filesTabPage.UseVisualStyleBackColor = true;
            // 
            // updateVariantsFilesKryptonButton
            // 
            this.updateVariantsFilesKryptonButton.Location = new System.Drawing.Point(301, 15);
            this.updateVariantsFilesKryptonButton.Name = "updateVariantsFilesKryptonButton";
            this.updateVariantsFilesKryptonButton.Size = new System.Drawing.Size(144, 21);
            this.updateVariantsFilesKryptonButton.TabIndex = 46;
            this.updateVariantsFilesKryptonButton.Values.Text = "Update variants files";
            this.updateVariantsFilesKryptonButton.Click += new System.EventHandler(this.updateVariantsFilesKryptonButton_Click);
            // 
            // filesKryptonComboBox
            // 
            this.filesKryptonComboBox.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawVariable;
            this.filesKryptonComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.filesKryptonComboBox.DropDownWidth = 121;
            this.filesKryptonComboBox.IntegralHeight = false;
            this.filesKryptonComboBox.Location = new System.Drawing.Point(174, 15);
            this.filesKryptonComboBox.Name = "filesKryptonComboBox";
            this.filesKryptonComboBox.Size = new System.Drawing.Size(121, 21);
            this.filesKryptonComboBox.TabIndex = 45;
            this.filesKryptonComboBox.SelectedIndexChanged += new System.EventHandler(this.filesKryptonComboBox_SelectedIndexChanged);
            // 
            // variantFilesKryptonDataGridView
            // 
            this.variantFilesKryptonDataGridView.AllowUserToAddRows = false;
            this.variantFilesKryptonDataGridView.AllowUserToDeleteRows = false;
            this.variantFilesKryptonDataGridView.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.variantFilesKryptonDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.infoColumn, this.variantColumn, this.fileNameColumn, this.productColumn, this.fileTypeColumn });
            this.variantFilesKryptonDataGridView.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.variantFilesKryptonDataGridView.GridStyles.Style = Krypton.Toolkit.DataGridViewStyle.Sheet;
            this.variantFilesKryptonDataGridView.GridStyles.StyleBackground = Krypton.Toolkit.PaletteBackStyle.GridBackgroundSheet;
            this.variantFilesKryptonDataGridView.GridStyles.StyleColumn = Krypton.Toolkit.GridStyle.Sheet;
            this.variantFilesKryptonDataGridView.GridStyles.StyleDataCells = Krypton.Toolkit.GridStyle.Sheet;
            this.variantFilesKryptonDataGridView.GridStyles.StyleRow = Krypton.Toolkit.GridStyle.Sheet;
            this.variantFilesKryptonDataGridView.Location = new System.Drawing.Point(12, 42);
            this.variantFilesKryptonDataGridView.MultiSelect = false;
            this.variantFilesKryptonDataGridView.Name = "variantFilesKryptonDataGridView";
            this.variantFilesKryptonDataGridView.RowHeadersVisible = false;
            this.variantFilesKryptonDataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.variantFilesKryptonDataGridView.Size = new System.Drawing.Size(612, 243);
            this.variantFilesKryptonDataGridView.TabIndex = 40;
            this.variantFilesKryptonDataGridView.CellEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.variantFilesKryptonDataGridView_CellEnter);
            // 
            // infoColumn
            // 
            this.infoColumn.HeaderText = "Info";
            this.infoColumn.Name = "infoColumn";
            this.infoColumn.ReadOnly = true;
            this.infoColumn.Width = 50;
            // 
            // variantColumn
            // 
            this.variantColumn.HeaderText = "Variant";
            this.variantColumn.Name = "variantColumn";
            this.variantColumn.ReadOnly = true;
            this.variantColumn.Visible = false;
            this.variantColumn.Width = 5;
            // 
            // fileNameColumn
            // 
            this.fileNameColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.fileNameColumn.HeaderText = "File Name";
            this.fileNameColumn.Name = "fileNameColumn";
            this.fileNameColumn.ReadOnly = true;
            // 
            // productColumn
            // 
            this.productColumn.HeaderText = "Product";
            this.productColumn.Name = "productColumn";
            this.productColumn.ReadOnly = true;
            this.productColumn.Width = 75;
            // 
            // fileTypeColumn
            // 
            this.fileTypeColumn.HeaderText = "Type";
            this.fileTypeColumn.Name = "fileTypeColumn";
            // 
            // nextKryptonButton
            // 
            this.nextKryptonButton.Location = new System.Drawing.Point(559, 335);
            this.nextKryptonButton.Name = "nextKryptonButton";
            this.nextKryptonButton.Size = new System.Drawing.Size(94, 23);
            this.nextKryptonButton.TabIndex = 0;
            this.nextKryptonButton.Values.Text = "Next >";
            this.nextKryptonButton.Click += new System.EventHandler(this.nextKryptonButton_Click);
            // 
            // backKryptonButton
            // 
            this.backKryptonButton.Location = new System.Drawing.Point(16, 335);
            this.backKryptonButton.Name = "backKryptonButton";
            this.backKryptonButton.Size = new System.Drawing.Size(94, 23);
            this.backKryptonButton.TabIndex = 1;
            this.backKryptonButton.Values.Text = "< Back";
            this.backKryptonButton.Visible = false;
            this.backKryptonButton.Click += new System.EventHandler(this.backKryptonButton_Click);
            // 
            // buildTabPage
            // 
            this.buildTabPage.Controls.Add(this.archiveStepLabel);
            this.buildTabPage.Controls.Add(this.filesStepLabel);
            this.buildTabPage.Controls.Add(this.imagesStepLabel);
            this.buildTabPage.Controls.Add(this.generalStepLabel);
            this.buildTabPage.Controls.Add(this.buildStatusLabel);
            this.buildTabPage.Controls.Add(this.archiveStepPictureBox);
            this.buildTabPage.Controls.Add(this.filesStepPictureBox);
            this.buildTabPage.Controls.Add(this.imagesStepPictureBox);
            this.buildTabPage.Controls.Add(this.generalStepPictureBox);
            this.buildTabPage.Location = new System.Drawing.Point(4, 22);
            this.buildTabPage.Name = "buildTabPage";
            this.buildTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.buildTabPage.Size = new System.Drawing.Size(637, 291);
            this.buildTabPage.TabIndex = 3;
            this.buildTabPage.Text = "Build";
            this.buildTabPage.UseVisualStyleBackColor = true;
            // 
            // generalStepPictureBox
            // 
            this.generalStepPictureBox.Location = new System.Drawing.Point(147, 68);
            this.generalStepPictureBox.Name = "generalStepPictureBox";
            this.generalStepPictureBox.Size = new System.Drawing.Size(35, 35);
            this.generalStepPictureBox.TabIndex = 0;
            this.generalStepPictureBox.TabStop = false;
            // 
            // imagesStepPictureBox
            // 
            this.imagesStepPictureBox.Location = new System.Drawing.Point(147, 109);
            this.imagesStepPictureBox.Name = "imagesStepPictureBox";
            this.imagesStepPictureBox.Size = new System.Drawing.Size(35, 35);
            this.imagesStepPictureBox.TabIndex = 1;
            this.imagesStepPictureBox.TabStop = false;
            // 
            // filesStepPictureBox
            // 
            this.filesStepPictureBox.Location = new System.Drawing.Point(147, 150);
            this.filesStepPictureBox.Name = "filesStepPictureBox";
            this.filesStepPictureBox.Size = new System.Drawing.Size(35, 35);
            this.filesStepPictureBox.TabIndex = 2;
            this.filesStepPictureBox.TabStop = false;
            // 
            // archiveStepPictureBox
            // 
            this.archiveStepPictureBox.Location = new System.Drawing.Point(147, 191);
            this.archiveStepPictureBox.Name = "archiveStepPictureBox";
            this.archiveStepPictureBox.Size = new System.Drawing.Size(35, 35);
            this.archiveStepPictureBox.TabIndex = 3;
            this.archiveStepPictureBox.TabStop = false;
            // 
            // buildStatusLabel
            // 
            this.buildStatusLabel.AutoSize = true;
            this.buildStatusLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buildStatusLabel.Location = new System.Drawing.Point(230, 13);
            this.buildStatusLabel.Name = "buildStatusLabel";
            this.buildStatusLabel.Size = new System.Drawing.Size(151, 24);
            this.buildStatusLabel.TabIndex = 4;
            this.buildStatusLabel.Text = "Building Mod...";
            // 
            // generalStepLabel
            // 
            this.generalStepLabel.Location = new System.Drawing.Point(188, 80);
            this.generalStepLabel.Name = "generalStepLabel";
            this.generalStepLabel.Size = new System.Drawing.Size(100, 23);
            this.generalStepLabel.TabIndex = 5;
            this.generalStepLabel.Text = "Basic Information";
            // 
            // imagesStepLabel
            // 
            this.imagesStepLabel.Location = new System.Drawing.Point(188, 121);
            this.imagesStepLabel.Name = "imagesStepLabel";
            this.imagesStepLabel.Size = new System.Drawing.Size(100, 23);
            this.imagesStepLabel.TabIndex = 6;
            this.imagesStepLabel.Text = "Icon and Banners";
            // 
            // filesStepLabel
            // 
            this.filesStepLabel.Location = new System.Drawing.Point(188, 162);
            this.filesStepLabel.Name = "filesStepLabel";
            this.filesStepLabel.Size = new System.Drawing.Size(100, 23);
            this.filesStepLabel.TabIndex = 7;
            this.filesStepLabel.Text = "File Information";
            // 
            // archiveStepLabel
            // 
            this.archiveStepLabel.Location = new System.Drawing.Point(188, 203);
            this.archiveStepLabel.Name = "archiveStepLabel";
            this.archiveStepLabel.Size = new System.Drawing.Size(100, 23);
            this.archiveStepLabel.TabIndex = 8;
            this.archiveStepLabel.Text = "Creating mod file";
            // 
            // ModCreatorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(669, 366);
            this.Controls.Add(this.backKryptonButton);
            this.Controls.Add(this.nextKryptonButton);
            this.Controls.Add(this.wizardTabControl);
            this.Location = new System.Drawing.Point(15, 15);
            this.Name = "ModCreatorForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.wizardTabControl.ResumeLayout(false);
            this.generalTabPage.ResumeLayout(false);
            this.generalTabPage.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.variantsKryptonDataGridView)).EndInit();
            this.imagesTabPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.bannersKryptonGroupBox.Panel)).EndInit();
            this.bannersKryptonGroupBox.Panel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.bannersKryptonGroupBox)).EndInit();
            this.bannersKryptonGroupBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.bannersPictureBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bannersVariantsKryptonComboBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.iconPictureBox)).EndInit();
            this.filesTabPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.filesKryptonComboBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.variantFilesKryptonDataGridView)).EndInit();
            this.buildTabPage.ResumeLayout(false);
            this.buildTabPage.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.generalStepPictureBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.imagesStepPictureBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.filesStepPictureBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.archiveStepPictureBox)).EndInit();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Label generalStepLabel;
        private System.Windows.Forms.Label imagesStepLabel;
        private System.Windows.Forms.Label filesStepLabel;
        private System.Windows.Forms.Label archiveStepLabel;

        private System.Windows.Forms.PictureBox imagesStepPictureBox;
        private System.Windows.Forms.PictureBox filesStepPictureBox;
        private System.Windows.Forms.PictureBox archiveStepPictureBox;
        private System.Windows.Forms.Label buildStatusLabel;

        private System.Windows.Forms.TabPage buildTabPage;
        private System.Windows.Forms.PictureBox generalStepPictureBox;

        private System.Windows.Forms.DataGridViewTextBoxColumn variantColumn;

        private System.Windows.Forms.DataGridViewComboBoxColumn fileTypeColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn productColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn fileNameColumn;
        private System.Windows.Forms.DataGridViewImageColumn infoColumn;

        private Krypton.Toolkit.KryptonButton updateVariantsFilesKryptonButton;

        private Krypton.Toolkit.KryptonComboBox filesKryptonComboBox;
        private Krypton.Toolkit.KryptonDataGridView variantFilesKryptonDataGridView;

        private Krypton.Toolkit.KryptonButton backKryptonButton;

        private Krypton.Toolkit.KryptonLabel bannerCounterKryptonLabel;

        private System.Windows.Forms.DataGridViewTextBoxColumn variantNameColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn variantIdColumn;

        private Krypton.Toolkit.KryptonGroupBox bannersKryptonGroupBox;
        private Krypton.Toolkit.KryptonComboBox bannersVariantsKryptonComboBox;

        private Krypton.Toolkit.KryptonDataGridView variantsKryptonDataGridView;

        private Krypton.Toolkit.KryptonLabel variantsKryptonLabel;

        private Krypton.Toolkit.KryptonButton removeVariantKryptonButton;

        private Krypton.Toolkit.KryptonTextBox variantKryptonTextBox;

        private Krypton.Toolkit.KryptonButton addVariantKryptonButton;

        private Krypton.Toolkit.KryptonButton addBannerKryptonButton;
        private Krypton.Toolkit.KryptonButton removeBannerKryptonButton;

        private Krypton.Toolkit.KryptonButton nextBannerKryptonButton;
        private Krypton.Toolkit.KryptonButton prevBannerKryptonButton;
        private Krypton.Toolkit.KryptonButton selectIconKryptonButton;

        private Krypton.Toolkit.KryptonLabel iconKryptonLabel;
        private System.Windows.Forms.PictureBox bannersPictureBox;

        private System.Windows.Forms.PictureBox iconPictureBox;

        private Krypton.Toolkit.KryptonLabel authorsKryptonLabel;
        private Krypton.Toolkit.KryptonTextBox authorsKryptonTextBox;

        private Krypton.Toolkit.KryptonLabel contactKryptonLabel;
        private Krypton.Toolkit.KryptonTextBox contactKryptonTextBox;

        private Krypton.Toolkit.KryptonLabel versionKryptonLabel;
        private Krypton.Toolkit.KryptonTextBox versionKryptonTextBox;

        private Krypton.Toolkit.KryptonTextBox descriptionKryptonTextBox;

        private Krypton.Toolkit.KryptonButton nextKryptonButton;
        private Krypton.Toolkit.KryptonTextBox nameKryptonTextBox;
        private Krypton.Toolkit.KryptonLabel nameKryptonLabel;
        private Krypton.Toolkit.KryptonLabel descriptionKryptonLabel;

        private System.Windows.Forms.TabPage filesTabPage;

        private System.Windows.Forms.TabControl wizardTabControl;
        private System.Windows.Forms.TabPage generalTabPage;
        private System.Windows.Forms.TabPage imagesTabPage;

        #endregion
    }
}