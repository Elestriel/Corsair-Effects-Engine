using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Corsair_Effects_Engine
{
    public interface ILight
    {
        Color LightColor { get; set; }
        bool EffectInProgress { get; set; }
    }

    /// <summary>
    /// Lights a key as the specified colour.
    /// </summary>
    /// <param name="lightColor">Colour to light up.</param>
    class LightSwitch : ILight
    {        
        public Color LightColor {
            get 
            {
                TimeSpan Difference = DateTime.Now - StartTime;
                if (Difference.TotalMilliseconds < Duration)
                {
                    return StartColor;
                }
                else 
                { return EndColor; };
            }
            set { }
        }

        public bool EffectInProgress
        {
            get
            {
                TimeSpan Difference = DateTime.Now - StartTime;
                if (Difference.TotalMilliseconds < Duration)
                { return true; }
                else
                { return false; };
            }
            set { }
        }

        private DateTime StartTime;
        private double Duration;
        private Color StartColor;
        private Color EndColor;

        public LightSwitch(Color startColor, Color endColor, double duration)
        {
            this.StartTime = DateTime.Now;
            this.Duration = duration;
            this.StartColor = startColor;
            this.EndColor = endColor;
        }
    }

    class LightFade : ILight
    {
        public Color LightColor
        {
            get
            {
                TimeSpan Difference = DateTime.Now - StartTime;

                if (Difference.TotalMilliseconds < SolidDuration)
                { return StartColor; }
                else if (Difference.TotalMilliseconds < TotalDuration)
                { 
                    double sA, sR, sG, sB, eA, eR, eG, eB;
                    sA = StartColor.A;
                    sR = StartColor.R;
                    sG = StartColor.G;
                    sB = StartColor.B;
                    eA = EndColor.A;
                    eR = EndColor.R;
                    eG = EndColor.G;
                    eB = EndColor.B;

                    byte nA, nR, nG, nB;
                    double StepMultiplier = Math.Abs(Difference.TotalMilliseconds - SolidDuration) / (TotalDuration - SolidDuration);
                    nA = (byte)(sA - ((sA - eA) * StepMultiplier));
                    nR = (byte)(sR - ((sR - eR) * StepMultiplier));
                    nG = (byte)(sG - ((sG - eG) * StepMultiplier));
                    nB = (byte)(sB - ((sB - eB) * StepMultiplier));
                    return Color.FromArgb(nA, nR, nG, nB);
                }
                else
                { return EndColor; };
            }
            set { }
        }

        public bool EffectInProgress
        {
            get
            {
                TimeSpan Difference = DateTime.Now - StartTime;
                if ((Difference.Seconds * 1000 + Difference.Milliseconds) < (TotalDuration))
                { return true; }
                else
                { return false; };
            }
            set { }
        }

        private DateTime StartTime;
        private double SolidDuration;
        private double TotalDuration;
        private Color StartColor;
        private Color EndColor;
        
        public LightFade(Color startColor, Color endColor, double solidDuration, double totalDuration)
        {
            this.StartTime = DateTime.Now;
            this.SolidDuration = solidDuration;
            this.TotalDuration = totalDuration;
            this.StartColor = startColor;
            this.EndColor = endColor;
        }
    }

    class LightSingle : ILight
    {
        public Color LightColor
        {
            get
            { return keyLight; }
            set { }
        }

        public bool EffectInProgress
        {
            get { return false;}
            set { }
        }

        private Color keyLight;

        public LightSingle(Color lightColor) {
            this.keyLight = lightColor;
        }
    }
}
