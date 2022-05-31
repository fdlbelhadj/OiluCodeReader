namespace oilu_lib
{
    partial class debugWindow
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.imageBox1 = new Emgu.CV.UI.ImageBox();
            this.zedChart = new ZedGraph.ZedGraphControl();
            ((System.ComponentModel.ISupportInitialize)(this.imageBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // imageBox1
            // 
            this.imageBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.imageBox1.Location = new System.Drawing.Point(12, 12);
            this.imageBox1.Name = "imageBox1";
            this.imageBox1.Size = new System.Drawing.Size(884, 360);
            this.imageBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.imageBox1.TabIndex = 2;
            this.imageBox1.TabStop = false;
            this.imageBox1.Visible = false;
            // 
            // zedChart
            // 
            this.zedChart.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.zedChart.Location = new System.Drawing.Point(12, 12);
            this.zedChart.Name = "zedChart";
            this.zedChart.ScrollGrace = 0D;
            this.zedChart.ScrollMaxX = 0D;
            this.zedChart.ScrollMaxY = 0D;
            this.zedChart.ScrollMaxY2 = 0D;
            this.zedChart.ScrollMinX = 0D;
            this.zedChart.ScrollMinY = 0D;
            this.zedChart.ScrollMinY2 = 0D;
            this.zedChart.Size = new System.Drawing.Size(884, 360);
            this.zedChart.TabIndex = 3;
            this.zedChart.UseExtendedPrintDialog = true;
            this.zedChart.Visible = false;
            // 
            // debugWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(905, 384);
            this.Controls.Add(this.imageBox1);
            this.Controls.Add(this.zedChart);
            this.Name = "debugWindow";
            this.Text = "debug";
            ((System.ComponentModel.ISupportInitialize)(this.imageBox1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private Emgu.CV.UI.ImageBox imageBox1;
        private ZedGraph.ZedGraphControl zedChart;
    }
}