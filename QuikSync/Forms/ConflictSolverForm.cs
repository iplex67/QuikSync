using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuikSync.LocalWorks.FileWorks;
using QuikSync.NetWorks.ConflictWorks;
using QuikSync.Properties;

namespace QuikSync
{
    public partial class ConflictSolverForm : Form
    {
        public enum FileExistanceStatus
        {
            ExistsOnHost,
            ExistsOnJoined,
            ExistsOnBoth
        }

        public List<FileDiffData> diffs;
        public List<SyncAction> actions;
        public List<FileExistanceStatus> existanceStatuses;

        private FilePreviewer previewer;

        /// <summary>
        /// True while a preview is being prepared. Previews run off the dialog thread, so the window stays responsive,
        /// but it cannot be closed until the temporary files are done.
        /// </summary>
        private bool previewBusy;

        public ConflictSolverForm(List<FileDiffData> fdd, FilePreviewer previewer)
        {
            InitializeComponent();
            diffs = fdd;
            this.previewer = previewer;

            actions = new List<SyncAction>();
            existanceStatuses = new List<FileExistanceStatus>();


            for (int i = 0; i < diffs.Count; i++)
            {
                actions.Add(SyncAction.NotChosen);
            }

            DisplayFileDiffData();
            ((DataGridViewImageColumn)(dataGridView1.Columns[0])).ImageLayout = DataGridViewImageCellLayout.Zoom;
            dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45,45,48);
            dataGridView1.EnableHeadersVisualStyles = false;
        }

        private void doneBtn_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// Function that displays FileDiff list on DataGridView.
        /// </summary>
        private void DisplayFileDiffData() 
        {
            foreach (var item in diffs)
            {
                string hs = item.Host.byteSize      == -1  ? "-" : FormatBytes(item.Host.byteSize);
                string js = item.Joined.byteSize    == -1  ? "-" : FormatBytes(item.Joined.byteSize);
                dataGridView1.Rows.Add(Resources.NotChosen, item.FileRelativePath, hs, js, item.Host.lastTime, item.Joined.lastTime);

                if (hs == "-")
                {
                    existanceStatuses.Add(FileExistanceStatus.ExistsOnJoined);
                }
                else if (js == "-")
                {
                    existanceStatuses.Add(FileExistanceStatus.ExistsOnHost);
                }
                else
                {
                    existanceStatuses.Add(FileExistanceStatus.ExistsOnBoth);
                }
            }
        }

        /// <summary>
        /// Applies specific SyncOption to all selected rows.
        /// </summary>
        /// <param name="sa">Specific SyncOption to be applied.</param>
        private void SetForSelectedRowsAction(SyncAction sa) 
        {
            for (int i = 0; i < dataGridView1.SelectedRows.Count; i++)
            {
                // Check if the file does exist on host or joined before assigning it's SyncAction. Bcoz we cant download the file that doesnt exist.

                bool SyncActionCouldBeAssigned = true;

                switch (sa)
                {
                    case SyncAction.GetFromHost:
                        if (existanceStatuses[dataGridView1.SelectedRows[i].Index] == FileExistanceStatus.ExistsOnJoined)
                            SyncActionCouldBeAssigned = false;
                        break;
                    case SyncAction.GetFromJoined:
                        if (existanceStatuses[dataGridView1.SelectedRows[i].Index] == FileExistanceStatus.ExistsOnHost)
                            SyncActionCouldBeAssigned = false;
                        break;
                    case SyncAction.Skip:
                        break;
                    case SyncAction.GetNewClone:
                        if (existanceStatuses[dataGridView1.SelectedRows[i].Index] == FileExistanceStatus.ExistsOnJoined)
                            SyncActionCouldBeAssigned = false;
                        break;
                    case SyncAction.Delete:
                        break;
                    case SyncAction.NotChosen:
                        break;
                    default:
                        break;
                }

                if (!SyncActionCouldBeAssigned)
                    continue;

                actions[dataGridView1.SelectedRows[i].Index] = sa;

                // Assign action image 
                switch (sa)
                {
                    case SyncAction.GetFromHost:
                        dataGridView1.Rows[dataGridView1.SelectedRows[i].Index].Cells[0].Value = Resources.Host;
                        break;
                    case SyncAction.GetFromJoined:
                        dataGridView1.Rows[dataGridView1.SelectedRows[i].Index].Cells[0].Value = Resources.Joined;
                        break;
                    case SyncAction.Skip:
                        dataGridView1.Rows[dataGridView1.SelectedRows[i].Index].Cells[0].Value = Resources.Skip;
                        break;
                    case SyncAction.GetNewClone:
                        dataGridView1.Rows[dataGridView1.SelectedRows[i].Index].Cells[0].Value = Resources.Clone;
                        break;
                    case SyncAction.NotChosen:
                        dataGridView1.Rows[dataGridView1.SelectedRows[i].Index].Cells[0].Value = Resources.NotChosen;
                        break;
                    case SyncAction.Delete:
                        dataGridView1.Rows[dataGridView1.SelectedRows[i].Index].Cells[0].Value = Resources.Delete;
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Function made for displaying not just Bytes but GB and MB and so on, so user could see more common things.
        /// </summary>
        /// <param name="bytes">Size that has to be formated from bytes.</param>
        /// <returns>Formated string for displaying size.</returns>
        private static string FormatBytes(long bytes)
        {
            string[] Suffix = { "B", "KB", "MB", "GB", "TB" };
            int i;
            double dblSByte = bytes;
            for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024)
            {
                dblSByte = bytes / 1024.0;
            }

            return String.Format("{0:0.##} {1}", dblSByte, Suffix[i]);
        }
        private void hostBtn_Click(object sender, EventArgs e)
        {
            SetForSelectedRowsAction(SyncAction.GetFromHost);
        }

        private void joinedBtn_Click(object sender, EventArgs e)
        {
            SetForSelectedRowsAction(SyncAction.GetFromJoined);
        }

        private void deleteBtn_Click(object sender, EventArgs e)
        {
            SetForSelectedRowsAction(SyncAction.Delete);
        }

        private void newCloneBtn_Click(object sender, EventArgs e)
        {
            SetForSelectedRowsAction(SyncAction.GetNewClone);
        }

        private void skipBtn_Click(object sender, EventArgs e)
        {
            SetForSelectedRowsAction(SyncAction.Skip);
        }

        private async void previewHostBtn_Click(object sender, EventArgs e)
        {
            await PreviewSelectedFile(FileExistanceStatus.ExistsOnJoined, "Host", previewer.PreviewHostFile);
        }

        private async void previewJoinedBtn_Click(object sender, EventArgs e)
        {
            await PreviewSelectedFile(FileExistanceStatus.ExistsOnHost, "Joined", previewer.PreviewLocalFile);
        }

        /// <summary>
        /// Opens a temporary copy of the first selected file, so user could see which version has to be kept.
        /// </summary>
        /// <param name="missingStatus">Existance status that means the file is not there on the previewed side.</param>
        /// <param name="side">Name of the previewed side for messages.</param>
        /// <param name="preview">Function that makes the copy and opens it.</param>
        private async Task PreviewSelectedFile(FileExistanceStatus missingStatus, string side, Func<string, string> preview)
        {
            if (dataGridView1.SelectedRows.Count == 0)
            {
                MessageBox.Show("Select a file to preview first.", "QuikSync", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int index = dataGridView1.SelectedRows[0].Index;

            if (existanceStatuses[index] == missingStatus)
            {
                MessageBox.Show($"The file does not exist on {side}.", "QuikSync", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string rel = diffs[index].FileRelativePath;

            SetPreviewBusy(true);
            try
            {
                await Task.Run(() => preview(rel));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not preview the file: {ex.Message}", "QuikSync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                SetPreviewBusy(false);
            }
        }

        private void SetPreviewBusy(bool busy)
        {
            previewBusy = busy;
            previewHostBtn.Enabled = !busy;
            previewJoinedBtn.Enabled = !busy;
            Cursor = busy ? Cursors.AppStarting : Cursors.Default;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (previewBusy)
            {
                // A download is still writing into the temporary directory that CleanUp removes.
                e.Cancel = true;
                MessageBox.Show("A preview is still being prepared. Wait for it to finish.", "QuikSync", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            base.OnFormClosing(e);
        }

        private void ConflictSolverForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            previewer.CleanUp();
        }

        private void closeWindowBtn_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void minimizeBtn_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        #region Magic code for dragging window wihout boarder!

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private void ConflictSolverForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        #endregion

        private void ConflictSolverForm_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawRectangle(new Pen(Color.FromArgb(38,38,42), 2), this.DisplayRectangle);
        }
    }
}
