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
    public partial class ModCreatorForm : KryptonForm
    {
        /// <summary>Index of the "Building Mod..." page, the last page of the wizard.</summary>
        private const int BuildTabIndex = 3;

        private ModData mod;
        private ModAssets assets;
        private ModPackageBuilder packageBuilder;
        private readonly BackgroundWorker buildWorker;
        private bool modBuilt;

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
            tabControl1.Selecting += (sender, e) =>
            {
                if (buildWorker.IsBusy && e.TabPageIndex != BuildTabIndex)
                    e.Cancel = true;
            };

            if (kryptonDataGridView1.Columns[4] is DataGridViewComboBoxColumn)
            {
                if (!(kryptonDataGridView1.Columns[4] is DataGridViewComboBoxColumn columnAlternative))
                    return;
                Enum.GetValues(typeof(ModFile.ModFileType)).Cast<ModFile.ModFileType>()
                    .Select(fileType => fileType.GetDescription()).ToList()
                    .ForEach(fileName => columnAlternative.Items.Add(fileName));
            }
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
            ReleaseCreator();
        }

        /// <summary>
        /// Ends the creator session. The working directory is deleted when it holds no mod files, or when
        /// the author agrees; otherwise it is kept, so closing the window never loses copied files.
        /// </summary>
        private void ReleaseCreator()
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

        /* Variants Management */
        private void kryptonButton1_Click(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex == BuildTabIndex)
            {
                // After a successful build the button reads "Close". The page can also be reached through
                // its tab header without building; there is no next page then.
                if (modBuilt)
                    Close();
                return;
            }

            if (tabControl1.SelectedIndex == 0)
            {
                if (!_ApplyBasicInformation())
                    return;

                bannersVariantsKryptonComboBox.Items.Clear();
                bannersVariantsKryptonComboBox.Items.AddRange(
                    mod.Variants.Values.Select(x => x.ToString() as object).ToArray());

                if (bannersVariantsKryptonComboBox.SelectedIndex == -1 &&
                    bannersVariantsKryptonComboBox.Items.Count > 0)
                {
                    bannersVariantsKryptonComboBox.SelectedIndex = 0;
                    Guid selectedVariantUuid = Guid.Parse(mod.Variants.First(value =>
                        value.Value.ToString() == bannersVariantsKryptonComboBox.Text).Key.ToString());
                    _bannerIndex = 0;
                    _UpdateBannerPreview(selectedVariantUuid);
                }
                else
                {
                    bannersPictureBox.Image = null;
                    kryptonButton5.Enabled = false;
                    kryptonButton6.Enabled = false;
                    prevBannerKryptonButton.Enabled = false;
                    nextBannerKryptonButton.Enabled = false;
                    kryptonLabel7.Values.ExtraText = string.Empty;
                }
                backKryptonButton.Visible = true;
            }

            if (tabControl1.SelectedIndex == 1)
            {
                filesKryptonComboBox.Items.Clear();


                filesKryptonComboBox.Items.AddRange(
                    mod.Variants.Values.Select(x => x.ToString() as object).ToArray());

                packageBuilder.GenerateVariantsFolders();
                // packageBuilder.ExportBannersAndIcon();
                nextKryptonButton.Text = "Build >";
            }

            if (tabControl1.SelectedIndex == 2)
            {
                if (!StartBuild())
                    return;
            }

            tabControl1.SelectTab(tabControl1.SelectedTab.TabIndex + 1);
        }

        /// <summary>
        /// Validates the first page and copies it into the mod.
        /// </summary>
        /// <returns>false, after telling the user why, if an input is invalid; the mod is not changed then.</returns>
        private bool _ApplyBasicInformation()
        {
            string name = nameKryptonTextBox.Text.Trim();
            if (name.Length == 0)
                return _RejectInput("Please enter a name for the mod.", nameKryptonTextBox);

            Version version;
            if (!Version.TryParse(versionKryptonTextBox.Text.Trim(), out version))
                return _RejectInput("Please enter a valid version, e.g. 1.0 or 1.0.0.0.", versionKryptonTextBox);

            // The other pages look variants up by their name, so names must be unique and not empty. Renaming
            // a variant in the grid is not checked by the add button, hence the check here.
            var variantNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                mod.Variants[Guid.Empty]
            };
            var variants = new List<KeyValuePair<Guid, string>>();
            foreach (DataGridViewRow row in variantsKryptonDataGridView1.Rows)
            {
                string variantName = Convert.ToString(row.Cells[0].Value).Trim();
                if (variantName.Length == 0)
                    return _RejectInput("Every variant needs a name.", variantsKryptonDataGridView1);
                if (!variantNames.Add(variantName))
                    return _RejectInput("The variant name \"" + variantName + "\" is used more than once.",
                        variantsKryptonDataGridView1);
                variants.Add(new KeyValuePair<Guid, string>(Guid.Parse(Convert.ToString(row.Cells[1].Value)), variantName));
            }

            mod.Name = name;
            mod.Description = descriptionKryptonTextBox.Text;
            mod.Version = version;
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

        private static bool _RejectInput(string message, Control control)
        {
            MessageBox.Show(message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            control.Focus();
            return false;
        }

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
                sfd.FileName = _GetDefaultArchiveName();
                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return false;
                eemPath = sfd.FileName;
            }

            // Keep the file types edited in the grid of the variant that is currently displayed.
            _SaveVariantFilesFromGrid();

            nextKryptonButton.Enabled = false;
            nextKryptonButton.Text = "Building...";
            backKryptonButton.Visible = false;
            label1.Text = "Building Mod...";

            // The worker saves the icon and banner bitmaps. GDI+ images must not be used by two threads at
            // once, so the previews let go of them until the build is finished.
            iconPictureBox.Image = null;
            bannersPictureBox.Image = null;

            buildWorker.RunWorkerAsync(eemPath);
            return true;
        }

        private string _GetDefaultArchiveName()
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
            bannersVariantsKryptonComboBox_SelectedIndexChanged(bannersVariantsKryptonComboBox, EventArgs.Empty);

            if (e.Error != null)
            {
                label1.Text = "Build failed";
                MessageBox.Show("The mod could not be built:\n\n" + e.Error.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Back to the files page, so the author can fix the problem and build again.
                tabControl1.SelectTab(BuildTabIndex - 1);
                nextKryptonButton.Text = "Build >";
                nextKryptonButton.Enabled = true;
                backKryptonButton.Visible = true;
                return;
            }

            modBuilt = true;
            label1.Text = "Mod built";
            nextKryptonButton.Text = "Close";
            nextKryptonButton.Enabled = true;
            MessageBox.Show("The mod has been saved to:\n" + e.Result, "Mod Creator",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void backKryptonButton_Click(object sender, EventArgs e)
        {
            tabControl1.SelectTab(tabControl1.SelectedTab.TabIndex - 1);
            nextKryptonButton.Text = "Next >";
            if (tabControl1.SelectedTab.TabIndex == 0)
                backKryptonButton.Visible = false;
        }

        private void addVariantKryptonButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(variantKryptonTextBox.Text))
            {
                if (variantsKryptonDataGridView1.Rows.Cast<DataGridViewRow>().Any(x =>
                        (x.Cells[0].Value.ToString().Equals(variantKryptonTextBox.Text,
                            StringComparison.InvariantCultureIgnoreCase))))
                {
                    MessageBox.Show("Variant already exists", "Warning",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                variantsKryptonDataGridView1.Rows.Add(variantKryptonTextBox.Text, Guid.NewGuid());
                variantKryptonTextBox.Clear();
            }
        }

        private void removeVariantKryptonButton_Click(object sender, EventArgs e)
        {
            if (variantsKryptonDataGridView1.SelectedRows.Count != 1)
                return;

            if (MessageBox.Show("Are you sure you want to remove this variant?\n" +
                                "If you need to simply rename it double click on the variant name cell.", "Warning",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                Guid variantId = Guid.Parse(variantsKryptonDataGridView1.SelectedRows[0].Cells[1].Value.ToString());
                // Variants only reach the mod when the first page is confirmed with "Next"; one added since
                // then exists only in the grid and has no data to delete.
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

                variantsKryptonDataGridView1.Rows.RemoveAt(variantsKryptonDataGridView1.SelectedRows[0].Index);
            }
        }

        /* Icon Management */

        private void iconKryptonButton4_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Image Files|*.bmp;*.jpg;*.jpeg;*.png";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    if (string.IsNullOrWhiteSpace(ofd.FileName))
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
        }

        /* Banner Management */
        private int _bannerIndex = 0;

        private void kryptonButton5_Click(object sender, EventArgs e)
        {
            if (bannersVariantsKryptonComboBox.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a variant to add a banner for it", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Guid selectedVariantUuid = Guid.Parse(mod.Variants.First(value =>
                value.Value.ToString() == bannersVariantsKryptonComboBox.Text).Key.ToString());

            if (!mod.DoesVariantExist(selectedVariantUuid))
            {
                MessageBox.Show("Variant does not exist", "Warning", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Image Files|*.jpg;*.jpeg;*.png";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    if (string.IsNullOrWhiteSpace(ofd.FileName))
                        return;
                    try
                    {
                        assets.AddBanner(selectedVariantUuid, Image.FromFile(ofd.FileName));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error while adding banner: " + ex.Message, "Warning",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    _bannerIndex = assets.GetBanners(selectedVariantUuid).Count - 1;
                    _UpdateBannerPreview(selectedVariantUuid);
                }
            }
        }

        private void kryptonButton6_Click(object sender, EventArgs e)
        {
            if (bannersVariantsKryptonComboBox.SelectedIndex == -1)
                return;
            Guid selectedVariantUuid = Guid.Parse(mod.Variants.First(value =>
                value.Value.ToString() == bannersVariantsKryptonComboBox.Text).Key.ToString());

            if (!assets.HasBanner(selectedVariantUuid))
                return;
            assets.RemoveBanner(selectedVariantUuid, _bannerIndex);
            if (_bannerIndex != 0)
                _bannerIndex--;
            _UpdateBannerPreview(selectedVariantUuid);
        }

        private void bannersVariantsKryptonComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (bannersVariantsKryptonComboBox.SelectedIndex == -1)
            {
                _UpdateBannerPreview(Guid.Empty);
            }
            else
            {
                Guid selectedVariantUuid = Guid.Parse(mod.Variants.First(value =>
                    value.Value.ToString() == bannersVariantsKryptonComboBox.Text).Key.ToString());
                _bannerIndex = 0;
                _UpdateBannerPreview(selectedVariantUuid);
            }
        }

        private void nextBannerKryptonButton_Click(object sender, EventArgs e)
        {
            if (bannersVariantsKryptonComboBox.SelectedIndex == -1)
                return;
            Guid selectedVariantUuid = Guid.Parse(mod.Variants.First(value =>
                value.Value.ToString() == bannersVariantsKryptonComboBox.Text).Key.ToString());

            if (!assets.HasBanner(selectedVariantUuid) || _bannerIndex + 1 == assets.GetBanners(selectedVariantUuid).Count)
                return;
            _bannerIndex++;
            _UpdateBannerPreview(selectedVariantUuid);
        }

        private void prevBannerKryptonButton_Click(object sender, EventArgs e)
        {
            if (bannersVariantsKryptonComboBox.SelectedIndex == -1)
                return;
            Guid selectedVariantUuid = Guid.Parse(mod.Variants.First(value =>
                value.Value.ToString() == bannersVariantsKryptonComboBox.Text).Key.ToString());

            if (!assets.HasBanner(selectedVariantUuid) || _bannerIndex == 0)
                return;
            _bannerIndex--;
            _UpdateBannerPreview(selectedVariantUuid);
        }

        private void _UpdateBannerPreview(Guid variantUuid)
        {
            if (!assets.HasBanner(variantUuid))
            {
                bannersPictureBox.Image = null;
                kryptonButton5.Enabled = true;
                kryptonButton6.Enabled = false;
                prevBannerKryptonButton.Enabled = false;
                nextBannerKryptonButton.Enabled = false;
                kryptonLabel7.Values.ExtraText = string.Empty;
            }
            else
            {
                int bannerStrIndex = assets.GetBanners(variantUuid).Count > 0 ? _bannerIndex + 1 : 0;

                bannersPictureBox.Image = assets.GetBanners(variantUuid)[_bannerIndex];
                kryptonButton5.Enabled = true;
                kryptonButton6.Enabled = true;
                prevBannerKryptonButton.Enabled = true;
                nextBannerKryptonButton.Enabled = true;
                kryptonLabel7.Values.ExtraText = "(" + bannerStrIndex + "/" + assets.GetBanners(variantUuid).Count + ")";
            }
        }

        /// <summary>
        /// Copies the file types chosen in the grid into the mod files. Each row keeps its <see cref="ModFile"/>
        /// in Tag, so the file does not have to be found again from the displayed product and path.
        /// </summary>
        private void _SaveVariantFilesFromGrid()
        {
            foreach (DataGridViewRow fileRow in kryptonDataGridView1.Rows)
            {
                if (fileRow.Tag is ModFile modFile)
                    modFile.FileType = EnumExtensions.ParseDescription<ModFile.ModFileType>(
                        Convert.ToString(fileRow.Cells[4].Value));
            }
        }

        private void _UpdateVariantFilesPreview(Guid variantUuid)
        {
            _SaveVariantFilesFromGrid();
            kryptonDataGridView1.Rows.Clear();

            foreach (var modFile in mod.ModFiles.FindAll(modFile => modFile.Variant == variantUuid))
            {
                // Only files inside a product folder are indexed; skip anything else (e.g. from a mod archive)
                // instead of failing on it.
                ModFile.ModFileProduct product;
                if (!modFile.TryGetProduct(out product))
                    continue;

                int rowIndex = kryptonDataGridView1.Rows.Add(null,
                    variantUuid.ToString(),
                    modFile.GetPathInProduct(),
                    product.GetDescription(),
                    modFile.FileType.GetDescription());
                kryptonDataGridView1.Rows[rowIndex].Tag = modFile;
            }
        }

        private void filesKryptonComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (filesKryptonComboBox.SelectedIndex == -1)
                return;
            Guid selectedVariantUuid = Guid.Parse(mod.Variants.First(value =>
                value.Value.ToString() == filesKryptonComboBox.Text).Key.ToString());

            List<string> ignoredFiles;
            try
            {
                ignoredFiles = packageBuilder.ReloadModFiles(selectedVariantUuid);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show("The files of the variant could not be read: " + ex.Message, "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _UpdateVariantFilesPreview(selectedVariantUuid);

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

        private void kryptonDataGridView1_CellEnter(object sender, DataGridViewCellEventArgs e)
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
            if (filesKryptonComboBox.SelectedIndex == -1)
                return;
            Guid selectedVariantUuid = Guid.Parse(mod.Variants.First(value =>
                value.Value.ToString() == filesKryptonComboBox.Text).Key.ToString());

            Process.Start("explorer.exe",
                packageBuilder.WorkingDirectory + Path.DirectorySeparatorChar + selectedVariantUuid);
        }
    }
}