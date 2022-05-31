using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Drawing;
using System.Threading.Tasks;

namespace oilu_lib
{

    public class OiluMarker 
    {
        Mat DataMat;
        PointF[] quad;   // position of the marker in the original bitmap
        private int threshold;

        public OiluMarker(Mat _inputImg, PointF[] quad, int threshold)
        {
            this.DataMat = _inputImg;
            this.quad = quad;
            this.threshold = threshold;
        }

        public OiluMarker(Marker marker):this(marker.DataMat, marker.CanonicalCorners.ToArray(),127)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] detect(bool debug = false)
        {
            string[] idds = { "", "", "", "" };
            PointF midPt = new PointF(-1, -1);
            // convert to binary
            // TODO : multithreshold 
            Mat binaryInputImg = new Mat();
            CvInvoke.Threshold(DataMat, binaryInputImg, threshold, 255, ThresholdType.BinaryInv | ThresholdType.Otsu);
            CvInvoke.Imshow("Binarized DATAMAT", binaryInputImg);
            CvInvoke.WaitKey();


            // get the center of the quadrangle
            if (!TwoLinesintersection(quad[0], quad[2], quad[1], quad[3], ref midPt))
                throw new Exception("invalid non convex quad");

            // Process vertical Triangles, then horizontal triangles
            //todo: launch in parallel
            PointF[][] q = new PointF[4][];
            q[0] = new PointF[] { quad[0], quad[1], midPt }; // top tri
            q[1] = new PointF[] { midPt, quad[2], quad[3] }; // bottom tri
            q[2] = new PointF[] { quad[0], midPt, quad[3] }; // left tri
            q[3] = new PointF[] { midPt, quad[1], quad[2] }; // right tri

            for (int i = 0; i < q.GetLength(0); i++)
            //Parallel.For(0, 4, i =>
            {
                Mat tr = ExtractROI(binaryInputImg, Array.ConvertAll(q[i], Point.Ceiling), debug);
                if (i == 1) CvInvoke.Flip(tr, tr, FlipType.Vertical);
                if (i == 2)
                {
                    CvInvoke.Transpose(tr, tr);
                    CvInvoke.Flip(tr, tr, FlipType.Horizontal);
                }
                if (i == 3)
                {
                    CvInvoke.Transpose(tr, tr);
                    CvInvoke.Flip(tr, tr, FlipType.Vertical);
                }
                tr = new Mat(tr, new Rectangle(0, 0, tr.Width, tr.Height / 2));
                Tri_histogramVH histo = new Tri_histogramVH(tr, debug);
                if (histo.isValid_OILU_Triangle() == MarkerDtectionError.VALID_MARKER)
                    idds[i] = histo.getTriangleBins();
            }
            //);
            return getMarkerId(idds);
        }

        
        public String[] getMarkerId(string[] idds) //, int nRotations) 
        {
            return idds;
        }

        #region some utility functions


        public static bool TwoLinesintersection(PointF p1, PointF p2, PointF q1, PointF q2, ref PointF midPt)
        {
            PointF x = new PointF(q1.X - p1.X, q1.Y - p1.Y);
            PointF d1 = new PointF(p2.X - p1.X, p2.Y - p1.Y);
            PointF d2 = new PointF(q2.X - q1.X, q2.Y - q1.Y);

            float cross = d1.X * d2.Y - d1.Y * d2.X;
            if (Math.Abs(cross) < /*EPS*/1e-8)
                return false;

            float t1 = (x.X * d2.Y - x.Y * d2.X) / cross;
            midPt = new PointF(p1.X + d1.X * t1, p1.Y + d1.Y * t1);
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="src"></param>
        /// <param name="ps"></param>
        /// <returns></returns>
        public static Mat ExtractROI(Mat src, Point[] ps, bool showROI = true)
        {
            Mat mask = new Mat(src.Size, src.Depth, src.NumberOfChannels);
            mask.SetTo(new MCvScalar(0, 0, 0));

            VectorOfVectorOfPoint v = new VectorOfVectorOfPoint();
            v.Push(new VectorOfPoint(ps));

            CvInvoke.FillPoly(mask, v, new MCvScalar(255, 255, 255));

            Mat mmmm = new Mat();
            CvInvoke.BitwiseAnd(src, mask, mmmm);
            if (showROI)
            {
                CvInvoke.NamedWindow("wind", NamedWindowType.Normal);
                CvInvoke.Imshow("wind", mmmm);
                CvInvoke.WaitKey(10*1000);
            }
            return mmmm;
        }

        

       

        #endregion some utility functions


    }



}