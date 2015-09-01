using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Corsair_Effects_Engine
{
    public static class Engine
    {
        public static bool PauseEngine = false;
        public static bool RunEngine = false;
        public static bool RestartEngine = false;

        private static IntPtr KeyboardPointer;
        private static IntPtr MousePointer;

        private static KeyData[] BackgroundKeys = new KeyData[149];
        private static KeyData[] ForegroundKeys = new KeyData[149];
        private static KeyData[] Keys = MainWindow.keyData;

        public static double BackgroundAnim = 0;
        
        public static void Start()
        {
            UpdateStatusMessage.NewMessage(5, "Engine started.");

            EngineComponents.InitDevices DeviceInit = new EngineComponents.InitDevices();
            EngineComponents.DeviceOutput Output = new EngineComponents.DeviceOutput();

            for (int i = 0; i < 149; i++)
            {
                BackgroundKeys[i] = new KeyData();
                BackgroundKeys[i].KeyColor = new LightSwitch(startColor: Color.FromRgb(255, 255, 255),
                                                       endColor: Color.FromRgb(0, 0, 0),
                                                       duration: 0);
                ForegroundKeys[i] = new KeyData();
                ForegroundKeys[i].KeyColor = new LightSwitch(startColor: Color.FromRgb(255, 255, 255),
                                                       endColor: Color.FromRgb(0, 0, 0),
                                                       duration: 0);
            }

            while (RunEngine)
            {
                UpdateStatusMessage.NewMessage(5, "Initializing Keyboard.");
                // Initialize keyboard
                KeyboardPointer = DeviceInit.GetKeyboardPointer();

                // Initialize mouse
                MousePointer = DeviceInit.GetMousePointer();

                // Initialize background animation sync
                DateTime lastResetTime = DateTime.Now;
                TimeSpan timeDifference;
                double timeDifferenceMS;

                while (!PauseEngine && RunEngine && !RestartEngine)
                {
                    // Update animation sync
                    timeDifference = DateTime.Now - lastResetTime;
                    timeDifferenceMS = timeDifference.Seconds * 1000 + timeDifference.Milliseconds;

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
            UpdateStatusMessage.NewMessage(5, "Engine is shutting down.");
        }

        private static void RenderBackground()
        {
            if (Properties.Settings.Default.BackgroundEffectEnabled)
            {
                int tBrightness = (byte)(Properties.Settings.Default.BackgroundBrightness / 2);
                int refKey = 140;
                if (Properties.Settings.Default.KeyboardModel == "K65-RGB") {refKey = 139; };

                switch (Properties.Settings.Default.BackgroundEffect)
                {
                    case "Rainbow":
                        #region Rainbow Code
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
                                                BackgroundKeys[key].KeyColor = new LightSingle(lightColor:
                                                    Color.FromRgb((byte)(tBrightness * (Math.Sin(((x - BackgroundAnim) / KeyboardMap.CanvasWidth) * 2 * 3.14f)) + 1 + tBrightness),
                                                                    (byte)(tBrightness * (Math.Sin((((x - BackgroundAnim) / KeyboardMap.CanvasWidth) * 2 * 3.14f) - (6.28f / 3))) + 1 + tBrightness),
                                                                    (byte)(tBrightness * (Math.Sin((((x - BackgroundAnim) / KeyboardMap.CanvasWidth) * 2 * 3.14f) + (6.28f / 3))) + 1 + tBrightness)));
                                                break;
                                            case "Left":
                                                BackgroundKeys[key].KeyColor = new LightSingle(lightColor:
                                                    Color.FromRgb((byte)(tBrightness * (Math.Sin(((x + BackgroundAnim) / KeyboardMap.CanvasWidth) * 2 * 3.14f)) + 1 + tBrightness),
                                                                    (byte)(tBrightness * (Math.Sin((((x + BackgroundAnim) / KeyboardMap.CanvasWidth) * 2 * 3.14f) - (6.28f / 3))) + 1 + tBrightness),
                                                                    (byte)(tBrightness * (Math.Sin((((x + BackgroundAnim) / KeyboardMap.CanvasWidth) * 2 * 3.14f) + (6.28f / 3))) + 1 + tBrightness)));
                                                break;
                                        }
                                        for (int k = 0; k < 5; k++) { BackgroundKeys[144 + k].KeyColor = BackgroundKeys[refKey].KeyColor; };
                                    }
                                }
                            }
                        }
                        #endregion Rainbow Code
                        break;
                    case "Spectrum Cycle":
                        #region Spectrum Cycle Code
                        for (int i = 0; i < 149; i++)
                        {
                            BackgroundKeys[i].KeyColor = new LightSingle(lightColor:
                                Color.FromRgb((byte)(tBrightness * (Math.Sin((BackgroundAnim / KeyboardMap.CanvasWidth) * 2 * 3.14f)) + 1 + tBrightness),
                                              (byte)(tBrightness * (Math.Sin(((BackgroundAnim / KeyboardMap.CanvasWidth) * 2 * 3.14f) - (6.28f / 3))) + 1 + tBrightness),
                                              (byte)(tBrightness * (Math.Sin(((BackgroundAnim / KeyboardMap.CanvasWidth) * 2 * 3.14f) + (6.28f / 3))) + 1 + tBrightness)));
                        }
                        #endregion Spectrum Cycle Code
                        break;
                }
            }
            else
            {
                for (int i = 0; i < 149; i++)
                { BackgroundKeys[i].KeyColor = new LightSingle(lightColor: Color.FromArgb(0, 0, 0, 0)); }
            }
        }

        private static void RenderForeground()
        {
            if (Properties.Settings.Default.ForegroundEffectEnabled)
            {
                switch (Properties.Settings.Default.ForegroundEffect)
                {
                    case "Spectrograph":
                        break;
                    case "Random Lights":
                        #region Random Lights Code
                        Random rnd = new Random();
                        Color SL = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsColorStartLower);
                        Color SU = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsColorStartUpper);
                        Color EL = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsColorEndLower);
                        Color EU = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsColorEndUpper);
                        Color startColor = Color.FromRgb(255, 255, 255);
                        Color endColor = Color.FromRgb(0, 0, 0);

                        // Test Render
                        int keyLight = rnd.Next(0, 149);
                        if (ForegroundKeys[keyLight].KeyColor.EffectInProgress == false)
                        {
                            switch (Properties.Settings.Default.ForegroundRandomLightsStartType)
                            {
                                case "Defined Colour":
                                    startColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsSolidColorStart);
                                    break;
                                case "Random Colour":
                                    startColor = Color.FromRgb((byte)rnd.Next(SL.R, SU.R), (byte)rnd.Next(SL.G, SU.G), (byte)rnd.Next(SL.B, SU.B));
                                    break;
                            }
                            switch (Properties.Settings.Default.ForegroundRandomLightsEndType)
                            {
                                case "Defined Colour":
                                    endColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsSolidColorEnd);
                                    break;
                                case "Background":
                                    endColor = Color.FromArgb(0, startColor.R, startColor.G, startColor.B);
                                    break;
                                case "Random Colour":
                                    endColor = Color.FromRgb((byte)rnd.Next(EL.R, EU.R), (byte)rnd.Next(EL.G, EU.G), (byte)rnd.Next(EL.B, EU.B));
                                    break;
                            }

                            switch (Properties.Settings.Default.ForegroundRandomLightsStyle)
                            {
                                case "Solid":
                                    ForegroundKeys[keyLight].KeyColor = new LightSwitch(startColor: startColor,
                                                                            endColor: endColor,
                                                                            duration: Properties.Settings.Default.ForegroundRandomLightsSolidDuration);
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
                        break;
                    case "Heatmap":
                        break;
                }
            }
            else
            {
                for (int i = 0; i < 149; i++)
                { ForegroundKeys[i].KeyColor = new LightSingle(lightColor: Color.FromArgb(0, 0, 0, 0)); }
            }
        }

        private static void BlendLayers()
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
                Keys[i].KeyColor = new LightSingle(lightColor: newColor);
            }
        }
    }
}
