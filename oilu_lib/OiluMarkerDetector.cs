using System;
using System.Collections.Generic;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using System.Drawing;
using System.Threading.Tasks;
using Emgu.CV.Structure;
using System.Diagnostics;
using System.IO;

namespace oilu_lib
{



    public class OiluMarkerDetector
    {

        private readonly int MIN_CONTOUR_SIZE = 70;
        private readonly int CANONICAL_MARKER_SIZE = 500;
        private readonly int HIERARCHICAL_VIEWS = 3;
        private readonly int MIN_VIEW_SIZE = 3;
        private readonly int VIEW_STEP = 10;
        private readonly int BINARIZATION_THRESH = 127;
        private readonly float EPSILON_CURVE_APPROX = 0.03f;
  



        private readonly int minContourSizeAllowed;
        private Size canonicalMarkerSize;
        private readonly bool debug;
        private Mat greyImage;
        //debugWindow debugW;
        private Stopwatch stopwatch;
        public long extractionTime;
        public long perspectiveTime;
        public long processTime;
        public string recap;

        public OiluMarkerDetector(int m_minContourSizeAllowed, Size canonicalMarkerSize, bool debug = false)
        {

            minContourSizeAllowed = m_minContourSizeAllowed;
            this.canonicalMarkerSize = canonicalMarkerSize;
            this.debug = debug;
            //if(this.debug) debugW = new debugWindow();
        }

        public OiluMarkerDetector(bool debug)
        {
            minContourSizeAllowed = MIN_CONTOUR_SIZE;
            this.canonicalMarkerSize = new Size(CANONICAL_MARKER_SIZE, CANONICAL_MARKER_SIZE);
            this.debug = debug;
            //if (this.debug) debugW = new debugWindow();
        }

        public List<OiluMarker> detectMarkers(Mat mat, int method )
        {

            var mList = getCandidateMarkers(mat);

            stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < mList.Count; i++)
            {
                var marker = mList[i];

                if (method > 3) method = 2;
                switch (method)
                {
                    case 1:
                        marker.getMarkerId(debug);
                        break;

                    case 2:
                        marker.getMarkerId_WithMHH(debug);
                        break;
                    case 3:
                        marker.getMarkerIdWithMergedTriangles(debug);
                        break;
                }
            }
            stopwatch.Stop();
            processTime = stopwatch.ElapsedMilliseconds;

           return mList;
        }
        

        public List<OiluMarker> getCandidateMarkers(Mat _image)
        {
            stopwatch = Stopwatch.StartNew();
                if (_image.IsEmpty) return null;

                // TODO : MAKE LOW RESOLUTION COPY ON WHICH THE DETECTION WORKS
                greyImage = new Mat();
                ConvertToGrey(_image, greyImage);
                // if (debug) debugW.ShowMat(greyImage, "GreyImage");

                VectorOfVectorOfPointF candidatesSet = DetectCandidates(greyImage);
            stopwatch.Stop();
            extractionTime = stopwatch.ElapsedMilliseconds;


            stopwatch = Stopwatch.StartNew();

                var mlist = CorrectPerspective(candidatesSet);
            
            stopwatch.Stop();
            perspectiveTime = stopwatch.ElapsedMilliseconds;

            return mlist;


        }

        private List<OiluMarker> CorrectPerspective(VectorOfVectorOfPointF candidates)
        {
            
            List<OiluMarker> markerList = new List<OiluMarker>();
            for (int i = 0; i < candidates.Size; i++)
            {
                Mat canonicalMarker = new Mat();
                OiluMarker marker = new OiluMarker(candidates[i], canonicalMarkerSize, ""+i);
                //markerList
                marker.correctPerspective(greyImage, canonicalMarker);
                marker.setDataMat(canonicalMarker);

                markerList.Add(marker);

            }

            return markerList;
        }

        VectorOfVectorOfPointF DetectCandidates(Mat greyImage)
        {

            VectorOfVectorOfPointF candidates = new VectorOfVectorOfPointF();
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();

 
            /// TODO: work on execution speed
            DetectInitialCandidates(greyImage, candidates, contours);

            reorderCandidatesCorners(ref candidates);
            //if (debug) ShowContours(contours, candidates);

            /// TODO: work on execution speed
            var res = _filterTooCloseCandidates(candidates, contours);
            //if (debug) ShowContours(contours, candidates);

            return res;

        }


        public void DetectInitialCandidates(Mat grey, VectorOfVectorOfPointF candidates, VectorOfVectorOfPoint contours)
        {

            int nScales = HIERARCHICAL_VIEWS;

            // ADD two cases of thresholding : canny and binarization threshold 
            VectorOfVectorOfPointF[] candidatesArrays = new VectorOfVectorOfPointF[nScales + 2];
            for (int i = 0; i < candidatesArrays.Length; i++)
                candidatesArrays[i] = new VectorOfVectorOfPointF();

            VectorOfVectorOfPoint[] contoursArrays = new VectorOfVectorOfPoint[nScales + 2];
            for (int i = 0; i < contoursArrays.Length; i++)
                contoursArrays[i] = new VectorOfVectorOfPoint();

            //for (int range = 0; range < nScales; range++)
            Parallel.For(0, nScales, range =>
            {
                int currScale = MIN_VIEW_SIZE + range * VIEW_STEP;
                Mat thresh = new Mat();
                Threshold(grey, thresh, currScale);

                // detect rectangles
                FindImageContours(thresh, candidatesArrays[range], contoursArrays[range]);
                // if (debug) debugW.ShowMat(thresh, "threshold adaptive");

            }
            );



            // to avoid the adaptivethreshold artifacts, add a threshold binarization case
            Mat thresholdImage = new Mat();
            CvInvoke.Threshold(grey, thresholdImage, BINARIZATION_THRESH, 255, ThresholdType.Binary);
            FindImageContours(thresholdImage, candidatesArrays[nScales], contoursArrays[nScales]);
            ////if (debug) debugW.ShowMat(thresholdImage, "thresholding thresh");

            //Mat cannyImg = new Mat();
            //CvInvoke.Canny(grey, cannyImg, 0, 0);
            //FindImageContours(thresholdImage, candidatesArrays[nScales + 1], contoursArrays[nScales + 1]);
            ////if (debug) debugW.ShowMat(cannyImg, "cannyImg");

            // join candidates
            for (int i = 0; i < nScales + 2; i++)
            {
                for (int j = 0; j < candidatesArrays[i].Size; j++)
                {
                    candidates.Push(candidatesArrays[i][j]);
                    contours.Push(contoursArrays[i][j]);
                }
            }
        }

       

        public void FindImageContours(Mat _inThresh, VectorOfVectorOfPointF candidatesOut, VectorOfVectorOfPoint contoursOut)
        {
            // calculate maximum and minimum sizes in pixels
            uint minPerimeterPixels = (uint)(0.03 * Math.Max(_inThresh.Cols, _inThresh.Rows));
            uint maxPerimeterPixels = (uint)(4 * Math.Max(_inThresh.Cols, _inThresh.Rows));

            Mat contoursImg = new Mat();
            _inThresh.CopyTo(contoursImg);
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(contoursImg, contours, null, RetrType.List, ChainApproxMethod.ChainApproxNone);
            // now filter list of contours
            for (int i = 0; i < contours.Size; i++)
            {
                // check perimeter
                if (contours[i].Size < minPerimeterPixels || contours[i].Size > maxPerimeterPixels)
                    continue;

                // check is square and is convex
                VectorOfPoint approxCurve = new VectorOfPoint();
                CvInvoke.ApproxPolyDP(contours[i], approxCurve, (CvInvoke.ArcLength(contours[i], true)) * EPSILON_CURVE_APPROX, true);
                
                if (approxCurve.Size != 4 || !CvInvoke.IsContourConvex(approxCurve)) continue;

                // check min distance between corners
                double minDistSq =
                    Math.Max(contoursImg.Cols, contoursImg.Rows) * Math.Max(contoursImg.Cols, contoursImg.Rows);
                for (int j = 0; j < 4; j++)
                {
                    double d = (double)(approxCurve[j].X - approxCurve[(j + 1) % 4].X) *
                               (double)(approxCurve[j].X - approxCurve[(j + 1) % 4].X) +
                               (double)(approxCurve[j].Y - approxCurve[(j + 1) % 4].Y) *
                               (double)(approxCurve[j].Y - approxCurve[(j + 1) % 4].Y);
                    minDistSq = Math.Min(minDistSq, d);
                }

                
                if (minDistSq < minContourSizeAllowed * minContourSizeAllowed) continue;

                bool tooNearBorder = false;
                for (int j = 0; j < 4; j++)
                {
                    if (approxCurve[j].X < 3 || approxCurve[j].Y < 3 || 
                        approxCurve[j].X > contoursImg.Cols - 1 - 3 ||
                        approxCurve[j].Y > contoursImg.Rows - 1 - 3)
                        tooNearBorder = true;
                }
                if (tooNearBorder) continue;


                PointF[] points = new PointF[4];
                for (int j = 0; j < 4; j++)
                {
                    points[j] = new PointF((float)approxCurve[j].X, (float)approxCurve[j].Y);
                }
                VectorOfPointF currentCandidate = new VectorOfPointF(points);
                candidatesOut.Push(currentCandidate);
                contoursOut.Push(contours[i]);

                
            }
            
        }

        VectorOfVectorOfPointF _filterTooCloseCandidates(VectorOfVectorOfPointF candidatesIn, VectorOfVectorOfPoint contoursIn)
        {


            //candGroup.resize(candidatesIn.size(), -1);
            int[] candGroup = new int[candidatesIn.Size];
            for (int i = 0; i < candidatesIn.Size; i++) candGroup[i] = -1;

            //vector < vector < unsigned int>> groupedCandidates;
            VectorOfVectorOfInt groupedCandidates = new VectorOfVectorOfInt();
            for (int i = 0; i < candidatesIn.Size; i++)
            {
                for (int j = i + 1; j < candidatesIn.Size; j++)
                {

                    int minimumPerimeter = Math.Min((int)contoursIn[i].Size, (int)contoursIn[j].Size);

                    // fc is the first corner considered on one of the markers, 4 combinations are possible
                    for (int fc = 0; fc < 4; fc++)
                    {
                        double distSq = 0;
                        for (int c = 0; c < 4; c++)
                        {
                            // modC is the corner considering first corner is fc
                            int modC = (c + fc) % 4;
                            distSq += (candidatesIn[i][modC].X - candidatesIn[j][c].X) *
                                      (candidatesIn[i][modC].X - candidatesIn[j][c].X) +
                                      (candidatesIn[i][modC].Y - candidatesIn[j][c].Y) *
                                      (candidatesIn[i][modC].Y - candidatesIn[j][c].Y);
                        }
                        distSq /= 4.0;

                        // if mean square distance is too low, remove the smaller one of the two markers
                        double minMarkerDistancePixels = (double)(minimumPerimeter) * 0.05;
                        if (distSq < minMarkerDistancePixels * minMarkerDistancePixels)
                        {

                            // i and j are not related to a group
                            if (candGroup[i] < 0 && candGroup[j] < 0)
                            {
                                // mark candidates with their corresponding group number
                                candGroup[i] = candGroup[j] = groupedCandidates.Size;

                                // create group
                                VectorOfInt grouped = new VectorOfInt();
                                grouped.Push(new int[1] { i });
                                grouped.Push(new int[1] { j });
                                groupedCandidates.Push(grouped);
                            }
                            // i is related to a group
                            else if (candGroup[i] > -1 && candGroup[j] == -1)
                            {
                                int group = candGroup[i];
                                candGroup[j] = group;

                                // add to group
                                groupedCandidates[group].Push(new int[1] { j });
                            }
                            // j is related to a group
                            else if (candGroup[j] > -1 && candGroup[i] == -1)
                            {
                                int group = candGroup[j];
                                candGroup[i] = group;

                                // add to group
                                groupedCandidates[group].Push(new int[1] { i });
                            }
                        }
                    }
                }
            }

            // save possible candidates

            VectorOfVectorOfPointF biggerCandidates = new VectorOfVectorOfPointF();
            //VectorOfVectorOfPoint biggerContours = new VectorOfVectorOfPoint();

            // save possible candidates
            for (int i = 0; i < groupedCandidates.Size; i++)
            {
                int smallerIdx = groupedCandidates[i][0];
                int biggerIdx = smallerIdx;
                double smallerArea = CvInvoke.ContourArea(candidatesIn[smallerIdx]);
                double biggerArea = smallerArea;

                // evaluate group elements
                for (int j = 1; j < groupedCandidates[i].Size; j++)
                {
                    int currIdx = groupedCandidates[i][j];
                    double currArea = CvInvoke.ContourArea(candidatesIn[currIdx]);

                    // check if current contour is bigger
                    if (currArea >= biggerArea)
                    {
                        biggerIdx = currIdx;
                        biggerArea = currArea;
                    }

                    
                }

                // add contours and candidates
                biggerCandidates.Push(candidatesIn[biggerIdx]);
                //biggerContours.Push(contoursIn[biggerIdx]);
            }


            var candidate = new VectorOfVectorOfPointF();
            candidate.Push(biggerCandidates);
            return candidate;
        }



        

        void Threshold(Mat _in, Mat _out, int winSize)
        {

            if (!(winSize >= 3)) return;
            if (winSize % 2 == 0) winSize++; // win size must be odd
            CvInvoke.AdaptiveThreshold(_in, _out, 255, AdaptiveThresholdType.GaussianC, ThresholdType.Binary, winSize, 7);
        }




        /**
* @brief Convert input image to gray if it is a 3-channels image
*/
        static void ConvertToGrey(Mat _in, Mat _out)
        {

            if (!((_in.Depth == DepthType.Cv8U && _in.NumberOfChannels == 1)
                || (_in.NumberOfChannels == 3)))
                return;

            if (_in.NumberOfChannels == 3)
                CvInvoke.CvtColor((IInputArray)_in, (IOutputArray)_out, ColorConversion.Bgr2Gray);
            else
                _in.CopyTo(_out);
        }



        

        static void reorderCandidatesCorners(ref VectorOfVectorOfPointF candidates)
        {
            PointF[][] pointfs = candidates.ToArrayOfArray();
            candidates.Clear();

            for (int i = 0; i < pointfs.GetLength(0); i++)
            {
                double dx1 = pointfs[i][1].X - pointfs[i][0].X;
                double dy1 = pointfs[i][1].Y - pointfs[i][0].Y;
                double dx2 = pointfs[i][2].X - pointfs[i][0].X;
                double dy2 = pointfs[i][2].Y - pointfs[i][0].Y;
                double crossProduct = (dx1 * dy2) - (dy1 * dx2);

                if (crossProduct < 0.0)
                { // not clockwise direction
                    //swap(candidates[i][1], candidates[i][3]);
                    PointF p = pointfs[i][1];
                    pointfs[i][1] = pointfs[i][3];
                    pointfs[i][3] = p;
                }
            }
            candidates = new VectorOfVectorOfPointF(pointfs);

        }

        private void ShowContours(VectorOfVectorOfPoint contours, VectorOfVectorOfPointF candidates)
        {
                Mat contoursMat = new Mat(greyImage.Size, greyImage.Depth, greyImage.NumberOfChannels);
                contoursMat.SetTo(new MCvScalar(0, 0, 0));
                CvInvoke.Polylines(contoursMat, contours, true, new MCvScalar(255, 255, 255));
                CvInvoke.Imshow("All contours", contoursMat );

                contoursMat.SetTo(new MCvScalar(0, 0, 0));
                VectorOfVectorOfPoint vect = new VectorOfVectorOfPoint();
                for (int i = 0; i < candidates.Size; i++)
                {
                    vect.Push(new VectorOfPoint(Array.ConvertAll(candidates[i].ToArray(), Point.Round)));
                }
                CvInvoke.Polylines(contoursMat, vect, true, new MCvScalar(255, 255, 255));
                CvInvoke.Imshow("approximated squares", contoursMat );
        }

        public void DrawMarkersList(Mat mat, List<OiluMarker> mList, Color c_invalid, Color c_valid, Color c_axis)
        {
            for (int i = 0; i < mList.Count; i++)
            {
                OiluMarker marker = mList[i];
                Color c = Color.Red;
                marker.drawMarker(mat, "" + i, c_invalid, c_valid, c_axis);
            }

        }


    }






}
