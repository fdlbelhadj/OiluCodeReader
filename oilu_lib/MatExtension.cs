using Emgu.CV;
using Emgu.CV.CvEnum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace oilu_lib
{
    public static class MatExtension
    {
        public static int GetValue(this Mat mat, int row, int col)
        {
            //var value = CreateElement(mat.Depth);
            int[] value = new int[1];
            Marshal.Copy(mat.DataPointer + (row * mat.Cols + col) * mat.ElementSize, value, 0, 1);
            return value[0];
        }

        public static void SetValue(this Mat mat, int row, int col, int value)
        {
            int[] values = { value };
            Marshal.Copy(values, 0, mat.DataPointer + (row * mat.Cols + col) * mat.ElementSize, 1);
        }
        public static void SetRowToValue(this Mat mat, int row, int value)
        {
            int[] values = { value };
            for (int i = 0; i < mat.Cols; i++)
            {
                Marshal.Copy(values, 0, mat.DataPointer + (row * mat.Cols + i) * mat.ElementSize, 1);
            }
        }
        //private static int CreateElement(DepthType depthType, int value)
        //{
        //    var element = CreateElement(depthType);
        //    element[0] = value;
        //    return element;
        //}

        //    private static int CreateElement(DepthType depthType)
        //    {
        //        if (depthType == DepthType.Cv8S)
        //        {
        //            return new sbyte[1];
        //        }
        //        if (depthType == DepthType.Cv8U)
        //        {
        //            return new byte[1];
        //        }
        //        if (depthType == DepthType.Cv16S)
        //        {
        //            return new short[1];
        //        }
        //        if (depthType == DepthType.Cv16U)
        //        {
        //            return new ushort[1];
        //        }
        //        if (depthType == DepthType.Cv32S)
        //        {
        //            return new int[1];
        //        }
        //        if (depthType == DepthType.Cv32F)
        //        {
        //            return new float[1];
        //        }
        //        if (depthType == DepthType.Cv64F)
        //        {
        //            return new double[1];
        //        }
        //        return new float[1];
        //    }
        //}
    }
}
