using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Empire_Earth_Mod_Lib;
using Empire_Earth_Mod_Lib.Serialization;
using Krypton.Toolkit;

namespace Empire_Earth_Mod
{
    public partial class ModCreatorForm : KryptonForm
    {
        private ModData mod;
        private ModData.Creator creator;

        public ModCreatorForm()
        {
            InitializeComponent();
            mod = new ModData();
            // Unique working directory below %LOCALAPPDATA%, released in OnFormClosed.
            creator = new ModData.Creator(mod);

            if (kryptonDataGridView1.Columns[4] is DataGridViewComboBoxColumn)
            {
                if (!(kryptonDataGridView1.Columns[4] is DataGridViewComboBoxColumn columnAlternative))
                    return;
                Enum.GetValues(typeof(ModFile.ModFileType)).Cast<ModFile.ModFileType>()
                    .Select(ModFile.GetModFileName).ToList()
                    .ForEach(fileName => columnAlternative.Items.Add(fileName));
            }
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
            if (creator == null)
                return;

            string workingDir = creator.GetWorkingDir();
            try
            {
                bool delete = !creator.ContainsModFiles() || MessageBox.Show(
                    "Delete the working folder of this mod, including the files you copied into it?\n\n" +
                    workingDir + "\n\nChoose \"No\" to keep the folder.", "Mod Creator",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) == DialogResult.Yes;
                if (delete)
                    creator.DeleteWorkingDirectory();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show("The working folder could not be deleted:\n" + workingDir + "\n\n" + ex.Message,
                    "Mod Creator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                creator.Dispose();
                creator = null;
            }
        }

        /* Variants Management */
        private void kryptonButton1_Click(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex == 0)
            {
                mod.Name = nameKryptonTextBox.Text;
                mod.Description = descriptionKryptonTextBox.Text;
                mod.Version = new Version(versionKryptonTextBox.Text);
                mod.Authors.Add(authorsKryptonTextBox.Text);

                foreach (DataGridViewRow row in variantsKryptonDataGridView1.Rows)
                {
                    mod.AddOrUpdateVariant(Guid.Parse(row.Cells[1].Value.ToString()), row.Cells[0].Value.ToString());
                }

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

                creator.GenerateVariantsFolders();
                // creator.ExportBannersAndIcon();
                nextKryptonButton.Text = "Build >";
            }

            if (tabControl1.SelectedIndex == 2)
            {
                nextKryptonButton.Enabled = false;
                nextKryptonButton.Text = "Building...";
                backKryptonButton.Visible = false;

                Task.Factory.StartNew(creator.ExportModInfos)
                    .ContinueWith(creator.ExportBannersAndIcon)
                    .ContinueWith(creator.ExportToZip);
            }

            tabControl1.SelectTab(tabControl1.SelectedTab.TabIndex + 1);

            Debug.WriteLine("STEP " + tabControl1.SelectedTab.TabIndex);
            Debug.WriteLine(
                JsonSerializer<ModData>.Serialize(
                    BinarySerializer<ModData>.Deserialize(BinarySerializer<ModData>.Serialize(mod))));
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
                if (mod.RemoveVariant(variantId))
                {
                    MessageBox.Show(
                        "Variant removed, some related data to that variant (banners, files, etc...) has been deleted.",
                        "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                        mod.SetIcon(Image.FromFile(ofd.FileName));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error while loading icon: " + ex.Message, "Warning",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    iconPictureBox.Image = mod.GetIcon();
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
                        mod.AddBanner(Image.FromFile(ofd.FileName), selectedVariantUuid);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error while adding banner: " + ex.Message, "Warning",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    _bannerIndex = mod.GetBanners(selectedVariantUuid).Count - 1;
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

            if (!mod.HasBanner(selectedVariantUuid))
                return;
            mod.GetBanners(selectedVariantUuid).RemoveAt(_bannerIndex);
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

            if (!mod.HasBanner(selectedVariantUuid) || _bannerIndex + 1 == mod.GetBanners(selectedVariantUuid).Count)
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

            if (!mod.HasBanner(selectedVariantUuid) || _bannerIndex == 0)
                return;
            _bannerIndex--;
            _UpdateBannerPreview(selectedVariantUuid);
        }

        private void _UpdateBannerPreview(Guid variantUuid)
        {
            if (!mod.HasBanner(variantUuid))
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
                int bannerStrIndex = mod.GetBanners(variantUuid).Count > 0 ? _bannerIndex + 1 : 0;

                bannersPictureBox.Image = mod.GetBanners(variantUuid)[_bannerIndex];
                kryptonButton5.Enabled = true;
                kryptonButton6.Enabled = true;
                prevBannerKryptonButton.Enabled = true;
                nextBannerKryptonButton.Enabled = true;
                kryptonLabel7.Values.ExtraText = "(" + bannerStrIndex + "/" + mod.GetBanners(variantUuid).Count + ")";
            }
        }

        private void _SaveVariantFilesFromGrid()
        {
            var modFileTypes = new Dictionary<string, ModFile.ModFileType>();
            foreach (DataGridViewRow fileRow in kryptonDataGridView1.Rows)
            {
                string productPath = ModFile.ParseModFileProduct(ModFile.GetProduct(fileRow.Cells[3].Value.ToString()));
                string builder = productPath + Path.DirectorySeparatorChar + fileRow.Cells[2].Value;
                creator.UpdateModFiles(
                    Guid.Parse(fileRow.Cells[1].Value.ToString()),
                    builder,
                    ModFile.ParseModFileType(fileRow.Cells[4].Value.ToString()));
            }
        }

        private void _UpdateVariantFilesPreview(Guid variantUuid)
        {
            _SaveVariantFilesFromGrid();
            kryptonDataGridView1.Rows.Clear();

            foreach (var modFile in mod.ModFiles.FindAll(modFile => modFile.Variant == variantUuid))
            {
                kryptonDataGridView1.Rows.Add(null,
                    variantUuid.ToString(),
                    modFile.RelativeFilePath.Substring(4),
                    modFile.GetProduct() == ModFile.ModFileProduct.EEC ? "EEC" :
                        modFile.GetProduct() == ModFile.ModFileProduct.AOC ? "AOC" : "Both",
                    ModFile.GetModFileName(modFile.FileType));
            }
        }

        private void filesKryptonComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (filesKryptonComboBox.SelectedIndex == -1)
                return;
            Guid selectedVariantUuid = Guid.Parse(mod.Variants.First(value =>
                value.Value.ToString() == filesKryptonComboBox.Text).Key.ToString());

            creator.ReloadModFiles(selectedVariantUuid);
            _UpdateVariantFilesPreview(selectedVariantUuid);
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
                creator.GetWorkingDir() + Path.DirectorySeparatorChar + selectedVariantUuid);
        }
    }
}