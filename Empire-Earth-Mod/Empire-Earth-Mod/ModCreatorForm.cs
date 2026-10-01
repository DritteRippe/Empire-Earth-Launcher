using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Empire_Earth_Mod_Lib;
using Krypton.Toolkit;

namespace Empire_Earth_Mod
{
    /// <summary>
    /// Wizard that creates a mod archive: general information and variants, icon and banners, files, build.
    /// </summary>
    public partial class ModCreatorForm : KryptonForm
    {
        /// <summary>Pages of the wizard; the values are the indexes of the tab pages.</summary>
        private enum WizardStep
        {
            General = 0,
            Images = 1,
            Files = 2,
            Build = 3
        }

        private readonly ModData mod;
        private readonly ModAssets assets;
        private ModPackageBuilder packageBuilder;
        private readonly BackgroundWorker buildWorker;
        private bool modBuilt;

        /// <summary>Index of the banner shown for the selected variant.</summary>
        private int bannerIndex;

        public ModCreatorForm()
        {
            InitializeComponent();
            mod = new ModData();
            assets = new ModAssets();
            // Unique working directory below %LOCALAPPDATA%, released in OnFormClosed.
            packageBuilder = new ModPackageBuilder(mod, assets);

            buildWorker = new BackgroundWorker();
            buildWorker.DoWork += buildWorker_DoWork;
            buildWorker.RunWorkerCompleted += buildWorker_RunWorkerCompleted;
            // While the mod is built in the background, the pages that edit it must not be reachable through
            // the tab headers.
            wizardTabControl.Selecting += (sender, e) =>
            {
                if (buildWorker.IsBusy && e.TabPageIndex != (int)WizardStep.Build)
                    e.Cancel = true;
            };
            // Keeps Back/Next right however a page is reached (buttons or tab headers).
            wizardTabControl.Selected += (sender, e) => UpdateNavigationButtons();

            fileTypeColumn.Items.AddRange(Enum.GetValues(typeof(ModFile.ModFileType)).Cast<ModFile.ModFileType>()
                .Select(fileType => (object)fileType.GetDescription()).ToArray());
            UpdateNavigationButtons();
        }

        private WizardStep CurrentStep
        {
            get { return (WizardStep)wizardTabControl.SelectedIndex; }
        }

        private void GoToStep(WizardStep step)
        {
            wizardTabControl.SelectedIndex = (int)step;
            UpdateNavigationButtons();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (buildWorker.IsBusy)
            {
                MessageBox.Show("Please wait until the mod has been built.", "Mod Creator",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                e.Cancel = true;
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            ReleasePackageBuilder();
        }

        /// <summary>
        /// Ends the creator session. The working directory is deleted when it holds no mod files, or when
        /// the author agrees; otherwise it is kept, so closing the window never loses copied files.
        /// </summary>
        private void ReleasePackageBuilder()
        {
            if (packageBuilder == null)
                return;

            string workingDir = packageBuilder.WorkingDirectory;
            try
            {
                bool delete = !packageBuilder.ContainsModFiles() || MessageBox.Show(
                    "Delete the working folder of this mod, including the files you copied into it?\n\n" +
                    workingDir + "\n\nChoose \"No\" to keep the folder.", "Mod Creator",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) == DialogResult.Yes;
                if (delete)
                    packageBuilder.DeleteWorkingDirectory();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show("The working folder could not be deleted:\n" + workingDir + "\n\n" + ex.Message,
                    "Mod Creator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                packageBuilder.Dispose();
                packageBuilder = null;
            }
        }

        /* Wizard navigation */

        private void nextKryptonButton_Click(object sender, EventArgs e)
        {
            switch (CurrentStep)
            {
                case WizardStep.General:
                    if (!CommitGeneralStep())
                        return;
                    EnterImagesStep();
                    break;
                case WizardStep.Images:
                    EnterFilesStep();
                    break;
                case WizardStep.Files:
                    if (!StartBuild())
                        return;
                    break;
                case WizardStep.Build:
                    // After a successful build the button reads "Close". The page can also be reached through
                    // its tab header without building; there is no next page then.
                    if (modBuilt)
                        Close();
                    return;
            }

            GoToStep(CurrentStep + 1);
        }

        private void backKryptonButton_Click(object sender, EventArgs e)
        {
            if (CurrentStep != WizardStep.General)
                GoToStep(CurrentStep - 1);
        }

        private void UpdateNavigationButtons()
        {
            bool building = buildWorker.IsBusy;
            WizardStep step = CurrentStep;
            backKryptonButton.Visible = step != WizardStep.General && !building && !(step == WizardStep.Build && modBuilt);

            switch (step)
            {
                case WizardStep.Files:
                    nextKryptonButton.Text = "Build >";
                    nextKryptonButton.Enabled = true;
                    break;
                case WizardStep.Build:
                    nextKryptonButton.Text = building ? "Building..." : "Close";
                    nextKryptonButton.Enabled = !building && modBuilt;
                    break;
                default:
                    nextKryptonButton.Text = "Next >";
                    nextKryptonButton.Enabled = true;
                    break;
            }
        }

        /* Step 1: general information and variants */

        /// <summary>
        /// Validates the first page and copies it into the mod.
        /// </summary>
        /// <returns>false, after telling the user why, if an input is invalid; the mod is not changed then.</returns>
        private bool CommitGeneralStep()
        {
            string name = nameKryptonTextBox.Text.Trim();
            if (name.Length == 0)
                return RejectInput("Please enter a name for the mod.", nameKryptonTextBox);

            Version version;
            if (!Version.TryParse(versionKryptonTextBox.Text.Trim(), out version))
                return RejectInput("Please enter a valid version, e.g. 1.0 or 1.0.0.0.", versionKryptonTextBox);

            // The variant lists of the other pages show the names, so they must be unique and not empty.
            // Renaming a variant in the grid is not checked by the add button, hence the check here.
            var variantNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                mod.Variants[Guid.Empty]
            };
            var variants = new List<KeyValuePair<Guid, string>>();
            foreach (DataGridViewRow row in variantsKryptonDataGridView.Rows)
            {
                string variantName = Convert.ToString(row.Cells[variantNameColumn.Index].Value).Trim();
                if (variantName.Length == 0)
                    return RejectInput("Every variant needs a name.", variantsKryptonDataGridView);
                if (!variantNames.Add(variantName))
                    return RejectInput("The variant name \"" + variantName + "\" is used more than once.",
                        variantsKryptonDataGridView);
                variants.Add(new KeyValuePair<Guid, string>(GetVariantId(row), variantName));
            }

            mod.Name = name;
            mod.Description = descriptionKryptonTextBox.Text;
            mod.Version = version;
            mod.Contact = contactKryptonTextBox.Text.Trim();
            // Replaced, not appended: the page can be confirmed several times (Back, then Next again).
            mod.Authors = authorsKryptonTextBox.Text
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(author => author.Trim())
                .Where(author => author.Length > 0)
                .ToList();
            foreach (var variant in variants)
                mod.AddOrUpdateVariant(variant.Key, variant.Value);
            return true;
        }

        private Guid GetVariantId(DataGridViewRow variantRow)
        {
            return Guid.Parse(Convert.ToString(variantRow.Cells[variantIdColumn.Index].Value));
        }

        private static bool RejectInput(string message, Control control)
        {
            MessageBox.Show(message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            control.Focus();
            return false;
        }

        private void addVariantKryptonButton_Click(object sender, EventArgs e)
        {
            string variantName = variantKryptonTextBox.Text.Trim();
            if (variantName.Length == 0)
                return;

            if (variantsKryptonDataGridView.Rows.Cast<DataGridViewRow>().Any(row =>
                    Convert.ToString(row.Cells[variantNameColumn.Index].Value)
                        .Equals(variantName, StringComparison.InvariantCultureIgnoreCase)))
            {
                MessageBox.Show("Variant already exists", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int rowIndex = variantsKryptonDataGridView.Rows.Add();
            DataGridViewRow newRow = variantsKryptonDataGridView.Rows[rowIndex];
            newRow.Cells[variantNameColumn.Index].Value = variantName;
            newRow.Cells[variantIdColumn.Index].Value = Guid.NewGuid();
            variantKryptonTextBox.Clear();
        }

        private void removeVariantKryptonButton_Click(object sender, EventArgs e)
        {
            if (variantsKryptonDataGridView.SelectedRows.Count != 1)
                return;

            if (MessageBox.Show("Are you sure you want to remove this variant?\n" +
                                "If you need to simply rename it double click on the variant name cell.", "Warning",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            DataGridViewRow selectedRow = variantsKryptonDataGridView.SelectedRows[0];
            Guid variantId = GetVariantId(selectedRow);
            // Variants only reach the mod when the first page is confirmed with "Next"; one added since then
            // exists only in the grid and has no data to delete.
            if (mod.DoesVariantExist(variantId))
            {
                bool relatedDataDeleted;
                try
                {
                    // Both always run: the files belong to the mod data, the banners to the assets.
                    bool filesRemoved = mod.RemoveVariant(variantId);
                    bool bannersRemoved = assets.RemoveVariant(variantId);
                    relatedDataDeleted = filesRemoved || bannersRemoved;
                }
                catch (DataException ex)
                {
                    MessageBox.Show("The variant cannot be removed: " + ex.Message, "Warning",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (relatedDataDeleted)
                {
                    MessageBox.Show(
                        "Variant removed, some related data to that variant (banners, files, etc...) has been deleted.",
                        "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }

            variantsKryptonDataGridView.Rows.Remove(selectedRow);
        }

        /* Step 2: icon and banners */

        /// <summary>Lists the variants and shows the banners of the first one.</summary>
        private void EnterImagesStep()
        {
            FillVariantList(bannersVariantsKryptonComboBox);
            // Selecting the first variant shows its banners (SelectedIndexChanged).
            if (bannersVariantsKryptonComboBox.Items.Count > 0)
                bannersVariantsKryptonComboBox.SelectedIndex = 0;
            else
                UpdateBannerPreview();
        }

        private void selectIconKryptonButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Image Files|*.bmp;*.jpg;*.jpeg;*.png";
                if (ofd.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(ofd.FileName))
                    return;
                try
                {
                    assets.Icon = Image.FromFile(ofd.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error while loading icon: " + ex.Message, "Warning",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                iconPictureBox.Image = assets.Icon;
            }
        }

        private void addBannerKryptonButton_Click(object sender, EventArgs e)
        {
            Guid? variant = GetSelectedVariant(bannersVariantsKryptonComboBox);
            if (variant == null)
            {
                MessageBox.Show("Please select a variant to add a banner for it", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Image Files|*.jpg;*.jpeg;*.png";
                if (ofd.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(ofd.FileName))
                    return;
                try
                {
                    assets.AddBanner(variant.Value, Image.FromFile(ofd.FileName));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error while adding banner: " + ex.Message, "Warning",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                bannerIndex = assets.GetBanners(variant.Value).Count - 1;
                UpdateBannerPreview();
            }
        }

        private void removeBannerKryptonButton_Click(object sender, EventArgs e)
        {
            Guid? variant = GetSelectedVariant(bannersVariantsKryptonComboBox);
            if (variant == null || !assets.HasBanner(variant.Value))
                return;
            assets.RemoveBanner(variant.Value, bannerIndex);
            if (bannerIndex != 0)
                bannerIndex--;
            UpdateBannerPreview();
        }

        private void bannersVariantsKryptonComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            bannerIndex = 0;
            UpdateBannerPreview();
        }

        private void nextBannerKryptonButton_Click(object sender, EventArgs e)
        {
            Guid? variant = GetSelectedVariant(bannersVariantsKryptonComboBox);
            if (variant == null || bannerIndex + 1 >= assets.GetBanners(variant.Value).Count)
                return;
            bannerIndex++;
            UpdateBannerPreview();
        }

        private void prevBannerKryptonButton_Click(object sender, EventArgs e)
        {
            Guid? variant = GetSelectedVariant(bannersVariantsKryptonComboBox);
            if (variant == null || bannerIndex == 0)
                return;
            bannerIndex--;
            UpdateBannerPreview();
        }

        /// <summary>Shows banner <see cref="bannerIndex"/> of the selected variant and the matching buttons.</summary>
        private void UpdateBannerPreview()
        {
            Guid? variant = GetSelectedVariant(bannersVariantsKryptonComboBox);
            IList<Image> banners = variant == null ? new Image[0] : assets.GetBanners(variant.Value);
            bool hasBanner = banners.Count > 0;
            if (bannerIndex >= banners.Count)
                bannerIndex = Math.Max(0, banners.Count - 1);

            bannersPictureBox.Image = hasBanner ? banners[bannerIndex] : null;
            addBannerKryptonButton.Enabled = variant != null;
            removeBannerKryptonButton.Enabled = hasBanner;
            prevBannerKryptonButton.Enabled = hasBanner;
            nextBannerKryptonButton.Enabled = hasBanner;
            bannerCounterKryptonLabel.Values.ExtraText = hasBanner
                ? "(" + (bannerIndex + 1) + "/" + banners.Count + ")"
                : string.Empty;
        }

        /* Step 3: files */

        /// <summary>Creates the variant folders in the working directory and lists the variants.</summary>
        private void EnterFilesStep()
        {
            packageBuilder.GenerateVariantsFolders();
            // No variant is preselected: selecting one reads its folder and may report ignored files.
            FillVariantList(filesKryptonComboBox);
        }

        private void filesKryptonComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            Guid? variant = GetSelectedVariant(filesKryptonComboBox);
            if (variant == null)
                return;

            List<string> ignoredFiles;
            try
            {
                ignoredFiles = packageBuilder.ReloadModFiles(variant.Value);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show("The files of the variant could not be read: " + ex.Message, "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            UpdateVariantFilesPreview(variant.Value);

            if (ignoredFiles.Count > 0)
            {
                const int maxListedFiles = 10;
                MessageBox.Show("These files are ignored because they are not inside one of the folders " +
                                string.Join(", ", EemFormat.ProductFolders) + ":\n\n" +
                                string.Join("\n", ignoredFiles.Take(maxListedFiles)) +
                                (ignoredFiles.Count > maxListedFiles ? "\n..." : string.Empty), "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Copies the file types chosen in the grid into the mod files. Each row keeps its <see cref="ModFile"/>
        /// in Tag, so the file does not have to be found again from the displayed product and path.
        /// </summary>
        private void SaveVariantFilesFromGrid()
        {
            foreach (DataGridViewRow fileRow in variantFilesKryptonDataGridView.Rows)
            {
                if (fileRow.Tag is ModFile modFile)
                    modFile.FileType = EnumExtensions.ParseDescription<ModFile.ModFileType>(
                        Convert.ToString(fileRow.Cells[fileTypeColumn.Index].Value));
            }
        }

        private void UpdateVariantFilesPreview(Guid variant)
        {
            SaveVariantFilesFromGrid();
            variantFilesKryptonDataGridView.Rows.Clear();

            foreach (var modFile in mod.ModFiles.FindAll(modFile => modFile.Variant == variant))
            {
                // Only files inside a product folder are indexed; skip anything else (e.g. from a mod archive)
                // instead of failing on it.
                ModFile.ModFileProduct product;
                if (!modFile.TryGetProduct(out product))
                    continue;

                DataGridViewRow row = variantFilesKryptonDataGridView.Rows[variantFilesKryptonDataGridView.Rows.Add()];
                row.Cells[variantColumn.Index].Value = variant.ToString();
                row.Cells[fileNameColumn.Index].Value = modFile.GetPathInProduct();
                row.Cells[productColumn.Index].Value = product.GetDescription();
                row.Cells[fileTypeColumn.Index].Value = modFile.FileType.GetDescription();
                row.Tag = modFile;
            }
        }

        private void variantFilesKryptonDataGridView_CellEnter(object sender, DataGridViewCellEventArgs e)
        {
            bool validClick = (e.RowIndex != -1 && e.ColumnIndex != -1);

            if (!(sender is DataGridView dataGridView))
                return;
            if (!(dataGridView.Columns[e.ColumnIndex] is DataGridViewComboBoxColumn) || !validClick)
                return;
            dataGridView.BeginEdit(true);
            ((ComboBox)dataGridView.EditingControl).DroppedDown = true;
        }

        private void updateVariantsFilesKryptonButton_Click(object sender, EventArgs e)
        {
            Guid? variant = GetSelectedVariant(filesKryptonComboBox);
            if (variant == null)
                return;

            // Quoted: the working directory contains spaces ("Empire Earth Launcher").
            Process.Start("explorer.exe",
                "\"" + Path.Combine(packageBuilder.WorkingDirectory, variant.Value.ToString()) + "\"");
        }

        /* Step 4: build */

        /// <summary>
        /// Checks that the mod can be built, asks where to save it and starts the build in the background.
        /// </summary>
        /// <returns>false if the build was not started (incomplete mod or cancelled by the user).</returns>
        private bool StartBuild()
        {
            if (assets.Icon == null)
            {
                MessageBox.Show("Please select an icon for the mod before building it.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            string eemPath;
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "Empire Earth Mod (" + EemFormat.SearchPattern + ")|" + EemFormat.SearchPattern;
                sfd.DefaultExt = EemFormat.Extension.TrimStart('.');
                sfd.AddExtension = true;
                sfd.FileName = GetDefaultArchiveName();
                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return false;
                eemPath = sfd.FileName;
            }

            // Keep the file types edited in the grid of the variant that is currently displayed.
            SaveVariantFilesFromGrid();

            buildStatusLabel.Text = "Building Mod...";

            // The worker saves the icon and banner bitmaps. GDI+ images must not be used by two threads at
            // once, so the previews let go of them until the build is finished.
            iconPictureBox.Image = null;
            bannersPictureBox.Image = null;

            buildWorker.RunWorkerAsync(eemPath);
            return true;
        }

        private string GetDefaultArchiveName()
        {
            string name = string.IsNullOrWhiteSpace(mod.Name) ? mod.Uuid.ToString() : mod.Name.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                name = name.Replace(invalidChar, '_');
            return name + EemFormat.Extension;
        }

        private void buildWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            string eemPath = (string)e.Argument;
            packageBuilder.Build(eemPath);
            e.Result = eemPath;
        }

        private void buildWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            iconPictureBox.Image = assets.Icon;
            // Shows the banners of the selected variant again (from the first one).
            bannerIndex = 0;
            UpdateBannerPreview();

            if (e.Error != null)
            {
                buildStatusLabel.Text = "Build failed";
                MessageBox.Show("The mod could not be built:\n\n" + e.Error.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Back to the files page, so the author can fix the problem and build again.
                GoToStep(WizardStep.Files);
                return;
            }

            modBuilt = true;
            buildStatusLabel.Text = "Mod built";
            UpdateNavigationButtons();
            MessageBox.Show("The mod has been saved to:\n" + e.Result, "Mod Creator",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /* Variant lists */

        /// <summary>Item of the variant lists: shows the name, identifies the variant by its id.</summary>
        private sealed class VariantItem
        {
            public VariantItem(Guid id, string name)
            {
                Id = id;
                Name = name;
            }

            public Guid Id { get; }

            public string Name { get; }

            public override string ToString()
            {
                return Name;
            }
        }

        private void FillVariantList(KryptonComboBox comboBox)
        {
            comboBox.Items.Clear();
            foreach (KeyValuePair<Guid, string> variant in mod.Variants)
                comboBox.Items.Add(new VariantItem(variant.Key, variant.Value));
        }

        /// <returns>The variant selected in <paramref name="comboBox"/>, or null if none is selected.</returns>
        private static Guid? GetSelectedVariant(KryptonComboBox comboBox)
        {
            var item = comboBox.SelectedItem as VariantItem;
            return item == null ? (Guid?)null : item.Id;
        }
    }
}
