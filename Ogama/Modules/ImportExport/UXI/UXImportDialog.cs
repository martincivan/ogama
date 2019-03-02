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

        private void button2_Click(object sender, EventArgs e)
        {
            progressBar1.Minimum = 0;
            progressBar1.Maximum = 0;
            foreach (DataGridViewRow value in directoriesView.Rows)
            {
                if (value.Cells[0].Value == "True")
                {
                    progressBar1.Maximum++;
                }
            }

            progressBar1.Value = 0;
            foreach (DataGridViewRow value in directoriesView.Rows)
            {
                if (value.Cells[0].Value == "True")
                {
                    UXImport.Run(value.Cells[1].Value);
                    progressBar1.Value++;
                }
            }
            string message = "Import data successfully written to database." + Environment.NewLine
                                     + "Please don´t forget to move the stimuli images to the SlideResources subfolder"
                                     + "of the experiment, otherwise no images will be shown.";
            ExceptionMethods.ProcessMessage("Success", message);
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}
