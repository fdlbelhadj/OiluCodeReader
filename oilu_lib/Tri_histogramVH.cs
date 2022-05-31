using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace oilu_lib
{
    public class Tri_histogramVH
    {
        //TODO : include these in params
        public static int MAX_BANDS_NUMBER_IN_TRIANGLE = 10;
        public static int MAX_BANDS_WIDTH_DIFF = 2;
        public static int MAX_BANDS_SIZE_DIFF = MAX_BANDS_WIDTH_DIFF * 255;
        public static float MIN_BAND_SIZE_FACTOR = 0.550F;
        public static int BLACK_VALUE = 255;    // inverted
        public static int WHITE_VALUE = 0;      //
        public static float EXPECTED_BAR_SUM_FACTOR = 3.0f/6;

        VectorOfFloat cumulHoriz, cumulVert;
        public byte[] Mhh_Bars;

        List<Band> H_Bands;
        List<Band> V_Bands; // list of zones on the current band that might contain barcode data
        readonly Mat src;

        //int BlackBandWidth = -1;
        private float WhiteBandWidth = -1;
        private float BlackBandWidth = -1;
        private readonly int srcWidth;
        private readonly bool debug;
        private debugWindow dwin;

        public Mat Src => src;

        public List<Band> H_template { get => H_Bands; set => H_Bands = value; }

        public Tri_histogramVH(Mat src, bool debug = true)
        {
            this.src = src;
            srcWidth  = src.Width;
            this.debug = debug;
            if(debug) dwin = new debugWindow();
        }

        public void CalculateDirectionalHisto( Direction direct)
        {
            if (src.IsEmpty) return;

            if (direct != Direction.V_Direction)
            {
                var cum = new Mat();
                CvInvoke.Reduce(src, cum, ReduceDimension.SingleCol, ReduceType.ReduceSum, DepthType.Cv32F);
                cumulHoriz = new VectorOfFloat();
                cum.CopyTo(cumulHoriz);
            }

            if (direct != Direction.H_Direction)
            {
                var cum = new Mat();
                CvInvoke.Reduce(src, cum, ReduceDimension.SingleRow, ReduceType.ReduceSum, DepthType.Cv32F);
                cumulVert = new VectorOfFloat();
                cum.CopyTo(cumulVert);
            }
        }

        public void GetHorizontalBands()
        {
            VectorOfFloat v = cumulHoriz;
            if (v.Size == 0) return;

            H_Bands = new List<Band>();
            Mhh_Bars = new byte[v.Size];
            for (int k = 0; k < v.Size; k++) Mhh_Bars[k] = 1;
            
            int baseLarge = srcWidth;
            int i = 0;
            int indexNewBand = i;
            double sum = 0;

            var prevBarType = getBarType(v[i], 0);
            if (prevBarType == BandType.WhiteBand ) Mhh_Bars[i] = 0;//MatExtension.SetRowToValue(src, i, WHITE_VALUE);
                
            for (i = 1; i < v.Size; i++)
            {
                var curBarType = getBarType(v[i], i);
                if (curBarType == BandType.WhiteBand) Mhh_Bars[i] = 0;//MatExtension.SetRowToValue(src, i, WHITE_VALUE);
                    
                if (curBarType != prevBarType)
                {
                    H_Bands.Add(new Band(baseLarge, indexNewBand, i, sum, prevBarType ));

                    baseLarge -= 2 * (i - indexNewBand);
                    prevBarType = curBarType;
                    indexNewBand = i;
                    sum = 0;
                }
                else sum += v[i];

            }
            // the last band ends at the border
            if (indexNewBand != v.Size)
                H_Bands.Add(new Band(baseLarge, indexNewBand, v.Size, sum , prevBarType ));
        }

        public void drawBandsInSrc(string str)
        {
            Mat mat = Mat.Zeros(src.Width, src.Height, DepthType.Cv8S, 3);
            src.ConvertTo(mat, DepthType.Cv8U);

            foreach (Band band in H_Bands)
            {
                CvInvoke.Line(mat, new Point(0, band.Start), new Point(src.Width, band.Start), new Emgu.CV.Structure.MCvScalar(255, 0,0));
            }

            CvInvoke.Imshow(str, mat);
            CvInvoke.WaitKey();
        }



        internal void deNoiseHisto(Direction direction)
        {
            if (direction != Direction.V_Direction) CvInvoke.MedianBlur(cumulHoriz, cumulHoriz, 3);
            if (direction != Direction.H_Direction) CvInvoke.MedianBlur(cumulVert, cumulVert, 3);
        }

        internal MarkerDtectionError Analyze_HorizBands()
        {
            if (H_Bands == null || H_Bands.Count < 2) return MarkerDtectionError.NO_BANDS_ERR;

            // delete the 1st band if it is white and its width is low
            // to correct square contours start
            if ((H_Bands[0].getBandType() == BandType.WhiteBand))//  && (H_Bands[0].getBandWidth() <3))
                H_Bands.RemoveAt(0);

            /// To be reviewed !!!!!!!
            // Test if the square is OILU marker 
            // 1- The number of bands is 10 : 5 blacks and 5 whites.
            // 2- ALL THE SIDES ARE BLACK
            // 3- The center is white
            // 4- The alternance is allmost high balcks + very low whites.
            // 6- black bands are almost equal in width
            // 7- proportional ascendant/descendant peaks 
            // 5- the bands have equal average number of pixels

            if (H_Bands.Count > MAX_BANDS_NUMBER_IN_TRIANGLE)
                return MarkerDtectionError.MAX_BANDS_NUMMBER_ERR;   // TODO: eliminate bands, lateral, with low bandwidth
            
            if (H_Bands[0].isWhiteBand())
                return MarkerDtectionError.BLACK_SIDES_ERR;         // TODO: check if the marker is inverted
            
            if (H_Bands[H_Bands.Count - 1].isBlackBand()) return MarkerDtectionError.WHITE_CENTER_BAND_ERR;

            // Alternance of PAIR bands
            if (H_Bands.Count % 2 != 0) return MarkerDtectionError.BANDS_ALTERN_ERR;
            var prevBandType = H_Bands[0].getBandType();
            for (int i = 1; i < H_Bands.Count; i++)
            {
                if (H_Bands[i].getBandType() == prevBandType) return MarkerDtectionError.BANDS_ALTERN_ERR;
                else prevBandType = H_Bands[i].getBandType();
            }

            // 6 black bands are equal in widths
            //int prevBandWidth = H_Bands[0].getBandWidth();
            //for (int i = 2; i < H_Bands.Count; i += 2)
            //{
            //    if (Math.Abs(H_Bands[i].getBandWidth() - prevBandWidth) > MAX_BANDS_WIDTH_DIFF)
            //        return MarkerDtectionError.BANDS_WIDTH_ERR;
            //    else prevBandWidth = H_Bands[i].getBandWidth();
            //}

            // proportial descendant black bands Baselarge
            double prevBandBaseLareg = H_Bands[0].getBaseLarge();
            for (int i = 1; i < H_Bands.Count; i ++)
            {
                if (H_Bands[i].getBaseLarge() > prevBandBaseLareg) return MarkerDtectionError.PROPORTIANL_BASE_WIDTH_ERR;
                else prevBandBaseLareg = H_Bands[i].getBaseLarge();
            }

            // get whiteband averages
            float sum = 0;
            for (int i = 1; i < H_Bands.Count; i += 2) sum += H_Bands[i].getBandWidth();
            WhiteBandWidth =  sum / (MAX_BANDS_NUMBER_IN_TRIANGLE - H_Bands.Count / 2);

            sum = 0;
            for (int i = 0; i < H_Bands.Count; i += 2) sum += H_Bands[i].getBandWidth();
            BlackBandWidth = sum / ( H_Bands.Count / 2);


            return MarkerDtectionError.VALID_MARKER;
        }


        internal MarkerDtectionError isValid_OILU_Triangle()
        {
            //1-  start by calculating horizontal histo

            CalculateDirectionalHisto(Direction.H_Direction);
            deNoiseHisto(Direction.H_Direction);
            #region debug and display
            if (debug)
            {
                debugWindow.DisplayMat(src);
                dwin.showHisto("Horizontal Hist plot ", null, getCumuls(Direction.H_Direction), Color.Black); 
            }
            #endregion
            GetHorizontalBands();

            //2- Calculate vertical histo
            CalculateDirectionalHisto(Direction.V_Direction);
            deNoiseHisto(Direction.V_Direction);
            #region debug and display
            if (debug)
            {
                // dwin.DisplayMat(src);
                dwin = new debugWindow();
                dwin.showHisto("Horiz and Vert Cum Histos plot", null, getCumuls(Direction.V_Direction), Color.Red);

            }
            #endregion


            var ret = Analyze_HorizBands();
            if (ret != MarkerDtectionError.VALID_MARKER) return ret;

            int nb_H_BlackBands = 0;
            ret = decode_HorizBands(ref nb_H_BlackBands);
            if (ret != MarkerDtectionError.VALID_MARKER)
                return ret;

            // analyze vertical histo
            //ret = Analyze_Vert_Bands();
            //if (nb_H_BlackBands != V_Bands.Count) return MarkerDtectionError.NO_CORRESPONDANCE_H_V;

            return MarkerDtectionError.VALID_MARKER;
        }

        public string getTriangleBins()
	    {
            string idd = "";
            for (int i = 0; i < H_Bands.Count; i++)
            {
                var band = H_Bands[i];
                if(band.getBandType() == BandType.BlackBand)
                    idd += band.bins;
                else
                {
                    for (int j = 1; j < band.bins.Length; j+=2)
                    {
                        idd += band.bins[j];
                    }
                }

            }
            return idd;
	    }

        public MarkerDtectionError decode_HorizBands(ref int nbBins)
        {
            if (H_Bands == null) return MarkerDtectionError.NO_BANDS_ERR;

            nbBins = 0;
            int i = 0;
            for (; i < H_Bands.Count; )//i++
            {
                Band band = H_Bands[i];
                //if (band.isBlackBand())     // IT should CONTAIN ONLY ONE BEAN = 1, elsewhere
                {                           // CHECK THE CASE 
                    if ((band.getBandSize() > band.getMinSize() * MIN_BAND_SIZE_FACTOR)|| 
                        (band.getBandWidth() >= BlackBandWidth * MIN_BAND_SIZE_FACTOR)) band.bins = "1";
                    else
                        band.bins = "0";    // TODO : return Error
                    nbBins++;
                }
                i++;
                band = H_Bands[i];
                //if (band.isWhiteBand())
                {
                    // TODO : DETECTANY ANOMALY FROM band size
                    double d = 1.0 * band.getBandWidth() / WhiteBandWidth;
                    int nb = (int)Math.Round(d);
                    // todo : if treating the last band ; conisder all resting bands.
                    if (i == H_Bands.Count - 1)
                        //nb = (int) Math.Truncate(d); 
                        nb = MAX_BANDS_NUMBER_IN_TRIANGLE - nbBins;
                    for (int j = 0; j < nb; j++) band.bins += "0";
                    nbBins += nb;
                }
                i++;

            }

            if (nbBins != MAX_BANDS_NUMBER_IN_TRIANGLE) 
                return MarkerDtectionError.BANDS_SIZE_ERR;
            return MarkerDtectionError.VALID_MARKER;
        }

        public string decode_HorizBandsAccordingToTemplate(List<Band> template = null)
        {
            if (template == null) return "-1";

            string Bins = "";
            
            for (int i = 0; i < template.Count; i+=2)
            {
                Band band = template[i];
                                           
                    int s = 0; // To check the number of included bins
                    for (int j = band.Start; j < band.End; j++)
                    {
                        s += Mhh_Bars[j];
                    }

                    if ( s >= band.getBandWidth() * MIN_BAND_SIZE_FACTOR) Bins += "1";
                    else
                        Bins += "0";    // TODO : return Error
            }

            return Bins;
        }

        private MarkerDtectionError Check_Vert_Bands()
        {
            // 
            if (cumulVert == null) return MarkerDtectionError.NO_BANDS_ERR;
            int expected_V_BandsNbr = (int)Math.Round(1.0 * cumulVert.ToArray().Max() / (BlackBandWidth * 255));

            V_Bands = new List<Band>();
            float previ = cumulVert[0];
            float prevj = cumulVert[cumulVert.Size - 1];
            if (previ - prevj > MAX_BANDS_SIZE_DIFF) return MarkerDtectionError.VERT_SYM_ERR;
            // TODO : make VERT_HISTO_EXPLOR_STEP dependent to the blackbandwith
            for (int i = 1; i < cumulVert.Size / 2; i++)
            {
                // CHECK THE SYMETRIE CONDITION
                if (cumulVert[i] - cumulVert[cumulVert.Size - 1 - i] > MAX_BANDS_SIZE_DIFF)
                    return MarkerDtectionError.VERT_SYM_ERR;
                // CHECK THE PROPRTIONAL INCREASING AND SYMETRIC CONDITION
                if (cumulVert[i] - previ < -MAX_BANDS_SIZE_DIFF ||
                    cumulVert[cumulVert.Size - 1 - i] - prevj < -MAX_BANDS_SIZE_DIFF)
                    return MarkerDtectionError.PROPORTIANL_SIZE_ERR;

                int L = 0; int ss = i;
                // COUNT THE NUMBER OF EQUAL BINS
                while ((i < cumulVert.Size / 2 ) && cumulVert[i] - previ == 0 && 
                        cumulVert[cumulVert.Size - 1 - i] - prevj == 0)
                {
                    L++; 
                    previ = cumulVert[i];
                    prevj = cumulVert[cumulVert.Size - 1-i];
                    i++;
                }
                if (L > 1 )//&&  Math.Abs(L - BlackBandWidth) <= 2
                {
                    // new band detected
                    V_Bands.Add( new Band(-1, ss, cumulVert.Size - ss - 1, L, BandType.VerticalBand));
                }
                else
                {
                    previ = cumulVert[i];
                    prevj = cumulVert[cumulVert.Size - 1 - i];
                }
            }

            return 
                //V_Bands.Count == expected_V_BandsNbr ? 
                MarkerDtectionError.VALID_MARKER
                //: MarkerDtectionError.INVALID_EXPECTED_VBANDS_NBR
                ;
        }

        internal void ThresholdHorizontalHisto(int th = 1530)
        {
            if (cumulHoriz == null) return;

            float[] ar = cumulHoriz.ToArray();

            for (int i = 0; i < cumulHoriz.Size; i++)
            {
                if (ar[i] < th) ar[i] = 0;
            }
            cumulHoriz.Clear();
            cumulHoriz = new VectorOfFloat(ar);
        }

        internal float[] getCumuls(Direction direction)
        {
            if (direction == Direction.H_Direction) return cumulHoriz.ToArray();
            else return cumulVert.ToArray();
        }

        private BandType getBarType(double v, int irow)
        {
            /// ..............
            ///  ............
            ///   .........
            ///     .   .   <---- to be eliminated   
            // return v  > threshold; 
            
            float expected_Bar_Sum = 255 * (srcWidth - 2 * irow);
            float thresh = expected_Bar_Sum * EXPECTED_BAR_SUM_FACTOR;
            if (thresh < 255 * 6) 
                thresh = 6 * 255;
            return v > thresh ? BandType.BlackBand : BandType.WhiteBand;
        }

    }

    public enum BandType
    {
        BlackBand = 0,
        WhiteBand,
        VerticalBand
    }

    public class Band
    {
        private readonly double size;
        private readonly BandType type;
        private readonly int BaseLarge;

        public string bins { get; set; }
        public int Start { get; private set; }
        public int End { get; private set; }

        public Band(int BaseLarge, int Start, int End, double size, BandType type)
        {
            this.Start = Start;
            this.End = End;
            this.size = size;
            this.type = type;
            this.BaseLarge = BaseLarge;
            bins = "";
        }

        public BandType getBandType()
        {
            return type;
        }

        internal int getBandWidth()
        {
            return End - Start;
        }

        internal bool isWhiteBand()
        {
            return type == BandType.WhiteBand;
        }

        internal bool isBlackBand()
        {
            return type == BandType.BlackBand;
        }

        internal double getBandSize()
        {
            return size;
        }

        internal int getBaseLarge()
        {
            return BaseLarge;
        }

        internal int getMinSize()
        {
            int s = 0;
            int bw = End - Start;
            if (type != BandType.WhiteBand)
                s = (BaseLarge - 2 * bw) * bw * 255; //    *bw / bw

            return s;
        }
       
    }

    public enum Direction
    {
        H_Direction,
        V_Direction,
        All_Directions
    }

    public enum MarkerDtectionError
    {
        VALID_MARKER = 0,
        MAX_BANDS_NUMMBER_ERR,
        BLACK_SIDES_ERR,
        WHITE_CENTER_BAND_ERR,
        BANDS_ALTERN_ERR,
        BANDS_SIZE_ERR,
        BANDS_WIDTH_ERR,
        PROPORTIANL_BASE_WIDTH_ERR,
        NO_BANDS_ERR,
        PROPORTIANL_SIZE_ERR,
        VERT_SYM_ERR,
        INVALID_EXPECTED_VBANDS_NBR,
        NO_CORRESPONDANCE_H_V
    }
}
