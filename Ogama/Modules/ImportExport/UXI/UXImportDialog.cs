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
using Ogama.ExceptionHandling;

namespace Ogama.Modules.ImportExport.UXI
{
    public partial class UXImportDialog : Form
    {
        public UXImportDialog()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var folderBrowser = new FolderBrowserDialog();
            if (folderBrowser.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            directoriesView.Rows.Clear();

            folderInput.Text = folderBrowser.SelectedPath;
            if (File.Exists(folderInput.Text + "\\settings.json"))
            {
                var n = directoriesView.Rows.Add();
                directoriesView.Rows[n].Cells[0].Value = "True";
                directoriesView.Rows[n].Cells[1].Value = folderInput.Text;
            }
            foreach (var dir in Directory.EnumerateDirectories(folderInput.Text))
            {
               if (UXImport.CheckFolder(dir)) {
                   var n = directoriesView.Rows.Add();
                   directoriesView.Rows[n].Cells[0].Value = "True";
                   directoriesView.Rows[n].Cells[1].Value = dir;
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow value in directoriesView.Rows)
            {
                if (value.Cells[0].Value == "True")
                {
                    UXImport.Run(value.Cells[1].Value);
                }
            }
            string message = "Import data successfully written to database." + Environment.NewLine
                                     + "Please don´t forget to move the stimuli images to the SlideResources subfolder"
                                     + "of the experiment, otherwise no images will be shown.";
            ExceptionMethods.ProcessMessage("Success", message);
        }
    }
}
