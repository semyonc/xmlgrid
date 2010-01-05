using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Xml;
using System.Windows.Forms;
using System.Net;

using WmHelp.XmlGrid;
using System.Reflection;

namespace XmlGridDemo
{
    public partial class Form1 : Form
    {

        public XmlGridView xmlGrid;        

        public Form1()
        {            
            InitializeComponent();

            this.Text = "XmlGridControl demo";

            xmlGrid = new XmlGridView();
            xmlGrid.Dock = DockStyle.Fill;
            xmlGrid.Location = new Point(0, 100);
            xmlGrid.Name = "xmlGridView1";
            xmlGrid.Size = new Size(100, 100);
            xmlGrid.TabIndex = 0;
            xmlGrid.AutoHeightCells = true;
            panel1.Controls.Add(xmlGrid);                        
        } 

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "xml files (*.xml)|*.xml|All files (*.*)|*.*";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                XmlDataDocument xmldoc = new XmlDataDocument();
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.IgnoreWhitespace = true;
                settings.ProhibitDtd = false;
                XmlUrlResolver resolver = new XmlUrlResolver();
                resolver.Credentials = CredentialCache.DefaultCredentials;
                settings.XmlResolver = resolver;
                XmlReader render = XmlReader.Create(dialog.FileName, settings);
                try
                {
                    try
                    {
                        xmldoc.Load(render);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, ex.Message, "Parse Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                finally
                {
                    render.Close();
                }
                GridBuilder builder = new GridBuilder();
                if (xmlGrid.ShowColumnHeader)
                {
                    GridCellGroup xmlgroup = new GridCellGroup();
                    xmlgroup.Flags = GroupFlags.Overlapped | GroupFlags.Expanded;
                    builder.ParseNodes(xmlgroup, null, xmldoc.ChildNodes);
                    GridCellGroup root = new GridCellGroup();
                    root.Table.SetBounds(1, 2);
                    root.Table[0, 0] = new GridHeadLabel();
                    root.Table[0, 0].Text = dialog.FileName;
                    root.Table[0, 1] = xmlgroup;
                    xmlGrid.Cell = root;
                }
                else
                {
                    GridCellGroup root = new GridCellGroup();
                    builder.ParseNodes(root, null, xmldoc.ChildNodes);
                    xmlGrid.Cell = root;
                }
            }
        }

        private void helpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Assembly asm = Assembly.GetAssembly(typeof(XmlGridView));
            string title = "XmlGridControl";
            MessageBox.Show(
                String.Format("{0} {1}\n", title, asm.GetName().Version) +
                "Copyright © Semyon A. Chertkov 2009\n" +
                "e-mail: semyonc@gmail.com",
                "About " + Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            GC.Collect();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
