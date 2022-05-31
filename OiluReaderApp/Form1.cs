using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using oilu_lib;

namespace WindowsFormsApplication1
{
    public partial class Form1 : Form
    {
        OiluMarkerDetector oiluDetector;
        private Image<Bgr, byte> inputImge;
        public Form1()
        {
            InitializeComponent();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog of = new OpenFileDialog();
            of.Filter = "Images (*.BMP;*.JPG;*.PNG,*.TIFF,*.TIF,*.MP4)|*.BMP;*.JPG;*.MP4;*.PNG;*.TIFF;*.TIF|All files (*.*)|*.*";
            if (of.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = of.FileName;

                if ((of.FileName.IndexOf(".mp4", StringComparison.OrdinalIgnoreCase) > 0))
                {
                    processVideo(of.FileName);
                }
                else
                {
                    textBox2.Text = "";

                    inputPBox.Image = inputImge = new Image<Bgr, byte>(textBox1.Text);
                }
            }

        }

        private void processVideo(string filename)
        {
            Mat frame;
            long nbFrame = 0;
            var vidCapture = new VideoCapture(filename);
            if (!vidCapture.IsOpened)
            {
                MessageBox.Show("pb occured when opening the video !!!");
            }
            else
            {
                string dirname = Path.GetDirectoryName(filename) + "\\" + Path.GetFileNameWithoutExtension(filename);
                Directory.CreateDirectory(dirname);

                while ((frame = vidCapture.QueryFrame()) != null)
                {

                    nbFrame++;
                    if (nbFrame % 10 == 0)
                    {
                        //    ProcessImage(frame, separateTriRadio.Checked ? 2:3);
                        //    inputPBox.Refresh();
                        frame.Save(dirname + "\\" + nbFrame + ".tif");
                    }
                }
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            inputPBox.Image = inputImge = new Image<Bgr, byte>(textBox1.Text);

            if (inputImge != null)
                ProcessImage(inputImge.Mat, 1);

        }

        private void ProcessImage(Mat mat, int method)
        {
            oiluDetector = new OiluMarkerDetector(checkBox1.Checked);

            var mList = oiluDetector.detectMarkers(mat, method);

            label1.Text = "NBsquares = " + mList.Count;

            oiluDetector.DrawMarkersList(mat, mList, Color.Red, Color.Red, Color.Blue);
            getMarkersBins(mList);
            inputPBox.Refresh();

        }


        private void getMarkersBins(List<OiluMarker> mList)
        {
            var str = "";

            for (int i = 0; i < mList.Count; i++)
            {
                var marker = mList[i];
                string id = marker.Id;
                string[] bins = marker.bins;
                str += "" + i + "-\t" + id + "-\t";
                for (int k = 0; k < bins.Length; k++)
                {
                    str += bins[k] + " - ";
                }
                textBox2.Text = str;
                textBox2.Refresh();

                str += "\r\n";
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            inputPBox.Image = inputImge = new Image<Bgr, byte>(textBox1.Text);
            if (inputImge != null)
            {
                int method = separateTriRadio.Checked ? 2 : 3;
                ProcessImage(inputImge.Mat, method);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {

            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            string folder = "C:\\Users\\fdl\\Desktop\\tmpppppppppp";
            string str = "";
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                folder = folderBrowserDialog.SelectedPath;

          

                string[] files = Directory.GetFiles(folder, "*.tif", SearchOption.AllDirectories);
                for (int j = 0; j < files.Length; j++)
                {
                    Mat mat = new Mat(files[j]);
                    if (mat != null)
                    {
                       
                        var oiluDetector = new OiluMarkerDetector(70, new Size(500, 500), false);
                        var mlist = oiluDetector.detectMarkers(mat, separateTriRadio.Checked ? 2 : 3);
                        
                        str = str +  Path.GetFileNameWithoutExtension(files[j]) + "\t" + mlist.Count + "\r\n" ;

                    }
                }
                textBox2.Text = str;
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            inputPBox.Image = inputImge = new Image<Bgr, byte>(textBox1.Text);
            MarkerDetector md = new MarkerDetector(70,new Size(250,250));

            var mList = md.findMarkers(inputImge.Mat);

            label1.Text = "NBsquares = " + mList.Count;

            md.DrawMarkersList(inputImge.Mat, mList, Color.Red, Color.Red, Color.Blue) ;
            //getMarkersBins(mList);
            inputPBox.Refresh();
        }
    }

}



