using System;
using System.Collections.Generic;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using System.Drawing;
using System.Threading.Tasks;
using Emgu.CV.Structure;

namespace oilu_lib
{
    public class MarkerDetector
    {
        private Size markerSize;
        private float m_minContourLengthAllowed;

        Mat m_grayscaleImage;
        Mat m_thresholdImg;
        VectorOfVectorOfPoint m_contours;
        VectorOfPointF m_markerCorners2d;


        public MarkerDetector(int m_minContourLengthAllowed, Size markerSize)
        {
            this.m_minContourLengthAllowed = m_minContourLengthAllowed;
            this.markerSize = markerSize;

            m_markerCorners2d = new VectorOfPointF();
            m_markerCorners2d.Push(new PointF[] {   new PointF(0, 0),
                                                    new PointF(markerSize.Width - 1, 0),
                                                    new PointF(markerSize.Width - 1, markerSize.Height - 1),
                                                    new PointF(0, markerSize.Height - 1)
                                                });
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
        public List<OiluMarker> findMarkers(Mat bgrMat )
        {

            // Convert the image to grayscale
            m_grayscaleImage = new Mat();
            prepareImage(bgrMat, m_grayscaleImage);

            // Make it binary
            m_thresholdImg = new Mat();
            performThreshold(m_grayscaleImage, m_thresholdImg);

            // Detect contours
            m_contours = new VectorOfVectorOfPoint();
            findContours(m_thresholdImg, m_contours, (int)m_minContourLengthAllowed);// m_grayscaleImage.Cols / 5);

            // Find closed contours that can be approximated with 4 points
            var detectedMarkers = new List<OiluMarker>();
            findMarkerCandidates(m_contours, detectedMarkers);

            // Find is them are markers
            detectMarkers(m_grayscaleImage, detectedMarkers);

            // Calcualte their poses
            // estimatePosition(detectedMarkers);

            //sort by id
            //detectedMarkers.Sort(new Comparison)
            // sort(detectedMarkers.begin(), detectedMarkers.end());
            return detectedMarkers;
        }

        public void prepareImage(Mat _in, Mat _out)
        {
            // Convert to grayscale
            if (!((_in.Depth == DepthType.Cv8U && _in.NumberOfChannels == 1)
                || (_in.NumberOfChannels == 3)))
                return;

            if (_in.NumberOfChannels == 3)
                CvInvoke.CvtColor((IInputArray)_in, (IOutputArray)_out, ColorConversion.Bgr2Gray);
            else
                _in.CopyTo(_out);
        }

        public void performThreshold(Mat grayscale, Mat thresholdImg)
        {
            CvInvoke.AdaptiveThreshold(grayscale, thresholdImg, 255, AdaptiveThresholdType.GaussianC, ThresholdType.BinaryInv, 7, 7);
        }

        void findContours(Mat thresholdImg, VectorOfVectorOfPoint contours, int minContourPointsAllowed)
        {
            Mat thresholdImgCopy = new Mat();
            thresholdImg.CopyTo(thresholdImgCopy);

            VectorOfVectorOfPoint allContours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(thresholdImgCopy, allContours, null, RetrType.List, ChainApproxMethod.ChainApproxNone);

            contours.Clear();
            for (int i = 0; i < allContours.Size; i++)
            {
                int contourSize = allContours[i].Size;
                if (contourSize > minContourPointsAllowed)
                {
                    contours.Push(allContours[i]);
                }
            }

            ShowContours(contours);
        }

        void findMarkerCandidates(VectorOfVectorOfPoint contours, List<OiluMarker> detectedMarkers)
        {
            VectorOfPoint approxCurve = new VectorOfPoint();

            List<OiluMarker> possibleMarkers = new List<OiluMarker>();

            // For each contour, analyze if it is a paralelepiped likely to be the marker
            for (int i = 0; i < contours.Size; i++)
            {
                // Approximate to a poligon
                CvInvoke.ApproxPolyDP(contours[i], approxCurve, (double)(contours[i].Size) * 0.03, true);

                // We interested only in polygons that contains only four vertices
                if (approxCurve.Size != 4 || !CvInvoke.IsContourConvex(approxCurve))
                    continue;

                // Ensure that the distace between consecutive points is large enough
                float minDist = 1e10f;
                for (int j = 0; j < 4; j++)
                {
                    Point vec = Point.Subtract(approxCurve[j], new Size(approxCurve[(j + 1) % 4]));
                    float squaredDistance = vec.X * vec.X + vec.Y * vec.Y;
                    minDist = Math.Min(minDist, squaredDistance);
                }

                // Check that distance is not very small
                if (minDist > m_minContourLengthAllowed* m_minContourLengthAllowed)
                {
                    VectorOfPointF corners = new VectorOfPointF();
                    for(int j = 0; j < 4; j++)
                    {
                        corners.Push(new PointF[] { new PointF(approxCurve[j].X, approxCurve[j].Y) });
                    }
                    OiluMarker m = new OiluMarker(corners,markerSize, "");
                    possibleMarkers.Add(m);
                }
            }

            //sort the points in anti-clockwise order
            for (int i = 0; i < possibleMarkers.Count; i++)
            {
                OiluMarker marker = possibleMarkers[i];

                //trace a line between the first and second point.
                //if the thrid point is at the right side, then the points are anti-clockwise
                PointF v1 = new PointF(marker.cornersInImg[1].X - marker.cornersInImg[0].X,
                                       marker.cornersInImg[1].Y - marker.cornersInImg[0].Y);
                PointF v2 = new PointF(marker.cornersInImg[2].X - marker.cornersInImg[0].X,
                                       marker.cornersInImg[2].Y - marker.cornersInImg[0].Y);

                double o = (v1.X * v2.Y) - (v1.Y * v2.X);

                if (o < 0.0)         //if the third point is in the left side, then sort in anti-clockwise order
                {
                    // swap marker points 1 and 3
                    marker.SwapPoints(1, 3);
                }
            }

            // remove these elements whose corners are too close to each other first detect candidates
            List<Pair> tooNearCandidates = new List<Pair>();
            for (int i = 0; i < possibleMarkers.Count; i++)
            {
                OiluMarker m1 = possibleMarkers[i];

                //calculate the average distance of each corner to the nearest corner of the other marker candidate
                for (int j = i + 1; j < possibleMarkers.Count; j++)
                {
                    OiluMarker m2 = possibleMarkers[j];

                    float distSquared = 0;

                    for (int c = 0; c < 4; c++)
                    {
                        PointF v = m1.cornersInImg[c] - new SizeF(m2.cornersInImg[c]);
                        distSquared += v.X * v.X + v.Y * v.Y;
                    }

                    distSquared /= 4;

                    if (distSquared < 100)
                    {
                        tooNearCandidates.Add( new Pair(i, j));
                    }
                }
            }

            //mark for removal the element of  the pair with smaller perimeter
            bool[] removalMask = new bool[possibleMarkers.Count];
            for (int i = 0; i < possibleMarkers.Count; i++) removalMask[i] = false;

            for (int i = 0; i < tooNearCandidates.Count; i++)
            {
                float p1 = possibleMarkers[tooNearCandidates[i].First].perimeter();
                float p2 = possibleMarkers[tooNearCandidates[i].Second].perimeter();

                int removalIndex;
                if (p1 > p2)
                    removalIndex = tooNearCandidates[i].Second;
                else
                    removalIndex = tooNearCandidates[i].First;

                removalMask[removalIndex] = true;
            }

            // Return candidates
            detectedMarkers.Clear();
            for (int i = 0; i < possibleMarkers.Count; i++)
            {
                if (!removalMask[i])
                    detectedMarkers.Add(possibleMarkers[i]);
            }
        }

        void detectMarkers(Mat grayscale, List<OiluMarker> detectedMarkers)
        {
            Mat canonicalMarker = new Mat();

            List<OiluMarker> goodMarkers = new List<OiluMarker>();

            // Identify the markers
            for (int i = 0; i < detectedMarkers.Count; i++)
            {
                OiluMarker marker = detectedMarkers[i];

                // Find the perspective transfomation that brings current marker to rectangular form
                Mat M = CvInvoke.GetPerspectiveTransform(marker.cornersInImg, m_markerCorners2d);

                // Transform image to get a canonical marker image
                CvInvoke.WarpPerspective(grayscale, canonicalMarker, M, markerSize);

                int id = -1; // OiluMarker.getMarkerId(canonicalMarker);
                if (id != -1)
                {
                    marker.Id = "" +id;
                    //sort the points so that they are always in the same order no matter the camera orientation
                    //rotate(marker.points.begin(), marker.points.begin() + 4 - nRotations, marker.points.end());

                    goodMarkers.Add(marker);
                }
            }

            //refine using subpixel accuracy the corners
            if (goodMarkers.Count > 0)
            {
                PointF[] preciseCorners = new PointF[4 * goodMarkers.Count];

                for (int i = 0; i < goodMarkers.Count; i++)
                {
                    OiluMarker marker = goodMarkers[i];

                    for (int c = 0; c < 4; c++)
                    {
                        preciseCorners[i * 4 + c] = marker.cornersInImg[c];
                    }
                }

                VectorOfPointF preciseCornersVect = new VectorOfPointF(preciseCorners);
                CvInvoke.CornerSubPix(grayscale, preciseCornersVect, new Size(5, 5), new Size(-1, -1), new MCvTermCriteria(10,0.1));

                //copy back
                for (int i = 0; i < goodMarkers.Count; i++)
                {
                    OiluMarker marker = goodMarkers[i];
                    PointF[] ptsf = new PointF[] {  preciseCornersVect[i * 4 + 0], preciseCornersVect[i * 4 + 1],
                                                    preciseCornersVect[i * 4 + 2], preciseCornersVect[i * 4 + 3]};
                    marker.cornersInImg.Clear();
                    marker.cornersInImg = new VectorOfPointF(ptsf);
                }
            }

            detectedMarkers = goodMarkers;
        }

        private void ShowContours(VectorOfVectorOfPoint contours)
        {
            Mat contoursMat = new Mat(m_grayscaleImage.Size, m_grayscaleImage.Depth, m_grayscaleImage.NumberOfChannels);
            contoursMat.SetTo(new MCvScalar(0, 0, 0));
            CvInvoke.Polylines(contoursMat, contours, true, new MCvScalar(255, 255, 255));
            CvInvoke.Imshow("All contours", contoursMat);

    
        }

        //public void estimatePosition(List<Marker> detectedMarkers)
        //{
        //    for (int i = 0; i < detectedMarkers.Count; i++)
        //    {
        //        Marker  m = detectedMarkers[i];
        //        Mat Rvec = new Mat();
        //        Mat Tvec = new Mat();
        //        Mat_<float> Tvec;
        //        Mat raux, taux;
        //        solvePnP(m_markerCorners3d, m.points, camMatrix, distCoeff, raux, taux);
        //        raux.convertTo(Rvec, CV_32F);
        //        taux.convertTo(Tvec, CV_32F);

        //        Mat_<float> rotMat(3, 3);
        //    Rodrigues(Rvec, rotMat);

        //    // Copy to transformation matrix
        //    m.transformation = Transformation();

        //    for (int col = 0; col < 3; col++)
        //    {
        //        for (int row = 0; row < 3; row++)
        //        {
        //            m.transformation.r().mat[row][col] = rotMat(row, col); // Copy rotation component
        //        }
        //        m.transformation.t().data[col] = Tvec(col); // Copy translation component
        //    }

        //    // Since solvePnP finds camera location, w.r.t to marker pose, to get marker pose w.r.t to the camera we invert it.
        //    m.transformation = m.transformation.g
        //    }

        private class Pair
        {
            public int First { get; private set; }
            public int Second { get; private set; }
            public Pair(int first, int second)
            {
                this.First = first;
                this.Second = second;
            }

            public override bool Equals(object obj)
            {
                if (Object.ReferenceEquals(this, obj))
                {
                    return true;
                }
                Pair instance = obj as Pair;
                if (instance == null)
                {
                    return false;
                }
                return this.First == instance.First && this.Second == instance.Second ||
                       this.First == instance.Second && this.Second == instance.First;
            }

            public override int GetHashCode()
            {
                return this.First.GetHashCode() ^ this.Second.GetHashCode();
            }
        }
    }
}
