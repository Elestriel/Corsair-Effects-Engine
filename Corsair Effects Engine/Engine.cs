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

        private static KeyData[] Keys = MainWindow.keyData;

        public static double BackgroundAnim = 0;
        
        public static void Start()
        {
            UpdateStatusMessage.NewMessage(5, "Engine started.");

            EngineComponents.InitDevices DeviceInit = new EngineComponents.InitDevices();
            EngineComponents.DeviceOutput Output = new EngineComponents.DeviceOutput();

            while (RunEngine)
            {
                UpdateStatusMessage.NewMessage(5, "Initializing Keyboard.");
                // Initialize keyboard
                KeyboardPointer = DeviceInit.GetKeyboardPointer();

                // Initialize mouse
                MousePointer = DeviceInit.GetMousePointer();

                while (!PauseEngine && RunEngine && !RestartEngine)
                {
                    // Use 'rendered' flag to occlude following layers
                    // Render static layer

                    // Render background layer
                    if (Properties.Settings.Default.BackgroundEffectEnabled) { RenderBackground(); };

                    // Render foreground layer
                    if (Properties.Settings.Default.ForegroundEffectEnabled) { RenderForeground(); };

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
            switch (Properties.Settings.Default.BackgroundEffect)
            {
                case "Rainbow":
                    int tBrightness = (byte)(Properties.Settings.Default.BackgroundBrightness / 2);
                    BackgroundAnim += 1;
                    if (BackgroundAnim > 103) { BackgroundAnim = 0; };

                    for (int y = 0; y < 7; y++)
                    {
                        for (int x = 0; x < KeyboardMap.CanvasWidth - 1; x++)
                        {
                            int key = KeyboardMap.LedMatrix[y, x];
                            if (key != 255)
                            {
                                if (!Keys[key].KeyColor.EffectInProgress)
                                {
                                    Keys[key].KeyColor = new LightSingle(lightColor:
                                        Color.FromRgb((byte)(tBrightness * (Math.Sin(((x - BackgroundAnim) / KeyboardMap.CanvasWidth) * 2 * 3.14f)) + 1 + tBrightness),
                                                      (byte)(tBrightness * (Math.Sin((((x - BackgroundAnim) / KeyboardMap.CanvasWidth) * 2 * 3.14f) - (6.28f / 3))) + 1 + tBrightness),
                                                      (byte)(tBrightness * (Math.Sin((((x - BackgroundAnim) / KeyboardMap.CanvasWidth) * 2 * 3.14f) + (6.28f / 3))) + 1 + tBrightness)));
                                }
                            }
                        }
                    }
                    break;
            }
        }

        private static void RenderForeground()
        {
            switch (Properties.Settings.Default.ForegroundEffect)
            {
                case "Spectrograph":
                    break;
                case "Random Lights":
                    #region Code
                    Random rnd = new Random();
                    Color SL = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsColorStartLower);
                    Color SU = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsColorStartUpper);
                    Color EL = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsColorEndLower);
                    Color EU = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsColorEndUpper);
                    Color startColor = Color.FromRgb(255, 255, 255);
                    Color endColor = Color.FromRgb(0, 0, 0);

                    // Test Render
                    int keyLight = rnd.Next(0, 149);
                    if (Keys[keyLight].KeyColor.EffectInProgress == false)
                    {
                        switch (Properties.Settings.Default.ForegroundRandomLightsStartType)
                        {
                            case "Defined Colour":
                                startColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsSolidColorStart);
                                break;
                            case "Random Colour":
                                startColor = Color.FromRgb((byte)rnd.Next(SL.R,SU.R), (byte)rnd.Next(SL.G,SU.G), (byte)rnd.Next(SL.B,SU.B));
                                break;
                        }
                        switch (Properties.Settings.Default.ForegroundRandomLightsEndType)
                        {
                            case "Defined Colour":
                                endColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.ForegroundRandomLightsSolidColorEnd);
                                break;
                            case "Random Colour":
                                endColor = Color.FromRgb((byte)rnd.Next(EL.R,EU.R), (byte)rnd.Next(EL.G,EU.G), (byte)rnd.Next(EL.B,EU.B));
                                break;
                        }

                        switch (Properties.Settings.Default.ForegroundRandomLightsStyle)
                        {
                            case "Solid":
                                Keys[keyLight].KeyColor = new LightSolid(startColor: startColor,
                                                                        endColor: endColor,
                                                                        duration: Properties.Settings.Default.ForegroundRandomLightsSolidDuration);
                                break;
                            case "Fade":
                                Keys[keyLight].KeyColor = new LightFade(startColor: startColor,
                                                                        endColor: endColor,
                                                                        solidDuration: Properties.Settings.Default.ForegroundRandomLightsFadeSolidDuration,
                                                                        totalDuration: Properties.Settings.Default.ForegroundRandomLightsFadeTotalDuration);
                                break;
                        }
                    }
                    #endregion Code
                    break;
                case "Reactive Typing":
                    break;
                case "Heatmap":
                    break;
            }

        }
    }
}
