using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using Ogama.ExceptionHandling;

namespace Ogama.Modules.ImportExport.UXI
{
    public delegate void CloseDelagate();
    public partial class UXImportDialog : Form
    {
        public UXImportDialog()
        {
            InitializeComponent();
        }

        public void setPreferredEye(String value)
        {
            preferredEye.Text = value;
        }

        public void setDirectories(List<String> list)
        {
            directoriesView.Rows.Clear();
            foreach (var dir in list)
            {
                var n = directoriesView.Rows.Add();
                directoriesView.Rows[n].Cells[0].Value = "True";
                directoriesView.Rows[n].Cells[1].Value = dir;
            }
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            ProgressForm form = new ProgressForm();
            form.Show();
            List<String> valueList = new List<string>();
            foreach (DataGridViewRow value in directoriesView.Rows)
            {
                if (value.Cells[0].Value == "True")
                {
                    valueList.Add((String) value.Cells[1].Value);
                }
            }
            form.setMaximum(valueList.Count);
            IProgress<int> p = new Progress<int>(v =>
            {
                form.setProgress(v);
                UXImport.mainWindowCache.RefreshContextPanelImageTabs();
                UXImport.mainWindowCache.RefreshContextPanelSubjects();
            });
            IProgress<int> k = new Progress<int>(v => form.Close());
            var tokenSource = new CancellationTokenSource();
            form.setTask(tokenSource);
            string eye = preferredEye.Text;
            bool importVideo = checkBox1.Checked;
            bool importMouseMovement = checkBox3.Checked;
            bool importMouseEvents = checkBox2.Checked;
            bool importKeyboardEvents = checkBox4.Checked;
            Task task = new Task(() =>
            {
                UXImport.Run(valueList, p, tokenSource.Token, eye, importVideo, importMouseMovement, importMouseEvents, importKeyboardEvents);
                k.Report(1);
            });
            task.Start();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow value in directoriesView.Rows)
            {
                value.Cells[0].Value = "True";
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow value in directoriesView.Rows)
            {
                value.Cells[0].Value = "False";
            }
        }
    }
}
