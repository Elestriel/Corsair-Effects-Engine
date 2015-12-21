using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;

namespace Corsair_Effects_Engine
{
    public class Engine
    {
        // RawInput
        public static RawInputHook InputHook = new RawInputHook();
        static RawInputKeyCodes InputKeys = new RawInputKeyCodes();

        // Public thread control variables
        public bool PauseEngine = false;
        public bool RunEngine = false;
        public bool RestartEngine = false;

        // Thread timing calculations
        private DateTime loopStart;
        private DateTime loopEnd;
        private TimeSpan loopTime;
        private double desiredFrameTime;
        private double sleepTime;

        // Pointers to the keyboard and mouse HIDs
        private IntPtr KeyboardPointer;
        private IntPtr MousePointer;

        // Key Data
        private static KeyData[] BackgroundKeys = new KeyData[149];
        private static KeyData[] ForegroundKeys = new KeyData[149];
        private KeyData[] ReactiveKeys = new KeyData[149];
        private static KeyData[] SpectroKeys = new KeyData[149];
        private KeyData[] Keys = MainWindow.keyData;

        // CPU Performance
        CpuUsageClass CpuUsage = new CpuUsageClass();

        // Effect variables
        private double BackgroundAnim = 0;
        public int HeatmapHighestStrikeCount = 0;
        public int[] HeatmapStrikeCount = new int[149];
        private int BreatheStep = 0;
        private DateTime BreatheStartTime;
        Random rnd = new Random();


        // CLEAN THIS UP!
        private string LastForegroundEffect = "";
        private string LastBackgroundEffect = "";
        //private string LastStaticProfile = "";

        // NAudio Stuff
        private static WasapiCapture audioCapture;
        private static SampleAggregator sampleAggregator;
        private static bool CalculateFFT;
        private static int captureSampleRate;
        private static int lowestBin;
        private static int highestBin;
        private static float[] spectroRainbowPositions;
        private static System.Drawing.Color[] spectroRainbowColors;
        private static byte mouseIntensity = 0;
        int refKey = 140;

        

        public Engine()
        {
            InputHook.OnRawInputFromKeyboard += InputFromKeyboard;
        }

        public void Start()
        {
            UpdateStatusMessage.NewMessage(5, "Initializing Engine.");

            bool NoEffects = false;

            EngineComponents.InitDevices DeviceInit = new EngineComponents.InitDevices();
            EngineComponents.DeviceOutput Output = new EngineComponents.DeviceOutput();
            EngineComponents.SdkOutput sdkOutput = new EngineComponents.SdkOutput();
            bool sdkInitSuccess = sdkOutput.InitializeSdk();
            if (!sdkInitSuccess) { Properties.Settings.Default.OptUseSdk = false; }

            for (int i = 0; i < 149; i++)
            {
                BackgroundKeys[i] = new KeyData();
                ForegroundKeys[i] = new KeyData();
                ReactiveKeys[i] = new KeyData();
                SpectroKeys[i] = new KeyData();
            }


            ClearAllKeys();

            while (RunEngine)
            {
                UpdateStatusMessage.NewMessage(5, "Initializing Devices.");
                // Initialize keyboard
                KeyboardPointer = DeviceInit.GetKeyboardPointer();

                // Set canvas size
                switch (Properties.Settings.Default.KeyboardModel)
                {
                    case "K65-RGB":
                        KeyboardMap.CanvasWidth = 76;
                        break;
                    case "K70-RGB":
                    case "STRAFE":
                    case "STRAFE-RGB":
                        KeyboardMap.CanvasWidth = 92;
                        break;
                    case "K95-RGB":
                        KeyboardMap.CanvasWidth = 104;
                        break;
                    default:
                        KeyboardMap.CanvasWidth = 3;
                        break;
                }

                // Initialize mouse
                MousePointer = DeviceInit.GetMousePointer();

                // Initialize background animation sync
                DateTime lastResetTime = DateTime.Now;
                TimeSpan timeDifference;
                double timeDifferenceMS;

                // Creates handles for NAudio
                NAudio_Initialize();

                RainbowSpectroPositions();

                UpdateStatusMessage.NewMessage(5, "Initialization Complete.");

                while (!PauseEngine && RunEngine && !RestartEngine)
                {
                    // Grab the time now
                    loopStart = DateTime.Now;

                    // Update animation sync
                    timeDifference = DateTime.Now - lastResetTime;
                    timeDifferenceMS = timeDifference.TotalMilliseconds;

                    BackgroundAnim = (timeDifferenceMS / Properties.Settings.Default.BackgroundRepeatTime) * (double)KeyboardMap.CanvasWidth;
                    if (timeDifferenceMS > Properties.Settings.Default.BackgroundRepeatTime)
                    {
                        lastResetTime = DateTime.Now;
                        BackgroundAnim = 0;
                    };

                    // Render static layer

                    // Render background layer
                    RenderBackground();

                    // Render foreground layer

                    if (Properties.Settings.Default.ForegroundEffect == "Spectrograph" &&
                        Properties.Settings.Default.ForegroundEffectEnabled)
                    { CalculateFFT = true; }
                    else { CalculateFFT = false; }
                    RenderForeground();

                    // Blend layers
                    BlendLayers();

                    // Output frame to keyboard preview
                    bool outputFrame = Properties.Settings.Default.BackgroundEffectEnabled ||
                                        Properties.Settings.Default.ForegroundEffectEnabled ||
                                        Properties.Settings.Default.StaticProfileEnabled;

                    // As long as an effect is enabled, output a frame
                    if (outputFrame)
                    {
                        NoEffects = false;
                    }
                    else
                    {
                        if (!NoEffects) // Output a black frame
                        {
                            ClearAllKeys();
                            NoEffects = true;
                            outputFrame = true;
                        }
                    }

                    if (outputFrame)
                    {
                        // Use direct control
                        if (!Properties.Settings.Default.OptUseSdk)
                        {

                            // Output frame to devices
                            if (Properties.Settings.Default.Opt16MColours)
                            { Output.UpdateKeyboard16M(KeyboardPointer, Keys); }
                            else { Output.UpdateKeyboard(KeyboardPointer, Keys); };

                            Output.UpdateMouse(MousePointer, Keys);
                        }
                        // Use SDK control
                        else
                        {
                            sdkOutput.UpdateKeyboard(Keys);
                            // Methods aren't yet made in the wrapper
                            //sdkOutput.UpdateMouse(Keys);
                            //sdkOutput.UpdateHeadset(System.Drawing.Color.Red);
                        }
                        if (NoEffects) { outputFrame = false; };
                    }

                    // Calculate the cycle time and sleep until the next frame should be processed
                    loopEnd = DateTime.Now;

                    loopTime = loopEnd - loopStart;

                    desiredFrameTime = 1000 / Properties.Settings.Default.OptFramesPerSecond;
                    sleepTime = desiredFrameTime - loopTime.TotalMilliseconds;
                    if (sleepTime < 0) sleepTime = 0;

                    if (sleepTime != 0)
                    { Thread.Sleep((int)sleepTime); }
                }
                if (RestartEngine)
                {
                    UpdateStatusMessage.NewMessage(5, "Reinitializing.");
                    RestartEngine = false;
                }
                while (PauseEngine && RunEngine)
                {
                    Thread.Sleep(1000);
                }
                NAudio_StopCapture();
                NAudio_Dispose();
            }
            if (Properties.Settings.Default.OptRestoreLightingOnExit)
            {
                UpdateStatusMessage.NewMessage(5, "Restoring lighting.");
                Output.RestoreKeyboard(KeyboardPointer);
                Output.RestoreMouse(MousePointer);
            }
            InputHook.OnRawInputFromKeyboard -= InputFromKeyboard;

            UpdateStatusMessage.NewMessage(5, "Engine is shutting down.");
        }

        public void ClearAllKeys()
        {
            for (int i = 0; i < 149; i++)
            {
                BackgroundKeys[i].KeyColor = new LightSingle(lightColor: Color.FromArgb(0, 0, 0, 0));
                ForegroundKeys[i].KeyColor = new LightSingle(lightColor: Color.FromArgb(0, 0, 0, 0));
                ReactiveKeys[i].KeyColor = new LightSingle(lightColor: Color.FromArgb(0, 0, 0, 0));
                SpectroKeys[i].KeyColor = new LightSingle(lightColor: Color.FromArgb(0, 0, 0, 0));
                Keys[i].KeyColor = new LightSingle(lightColor: Color.FromArgb(0, 0, 0, 0));
            }
        }

        private void RenderBackground()
        {
            if (Properties.Settings.Default.BackgroundEffectEnabled)
            {
                double tBrightness = ((double)Properties.Settings.Default.BackgroundBrightness / 255D);
                if (Properties.Settings.Default.KeyboardModel == "K65-RGB") { refKey = 139; };
                if (KeyboardMap.CanvasWidth == 3) { refKey = 0; };

                if (LastBackgroundEffect != Properties.Settings.Default.BackgroundEffect) { ClearAllKeys(); };
                LastBackgroundEffect = Properties.Settings.Default.BackgroundEffect;

                switch (Properties.Settings.Default.BackgroundEffect)
                {
                    case "Solid":
                        for (int i = 0; i < 149; i++)
                        { BackgroundKeys[i].KeyColor = new LightSingle(
                            lightColor: (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundSolidColor)); }
                        break;
                    case "Breathe":
                        #region Breathe Code
                        int Step1T = Properties.Settings.Default.BackgroundBreatheStepOne;
                        int TransT = Properties.Settings.Default.BackgroundBreatheTransition;
                        int Step2T = Properties.Settings.Default.BackgroundBreatheStepTwo;
                        double sA, sR, sG, sB, eA, eR, eG, eB;
                        byte nA = 0, nR = 0, nG = 0, nB = 0;
                        double StepMultiplier;

                        Color breatheColor = Color.FromArgb(0, 0, 0, 0);
                        Color StartColor;
                        Color EndColor;

                        TimeSpan Difference = DateTime.Now - BreatheStartTime;

                        switch (BreatheStep)
                        {
                            case 0:
                                BreatheStartTime = DateTime.Now;
                                breatheColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundBreatheStepOneColor);
                                BreatheStep = 1;
                                break;
                            case 1:
                                breatheColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundBreatheStepOneColor);
                                if (Difference.TotalMilliseconds >= Step1T) { BreatheStep = 2; };
                                break;
                            case 2:
                                StartColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundBreatheStepOneColor);
                                EndColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundBreatheStepTwoColor);

                                sA = StartColor.A;
                                sR = StartColor.R;
                                sG = StartColor.G;
                                sB = StartColor.B;
                                eA = EndColor.A;
                                eR = EndColor.R;
                                eG = EndColor.G;
                                eB = EndColor.B;

                                if (Difference.TotalMilliseconds < (Step1T + TransT))
                                {
                                    StepMultiplier = Math.Abs(Difference.TotalMilliseconds - (Step1T + TransT)) / ((Step1T + TransT + Step2T) - (Step1T + TransT));
                                    nA = (byte)(eA - ((eA - sA) * StepMultiplier));
                                    nR = (byte)(eR - ((eR - sR) * StepMultiplier));
                                    nG = (byte)(eG - ((eG - sG) * StepMultiplier));
                                    nB = (byte)(eB - ((eB - sB) * StepMultiplier));
                                    breatheColor = Color.FromArgb(nA, nR, nG, nB);
                                }
                                else
                                {
                                    breatheColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundBreatheStepTwoColor);
                                    BreatheStep = 3;
                                };
                                break;
                            case 3:
                                breatheColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundBreatheStepTwoColor);
                                if (Difference.TotalMilliseconds >= (Step1T + TransT + Step2T)) { BreatheStep = 4; };
                                break;
                            case 4:
                                StartColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundBreatheStepTwoColor);
                                EndColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundBreatheStepOneColor);

                                sA = StartColor.A;
                                sR = StartColor.R;
                                sG = StartColor.G;
                                sB = StartColor.B;
                                eA = EndColor.A;
                                eR = EndColor.R;
                                eG = EndColor.G;
                                eB = EndColor.B;

                                if (Difference.TotalMilliseconds < (Step1T + TransT * 2 + Step2T))
                                {
                                    StepMultiplier = Math.Abs(Difference.TotalMilliseconds - (Step1T + TransT * 2 + Step2T)) / ((Step1T + TransT * 2 + Step2T) - (Step1T + TransT + Step2T));
                                    nA = (byte)(eA - ((eA - sA) * StepMultiplier));
                                    nR = (byte)(eR - ((eR - sR) * StepMultiplier));
                                    nG = (byte)(eG - ((eG - sG) * StepMultiplier));
                                    nB = (byte)(eB - ((eB - sB) * StepMultiplier));
                                    breatheColor = Color.FromArgb(nA, nR, nG, nB);
                                }
                                else
                                {
                                    breatheColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundBreatheStepOneColor);
                                    BreatheStep = 0;
                                };
                                break;
                        }

                        for (int i = 0; i < 149; i++)
                        { BackgroundKeys[i].KeyColor = new LightSingle(lightColor: breatheColor); };

                        break;
                    #endregion Breathe Code
                    case "Spectrum Cycle":
                        for (int i = 0; i < 149; i++)
                        { BackgroundKeys[i].KeyColor = new LightSingle(
                            lightColor: ColorFromHSV((BackgroundAnim / KeyboardMap.CanvasWidth) * 360, 1, tBrightness)); }
                        break;
                    case "Rainbow":
                        #region Rainbow Code
                        double rainbowKey = 0;

                        for (int y = 0; y < 7; y++)
                        {
                            for (int x = 0; x < KeyboardMap.CanvasWidth - 1; x++)
                            {
                                int key = KeyboardMap.LedMatrix[y, x];
                                if (key != 255)
                                {
                                    if (!BackgroundKeys[key].KeyColor.EffectInProgress)
                                    {
                                        switch (Properties.Settings.Default.BackgroundRainbowDirection)
                                        {
                                            case "Right":
                                                rainbowKey = 1 - ((double)x - (double)BackgroundAnim) / (double)KeyboardMap.CanvasWidth;
                                                break;
                                            case "Left":
                                                rainbowKey = ((double)x + (double)BackgroundAnim) / (double)KeyboardMap.CanvasWidth;
                                                break;
                                            case "Up":
                                                rainbowKey = 1 - (double)BackgroundAnim / (double)KeyboardMap.CanvasWidth;
                                                rainbowKey = ((double)y / 7d) - rainbowKey;
                                                if (rainbowKey < 0) { rainbowKey += 1; };
                                                break;
                                            case "Down":
                                                rainbowKey = (double)BackgroundAnim / (double)KeyboardMap.CanvasWidth;
                                                rainbowKey = 1 - (((double)y / 7d) - rainbowKey);
                                                if (rainbowKey < 0) { rainbowKey += 1; };
                                                break;
                                        }
                                        BackgroundKeys[key].KeyColor = new LightSingle(ColorFromHSV(rainbowKey * 360, 1, tBrightness));
                                    }
                                }
                            }
                        }
                        for (int k = 0; k < 5; k++) { BackgroundKeys[144 + k].KeyColor = BackgroundKeys[refKey].KeyColor; };
                        #endregion Rainbow Code
                        break;
                    case "Image":
                        if (MainWindow.BackgroundImageSelection.OutputImage != null) { BitmapToKeyboard(MainWindow.BackgroundImageSelection.OutputImage, "Background"); }
                        break;
                    case "CPU Usage":
                        double cpuUsage = CpuUsage.GetUsage();
                        Color cpuColor;

                        if (cpuUsage <= .25) {
                            cpuColor = BlendColors((Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundCpuColor0),
                                                   (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundCpuColor25),
                                                   (cpuUsage * 4d));
                        }
                        else if (cpuUsage > .25 && cpuUsage <= .5) {
                            cpuColor = BlendColors((Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundCpuColor25),
                                                   (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundCpuColor50),
                                                   ((cpuUsage - .25) * 4d));
                        }
                        else if (cpuUsage > .5 && cpuUsage <= .75) {
                            cpuColor = BlendColors((Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundCpuColor50),
                                                   (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundCpuColor75),
                                                   ((cpuUsage - .5) * 4d));
                        }
                        else /* if (cpuUsage > .75 && cpuUsage <= 1) */ {
                            cpuColor = BlendColors((Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundCpuColor75),
                                                       (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundCpuColor100),
                                                       ((cpuUsage - .75) * 4d));
                        }

                        for (int i = 0; i < 149; i++)
                        {
                            BackgroundKeys[i].KeyColor = new LightSingle(
                              lightColor: Color.FromArgb(cpuColor.A, cpuColor.R, cpuColor.G, cpuColor.B));
                        }
                        break;
                }
            }
            else
            {
                for (int i = 0; i < 149; i++)
                { BackgroundKeys[i].KeyColor = new LightSingle(lightColor: Color.FromArgb(0, 0, 0, 0)); }
            }
        }

        private void RenderForeground()
        {
            if (Properties.Settings.Default.ForegroundEffectEnabled)
            {
                Color SL = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsColorStartLower);
                Color SU = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsColorStartUpper);
                Color EL = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsColorEndLower);
                Color EU = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsColorEndUpper);
                Color startColor = Color.FromArgb(255, 255, 255, 255);
                Color endColor = Color.FromArgb(0, 0, 0, 0);

                if (LastForegroundEffect != Properties.Settings.Default.ForegroundEffect)
                {
                    ClearAllKeys();
                    if (LastForegroundEffect == "Spectrograph")
                    {
                        NAudio_StopCapture();
                        NAudio_Dispose();
                    }
                    if (Properties.Settings.Default.ForegroundEffect == "Spectrograph")
                    {
                        RestartEngine = true;
                    }
                };
                LastForegroundEffect = Properties.Settings.Default.ForegroundEffect;

                switch (Properties.Settings.Default.ForegroundEffect)
                {
                    case "Spectrograph":
                        ForegroundKeys = SpectroKeys; // Defer to spectro map
                        break;
                    case "Random Lights":
                        #region Random Lights Code
                        int keyLight = rnd.Next(0, 149);

                        if (ForegroundKeys[keyLight].KeyColor.EffectInProgress == false)
                        {
                            switch (Properties.Settings.Default.ForegroundRandomLightsStartType)
                            {
                                case "Defined Colour":
                                    startColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsSwitchColorStart);
                                    break;
                                case "Random Colour":
                                    startColor = Color.FromArgb(255, (byte)rnd.Next(SL.R, SU.R), (byte)rnd.Next(SL.G, SU.G), (byte)rnd.Next(SL.B, SU.B));
                                    break;
                            }

                            switch (Properties.Settings.Default.ForegroundRandomLightsEndType)
                            {
                                case "None":
                                    endColor = Color.FromArgb(0, startColor.R, startColor.G, startColor.B);
                                    break;
                                case "Defined Colour":
                                    endColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsSwitchColorEnd);
                                    break;
                                case "Random Colour":
                                    endColor = Color.FromArgb(255, (byte)rnd.Next(EL.R, EU.R), (byte)rnd.Next(EL.G, EU.G), (byte)rnd.Next(EL.B, EU.B));
                                    break;
                            }

                            switch (Properties.Settings.Default.ForegroundRandomLightsStyle)
                            {
                                case "Switch":
                                    ForegroundKeys[keyLight].KeyColor = new LightSwitch(startColor: startColor,
                                                                            endColor: endColor,
                                                                            duration: Properties.Settings.Default.ForegroundRandomLightsSwitchDuration);
                                    break;
                                case "Fade":
                                    ForegroundKeys[keyLight].KeyColor = new LightFade(startColor: startColor,
                                                                            endColor: endColor,
                                                                            solidDuration: Properties.Settings.Default.ForegroundRandomLightsFadeSolidDuration,
                                                                            totalDuration: Properties.Settings.Default.ForegroundRandomLightsFadeTotalDuration);
                                    break;
                            }
                        }
                        #endregion Random Lights Code
                        break;
                    case "Reactive Typing":
                        ForegroundKeys = ReactiveKeys; // Defer to reactive map
                        break;
                    case "Heatmap":
                        ForegroundKeys = ReactiveKeys; // Defer to reactive map
                        break;
                    case "CPU Usage Bar":
                        #region CPU Usage Bar Code
                        double cpuUsage = CpuUsage.GetUsage();
                        Color cpuColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundCpuBarColor0);

                        System.Drawing.Bitmap cpuBmp = new System.Drawing.Bitmap(KeyboardMap.CanvasWidth, 7);
                        System.Drawing.Drawing2D.GraphicsPath cpuBarPath = new System.Drawing.Drawing2D.GraphicsPath(System.Drawing.Drawing2D.FillMode.Winding);

                        if (!(cpuUsage > 0) && !(cpuUsage < 1)) { cpuUsage = 0; };

                        System.Drawing.Point cpuTL = new System.Drawing.Point(0, 0);
                        System.Drawing.Point cpuBL = new System.Drawing.Point(0, 0);
                        System.Drawing.Point cpuTR = new System.Drawing.Point(0, 0);
                        System.Drawing.Point cpuBR = new System.Drawing.Point(0, 0);

                        switch (Properties.Settings.Default.ForegroundCpuBarDirection)
                        {
                            case "Up":
                                cpuTL = new System.Drawing.Point(0, (int)(cpuBmp.Height - (cpuBmp.Height * cpuUsage)));
                                cpuBL = new System.Drawing.Point(0, cpuBmp.Height);
                                cpuTR = new System.Drawing.Point(cpuBmp.Width, (int)(cpuBmp.Height - (cpuBmp.Height * cpuUsage)));
                                cpuBR = new System.Drawing.Point(cpuBmp.Width, cpuBmp.Height);
                                break;
                            case "Down":
                                cpuTL = new System.Drawing.Point(cpuBmp.Width, 1 + (int)(cpuBmp.Height * cpuUsage));
                                cpuBL = new System.Drawing.Point(cpuBmp.Width, 0);
                                cpuTR = new System.Drawing.Point(0, 1 + (int)(cpuBmp.Height * cpuUsage));
                                cpuBR = new System.Drawing.Point(0, 0);
                                break;
                            case "Right":
                                cpuTL = new System.Drawing.Point(0, 0);
                                cpuBL = new System.Drawing.Point(0, cpuBmp.Height);
                                cpuTR = new System.Drawing.Point((int)(cpuBmp.Width * cpuUsage), 0);
                                cpuBR = new System.Drawing.Point((int)(cpuBmp.Width * cpuUsage), cpuBmp.Height);
                                break;
                            case "Left":
                                cpuTL = new System.Drawing.Point((int)(cpuBmp.Width - (cpuBmp.Width * cpuUsage)), cpuBmp.Height);
                                cpuBL = new System.Drawing.Point(cpuBmp.Width, cpuBmp.Height);
                                cpuTR = new System.Drawing.Point((int)(cpuBmp.Width - (cpuBmp.Width * cpuUsage)), 0);
                                cpuBR = new System.Drawing.Point(cpuBmp.Width, 0);
                                break;
                        }


                        cpuBarPath.AddLine(cpuTL, cpuTR);
                        cpuBarPath.AddLine(cpuTR, cpuBR);
                        cpuBarPath.AddLine(cpuBR, cpuBL);
                        cpuBarPath.AddLine(cpuBL, cpuTL);

                        cpuBarPath.CloseFigure();

                        using (System.Drawing.Graphics gr = System.Drawing.Graphics.FromImage(cpuBmp))
                        {
                            gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;

                            Color c = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundCpuBarColor0);
                            using (System.Drawing.SolidBrush br = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B)))
                            { gr.FillPath(br, cpuBarPath); }
                        }

                        BitmapToKeyboard(cpuBmp, "Foreground");
                        #endregion CPU Usage Bar Code
                        break;
                }
            }
            else
            {
                for (int i = 0; i < 149; i++)
                { ForegroundKeys[i].KeyColor = new LightSingle(lightColor: Color.FromArgb(0, 0, 0, 0)); }
            }
        }

        /// <summary>
        /// Returns a colour based on the blend bias of the two provided colours.
        /// </summary>
        /// <param name="C1"></param>
        /// <param name="C2"></param>
        /// <param name="Bias">0 for full C1 bias, 1 for full C2 bias, .5 for 50/50.</param>
        /// <returns></returns>
        private Color BlendColors(Color C1, Color C2, double Bias)
        {
            byte newA = (byte)(((double)C1.A * (1 - Bias)) + ((double)C2.A * Bias));
            byte newR = (byte)(((double)C1.R * (1 - Bias)) + ((double)C2.R * Bias));
            byte newG = (byte)(((double)C1.G * (1 - Bias)) + ((double)C2.G * Bias));
            byte newB = (byte)(((double)C1.B * (1 - Bias)) + ((double)C2.B * Bias));

            return Color.FromArgb((byte)(newA),
                                  (byte)(newR),
                                  (byte)(newG),
                                  (byte)(newB));
        }

        private Color AlphaBlend(Color SRC, Color DST)
        {
            double srcA = (double)((double)SRC.A / 255D);
            double dstA = (double)((double)DST.A / 255D);
            double outA = (double)(srcA + dstA * (1 - srcA));

            return Color.FromArgb((byte)(255 * outA),
                                        (byte)((((double)SRC.R * srcA) + (((double)DST.R * dstA) * (1 - srcA))) / outA),
                                        (byte)((((double)SRC.G * srcA) + (((double)DST.G * dstA) * (1 - srcA))) / outA),
                                        (byte)((((double)SRC.B * srcA) + (((double)DST.B * dstA) * (1 - srcA))) / outA));
        }

        private void BlendLayers()
        {

            Color FG;
            Color BG;
            Color newColor;
            double alphaDifference;


            for (int i = 0; i < 149; i++)
            {
                FG = ForegroundKeys[i].KeyColor.LightColor;
                BG = BackgroundKeys[i].KeyColor.LightColor;
                alphaDifference = 1 - ((double)FG.A / 255D);
                newColor = Color.FromRgb((byte)(FG.R - ((FG.R - BG.R) * alphaDifference)),
                                         (byte)(FG.G - ((FG.G - BG.G) * alphaDifference)),
                                         (byte)(FG.B - ((FG.B - BG.B) * alphaDifference)));

                Keys[i].KeyColor = new LightSingle(lightColor: AlphaBlend(ForegroundKeys[i].KeyColor.LightColor, BackgroundKeys[i].KeyColor.LightColor));
            }
        }

        public void InputFromKeyboard(RAWINPUTHEADER riHeader, RAWKEYBOARD riKeyboard)
        {
            if (riKeyboard.Flags == 0x0) { return; };
            if (riKeyboard.Flags == 0x2 && Keyboard.IsKeyToggled(Key.NumLock)) { return; };

            int keyLight = InputKeys.GetKeyCodeFromDict(riKeyboard.MakeCode, riKeyboard.VKey, riKeyboard.Flags, Keyboard.IsKeyToggled(Key.NumLock));

            if (Properties.Settings.Default.ForegroundEffect == "Reactive Typing")
            {
                #region Reactive Typing
                Color SL = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundReactiveColorStartLower);
                Color SU = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundReactiveColorStartUpper);
                Color EL = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundReactiveColorEndLower);
                Color EU = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundReactiveColorEndUpper);
                Color startColor = Color.FromRgb(255, 255, 255);
                Color endColor = Color.FromRgb(0, 0, 0);

                switch (Properties.Settings.Default.ForegroundReactiveStartType)
                {
                    case "Defined Colour":
                        startColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundReactiveSwitchColorStart);
                        break;
                    case "Random Colour":
                        startColor = Color.FromRgb((byte)rnd.Next(SL.R, SU.R), (byte)rnd.Next(SL.G, SU.G), (byte)rnd.Next(SL.B, SU.B));
                        break;
                }

                switch (Properties.Settings.Default.ForegroundReactiveEndType)
                {
                    case "None":
                        endColor = Color.FromArgb(0, startColor.R, startColor.G, startColor.B);
                        break;
                    case "Defined Colour":
                        endColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundReactiveSwitchColorEnd);
                        break;
                    case "Random Colour":
                        endColor = Color.FromRgb((byte)rnd.Next(EL.R, EU.R), (byte)rnd.Next(EL.G, EU.G), (byte)rnd.Next(EL.B, EU.B));
                        break;
                }

                switch (Properties.Settings.Default.ForegroundReactiveStyle)
                {
                    case "Switch":
                        ReactiveKeys[keyLight].KeyColor = new LightSwitch(startColor: startColor,
                                                                endColor: endColor,
                                                                duration: Properties.Settings.Default.ForegroundReactiveSwitchSolidDuration);
                        break;
                    case "Fade":
                        ReactiveKeys[keyLight].KeyColor = new LightFade(startColor: startColor,
                                                                endColor: endColor,
                                                                solidDuration: Properties.Settings.Default.ForegroundReactiveFadeSolidDuration,
                                                                totalDuration: Properties.Settings.Default.ForegroundReactiveFadeTotalDuration);
                        break;
                }
                #endregion Reactive Typing
            }
            else if (Properties.Settings.Default.ForegroundEffect == "Heatmap")
            {
                #region Heatmap
                Color CM = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundHeatmapColorMost);
                Color CL = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundHeatmapColorLeast);

                double keyIntensity;
                HeatmapStrikeCount[keyLight] += 1;
                if (HeatmapStrikeCount[keyLight] > HeatmapHighestStrikeCount) { HeatmapHighestStrikeCount = HeatmapStrikeCount[keyLight]; };

                for (int i = 0; i < 149; i++)
                {
                    keyIntensity = 1 - ((double)HeatmapStrikeCount[i] / (double)HeatmapHighestStrikeCount);
                    // Make a floor for the least-struck key
                    if (Properties.Settings.Default.Opt16MColours && keyIntensity > .95 && HeatmapStrikeCount[i] > 0)
                    { keyIntensity = .95; }
                    else if (!Properties.Settings.Default.Opt16MColours && keyIntensity > .85 && HeatmapStrikeCount[i] > 0)
                    { keyIntensity = .85; }

                    Color newColor = Color.FromArgb(255,
                                                    (byte)(CM.R - ((CM.R - CL.R) * keyIntensity)),
                                                    (byte)(CM.G - ((CM.G - CL.G) * keyIntensity)),
                                                    (byte)(CM.B - ((CM.B - CL.B) * keyIntensity)));

                    if (HeatmapStrikeCount[i] == 0)
                    { newColor = Color.FromArgb(0, 0, 0, 0); }

                    ReactiveKeys[i].KeyColor = new LightSingle(lightColor: newColor);
                    #endregion Heatmap
                }
            }
        }

        public static Color ColorFromHSV(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            byte v = (byte)Convert.ToInt32(value);
            byte p = (byte)Convert.ToInt32(value * (1 - saturation));
            byte q = (byte)Convert.ToInt32(value * (1 - f * saturation));
            byte t = (byte)Convert.ToInt32(value * (1 - (1 - f) * saturation));

            if (hi == 0)
                return Color.FromArgb(255, v, t, p);
            else if (hi == 1)
                return Color.FromArgb(255, q, v, p);
            else if (hi == 2)
                return Color.FromArgb(255, p, v, t);
            else if (hi == 3)
                return Color.FromArgb(255, p, q, v);
            else if (hi == 4)
                return Color.FromArgb(255, t, p, v);
            else
                return Color.FromArgb(255, v, p, q);
        }

        #region NAudio Methods

        private void NAudio_Initialize()
        {
            MMDeviceEnumerator deviceEnum = new MMDeviceEnumerator();
            audioCapture = null;

            List<MMDevice> AudioDeviceList;
            if (Properties.Settings.Default.OptAudioFromInput)
            { AudioDeviceList = new List<MMDevice>(deviceEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToArray()); }
            else
            { AudioDeviceList = new List<MMDevice>(deviceEnum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToArray()); }

            foreach (MMDevice dev in AudioDeviceList)
            {
                if (dev.FriendlyName == Properties.Settings.Default.AudioOutputDevice &&
                    Properties.Settings.Default.OptAudioFromOutput)
                {
                    audioCapture = new WasapiLoopbackCapture(dev);
                }
                else if (dev.FriendlyName == Properties.Settings.Default.AudioInputDevice &&
                    Properties.Settings.Default.OptAudioFromInput)
                { audioCapture = new WasapiCapture(dev); }
            }

            if (audioCapture == null) { return; };

            UpdateStatusMessage.NewMessage(4, "Audio Channels: " + audioCapture.WaveFormat.Channels.ToString());
            captureSampleRate = audioCapture.WaveFormat.SampleRate;

            sampleAggregator = new SampleAggregator(Int32.Parse(Properties.Settings.Default.FftSize));
            sampleAggregator.FftCalculated += new EventHandler<FftEventArgs>(FftCalculated);
            sampleAggregator.MaximumCalculated += new EventHandler<MaxSampleEventArgs>(MaxCalculated);

            audioCapture.DataAvailable += new EventHandler<WaveInEventArgs>(NAudio_DataAvailable);

            audioCapture.StartRecording();
        }

        private void NAudio_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (!CalculateFFT) { return; };
            byte[] buffer = e.Buffer;
            int bytesRecorded = e.BytesRecorded;
            int bufferIncrement = audioCapture.WaveFormat.BlockAlign;

            for (int index = 0; index < bytesRecorded; index += bufferIncrement)
            {
                sampleAggregator.Add(BitConverter.ToSingle(buffer, index),
                                     BitConverter.ToSingle(buffer, index + 4));
            }
            CalculateFFT = false;
        }

        private void NAudio_StopCapture()
        {
            UpdateStatusMessage.NewMessage(6, "Stopping Capture");
            try { audioCapture.StopRecording(); }
            catch { }
        }

        private void NAudio_Dispose()
        {
            if (audioCapture != null)
            {
                audioCapture.DataAvailable -= NAudio_DataAvailable;
                audioCapture.Dispose();
                audioCapture = null;
            }

            try { sampleAggregator.FftCalculated -= FftCalculated; }
            catch { }

            UpdateStatusMessage.NewMessage(6, "Capture Destroyed");
        }

        private int CalculateBinPos(int col, int first, int last, int maxWidth)
        {
            double multiplier;
            multiplier = ((double)col - (double)first) / ((double)last - 1 - (double)first);

            if (Properties.Settings.Default.FftUseLogX)
            { return (int)(multiplier * maxWidth); }
            else
            {
                if (multiplier <= 0.01) { return 0; }
                else if (multiplier >= 1) { return maxWidth; }
                else { return (int)((1 + Math.Log(multiplier, 100)) * maxWidth); }
            }
        }

        private int FindLowestBin(int inputFrequency, int sampleCount, int minFrequency)
        {
            int freq;
            for (int b = 0; b < sampleCount / 2; b++)
            {
                freq = b * inputFrequency / sampleCount;
                if (freq >= minFrequency) { return b; }
            }
            return 0;
        }

        private int FindHighestBin(int inputFrequency, int sampleCount, int maxFrequency)
        {
            int freq;
            for (int b = sampleCount / 2; b > 0; b--)
            {
                freq = b * inputFrequency / sampleCount;
                if (freq <= maxFrequency) { return b; }
            }
            return sampleCount / 2;

        }

        private double GetYPosLog(Complex c, int maxWidth, int xPos)
        {
            double intensityDB = 10 * Math.Log10(Math.Sqrt(c.X * c.X + c.Y * c.Y));
            double minDB = (double)Properties.Settings.Default.FftAmplitudeMin;
            double maxDB = (double)Properties.Settings.Default.FftAmplitudeMax;

            if (Properties.Settings.Default.FftUseFrequencyBoost)
            {
                double position;
                if (Properties.Settings.Default.FftUseLogX)
                { position = Math.Log((double)xPos, (double)maxWidth) / ((double)highestBin - (double)lowestBin); }
                else { position = (double)xPos / ((double)highestBin - (double)lowestBin); }
                intensityDB += (position * (double)Properties.Settings.Default.FftBoostHighFrequencies);
            }

            if (intensityDB < minDB) { intensityDB = minDB; }
            if (intensityDB > maxDB) { intensityDB = maxDB; }

            // Linear
            double intensityPercent = (intensityDB - maxDB) / (minDB - maxDB);

            // Logarithmic
            if (Properties.Settings.Default.FftUseLogY)
            {
                if (intensityPercent != 0)
                {
                    intensityPercent = 1 + Math.Log(intensityPercent, 100);
                }
            }
            return intensityPercent * 7;
        }

        private void AddResult(int index, double power, System.Drawing.Drawing2D.GraphicsPath path)
        {
            int binToUse = CalculateBinPos(index, lowestBin, highestBin, KeyboardMap.CanvasWidth);

            path.AddLine(binToUse, (int)power, binToUse, (int)power);
        }

        private void RainbowSpectroPositions()
        {
            spectroRainbowPositions = new float[KeyboardMap.CanvasWidth];
            for (int i = 0; i < KeyboardMap.CanvasWidth; i++)
            { spectroRainbowPositions[i] = (float)((double)i / ((double)KeyboardMap.CanvasWidth - 1)); }
        }

        private void RainbowSpectroColors(string direction)
        {
            double tBrightness = ((double)Properties.Settings.Default.FftRainbowBrightness / 255D);
            spectroRainbowColors = new System.Drawing.Color[KeyboardMap.CanvasWidth];
            Color tc;
            double pos;

            for (int i = 0; i < KeyboardMap.CanvasWidth; i++)
            {

                if (direction == "Right" || direction == "Down")
                { pos = 1 - ((double)i - (double)BackgroundAnim) / (double)KeyboardMap.CanvasWidth; }
                else
                { pos = ((double)i + (double)BackgroundAnim) / (double)KeyboardMap.CanvasWidth; }
                tc = ColorFromHSV(pos * 360, 1, tBrightness);
                spectroRainbowColors[i] = System.Drawing.Color.FromArgb(tc.A, tc.R, tc.G, tc.B);
            }
        }

        private void FftCalculated(object sender, FftEventArgs e)
        {
            System.Drawing.Drawing2D.GraphicsPath fftPath;
            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(KeyboardMap.CanvasWidth, 7);
            fftPath = new System.Drawing.Drawing2D.GraphicsPath(System.Drawing.Drawing2D.FillMode.Winding);

            fftPath.AddLine(0, bmp.Height, 0, bmp.Height);

            lowestBin = FindLowestBin(captureSampleRate, e.Result.Length, Properties.Settings.Default.FftFrequencyMin);
            highestBin = FindHighestBin(captureSampleRate, e.Result.Length, Properties.Settings.Default.FftFrequencyMax);
            int binsPerPoint = Int32.Parse(Properties.Settings.Default.FftBinsPerPoint);

            for (int n = lowestBin; n < highestBin; n += binsPerPoint)
            {
                // Average the bins
                double yPos = 0;
                for (int b = 0; b < binsPerPoint; b++)
                {
                    yPos += GetYPosLog(e.Result[n + b], KeyboardMap.CanvasWidth, n);
                }
                //AddResult(n / binsPerPoint, yPos / binsPerPoint, (Single)XPos);
                AddResult(n, yPos / binsPerPoint, fftPath);
            }

            // Add end point.
            fftPath.AddLine(bmp.Width, bmp.Height, bmp.Width, bmp.Height);

            // Close image
            fftPath.CloseFigure();

            using (System.Drawing.Graphics gr = System.Drawing.Graphics.FromImage(bmp))
            {
                //gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;

                Color c;

                if (Properties.Settings.Default.ForegroundSpectroStyle == "Solid")
                {
                    c = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundSpectroColor);
                    using (System.Drawing.SolidBrush br = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B)))
                    {
                        gr.FillPath(br, fftPath);
                    }
                }
                else if (Properties.Settings.Default.ForegroundSpectroStyle == "Gradient Horizontal")
                {
                    Color tc1 = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundSpectroColor);
                    Color tc2 = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundSpectroColorGradient);
                    System.Drawing.Color c1 = System.Drawing.Color.FromArgb(tc1.A, tc1.R, tc1.G, tc1.B);
                    System.Drawing.Color c2 = System.Drawing.Color.FromArgb(tc2.A, tc2.R, tc2.G, tc2.B);
                    System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, 104, 7);
                    using (System.Drawing.Drawing2D.LinearGradientBrush lbr = new System.Drawing.Drawing2D.LinearGradientBrush(rect, c1, c2, (Single)0))
                    {
                        gr.FillPath(lbr, fftPath);
                    }
                }
                else if (Properties.Settings.Default.ForegroundSpectroStyle == "Gradient Vertical")
                {
                    Color tc1 = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundSpectroColor);
                    Color tc2 = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundSpectroColorGradient);
                    System.Drawing.Color c1 = System.Drawing.Color.FromArgb(tc1.A, tc1.R, tc1.G, tc1.B);
                    System.Drawing.Color c2 = System.Drawing.Color.FromArgb(tc2.A, tc2.R, tc2.G, tc2.B);
                    System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, 104, 7);
                    using (System.Drawing.Drawing2D.LinearGradientBrush lbr = new System.Drawing.Drawing2D.LinearGradientBrush(rect, c1, c2, (Single)90))
                    {
                        gr.FillPath(lbr, fftPath);
                    }
                }
                else if (Properties.Settings.Default.ForegroundSpectroStyle == "Rainbow")
                {
                    System.Drawing.Rectangle rect;
                    if (Properties.Settings.Default.SpectroRainbowDirection == "Left" ||
                        Properties.Settings.Default.SpectroRainbowDirection == "Right")
                    { rect = new System.Drawing.Rectangle(0, 0, 104, 7); }
                    else
                    { rect = new System.Drawing.Rectangle(0, 0, 7, 104); }
                    System.Drawing.Drawing2D.LinearGradientBrush br =
                        new System.Drawing.Drawing2D.LinearGradientBrush(rect, System.Drawing.Color.Black, System.Drawing.Color.Black, 0, false);
                    System.Drawing.Drawing2D.ColorBlend cb = new System.Drawing.Drawing2D.ColorBlend();

                    cb.Positions = spectroRainbowPositions;
                    RainbowSpectroColors(Properties.Settings.Default.SpectroRainbowDirection);
                    cb.Colors = spectroRainbowColors;
                    br.InterpolationColors = cb;

                    if (Properties.Settings.Default.SpectroRainbowDirection == "Up" ||
                        Properties.Settings.Default.SpectroRainbowDirection == "Down")
                    { br.RotateTransform(90); }

                    gr.FillPath(br, fftPath);
                }
                else if (Properties.Settings.Default.ForegroundSpectroStyle == "Spectrum Cycle")
                {
                    double tBrightness = ((double)Properties.Settings.Default.FftRainbowBrightness / 255D);
                    double pos = ((double)BackgroundAnim / (double)KeyboardMap.CanvasWidth);
                    c = ColorFromHSV(pos * 360, 1, tBrightness);

                    using (System.Drawing.SolidBrush br = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B)))
                    {
                        gr.FillPath(br, fftPath);
                    }
                }
                else if (Properties.Settings.Default.ForegroundSpectroStyle == "Defined Rows")
                {
                    Color rc1 = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundSpectroRow7);
                    Color rc2 = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundSpectroRow6);
                    Color rc3 = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundSpectroRow5);
                    Color rc4 = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundSpectroRow4);
                    Color rc5 = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundSpectroRow3);
                    Color rc6 = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundSpectroRow2);
                    Color rc7 = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundSpectroRow1);

                    System.Drawing.Color[] colors = new System.Drawing.Color[] {System.Drawing.Color.FromArgb(rc1.A, rc1.R, rc1.G, rc1.B),
                                                                                System.Drawing.Color.Transparent,
                                                                                System.Drawing.Color.FromArgb(rc2.A, rc2.R, rc2.G, rc2.B),
                                                                                System.Drawing.Color.Transparent,
                                                                                System.Drawing.Color.FromArgb(rc3.A, rc3.R, rc3.G, rc3.B),
                                                                                System.Drawing.Color.Transparent,
                                                                                System.Drawing.Color.FromArgb(rc4.A, rc4.R, rc4.G, rc4.B),
                                                                                System.Drawing.Color.Transparent,
                                                                                System.Drawing.Color.FromArgb(rc5.A, rc5.R, rc5.G, rc5.B),
                                                                                System.Drawing.Color.Transparent,
                                                                                System.Drawing.Color.FromArgb(rc6.A, rc6.R, rc6.G, rc6.B),
                                                                                System.Drawing.Color.Transparent,
                                                                                System.Drawing.Color.FromArgb(rc7.A, rc7.R, rc7.G, rc7.B),
                                                                                System.Drawing.Color.Transparent
                                                                                };

                    System.Drawing.Drawing2D.ColorBlend grads = new System.Drawing.Drawing2D.ColorBlend(14);
                    grads.Colors = colors;
                    grads.Positions = new float[] { 0f, 1 / 14f, 2 / 14f, 3 / 14f, 4 / 14f, 5 / 14f, 6 / 14f,
                                                7 / 14f, 8 / 14f, 9 / 14f, 10 / 14f, 11 / 14f, 12 / 14f, 1f };

                    System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, KeyboardMap.CanvasWidth, 7);

                    for (int i = 0; i < 14; i++)
                    {
                        System.Drawing.Drawing2D.LinearGradientBrush lbr = new System.Drawing.Drawing2D.LinearGradientBrush(rect, System.Drawing.Color.Black, System.Drawing.Color.Black, System.Drawing.Drawing2D.LinearGradientMode.Vertical);
                        lbr.InterpolationColors = grads;
                        gr.FillPath(lbr, fftPath);
                    };
                }
            }
            BitmapToKeyboard(bmp, "Spectro");

        }

        private void MaxCalculated(object sender, MaxSampleEventArgs e)
        {
            if (!(Properties.Settings.Default.ForegroundSpectroOnMouse)) { return; };
            double intensityDB = 10 * Math.Log(Math.Abs(e.MaxSample));
            double minDB = (double)Properties.Settings.Default.FftAmplitudeMin;
            double maxDB = (double)Properties.Settings.Default.FftAmplitudeMax;

            if (intensityDB < minDB) { intensityDB = minDB; }
            if (intensityDB > maxDB) { intensityDB = maxDB; }

            // Linear
            double intensityPercent = 1 - (intensityDB - maxDB) / (minDB - maxDB);

            // Logarithmic
            if (Properties.Settings.Default.FftUseLogY)
            {
                if (intensityPercent != 0)
                {
                    intensityPercent = 1 + Math.Log(intensityPercent, 100);
                }
            }

            byte intensity = (byte)(Math.Abs(intensityPercent) * 255);
            
            if (mouseIntensity < 0) { mouseIntensity = 0; };
            if (mouseIntensity > 255) { mouseIntensity = 255; };
            mouseIntensity = (byte)((mouseIntensity + intensity) / 2);

            if (mouseIntensity - 30 > SpectroKeys[144].KeyColor.Intensity) {
                Color refColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundSpectroColor);
                for (int i = 144; i < 149; i++)
                {
                    SpectroKeys[i].KeyColor = new IntensityLight(lightColor: Color.FromArgb(refColor.A, refColor.R, refColor.G, refColor.B),
                                                                 intensity: mouseIntensity,
                                                                 fadeTime: 100);
                }
            }

        }

        #endregion NAudio Methods

        private static void BitmapToKeyboard(System.Drawing.Bitmap bmp, string keySet)
        {
            int key;
            for (int c = 0; c < bmp.Width; c++)
            {
                for (int r = 0; r < bmp.Height; r++)
                {
                    key = KeyboardMap.LedMatrix[r, c];
                    if (key >= 0 && key < 144) {
                        Color keyCol = Color.FromArgb(bmp.GetPixel(c, r).A,
                                                      bmp.GetPixel(c, r).R,
                                                      bmp.GetPixel(c, r).G,
                                                      bmp.GetPixel(c, r).B);
                        if (keySet == "Background") { BackgroundKeys[key].KeyColor = new LightSingle(lightColor: keyCol); }
                        if (keySet == "Spectro") { SpectroKeys[key].KeyColor = new LightSingle(lightColor: keyCol); }
                        if (keySet == "Foreground") { ForegroundKeys[key].KeyColor = new LightSingle(lightColor: keyCol); }
                    }
                }
            }
        }
    }

    public class RawInputKeyCodes
    {
        Dictionary<Tuple<byte, byte, byte, bool>, int> keyDict;

        public RawInputKeyCodes() {
            keyDict = new Dictionary<Tuple<byte, byte, byte, bool>, int>();

            // Letters
            keyDict.Add(Tuple.Create((byte)0x1E, (byte)0x41, (byte)0x01, (true || false)), 15); //A
            keyDict.Add(Tuple.Create((byte)0x30, (byte)0x42, (byte)0x01, (true || false)), 76); //B
            keyDict.Add(Tuple.Create((byte)0x2E, (byte)0x43, (byte)0x01, (true || false)), 52); //C
            keyDict.Add(Tuple.Create((byte)0x20, (byte)0x44, (byte)0x01, (true || false)), 39); //D
            keyDict.Add(Tuple.Create((byte)0x12, (byte)0x45, (byte)0x01, (true || false)), 38); //E
            keyDict.Add(Tuple.Create((byte)0x21, (byte)0x46, (byte)0x01, (true || false)), 51); //F
            keyDict.Add(Tuple.Create((byte)0x22, (byte)0x47, (byte)0x01, (true || false)), 63); //G
            keyDict.Add(Tuple.Create((byte)0x23, (byte)0x48, (byte)0x01, (true || false)), 75); //H
            keyDict.Add(Tuple.Create((byte)0x17, (byte)0x49, (byte)0x01, (true || false)), 98); //I
            keyDict.Add(Tuple.Create((byte)0x24, (byte)0x4A, (byte)0x01, (true || false)), 87); //J
            keyDict.Add(Tuple.Create((byte)0x25, (byte)0x4B, (byte)0x01, (true || false)), 99); //K
            keyDict.Add(Tuple.Create((byte)0x26, (byte)0x4C, (byte)0x01, (true || false)), 111); //L
            keyDict.Add(Tuple.Create((byte)0x32, (byte)0x4D, (byte)0x01, (true || false)), 100); //M
            keyDict.Add(Tuple.Create((byte)0x31, (byte)0x4E, (byte)0x01, (true || false)), 88); //N
            keyDict.Add(Tuple.Create((byte)0x18, (byte)0x4F, (byte)0x01, (true || false)), 110); //O
            keyDict.Add(Tuple.Create((byte)0x19, (byte)0x50, (byte)0x01, (true || false)), 122); //P
            keyDict.Add(Tuple.Create((byte)0x10, (byte)0x51, (byte)0x01, (true || false)), 14); //Q
            keyDict.Add(Tuple.Create((byte)0x13, (byte)0x52, (byte)0x01, (true || false)), 50); //R
            keyDict.Add(Tuple.Create((byte)0x1F, (byte)0x53, (byte)0x01, (true || false)), 27); //S
            keyDict.Add(Tuple.Create((byte)0x14, (byte)0x54, (byte)0x01, (true || false)), 62); //T
            keyDict.Add(Tuple.Create((byte)0x16, (byte)0x55, (byte)0x01, (true || false)), 86); //U
            keyDict.Add(Tuple.Create((byte)0x2F, (byte)0x56, (byte)0x01, (true || false)), 64); //V
            keyDict.Add(Tuple.Create((byte)0x11, (byte)0x57, (byte)0x01, (true || false)), 26); //W
            keyDict.Add(Tuple.Create((byte)0x2D, (byte)0x58, (byte)0x01, (true || false)), 40); //X
            keyDict.Add(Tuple.Create((byte)0x15, (byte)0x59, (byte)0x01, (true || false)), 74); //Y
            keyDict.Add(Tuple.Create((byte)0x2C, (byte)0x5A, (byte)0x01, (true || false)), 28); //Z

            // Number Row
            keyDict.Add(Tuple.Create((byte)0x02, (byte)0x31, (byte)0x01, (true || false)), 13); //1
            keyDict.Add(Tuple.Create((byte)0x03, (byte)0x32, (byte)0x01, (true || false)), 25); //2
            keyDict.Add(Tuple.Create((byte)0x04, (byte)0x33, (byte)0x01, (true || false)), 37); //3
            keyDict.Add(Tuple.Create((byte)0x05, (byte)0x34, (byte)0x01, (true || false)), 49); //4
            keyDict.Add(Tuple.Create((byte)0x06, (byte)0x35, (byte)0x01, (true || false)), 61); //5
            keyDict.Add(Tuple.Create((byte)0x07, (byte)0x36, (byte)0x01, (true || false)), 73); //6
            keyDict.Add(Tuple.Create((byte)0x08, (byte)0x37, (byte)0x01, (true || false)), 85); //7
            keyDict.Add(Tuple.Create((byte)0x09, (byte)0x38, (byte)0x01, (true || false)), 97); //8
            keyDict.Add(Tuple.Create((byte)0x0A, (byte)0x39, (byte)0x01, (true || false)), 109); //9
            keyDict.Add(Tuple.Create((byte)0x0B, (byte)0x30, (byte)0x01, (true || false)), 121); //0

            // F Keys
            keyDict.Add(Tuple.Create((byte)0x3B, (byte)0x70, (byte)0x01, (true || false)), 12); //F1
            keyDict.Add(Tuple.Create((byte)0x3C, (byte)0x71, (byte)0x01, (true || false)), 24); //F2
            keyDict.Add(Tuple.Create((byte)0x3D, (byte)0x72, (byte)0x01, (true || false)), 36); //F3
            keyDict.Add(Tuple.Create((byte)0x3E, (byte)0x73, (byte)0x01, (true || false)), 48); //F4
            keyDict.Add(Tuple.Create((byte)0x3F, (byte)0x74, (byte)0x01, (true || false)), 60); //F5
            keyDict.Add(Tuple.Create((byte)0x40, (byte)0x75, (byte)0x01, (true || false)), 72); //F6
            keyDict.Add(Tuple.Create((byte)0x41, (byte)0x76, (byte)0x01, (true || false)), 84); //F7
            keyDict.Add(Tuple.Create((byte)0x42, (byte)0x77, (byte)0x01, (true || false)), 96); //F8
            keyDict.Add(Tuple.Create((byte)0x43, (byte)0x78, (byte)0x01, (true || false)), 108); //F9
            keyDict.Add(Tuple.Create((byte)0x44, (byte)0x79, (byte)0x01, (true || false)), 120); //F10
            keyDict.Add(Tuple.Create((byte)0x57, (byte)0x7A, (byte)0x01, (true || false)), 132); //F11
            keyDict.Add(Tuple.Create((byte)0x58, (byte)0x7B, (byte)0x01, (true || false)), 6); //F12

            // Keys around letters
            keyDict.Add(Tuple.Create((byte)0x01, (byte)0x1B, (byte)0x01, (true || false)), 0); //Escape
            keyDict.Add(Tuple.Create((byte)0x29, (byte)0xC0, (byte)0x01, (true || false)), 1); //Tilde
            keyDict.Add(Tuple.Create((byte)0x0F, (byte)0x09, (byte)0x01, (true || false)), 2); //Tab
            keyDict.Add(Tuple.Create((byte)0x3A, (byte)0x14, (byte)0x01, (true || false)), 3); //Caps Lock
            keyDict.Add(Tuple.Create((byte)0x2A, (byte)0x10, (byte)0x01, (true || false)), 4); //Left Shift
            keyDict.Add(Tuple.Create((byte)0x1D, (byte)0x11, (byte)0x01, (true || false)), 5); //Left Control
            keyDict.Add(Tuple.Create((byte)0x5B, (byte)0x5B, (byte)0x03, (true || false)), 17); //Left Win
            keyDict.Add(Tuple.Create((byte)0x38, (byte)0x12, (byte)0x01, (true || false)), 29); //Left Alt
            keyDict.Add(Tuple.Create((byte)0x39, (byte)0x20, (byte)0x01, (true || false)), 53); //Space
            keyDict.Add(Tuple.Create((byte)0x38, (byte)0x12, (byte)0x03, (true || false)), 89); //Right Alt
            keyDict.Add(Tuple.Create((byte)0x5C, (byte)0x5C, (byte)0x03, (true || false)), 101); //Right Win
            keyDict.Add(Tuple.Create((byte)0x5D, (byte)0x5D, (byte)0x03, (true || false)), 113); //Right Apps
            keyDict.Add(Tuple.Create((byte)0x1D, (byte)0x11, (byte)0x03, (true || false)), 91); //Right Control
            keyDict.Add(Tuple.Create((byte)0x36, (byte)0x10, (byte)0x01, (true || false)), 79); //Right Shift
            keyDict.Add(Tuple.Create((byte)0x1C, (byte)0x0D, (byte)0x01, (true || false)), 126); //Enter
            keyDict.Add(Tuple.Create((byte)0x2B, (byte)0xDC, (byte)0x01, (true || false)), 102); //Pipe
            keyDict.Add(Tuple.Create((byte)0x0E, (byte)0x08, (byte)0x01, (true || false)), 31); //Backspace
            keyDict.Add(Tuple.Create((byte)0x0C, (byte)0xBD, (byte)0x01, (true || false)), 133); // -
            keyDict.Add(Tuple.Create((byte)0x0D, (byte)0xBB, (byte)0x01, (true || false)), 7); // =
            keyDict.Add(Tuple.Create((byte)0x1A, (byte)0xDB, (byte)0x01, (true || false)), 134); // [
            keyDict.Add(Tuple.Create((byte)0x1B, (byte)0xDD, (byte)0x01, (true || false)), 90); // ]
            keyDict.Add(Tuple.Create((byte)0x27, (byte)0xBA, (byte)0x01, (true || false)), 123); // ;
            keyDict.Add(Tuple.Create((byte)0x28, (byte)0xDE, (byte)0x01, (true || false)), 135); // '
            keyDict.Add(Tuple.Create((byte)0x33, (byte)0xBC, (byte)0x01, (true || false)), 112); // ,
            keyDict.Add(Tuple.Create((byte)0x34, (byte)0xBE, (byte)0x01, (true || false)), 124); // .
            keyDict.Add(Tuple.Create((byte)0x35, (byte)0xBF, (byte)0x01, (true || false)), 136); // /
            keyDict.Add(Tuple.Create((byte)0x2B, (byte)0xE2, (byte)0x01, (true || false)), 16); //EU \

            // System Keys
            keyDict.Add(Tuple.Create((byte)0x2A, (byte)0xFF, (byte)0x03, true), 18); //Print Screen
            keyDict.Add(Tuple.Create((byte)0x37, (byte)0x2C, (byte)0x03, (true || false)), 18); //Print Screen
            keyDict.Add(Tuple.Create((byte)0x37, (byte)0x2C, (byte)0x02, (true || false)), 18); //Print Screen
            keyDict.Add(Tuple.Create((byte)0x46, (byte)0x91, (byte)0x01, (true || false)), 30); //Scroll Lock
            keyDict.Add(Tuple.Create((byte)0x1D, (byte)0x13, (byte)0x05, (true || false)), 42); //Pause
            keyDict.Add(Tuple.Create((byte)0x1D, (byte)0x13, (byte)0x04, (true || false)), 42); //Pause
            keyDict.Add(Tuple.Create((byte)0x45, (byte)0xFF, (byte)0x01, (true || false)), 42); //Pause

            // Navigation with NumLock ON
            keyDict.Add(Tuple.Create((byte)0x52, (byte)0x2D, (byte)0x03, (true || false)), 54); //Insert
            keyDict.Add(Tuple.Create((byte)0x47, (byte)0x24, (byte)0x03, (true || false)), 66); //Home
            keyDict.Add(Tuple.Create((byte)0x49, (byte)0x21, (byte)0x03, (true || false)), 78); //Page Up
            keyDict.Add(Tuple.Create((byte)0x53, (byte)0x2E, (byte)0x03, (true || false)), 43); //Delete
            keyDict.Add(Tuple.Create((byte)0x4F, (byte)0x23, (byte)0x03, (true || false)), 55); //End
            keyDict.Add(Tuple.Create((byte)0x51, (byte)0x22, (byte)0x03, (true || false)), 67); //Page Down

            // Navigation with NumLock OFF
            keyDict.Add(Tuple.Create((byte)0x52, (byte)0x2D, (byte)0x02, (true || false)), 54); //Insert
            keyDict.Add(Tuple.Create((byte)0x47, (byte)0x24, (byte)0x02, (true || false)), 66); //Home
            keyDict.Add(Tuple.Create((byte)0x49, (byte)0x21, (byte)0x02, (true || false)), 78); //Page Up
            keyDict.Add(Tuple.Create((byte)0x53, (byte)0x2E, (byte)0x02, (true || false)), 43); //Delete
            keyDict.Add(Tuple.Create((byte)0x4F, (byte)0x23, (byte)0x02, (true || false)), 55); //End
            keyDict.Add(Tuple.Create((byte)0x51, (byte)0x22, (byte)0x02, (true || false)), 67); //Page Down

            // Arrows with NumLock ON
            keyDict.Add(Tuple.Create((byte)0x48, (byte)0x26, (byte)0x03, (true || false)), 103); //Up
            keyDict.Add(Tuple.Create((byte)0x4B, (byte)0x25, (byte)0x03, (true || false)), 115); //Left
            keyDict.Add(Tuple.Create((byte)0x50, (byte)0x28, (byte)0x03, (true || false)), 127); //Down
            keyDict.Add(Tuple.Create((byte)0x4D, (byte)0x27, (byte)0x03, (true || false)), 139); //Right

            // Arrows with NumLock OFF
            keyDict.Add(Tuple.Create((byte)0x48, (byte)0x26, (byte)0x02, (true || false)), 103); //Up
            keyDict.Add(Tuple.Create((byte)0x4B, (byte)0x25, (byte)0x02, (true || false)), 115); //Left
            keyDict.Add(Tuple.Create((byte)0x50, (byte)0x28, (byte)0x02, (true || false)), 127); //Down
            keyDict.Add(Tuple.Create((byte)0x4D, (byte)0x27, (byte)0x02, (true || false)), 139); //Right

            // NumPad Operators
            keyDict.Add(Tuple.Create((byte)0x45, (byte)0x90, (byte)0x01, (true || false)), 80); //NumLock
            keyDict.Add(Tuple.Create((byte)0x35, (byte)0x6F, (byte)0x03, (true || false)), 92); // /
            keyDict.Add(Tuple.Create((byte)0x35, (byte)0x6F, (byte)0x02, (true || false)), 92); // /
            keyDict.Add(Tuple.Create((byte)0x37, (byte)0x6A, (byte)0x01, (true || false)), 104); // *
            keyDict.Add(Tuple.Create((byte)0x4A, (byte)0x6D, (byte)0x01, (true || false)), 116); // -
            keyDict.Add(Tuple.Create((byte)0x4E, (byte)0x6B, (byte)0x01, (true || false)), 128); // +
            keyDict.Add(Tuple.Create((byte)0x1C, (byte)0x0D, (byte)0x03, (true || false)), 140); //Enter
            keyDict.Add(Tuple.Create((byte)0x1C, (byte)0x0D, (byte)0x02, (true || false)), 140); //Enter

            // NumPad with NumLock ON
            keyDict.Add(Tuple.Create((byte)0x4F, (byte)0x61, (byte)0x01, (true || false)), 93); //1
            keyDict.Add(Tuple.Create((byte)0x50, (byte)0x62, (byte)0x01, (true || false)), 105); //2
            keyDict.Add(Tuple.Create((byte)0x51, (byte)0x63, (byte)0x01, (true || false)), 117); //3
            keyDict.Add(Tuple.Create((byte)0x4B, (byte)0x64, (byte)0x01, (true || false)), 57); //4
            keyDict.Add(Tuple.Create((byte)0x4C, (byte)0x65, (byte)0x01, (true || false)), 69); //5
            keyDict.Add(Tuple.Create((byte)0x4D, (byte)0x66, (byte)0x01, (true || false)), 81); //6
            keyDict.Add(Tuple.Create((byte)0x47, (byte)0x67, (byte)0x01, (true || false)), 9); //7
            keyDict.Add(Tuple.Create((byte)0x48, (byte)0x68, (byte)0x01, (true || false)), 21); //8
            keyDict.Add(Tuple.Create((byte)0x49, (byte)0x69, (byte)0x01, (true || false)), 33); //9
            keyDict.Add(Tuple.Create((byte)0x52, (byte)0x60, (byte)0x01, (true || false)), 129); //0
            keyDict.Add(Tuple.Create((byte)0x53, (byte)0x6E, (byte)0x01, (true || false)), 141); //Decimal

            // NumPad with NumLock OFF
            keyDict.Add(Tuple.Create((byte)0x4F, (byte)0x23, (byte)0x01, (true || false)), 93); //1
            keyDict.Add(Tuple.Create((byte)0x50, (byte)0x28, (byte)0x01, (true || false)), 105); //2
            keyDict.Add(Tuple.Create((byte)0x51, (byte)0x22, (byte)0x01, (true || false)), 117); //3
            keyDict.Add(Tuple.Create((byte)0x4B, (byte)0x25, (byte)0x01, (true || false)), 57); //4
            keyDict.Add(Tuple.Create((byte)0x4C, (byte)0x0C, (byte)0x01, (true || false)), 69); //5
            keyDict.Add(Tuple.Create((byte)0x4D, (byte)0x27, (byte)0x01, (true || false)), 81); //6
            keyDict.Add(Tuple.Create((byte)0x47, (byte)0x24, (byte)0x01, (true || false)), 9); //7
            keyDict.Add(Tuple.Create((byte)0x48, (byte)0x26, (byte)0x01, (true || false)), 21); //8
            keyDict.Add(Tuple.Create((byte)0x49, (byte)0x21, (byte)0x01, (true || false)), 33); //9
            keyDict.Add(Tuple.Create((byte)0x52, (byte)0x2D, (byte)0x01, (true || false)), 129); //0
            keyDict.Add(Tuple.Create((byte)0x53, (byte)0x2E, (byte)0x01, (true || false)), 141); //Decimal

            // Media Keys
            keyDict.Add(Tuple.Create((byte)0x00, (byte)0xB2, (byte)0x03, (true || false)), 32); //Stop
            keyDict.Add(Tuple.Create((byte)0x00, (byte)0xB1, (byte)0x03, (true || false)), 44); //Previous
            keyDict.Add(Tuple.Create((byte)0x00, (byte)0xB3, (byte)0x03, (true || false)), 56); //Play
            keyDict.Add(Tuple.Create((byte)0x00, (byte)0xB0, (byte)0x03, (true || false)), 68); //Next
            keyDict.Add(Tuple.Create((byte)0x00, (byte)0xAD, (byte)0x03, (true || false)), 20); //Mute
        }

        public int GetKeyCodeFromDict(int mcode, int vkey, int flag, bool numLock)
        { return (keyDict[Tuple.Create((byte)mcode, (byte)vkey, (byte)flag, numLock)]); }
    }

    public class CpuUsageClass
    {
        // Performace Stuff
        private PerformanceCounter cpuCounter;
        private double CurrentValue = 0;
        private DateTime LastUpdateTime = DateTime.Now;
        private TimeSpan TimeSinceLastUpdate;
        private int CumulativeSamples = 0;
        private double CumulativeValue = 0;

        public CpuUsageClass()
        {
            cpuCounter = new PerformanceCounter();
            cpuCounter.CategoryName = "Processor";
            cpuCounter.CounterName = "% Processor Time";
            cpuCounter.InstanceName = "_Total";
        }

        public double GetUsage()
        {
            if (LastUpdateTime == null) { LastUpdateTime = DateTime.Now; }
            TimeSinceLastUpdate = DateTime.Now - LastUpdateTime;

            if (TimeSinceLastUpdate.TotalMilliseconds < Properties.Settings.Default.CpuUpdateTime)
            {
                CumulativeValue += (cpuCounter.NextValue() / 100);
                CumulativeSamples += 1;
            }
            else
            {
                LastUpdateTime = DateTime.Now;
                CurrentValue = (CumulativeValue / CumulativeSamples);
                CumulativeSamples = 0;
                CumulativeValue = 0;
            }
            return CurrentValue;
        }
    }
}