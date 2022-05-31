using Emgu.CV;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ZedGraph;

namespace oilu_lib
{
    public partial class debugWindow : Form
    {

        public debugWindow()
        {
            InitializeComponent();

        }

        public static void DisplayMat(Mat mat)
        {
                CvInvoke.Imshow("mat", mat);
                CvInvoke.WaitKey();
        }

        public void ShowMat(Mat mat, string title = "")
        {
            zedChart.Visible = false;
            imageBox1.Visible = true;
            this.Text = title;
            imageBox1.Image = mat;
            this.ShowDialog();

        }

        public void showHisto(string title, float[] xLabels, float[] yValues, Color col)
        {
            {
                if (xLabels == null && yValues == null) return;
                if (xLabels == null)
                {
                    xLabels = new float[yValues.Length];
                    for (int i = 0; i < yValues.Length; i++)
                        xLabels[i] = i;
                }
                PointPairList list = new PointPairList(Array.ConvertAll(xLabels, x => (double)x),
                                                       Array.ConvertAll(yValues, x => (double)x));
                // Array.ConvertAll<int, double>(yValues, x => x));

                zedChart.GraphPane.Title.Text = "cumul histos";
                zedChart.GraphPane.YAxis.Title.Text = "cumul Y";
                LineItem line = zedChart.GraphPane.AddCurve(title, list, col, SymbolType.None);
                line.Line.Style = System.Drawing.Drawing2D.DashStyle.Solid;

                zedChart.GraphPane.YAxis.Scale.MaxAuto = true;
                zedChart.GraphPane.YAxis.Scale.MinAuto = true;
                zedChart.GraphPane.XAxis.Type = AxisType.LinearAsOrdinal;
                zedChart.Visible = true;
                imageBox1.Visible = false;

                zedChart.AxisChange();
                this.ShowDialog();

            }
        }

        public void showHisto(string title, byte[] xLabels, byte[] yValues, Color col)
        {
            {
                if (xLabels == null && yValues == null) return;
                if (xLabels == null)
                {
                    xLabels = new byte[yValues.Length];
                    for (int i = 0; i < yValues.Length; i++)
                        xLabels[i] = 0;
                }
                PointPairList list = new PointPairList(Array.ConvertAll(xLabels, x => (double)x),
                                                       Array.ConvertAll(yValues, x => (double)x));
                // Array.ConvertAll<int, double>(yValues, x => x));

                zedChart.GraphPane.Title.Text = "cumul histos";
                zedChart.GraphPane.YAxis.Title.Text = "cumul Y";
                LineItem line = zedChart.GraphPane.AddCurve(title, list, col, SymbolType.None);
                line.Line.Style = System.Drawing.Drawing2D.DashStyle.Solid;

                zedChart.GraphPane.YAxis.Scale.MaxAuto = true;
                zedChart.GraphPane.YAxis.Scale.MinAuto = true;
                zedChart.GraphPane.XAxis.Type = AxisType.LinearAsOrdinal;
                zedChart.Visible = true;
                imageBox1.Visible = false;

                zedChart.AxisChange();
                this.ShowDialog();

            }
        }

    }
}
