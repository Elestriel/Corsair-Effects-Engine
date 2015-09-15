﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text;

using NAudio.Dsp;

namespace Corsair_Effects_Engine
{
    /// <summary>
    /// Custom Sample Aggregator and FFT Calculator.
    /// </summary>
    /// <remarks>
    /// Ported from http://www.codeproject.com/Articles/990040/MultiWave-a-portable-multi-device-NET-audio-player
    /// </remarks>
    public class SampleAggregator
    {
        // volume
        public event EventHandler<MaxSampleEventArgs> MaximumCalculated;
        private double maxValue;
        private double minValue;

        public int NotificationCount
        {
            get { return m_NotificationCount; }
            set { m_NotificationCount = value; }
        }
        private int m_NotificationCount;
        private int count;

        // FFT
        public event EventHandler<FftEventArgs> FftCalculated;
        public bool PerformFFT
        {
            get { return m_PerformFFT; }
            set { m_PerformFFT = value; }
        }
        private bool m_PerformFFT;
        private PrecisionComplex[] fftBuffer;
        private FftEventArgs fftArgs;
        private int fftPos;
        private int fftLength;
        private int m;

        public SampleAggregator(int fftLength = 1024)
        {
            if (!IsPowerOfTwo(fftLength))
            {
                throw new ArgumentException("FFT Length must be a power of two");
            }
            this.m = Convert.ToInt32(Math.Log(fftLength, 2.0));
            this.fftLength = fftLength;
            this.fftBuffer = new PrecisionComplex[fftLength];
            this.fftArgs = new FftEventArgs(fftBuffer);
        }

        private bool IsPowerOfTwo(int x)
        {
            return (x & (x - 1)) == 0;
        }


        public void Reset()
        {
            count = 0;
            maxValue = 0;
            minValue = 0;
        }

        public void Add(double value)
        {
            if (PerformFFT)
            {
                fftBuffer[fftPos].X = Convert.ToSingle(value * FastFourierTransform.BlackmannHarrisWindow(fftPos, fftBuffer.Length));
                fftBuffer[fftPos].Y = 0;
                fftPos += 1;
                if (fftPos >= fftBuffer.Length)
                {
                    fftPos = 0;
                    // 1024 = 2^10
                    PrecisionFastFourierTransform.FFT(true, m, fftBuffer);
                    if (FftCalculated != null)
                    {
                        FftCalculated(this, fftArgs);
                    }
                }
            }

            maxValue = Math.Max(maxValue, value);
            minValue = Math.Min(minValue, value);
            count += 1;
            if (count >= NotificationCount && NotificationCount > 0)
            {
                if (MaximumCalculated != null)
                {
                    MaximumCalculated(this, new MaxSampleEventArgs(minValue, maxValue));
                }
                Reset();
            }
        }

    }

    public class MaxSampleEventArgs : EventArgs
    {
        [DebuggerStepThrough()]
        public MaxSampleEventArgs(double minValue, double maxValue)
        {
            this.MaxSample = maxValue;
            this.MinSample = minValue;
        }
        public double MaxSample
        {
            get { return m_MaxSample; }
            private set { m_MaxSample = value; }
        }
        private double m_MaxSample;
        public double MinSample
        {
            get { return m_MinSample; }
            private set { m_MinSample = value; }
        }
        private double m_MinSample;
    }

    public class FftEventArgs : EventArgs
    {
        [DebuggerStepThrough()]
        public FftEventArgs(PrecisionComplex[] result)
        {
            this.Result = result;
        }
        public PrecisionComplex[] Result
        {
            get { return m_Result; }
            private set { m_Result = value; }
        }

        private PrecisionComplex[] m_Result;
    }

    public struct PrecisionComplex
    {
        public double X;
        public double Y;
    }

    public sealed class PrecisionFastFourierTransform
    {
        private PrecisionFastFourierTransform()
        {
        }
        /// <summary>
        /// This computes an in-place complex-to-complex FFT 
        /// x and y are the real and imaginary arrays of 2^m points.
        /// </summary>
        public static void FFT(bool forward, int m, PrecisionComplex[] data)
        {
            int n = 0;
            int i = 0;
            int i1 = 0;
            int j = 0;
            int k = 0;
            int i2 = 0;
            int l = 0;
            int l1 = 0;
            int l2 = 0;
            double c1 = 0;
            double c2 = 0;
            double tx = 0;
            double ty = 0;
            double t1 = 0;
            double t2 = 0;
            double u1 = 0;
            double u2 = 0;
            double z = 0;

            // Calculate the number of points
            n = 1;
            for (i = 0; i <= m - 1; i++)
            {
                n *= 2;
            }

            // Do the bit reversal
            i2 = n >> 1;
            j = 0;
            for (i = 0; i <= n - 2; i++)
            {
                if (i < j)
                {
                    tx = data[i].X;
                    ty = data[i].Y;
                    data[i].X = data[j].X;
                    data[i].Y = data[j].Y;
                    data[j].X = tx;
                    data[j].Y = ty;
                }
                k = i2;

                while (k <= j)
                {
                    j -= k;
                    k >>= 1;
                }
                j += k;
            }

            // Compute the FFT 
            c1 = -1f;
            c2 = 0f;
            l2 = 1;
            for (l = 0; l <= m - 1; l++)
            {
                l1 = l2;
                l2 <<= 1;
                u1 = 1f;
                u2 = 0f;
                for (j = 0; j <= l1 - 1; j++)
                {
                    i = j;
                    while (i < n)
                    {
                        i1 = i + l1;
                        t1 = u1 * data[i1].X - u2 * data[i1].Y;
                        t2 = u1 * data[i1].Y + u2 * data[i1].X;
                        data[i1].X = data[i].X - t1;
                        data[i1].Y = data[i].Y - t2;
                        data[i].X += t1;
                        data[i].Y += t2;
                        i += l2;
                    }
                    z = u1 * c1 - u2 * c2;
                    u2 = u1 * c2 + u2 * c1;
                    u1 = z;
                }
                c2 = Convert.ToSingle(Math.Sqrt((1f - c1) / 2f));
                if (forward)
                { c2 = -c2; };
                c1 = Convert.ToSingle(Math.Sqrt((1f + c1) / 2f));
            }

            // Scaling for forward transform 
            if (forward)
            {
                for (i = 0; i <= n - 1; i++)
                {
                    data[i].X /= n;
                    data[i].Y /= n;
                }
            }
        }

        /// <summary>
        /// Applies a Hamming Window
        /// </summary>
        /// <param name="n">Index into frame</param>
        /// <param name="frameSize">Frame size (e.g. 1024)</param>
        /// <returns>Multiplier for Hamming window</returns>
        public static double HammingWindow(int n, int frameSize)
        {
            return 0.54 - 0.46 * Math.Cos((2 * Math.PI * n) / (frameSize - 1));
        }

        /// <summary>
        /// Applies a Hann Window
        /// </summary>
        /// <param name="n">Index into frame</param>
        /// <param name="frameSize">Frame size (e.g. 1024)</param>
        /// <returns>Multiplier for Hann window</returns>
        public static double HannWindow(int n, int frameSize)
        {
            return 0.5 * (1 - Math.Cos((2 * Math.PI * n) / (frameSize - 1)));
        }

        /// <summary>
        /// Applies a Blackman-Harris Window
        /// </summary>
        /// <param name="n">Index into frame</param>
        /// <param name="frameSize">Frame size (e.g. 1024)</param>
        /// <returns>Multiplier for Blackmann-Harris window</returns>
        public static double BlackmannHarrisWindow(int n, int frameSize)
        {
            return 0.35875 - (0.48829 * Math.Cos((2 * Math.PI * n) / (frameSize - 1))) + (0.14128 * Math.Cos((4 * Math.PI * n) / (frameSize - 1))) - (0.01168 * Math.Cos((6 * Math.PI * n) / (frameSize - 1)));
        }
    }
}