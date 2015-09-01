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
    /// Lights a key as the first colour for the duration and then changes to the second colour.
    /// </summary>
    /// <param name="startColor">Colour to light up.</param>
    /// <param name="endColor">Colour to change to when time is up.</param>
    /// <param name="duration">Time in milliseconds for the light to stay lit before reverting to off.</param>
    class LightSolid : ILight
    {        
        public Color LightColor {
            get 
            {
                TimeSpan Difference = DateTime.Now - StartTime;
                if ((Difference.Seconds * 1000 + Difference.Milliseconds) < (Duration)) 
                { return StartColor; }
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
                if ((Difference.Seconds * 1000 + Difference.Milliseconds) < (Duration))
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

        public LightSolid(Color startColor, Color endColor, double duration)
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
                double DifferenceMS = Difference.Seconds * 1000 + Difference.Milliseconds;
                if ((DifferenceMS) < (SolidDuration))
                { return StartColor; }
                else if ((DifferenceMS) < (TotalDuration))
                { 
                    double sR, sG, sB, eR, eG, eB;
                    sR = StartColor.R;
                    sG = StartColor.G;
                    sB = StartColor.B;
                    eR = EndColor.R;
                    eG = EndColor.G;
                    eB = EndColor.B;

                    byte nR, nG, nB;
                    double StepMultiplier = (DifferenceMS - SolidDuration) / (TotalDuration - SolidDuration);
                    nR = (byte)(sR - ((sR - eR) * StepMultiplier));
                    nG = (byte)(sG - ((sG - eG) * StepMultiplier));
                    nB = (byte)(sB - ((sB - eB) * StepMultiplier));
                    return Color.FromRgb(nR, nG, nB);
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
