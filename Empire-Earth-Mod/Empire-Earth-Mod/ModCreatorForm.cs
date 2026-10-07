using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Empire_Earth_Mod.Properties;
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

        /// <summary>True while <see cref="GoToStep"/> changes the page, which already prepared it.</summary>
        private bool changingStepByCode;

        /// <summary>Index of the banner shown for the selected variant.</summary>
        private int bannerIndex;

        /// <exception cref="IOException">The working directory cannot be created.</exception>
        /// <exception cref="UnauthorizedAccessException">The working directory cannot be created.</exception>
        public ModCreatorForm()
        {
            mod = new ModData();
            assets = new ModAssets();
            // Unique working directory below %LOCALAPPDATA%, released in OnFormClosed. Created before the
            // controls, so that a failure leaves no half-built window behind.
            packageBuilder = new ModPackageBuilder(mod, assets);

            InitializeComponent();

            buildWorker = new BackgroundWorker();
            buildWorker.DoWork += buildWorker_DoWork;
            buildWorker.RunWorkerCompleted += buildWorker_RunWorkerCompleted;
            wizardTabControl.Selecting += wizardTabControl_Selecting;
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
            changingStepByCode = true;
            try
            {
                wizardTabControl.SelectedIndex = (int)step;
            }
            finally
            {
                changingStepByCode = false;
            }
            UpdateNavigationButtons();
        }

        /// <summary>
        /// Page changes through the tab headers. They must not skip what "Next" does: moving forward validates
        /// and commits every page that is left (an invalid first page keeps the wizard there) and prepares the
        /// pages that are entered. Moving back loses nothing and is always allowed.
        /// </summary>
        private void wizardTabControl_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (changingStepByCode || e.TabPageIndex < 0)
                return;

            // While the mod is built in the background, the pages that edit it must not be reachable.
            if (buildWorker.IsBusy)
            {
                e.Cancel = e.TabPageIndex != (int)WizardStep.Build;
                return;
            }

            var target = (WizardStep)e.TabPageIndex;
            if (target <= CurrentStep)
                return;

            // Building is started by the "Build" button only (it asks where to save the archive). Once the mod
            // is built, the result page can be shown again.
            if (target == WizardStep.Build && !modBuilt)
            {
                e.Cancel = true;
                ShowWarning(Resources.UseBuildButton);
                return;
            }

            for (WizardStep step = CurrentStep; step < target; step++)
            {
                if (!PrepareNextStep(step))
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (buildWorker.IsBusy)
            {
                MessageBox.Show(Resources.WaitForBuild, Resources.ModCreatorTitle,
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
                    Format(Resources.DeleteWorkingFolderFormat, workingDir), Resources.ModCreatorTitle,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) == DialogResult.Yes;
                if (delete)
                    packageBuilder.DeleteWorkingDirectory();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(Format(Resources.WorkingFolderNotDeletedFormat, workingDir, ex.Message),
                    Resources.ModCreatorTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                case WizardStep.Images:
                    if (!PrepareNextStep(CurrentStep))
                        return;
                    break;
                case WizardStep.Files:
                    if (!StartBuild())
                        return;
                    break;
                case WizardStep.Build:
                    // There is no next page: the button reads "Close" after a successful build and is disabled
                    // while building (the tab header cannot open this page before the build).
                    if (modBuilt)
                        Close();
                    return;
            }

            GoToStep(CurrentStep + 1);
        }

        /// <summary>
        /// Leaves <paramref name="step"/> towards the following page: validates and commits its input and
        /// prepares the following page. Used by "Next" and by the tab headers.
        /// </summary>
        /// <returns>false if the input of <paramref name="step"/> is invalid (the user was told why).</returns>
        private bool PrepareNextStep(WizardStep step)
        {
            switch (step)
            {
                case WizardStep.General:
                    if (!CommitGeneralStep())
                        return false;
                    EnterImagesStep();
                    return true;
                case WizardStep.Images:
                    EnterFilesStep();
                    return true;
                default:
                    // The files page has nothing to commit before the build (StartBuild saves the grid).
                    return true;
            }
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
                    nextKryptonButton.Text = Resources.BuildButton;
                    nextKryptonButton.Enabled = true;
                    break;
                case WizardStep.Build:
                    nextKryptonButton.Text = building ? Resources.BuildingButton : Resources.CloseButton;
                    nextKryptonButton.Enabled = !building && modBuilt;
                    break;
                default:
                    nextKryptonButton.Text = Resources.NextButton;
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
                return RejectInput(Resources.EnterModName, nameKryptonTextBox);

            Version version;
            if (!ModData.TryParseVersion(versionKryptonTextBox.Text, out version))
                return RejectInput(Resources.EnterValidVersion, versionKryptonTextBox);

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
                    return RejectInput(Resources.VariantNameMissing, variantsKryptonDataGridView);
                if (!variantNames.Add(variantName))
                    return RejectInput(Format(Resources.VariantNameDuplicateFormat, variantName),
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
            ShowWarning(message);
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
                ShowWarning(Resources.VariantExists);
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

            if (MessageBox.Show(Resources.ConfirmRemoveVariant, Resources.WarningTitle,
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
                    ShowWarning(Format(Resources.VariantNotRemovedFormat, ex.Message));
                    return;
                }

                if (relatedDataDeleted)
                {
                    MessageBox.Show(Resources.VariantRemovedWithData, Resources.InfoTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                ofd.Filter = Resources.IconFileFilter;
                if (ofd.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(ofd.FileName))
                    return;
                try
                {
                    assets.Icon = Image.FromFile(ofd.FileName);
                }
                catch (Exception ex)
                {
                    ShowWarning(Format(Resources.IconNotLoadedFormat, ex.Message));
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
                ShowWarning(Resources.SelectVariantForBanner);
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = Resources.BannerFileFilter;
                if (ofd.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(ofd.FileName))
                    return;
                try
                {
                    assets.AddBanner(variant.Value, Image.FromFile(ofd.FileName));
                }
                catch (Exception ex)
                {
                    ShowWarning(Format(Resources.BannerNotAddedFormat, ex.Message));
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
                ShowWarning(Format(Resources.VariantFilesUnreadableFormat, ex.Message));
                return;
            }
            UpdateVariantFilesPreview(variant.Value);

            if (ignoredFiles.Count > 0)
            {
                const int maxListedFiles = 10;
                ShowWarning(Format(Resources.IgnoredFilesFormat, string.Join(", ", EemFormat.ProductFolders),
                    string.Join("\n", ignoredFiles.Take(maxListedFiles)) +
                    (ignoredFiles.Count > maxListedFiles ? "\n..." : string.Empty)));
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
                ShowWarning(Resources.SelectIconBeforeBuild);
                return false;
            }

            string eemPath;
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = Format(Resources.ModArchiveFilterFormat, EemFormat.SearchPattern);
                sfd.DefaultExt = EemFormat.Extension.TrimStart('.');
                sfd.AddExtension = true;
                sfd.FileName = GetDefaultArchiveName();
                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return false;
                eemPath = sfd.FileName;
            }

            // Keep the file types edited in the grid of the variant that is currently displayed.
            SaveVariantFilesFromGrid();

            buildStatusLabel.Text = Resources.BuildStatusBuilding;

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
                buildStatusLabel.Text = Resources.BuildStatusFailed;
                MessageBox.Show(Format(Resources.BuildFailedFormat, e.Error.Message), Resources.ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Back to the files page, so the author can fix the problem and build again.
                GoToStep(WizardStep.Files);
                return;
            }

            modBuilt = true;
            buildStatusLabel.Text = Resources.BuildStatusDone;
            UpdateNavigationButtons();
            MessageBox.Show(Format(Resources.ModSavedFormat, e.Result), Resources.ModCreatorTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /* Helpers */

        private static string Format(string format, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, format, args);
        }

        private static void ShowWarning(string message)
        {
            MessageBox.Show(message, Resources.WarningTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
