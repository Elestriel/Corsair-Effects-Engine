using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.DSP;
using CSCore.SoundIn;
using CSCore.Utils;

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

        // Pointers to the keyboard and mouse HIDs
        private IntPtr KeyboardPointer;
        private IntPtr MousePointer;

        // Key Data
        private KeyData[] BackgroundKeys = new KeyData[149];
        private KeyData[] ForegroundKeys = new KeyData[149];
        private KeyData[] ReactiveKeys = new KeyData[149];
        private static KeyData[] SpectroKeys = new KeyData[149];
        private KeyData[] Keys = MainWindow.keyData;

        // Effect variables
        private double BackgroundAnim = 0;
        public int HeatmapHighestStrikeCount = 0;
        public int[] HeatmapStrikeCount = new int[149];
        Random rnd = new Random();

        private string LastForegroundEffect = "";
        private string LastBackgroundEffect = "";
        //private string LastStaticProfile = "";

        // CSCore Stuff
        private static WasapiCapture audioCapture;
        private static SampleAggregator sampleAggregator;
        private static int fftLength;

        public Engine()
        {
            InputHook.OnRawInputFromKeyboard += InputFromKeyboard;
        }

        public void Start()
        {
            UpdateStatusMessage.NewMessage(5, "Initializing Engine.");

            EngineComponents.InitDevices DeviceInit = new EngineComponents.InitDevices();
            EngineComponents.DeviceOutput Output = new EngineComponents.DeviceOutput();

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

                // Initialize mouse
                MousePointer = DeviceInit.GetMousePointer();

                // Initialize background animation sync
                DateTime lastResetTime = DateTime.Now;
                TimeSpan timeDifference;
                double timeDifferenceMS;

                // Creates handles for CSCore
                CSCore_Initialize();

                UpdateStatusMessage.NewMessage(5, "Initialization Complete.");

                while (!PauseEngine && RunEngine && !RestartEngine)
                {
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
                    RenderForeground();

                    // Blend layers
                    BlendLayers();

                    // Output frame to keyboard preview

                    // Output frame to devices
                    if (Properties.Settings.Default.Opt16MColours)
                    { Output.UpdateKeyboard16M(KeyboardPointer, Keys); }
                    else { Output.UpdateKeyboard(KeyboardPointer, Keys); };

                    Output.UpdateMouse(MousePointer, Keys);

                    //UpdateStatusMessage.NewMessage(5, "Engine MainLoop");
                    Thread.Sleep(Properties.Settings.Default.OptFrameDelay);
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
            }
            if (Properties.Settings.Default.OptRestoreLightingOnExit)
            {
                UpdateStatusMessage.NewMessage(5, "Restoring lighting.");
                Output.RestoreKeyboard(KeyboardPointer);
                Output.RestoreMouse(MousePointer);
            }
            InputHook.OnRawInputFromKeyboard -= InputFromKeyboard;

            CSCore_StopCapture();
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
                int refKey = 140;
                if (Properties.Settings.Default.KeyboardModel == "K65-RGB") { refKey = 139; };

                if (LastBackgroundEffect != Properties.Settings.Default.BackgroundEffect) { ClearAllKeys(); };
                LastBackgroundEffect = Properties.Settings.Default.BackgroundEffect;

                switch (Properties.Settings.Default.BackgroundEffect)
                {
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
                                        }
                                        BackgroundKeys[key].KeyColor = new LightSingle(ColorFromHSV(rainbowKey * 360, 1, tBrightness));
                                        for (int k = 0; k < 5; k++) { BackgroundKeys[144 + k].KeyColor = BackgroundKeys[refKey].KeyColor; };
                                    }
                                }
                            }
                        }
                        #endregion Rainbow Code
                        break;
                    case "Spectrum Cycle":
                        for (int i = 0; i < 149; i++)
                        {
                            BackgroundKeys[i].KeyColor = new LightSingle(lightColor: ColorFromHSV((BackgroundAnim / KeyboardMap.CanvasWidth) * 360, 1, tBrightness));
                        }
                        break;
                    case "Solid":
                        for (int i = 0; i < 149; i++) {
                            BackgroundKeys[i].KeyColor = new LightSingle(lightColor: (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.BackgroundSolidColor));
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

                if (LastForegroundEffect != Properties.Settings.Default.ForegroundEffect) { ClearAllKeys(); };
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
                                    if (keyLight == 145) { UpdateStatusMessage.NewMessage(0, "Mouse 2: " + startColor.ToString() + " " + endColor.ToString() + " " +
                                        Properties.Settings.Default.ForegroundRandomLightsFadeSolidDuration + " " + Properties.Settings.Default.ForegroundRandomLightsFadeTotalDuration); };
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
                }
            }
            else
            {
                for (int i = 0; i < 149; i++)
                { ForegroundKeys[i].KeyColor = new LightSingle(lightColor: Color.FromArgb(0, 0, 0, 0)); }
            }
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
                //Keys[i].KeyColor = new LightSingle(lightColor: newColor);
            }
        }

        public void InputFromKeyboard(RAWINPUTHEADER riHeader, RAWKEYBOARD riKeyboard)
        {
            if (riKeyboard.Flags == 0x0) { return; };
            if (riKeyboard.Flags == 0x2 && Keyboard.IsKeyToggled(Key.NumLock)) { return; };

            int keyLight = InputKeys.GetKeyCodeFromDict(riKeyboard.MakeCode, riKeyboard.VKey, riKeyboard.Flags, Keyboard.IsKeyToggled(Key.NumLock));

            if (Properties.Settings.Default.ForegroundEffect == "Reactive Typing")
            {
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
            } //Reactive Typing
            else if (Properties.Settings.Default.ForegroundEffect == "Heatmap")
            {
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

                    //ReactiveKeys[i].KeyColor = new LightSingle(lightColor: AlphaBlend(CM, CL));
                }
            } //Heatmap
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

        #region CSCore Methods
        
        private static void CSCore_Initialize()
        {
            MMDeviceEnumerator deviceEnum = new MMDeviceEnumerator();

            List<MMDevice> AudioDeviceList;
            if (Properties.Settings.Default.OptAudioFromInput)
            { AudioDeviceList = new List<MMDevice>(deviceEnum.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active).ToArray()); }
            else
            { AudioDeviceList = new List<MMDevice>(deviceEnum.EnumAudioEndpoints(DataFlow.Render, DeviceState.Active).ToArray()); }

            MMDevice captureDevice = deviceEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            foreach (MMDevice dev in AudioDeviceList)
            {
                if ((dev.FriendlyName == Properties.Settings.Default.AudioOutputDevice &&
                    Properties.Settings.Default.OptAudioFromOutput) ||
                    (dev.FriendlyName == Properties.Settings.Default.AudioInputDevice &&
                    Properties.Settings.Default.OptAudioFromInput))
                    { captureDevice = dev; };
            }

            switch (Properties.Settings.Default.OptAudioFromOutput)
            {
                case true:
                    audioCapture = new WasapiLoopbackCapture();
                    break;
                case false:
                    audioCapture = new WasapiCapture();
                    audioCapture.Device = captureDevice;
                    break;
                default:
                    audioCapture = new WasapiLoopbackCapture();
                    break;
            }

            UpdateStatusMessage.NewMessage(6, captureDevice.FriendlyName);
            audioCapture.Initialize();
            UpdateStatusMessage.NewMessage(0, audioCapture.WaveFormat.Channels.ToString());
            int captureSampleRate = audioCapture.WaveFormat.SampleRate;
            switch (captureSampleRate)
            {
                case 48000: fftLength = 1024; break;
                case 96000: fftLength = 2048; break;
                case 192000: fftLength = 4096; break;
                default: fftLength = 1024; break;
            }

            sampleAggregator = new SampleAggregator(fftLength);
            sampleAggregator.PerformFFT = true;
            sampleAggregator.FftCalculated += new EventHandler<FftEventArgs>(FftCalculated);

            audioCapture.DataAvailable += new EventHandler<DataAvailableEventArgs>(CSCore_DataAvailable);

            audioCapture.Start();
        }

        private static void CSCore_DataAvailable(object sender, DataAvailableEventArgs e)
        {
            byte[] buffer = e.Data;
            int bytesRecorded = e.ByteCount;
            int bufferIncrement = audioCapture.WaveFormat.BlockAlign;

            for (int index = 0; index < bytesRecorded; index += bufferIncrement)
            {
                float sample32 = BitConverter.ToSingle(buffer, index);
                if (sampleAggregator.Add(sample32) == true)
                {
                    break;
                };
            }
        }

        private static void CSCore_StopCapture()
        {
            UpdateStatusMessage.NewMessage(2, "Stopping Capture");
            try { audioCapture.Stop(); }
            catch { }
            CSCore_Cleanup();
        }

        private static void CSCore_Cleanup()
        {
            if (audioCapture != null)
            {
                audioCapture.Dispose();
                audioCapture = null;
            }
            UpdateStatusMessage.NewMessage(2, "Capture Destroyed");
        }

        private static void FftCalculated(object sender, FftEventArgs e)
        {
            int CanvasWidth = KeyboardMap.CanvasWidth;
            int key;

            //float[] fftData = new float[fftLength];
            byte[] fftData = new byte[fftLength];
            
            for (int i = 0; i < fftLength - 1; i+=2)
            {
                //fftData[i] = e.Result[i].Real;
                double fftmag = Math.Sqrt((e.Result[i].Real * e.Result[i + 1].Real) + (e.Result[i].Imaginary * e.Result[i + 1].Imaginary));
                //double fftmag = Math.Sqrt((e.Result[i].Value * e.Result[i + 1].Value));
                fftData[i] = (byte)(fftmag);
            }

            byte[] compData = CompressFftToKeyboard(fftData, CanvasWidth);
            //float[] compData = CompressFftToKeyboard(fftData, CanvasWidth);
            //float[] compData = fftData;

            for (int i = 0; i < CanvasWidth; i++)
            {
                for (int k = 0; k < 7; k++)
                {
                    key = KeyboardMap.LedMatrix[k, i];
                    if ((int)compData[i] > ((32 / (1 + (i * .95))) * (7 - k)))
                    //if (compData[i] > (-Math.Log10(i)+2.2) * (7-k))
                    //if (compData[i] > Math.Sqrt((7 - k) / 6))
                    {
                        if (key >= 0 && key < 144) { SpectroKeys[key].KeyColor = new LightSingle(lightColor: Color.FromArgb(255, 255, 255, 255)); };
                    }
                    else { if (key >= 0 && key < 144) { SpectroKeys[key].KeyColor = new LightSingle(lightColor: Color.FromArgb(0, 0, 0, 0)); } };
                }
            }
        }

        private static float[] CompressFftToKeyboard(float[] fftData, int kWidth)
        {
            float[] compressedData = new float[kWidth];

            int tempCalc;
            double iPower;
            string tempStr = "";
            int prevCalc = 0;

            for (int i = 0; i < kWidth; i++)
            {
                iPower = Math.Pow((double)i, 2);
                float keyboardSizeMultiplier = (float)(Math.Pow(kWidth, 2));
                tempCalc = (int)((330f / keyboardSizeMultiplier) * iPower) + 1;

                while (prevCalc >= tempCalc) { tempCalc += 1; };

                compressedData[i] = Math.Abs(fftData[tempCalc]);
                prevCalc = tempCalc;

                tempStr += tempCalc + ", ";
            }

            return compressedData;
        }

        private static byte[] CompressFftToKeyboard(byte[] fftData, int kWidth)
        {
            byte[] compressedData = new byte[kWidth];

            int tempCalc;
            double iPower;
            string tempStr = "";
            int prevCalc = 0;

            for (int i = 0; i < kWidth; i++)
            {
                iPower = Math.Pow((double)i, 2);
                float keyboardSizeMultiplier = (float)(Math.Pow(kWidth, 2));
                tempCalc = (int)((330f / keyboardSizeMultiplier) * iPower) + 1;

                while (prevCalc >= tempCalc) { tempCalc += 1; };

                compressedData[i] = fftData[tempCalc];
                prevCalc = tempCalc;

                tempStr += tempCalc + ", ";
            }

            return compressedData;
        }
        
        #endregion CSCore Methods
    }

    public class RawInputKeyCodes
    {
        Dictionary<Tuple<byte, byte, byte, bool>, int> keyDict;
        
        public RawInputKeyCodes() {
            keyDict = new Dictionary<Tuple<byte,byte,byte,bool>,int>();

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

    #region CSCore Supporting Classes

    class SampleAggregator
    {
        public event EventHandler<FftEventArgs> FftCalculated;
        public bool PerformFFT { get; set; }

        private Complex[] fftBuffer;
        private FftEventArgs fftArgs;
        private int fftPos;
        private int fftLength;
        private int m;

        public SampleAggregator(int fftLength)
        {
            if (!IsPowerOfTwo(fftLength))
            {
                throw new ArgumentException("FFT Length must be a power of two");
            }
            this.m = (int)Math.Log(fftLength, 2.0);
            this.fftLength = fftLength;
            this.fftBuffer = new Complex[fftLength];
            this.fftArgs = new FftEventArgs(fftBuffer);
        }

        bool IsPowerOfTwo(int x)
        {
            return (x & (x - 1)) == 0;
        }

        public bool Add(float value)
        {
            if (PerformFFT && FftCalculated != null)
            {
                //fftBuffer[fftPos].Real = value * 0.5f * (float)Math.Cos((2 * Math.PI) / (fftLength - 1)) * 30 * 100;
                //fftBuffer[fftPos].Real = (value / 2f) / (60f / 8.69f);
                fftBuffer[fftPos].Real = (float)(value * FastFourierTransformation.HammingWindow(fftPos, fftLength));
                fftBuffer[fftPos].Imaginary = 0; // This is always zero with audio.
                fftPos++;
                if (fftPos >= fftLength)
                {
                    fftPos = 0;
                    FastFourierTransformation.Fft(fftBuffer, m, FftMode.Forward);
                    FftCalculated(this, fftArgs);
                    return true;
                }
            }
            return false;
        }
    }

    public class FftEventArgs : EventArgs
    {
        public FftEventArgs(Complex[] result)
        {
            this.Result = result;
        }
        public Complex[] Result { get; private set; }
    }

    #endregion CSCore Supporting Classes
}