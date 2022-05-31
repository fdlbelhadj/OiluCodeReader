using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace oilu_lib
{
    class Cumulatvie_H_Histo
    {
        public static int MAX_BANDS_NUMBER_IN_TRIANGLE = 10;


        private readonly Mat src;
        private readonly Tri_histogramVH[]  vHs;
        private byte[] Merged_H_Histo;

        private readonly int srcWidth;
        private readonly int srcHeiht;
        private readonly bool debug;
        private debugWindow dwin;
        public int ID { get; private set; }



        public List<Band> Merged_H_template; // list of zones on the current band that might contain barcode data

        public int totalDetectedBands { get; private set; }

        public Cumulatvie_H_Histo(Mat src, Tri_histogramVH vH0, Tri_histogramVH vH1, Tri_histogramVH vH2, Tri_histogramVH vH3, bool debug = false)
        {
            this.src = src ?? throw new ArgumentNullException(nameof(src));
            vHs = new Tri_histogramVH[4];
            vHs[0] = vH0 ?? throw new ArgumentNullException(nameof(vH0));
            vHs[1] = vH1 ?? throw new ArgumentNullException(nameof(vH1));
            vHs[2] = vH2 ?? throw new ArgumentNullException(nameof(vH2));
            vHs[3] = vH3 ?? throw new ArgumentNullException(nameof(vH3));

            srcWidth = src.Width;
            srcHeiht = src.Height;

            this.debug = debug;
            if (this.debug) dwin = new debugWindow();
            ID = -1;
        }

        public Cumulatvie_H_Histo(Mat src, Tri_histogramVH[] vHs, bool debug = false)
        {
            this.src = src ?? throw new ArgumentNullException(nameof(src));
            this.vHs = vHs ?? throw new ArgumentNullException(nameof(vHs));
            if (vHs[0] == null) throw new ArgumentNullException("first triHisto");
            if (vHs[1] == null) throw new ArgumentNullException("first triHisto");
            if (vHs[2] == null) throw new ArgumentNullException("2nd triHisto");
            if (vHs[3] == null) throw new ArgumentNullException("3rd trihisto");

            srcWidth = src.Width;
            srcHeiht = src.Height;

            this.debug = debug;
            if (this.debug) dwin = new debugWindow();
            ID = -1;
        }

        internal bool isValidOiluMarker()
        {
            MergeTriHistos();
            FindMergd_Template();
            return CheckMarged_Template() == MarkerDtectionError.VALID_MARKER;

        }

        public string[] IdentifyMarker()
        {
            if (isValidOiluMarker() == false) return null;
            string[] bins = new string[4];
            for (int i = 0; i < 4; i++)
            {
                bins[i] = vHs[i].decode_HorizBandsAccordingToTemplate(Merged_H_template);
            }

            
            return bins;
        }


        internal void MergeTriHistos()
        {
            Merged_H_Histo = new byte[srcHeiht/2];
            for (int i = 0; i < Merged_H_Histo.Length; i++)
            {
                Merged_H_Histo[i] = (byte)(vHs[0].Mhh_Bars[i] | vHs[1].Mhh_Bars[i] | vHs[2].Mhh_Bars[i] | vHs[3].Mhh_Bars[i]);
            }
        }

        internal void FindMergd_Template()
        {
            byte[] v = Merged_H_Histo;
            Merged_H_template = new List<Band>();

            int baseLarge = srcWidth;
            int i = 0;
            int indexNewBand = i;
            double sum = 0;

            var prevBarType = getBarType(v[i], 0);

            for (i = 1; i < v.Length; i++)
            {
                var curBarType = (getBarType(v[i], i));

                if (curBarType != prevBarType)
                {
                    Merged_H_template.Add(new Band(baseLarge, indexNewBand, i, sum, prevBarType));

                    baseLarge -= 2 * (i - indexNewBand);
                    prevBarType = curBarType;
                    indexNewBand = i;
                    sum = 0;
                }
                else sum += v[i];

            }
            // the last band ends at the border
            if (indexNewBand != v.Length)
                Merged_H_template.Add(new Band(baseLarge, indexNewBand, v.Length, sum, prevBarType));
        }

        internal void ShowHistos()
        {
            if (debug)
            {
                dwin.showHisto("0 - LEFT Hist plot ", null, vHs[0].Mhh_Bars, Color.Black);
                dwin.showHisto("1 - Bot Hist plot ", null, vHs[1].Mhh_Bars, Color.Red);
                dwin.showHisto("2 - Right Hist plot ", null, vHs[2].Mhh_Bars, Color.Green);
                dwin.showHisto("3 - TOP Hist plot ", null, vHs[3].Mhh_Bars, Color.Blue);

            }
        }

        internal MarkerDtectionError CheckMarged_Template()
        {
            if (Merged_H_template == null || Merged_H_template.Count < 2) return MarkerDtectionError.NO_BANDS_ERR;

            // to correct square contours start
            // delete the 1st band if it is white and its width is low
            if ((Merged_H_template[0].getBandType() == BandType.WhiteBand))//  && (H_Bands[0].getBandWidth() <3))
                Merged_H_template.RemoveAt(0);

            /// To be reviewed !!!!!!!
            // Test if the square is OILU marker 
            // 1- The number of bands is 10 : 5 blacks and 5 whites.
            // 2- ALL THE SIDES ARE BLACK
            // 3- The center is white
            // 4- The alternance is allmost high balcks + very low whites.
            // 6- black bands are almost equal in width
            // 7- proportional ascendant/descendant peaks 
            // 5- the bands have equal average number of pixels

            //if (Merged_H_Bands.Count > MAX_BANDS_NUMBER_IN_TRIANGLE)
            //    return MarkerDtectionError.MAX_BANDS_NUMMBER_ERR;   // TODO: eliminate bands, lateral, with low bandwidth

            totalDetectedBands = Merged_H_template.Count;

            if (Merged_H_template[0].isWhiteBand())
                return MarkerDtectionError.BLACK_SIDES_ERR;         // TODO: check if the marker is inverted

            if (Merged_H_template[totalDetectedBands - 1].isBlackBand()) return MarkerDtectionError.WHITE_CENTER_BAND_ERR;

            // Alternance of PAIR bands
            if (Merged_H_template.Count % 2 != 0) return MarkerDtectionError.BANDS_ALTERN_ERR;
            var prevBandType = Merged_H_template[0].getBandType();
            for (int i = 1; i < totalDetectedBands; i++)
            {
                if (Merged_H_template[i].getBandType() == prevBandType) return MarkerDtectionError.BANDS_ALTERN_ERR;
                else prevBandType = Merged_H_template[i].getBandType();
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
            double prevBandBaseLareg = Merged_H_template[0].getBaseLarge();
            for (int i = 1; i < totalDetectedBands; i++)
            {
                if (Merged_H_template[i].getBaseLarge() > prevBandBaseLareg) return MarkerDtectionError.PROPORTIANL_BASE_WIDTH_ERR;
                else prevBandBaseLareg = Merged_H_template[i].getBaseLarge();
            }

            //Decode Horiz bands in each triangle according to the merged Gabarit



            return MarkerDtectionError.VALID_MARKER;
        }


            
        private BandType getBarType(double v, int irow)
        {
            return v == 1 ? BandType.BlackBand : BandType.WhiteBand;
        }

    }
}
