using System;
using Emgu.CV;
using Emgu.CV.Util;
using System.Drawing;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace oilu_lib
{
    public class OiluMarker
    {

        public VectorOfPointF cornersInImg;
        private double threshold;
        public string[] bins;
        private readonly Size CanonicalMarkerSize;
        private readonly string tag;


        public VectorOfPointF CanonicalCorners { get; private set; }

        public string Id { get; set; }
        public Mat DataMat { get; private set; }

        //public OiluMarker(): this(new VectorOfPointF(),new Size(100,100), "s")
        //{

        //}
        public bool isValidOiluMarker() { 
            return Id != "-1"; 
        }

        

        public OiluMarker(VectorOfPointF corners, Size canonicalMarkerSize, string tag )
        {
            Id = "";
            this.cornersInImg = new VectorOfPointF( corners.ToArray());
            this.CanonicalMarkerSize = canonicalMarkerSize;
            this.tag = tag;
            CanonicalCorners = new VectorOfPointF(new PointF[] {   new PointF(0, 0),
                                                    new PointF(canonicalMarkerSize.Width - 1, 0),
                                                    new PointF(canonicalMarkerSize.Width - 1, canonicalMarkerSize.Height - 1),
                                                    new PointF(0, canonicalMarkerSize.Height - 1)
                                                });
            threshold = 127;            
        }

 

        public void drawMarker(Mat inMat,string text, Color cinvalid, Color cvalid, Color caxis)
        {
            
            Point[] Ps = Array.ConvertAll(cornersInImg.ToArray(), Point.Round);

            Color c = Id == "" ? cinvalid : cvalid;
            if(Id != "") CvInvoke.Polylines(inMat, Ps, true, new Bgr(c).MCvScalar,4);

            // draw axis
            CvInvoke.Line(inMat, Ps[0], Ps[2], new Bgr(caxis).MCvScalar);
            CvInvoke.Line(inMat, Ps[1], Ps[3], new Bgr(caxis).MCvScalar);

            PointF midPt = default;
            if (!TwoLinesintersection(Ps[0], Ps[2], Ps[1], Ps[3], ref midPt))
                throw new Exception("Invalid convex quad");

            CvInvoke.PutText(inMat, text, new Point((int)Ps[0].X, (int)Ps[0].Y-10), FontFace.HersheyPlain, 5, new MCvScalar(36, 255, 12),2);

        }


        public string getMarkerId( bool debug = false)
        {
            bins = new string[] { "", "", "", "" };
            PointF midPt = new PointF(-1, -1);
            // convert to binary
            // TODO : multithreshold 
            Mat binaryInputImg = new Mat();
            CvInvoke.Threshold(DataMat, binaryInputImg, threshold, 255, ThresholdType.BinaryInv | ThresholdType.Otsu);
            if (debug)
            {
                CvInvoke.Imshow("Binarized DATAMAT", binaryInputImg);
                CvInvoke.WaitKey();
            }


            // get the center of the quadrangle
            if (!TwoLinesintersection(CanonicalCorners[0], CanonicalCorners[2], CanonicalCorners[1], CanonicalCorners[3], ref midPt))
                throw new Exception("invalid convex quad");

            // Process vertical Triangles, then horizontal triangles
            //todo: launch in parallel
            PointF[][] q = new PointF[4][];
            q[0] = new PointF[] { CanonicalCorners[0], CanonicalCorners[1], midPt }; // top tri
            q[1] = new PointF[] { midPt, CanonicalCorners[2], CanonicalCorners[3] }; // bottom tri
            q[2] = new PointF[] { CanonicalCorners[0], midPt, CanonicalCorners[3] }; // left tri
            q[3] = new PointF[] { midPt, CanonicalCorners[1], CanonicalCorners[2] }; // right tri

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
                    bins[i] = histo.getTriangleBins();
            }
            //);
            return Id = Decode(bins);

        }


        public string getMarkerId_WithMHH( bool debug)
        {
            bins = new string[] { "", "", "", "" };
            PointF midPt = new PointF(-1, -1);

            // TODO : multithreshold 
            Mat binaryInputImg = new Mat();
            CvInvoke.Threshold(DataMat, binaryInputImg, threshold, 255, ThresholdType.BinaryInv | ThresholdType.Otsu);
            if (debug)
            {
                CvInvoke.Imshow("Binarized DATAMAT", binaryInputImg);
                CvInvoke.WaitKey();
            }
            //binaryInputImg = binaryInputImg;

            // get the center of the quadrangle
            if (!TwoLinesintersection(CanonicalCorners[0], CanonicalCorners[2], CanonicalCorners[1], CanonicalCorners[3], ref midPt))
                throw new Exception("invalid convex quad");

            // Process vertical Triangles, then horizontal triangles
            //todo: launch in parallel
            PointF[][] q = new PointF[4][];
            q[0] = new PointF[] { CanonicalCorners[0], CanonicalCorners[1], midPt }; // top tri
            q[1] = new PointF[] { midPt, CanonicalCorners[2], CanonicalCorners[3] }; // bottom tri
            q[2] = new PointF[] { CanonicalCorners[0], midPt, CanonicalCorners[3] }; // left tri
            q[3] = new PointF[] { midPt, CanonicalCorners[1], CanonicalCorners[2] }; // right tri

            Tri_histogramVH[] triHistos = new Tri_histogramVH[4];
            //for (int i = 0; i < q.GetLength(0); i++)
            Parallel.For(0, 4, i =>
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
                triHistos[i] = new Tri_histogramVH(tr, debug);
                triHistos[i].CalculateDirectionalHisto(Direction.All_Directions);



                triHistos[i].deNoiseHisto(Direction.All_Directions);
                triHistos[i].GetHorizontalBands();
                triHistos[i].Analyze_HorizBands();
                //if (ret != MarkerDtectionError.VALID_MARKER) bins[i] = "-1";

            }
            );

            var Cum_H_Template = new Cumulatvie_H_Histo(binaryInputImg, triHistos, debug);
            if (debug) Cum_H_Template.ShowHistos();
            //if (Cum_H_Template.isValidOiluMarker() == false) return Id = "-1";
            bins = Cum_H_Template.IdentifyMarker();
            return Id = Decode(bins);
        }


        public string getMarkerIdWithMergedTriangles( bool debug)
        {
            bins = new string[] { "", "", "", "" };
            PointF midPt = new PointF(-1, -1);
            // convert to binary
            // TODO : multithreshold 
            Mat binaryInputImg = new Mat();
            CvInvoke.Threshold(DataMat, binaryInputImg, threshold, 255, ThresholdType.BinaryInv | ThresholdType.Otsu);
            if (debug)
            {
                CvInvoke.Imshow("Binarized DATAMATA", binaryInputImg);
                CvInvoke.WaitKey();
            }


            // get the center of the quadrangle
            if (!TwoLinesintersection(CanonicalCorners[0], CanonicalCorners[2], CanonicalCorners[1], CanonicalCorners[3], ref midPt))
                throw new Exception("invalid convex quad");

            // Process vertical Triangles, then horizontal triangles
            //todo: launch in parallel
            PointF[][] q = new PointF[4][];
            q[0] = new PointF[] { CanonicalCorners[0], CanonicalCorners[1], midPt }; // top tri
            q[1] = new PointF[] { midPt, CanonicalCorners[2], CanonicalCorners[3] }; // bottom tri
            q[2] = new PointF[] { CanonicalCorners[0], midPt, CanonicalCorners[3] }; // left tri
            q[3] = new PointF[] { midPt, CanonicalCorners[1], CanonicalCorners[2] }; // right tri

            Tri_histogramVH[] triHistos = new Tri_histogramVH[4];
            for (int i = 0; i < 4; i++)
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

                triHistos[i] = new Tri_histogramVH(tr, debug);
                triHistos[i].CalculateDirectionalHisto(Direction.H_Direction);

                triHistos[i].deNoiseHisto(Direction.H_Direction);
                triHistos[i].GetHorizontalBands();
                triHistos[i].Analyze_HorizBands();
                if (debug) triHistos[i].drawBandsInSrc("trihisto " + i );




            }
            //);

            Mat mergedTriMat = Mat.Zeros( binaryInputImg.Height / 2,binaryInputImg.Width, binaryInputImg.Depth, binaryInputImg.NumberOfChannels) ;
            for (int i = 0; i < 4; i++)
            {
                mergedTriMat = mergedTriMat | triHistos[i].Src;
            }

            var mergedTriangle = new Tri_histogramVH(mergedTriMat, debug);
            //if (
            //mergedTriangle.isValid_OILU_Triangle(); //== MarkerDtectionError.VALID_MARKER)
            //    bins[0] = mergedTriangle.getTriangleBins();
            mergedTriangle.CalculateDirectionalHisto(Direction.H_Direction);
            mergedTriangle.deNoiseHisto(Direction.H_Direction);
            mergedTriangle.GetHorizontalBands();
            mergedTriangle.Analyze_HorizBands();
            if (debug) mergedTriangle.drawBandsInSrc("bands OR");

            for (int i = 0; i < 4; i++)
            {
                bins[i] = triHistos[i].decode_HorizBandsAccordingToTemplate(mergedTriangle.H_template);
            }



            return Id = Decode(bins);
        }

        internal float perimeter()
        {
            PointF v1 = new PointF(cornersInImg[1].X - cornersInImg[0].X, cornersInImg[1].Y - cornersInImg[0].Y);
            PointF v2 = new PointF(cornersInImg[2].X - cornersInImg[1].X, cornersInImg[2].Y - cornersInImg[1].Y);
            PointF v3 = new PointF(cornersInImg[3].X - cornersInImg[2].X, cornersInImg[3].Y - cornersInImg[2].Y);
            PointF v0 = new PointF(cornersInImg[0].X - cornersInImg[3].X, cornersInImg[0].Y - cornersInImg[3].Y);

            return  v0.X * v0.X + v1.X * v1.X + v2.X * v2.X + v3.X * v3.X ;
        }

        internal void SwapPoints(int v1, int v2)
        {
            PointF[] pointFs = cornersInImg.ToArray();
            PointF p = pointFs[v1];
            pointFs[v1] = pointFs[v2];
            pointFs[v2] = p;
            cornersInImg = new VectorOfPointF(pointFs);
        }
        private string Decode(string[] idds) //, int nRotations) 
        {
            if (idds == null) return "";
            
            if ((idds[0].Length == 0)|| (idds[1].Length == 0) || (idds[2].Length == 0) || (idds[3].Length == 0)) return "";
            if (idds[0].Length + idds[1].Length + idds[2].Length + idds[3].Length != 4* idds[0].Length) return "";

            string digit = "";
            for (int i = 1; i < idds[0].Length; i++)
            {
                int b = idds[2][i] == '1' ? 1 : 0;
                
                b <<= 1;
                b = idds[1][i] == '1' ? b|1 : b|0;
                
                b <<= 1;
                b = idds[3][i] == '1' ? b|1 : b|0;

                b <<= 1;
                b = idds[0][i] == '1' ? b|1 : b|0;

                sbyte bb = binarySegmentsToOilu((byte)b);
                if (bb == -1) return "";
                else digit += bb;
            }
            
            return digit;
        }

        byte oiluToBinarySegments(byte digit)
        {

            switch (digit)
            {
                case 0:
                    return 0b1111;
                case 1:
                    return 0b1000;
                case 2:
                    return 0b1100;
                case 3:
                    return 0b1110;
                case 4:
                    return 0b0110;
                case 5:
                    return 0b0111;
                case 6:
                    return 0b0011;
                case 7:
                    return 0b1011;
                case 8:
                    return 0b1001;
                case 9:
                    return 0b1101;
                default:
                    return 0;
            }
        }

        sbyte binarySegmentsToOilu(byte segments)
        {

            switch (segments)
            {
                case 0:
                    return -1;
                case 1:
                    return 1;// -1;
                case 2:
                    return 1;// -1;
                case 3:
                    return 6;
                case 4:
                    return 1;// -1;
                case 5:
                    return 1;// -1;
                case 6:
                    return 4;
                case 7:
                    return 5;
                case 8:
                    return 1;
                case 9:
                    return 8;
                case 10:
                    return -1;
                case 11:
                    return 7;
                case 12:
                    return 2;
                case 13:
                    return 9;
                case 14:
                    return 3;
                case 15:
                    return 0;
                default:
                    return -1;
            }
        }

        internal void setDataMat(Mat m) => this.DataMat = m;

        internal void correctPerspective(Mat inuputImg, Mat canonicalMarker)
        {
            Mat M = CvInvoke.GetPerspectiveTransform(cornersInImg, CanonicalCorners);
            CvInvoke.WarpPerspective(inuputImg, canonicalMarker, M, CanonicalMarkerSize);
        }




        #region some utilities functions


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
        private Mat ExtractROI(Mat src, Point[] ps, bool showROI = true)
        {
            Mat mask = new Mat(src.Size, src.Depth, src.NumberOfChannels);
            mask.SetTo(new MCvScalar(0, 0, 0));

            VectorOfVectorOfPoint v = new VectorOfVectorOfPoint();
            v.Push(new VectorOfPoint(ps));

            CvInvoke.FillPoly(mask, v, new MCvScalar(255, 255, 255));

            Mat tr = new Mat();
            CvInvoke.BitwiseAnd(src, mask, tr);

            return tr;
        }





        #endregion some utility functions



    }

}

