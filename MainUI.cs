using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileDetective
{
    public partial class MainUI : Form
    {
        public MainUI()
        {
            InitializeComponent();
        }

        private void nudMixSz_ValueChanged(object sender, EventArgs e)
        {
            if(nudMixSz.Value > nudMaxSz.Value)
            {
                nudMaxSz.Value = nudMixSz.Value + 50;
            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            if(fbd.ShowDialog(this) == DialogResult.OK)
            {
                if (!string.IsNullOrEmpty(fbd.SelectedPath))
                {
                    txtSearchPath.Text = fbd.SelectedPath;
                }
                
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (nudMaxSz.Value < nudMixSz.Value)
                return;

            if (string.IsNullOrEmpty(txtSearchPath.Text))
                return;


            bw.RunWorkerAsync();

            lblRunning.Text = "Running ...";
            lblRunning.Visible = true;
            lblProgress.Text = "";


            btnStop.Enabled = true;
           
            btnStart.Enabled = false;
            btnBrowse.Enabled = false;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if(bw.WorkerSupportsCancellation == true)
            {
                bw.CancelAsync();
                lblRunning.Text = "Stopping ...";
            }
        }

        List<FoundFile> _foundFiles;
        List<FoundFile> FoundFiles
        {
            get { return _foundFiles; }
            set { _foundFiles = value; }
        }

        private long _filesCount;
        long FilesCount
        {
            get { return _filesCount; }
            set { _filesCount = value; }
        }
        private void bw_DoWork(object sender, DoWorkEventArgs e)
        {

            try
            {

                var searcher = new Lib.DirSearcher(txtSearchPath.Text);

                FoundFiles = new List<FoundFile>();
                searcher.Initialize();
                var files = searcher.Files;
                int count = files.Count();

                FilesCount = count;


                int current = 0;

                foreach (var file in searcher.EnumerateFiles())
                {
                    if (file != null)
                    {
                        var szInMb = (file.Length / (1024 * 1024));
                        if(szInMb >= nudMixSz.Value && szInMb <= nudMaxSz.Value)
                        {
                            // Put it found files
                            FoundFiles.Add(new FoundFile
                            {
                                Filename = file.Name,
                                Path = file.FullName,
                                Size = szInMb
                            });
                        }
                    }


                    
                    // Report progress or cancel
                    current++;
                    if (e.Cancel == true)
                    {
                        bw.ReportProgress(current);
                        break;
                    }

                    bw.ReportProgress(current);
                }

            }
            catch(UnauthorizedAccessException uae)
            {
                Console.WriteLine(uae.Message);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }


        }

        private void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            lblProgress.Text = String.Format("Processing : {0} of {1} files.", e.ProgressPercentage, FilesCount);
            lblProgress.Update();
            _UpdateGrid();
            
        }

        private void _UpdateGrid() {
            dgv.DataSource = FoundFiles;
            dgv.Update();
        }
        private void _ResetComponents()
        {
            lblRunning.Text = "Done!";
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            btnBrowse.Enabled = true;


        }
        private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _ResetComponents();
            _UpdateGrid();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            var props = DiskRamp.Properties.Settings.Default;

            props.MaxSize = (long)nudMaxSz.Value;
            props.MinSize = (long)nudMixSz.Value;
            props.LastPath = txtSearchPath.Text;

            props.Save();

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var props = DiskRamp.Properties.Settings.Default;

            nudMaxSz.Value = props.MaxSize;
            nudMixSz.Value = props.MinSize;
            txtSearchPath.Text = props.LastPath;



        }

        private IEnumerable<FoundFile> _GetSelectedRows() {
            var enumerator = dgv.SelectedRows.GetEnumerator();

            FoundFile current = null;

            while (enumerator!= null &&  enumerator.MoveNext())
            {
                var curItm = enumerator.Current as DataGridViewRow;
                if(curItm != null)
                {
                    current = (FoundFile)curItm.DataBoundItem;
                    yield return current;
                }
            }

            
        }
        private void cmsActions_Opening(object sender, CancelEventArgs e)
        {

            if ( dgv.SelectedRows != null && _GetSelectedRows()?.Count() >  0)
            {
                actDelete.Enabled = true;
                actDetails.Enabled = true;
            }
            else
            {
                actDelete.Enabled = false;
                actDetails.Enabled = false;
            }
        }

        private void actDetails_Click(object sender, EventArgs e)
        {
            var rows = _GetSelectedRows();

            var totalSz = rows.Sum(i => i.Size);

            var details = String.Format("{0} Items selected({1:#,###.##} MB).", rows.Count(), totalSz);
            MessageBox.Show(details, "Disk Ramp", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void actDelete_Click(object sender, EventArgs e)
        {
            var rows = _GetSelectedRows();
            var totalSz = rows.Sum(i => i.Size);

            var details = String.Format("Confirm you want to delete {0} items(s) to free up {1:#,###.##} MB on your disk", rows.Count(), totalSz);
            if (MessageBox.Show(details, "Disk Ramp", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                // Show Progress Bar
                
                foreach(var itm in rows)
                {
                    lblProgress.Text = "Deleting ... ( " + itm.Filename + " - " + itm.Size + "MB )";
                    _DeleteFile(itm.Path);
                }

                lblProgress.Text = "Files deleted successfully";
            }
        }

        private void _DeleteFile(string filename)
        {
            if(!String.IsNullOrEmpty(filename))
            {
                File.Delete(filename);
            }
            
        }
    }
}
