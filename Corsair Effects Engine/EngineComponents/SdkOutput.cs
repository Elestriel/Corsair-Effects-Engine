using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CUE.NET;
using CUE.NET.Devices.Generic;
using CUE.NET.Devices.Generic.Enums;
using CUE.NET.Devices.Headset;
using CUE.NET.Devices.Headset.Enums;
using CUE.NET.Devices.Mouse;
using CUE.NET.Devices.Mouse.Enums;
using CUE.NET.Devices.Keyboard;
using CUE.NET.Devices.Keyboard.Enums;
using CUE.NET.Devices.Keyboard.Extensions;
using CUE.NET.Devices.Keyboard.Keys;
using CUE.NET.Exceptions;

namespace Corsair_Effects_Engine.EngineComponents
{
    class SdkOutput
    {
        private CorsairKeyboard sdkKeyboard;
        private CorsairMouse sdkMouse;
        private CorsairHeadset sdkHeadset;
        private CorsairKeyCodes sdkKeys = new CorsairKeyCodes();

        public SdkOutput()
        {

        }

        public bool InitializeSdk()
        {
            UpdateStatusMessage.NewMessage(4, "Initializing SDK");
            try { CueSDK.Initialize(); }
            catch (Exception e) { UpdateStatusMessage.NewMessage(3, e.Message.ToString()); return false; }
            if (CueSDK.LastError != CorsairError.Success) 
            {
                UpdateStatusMessage.NewMessage(3, "Error initializing SDK.");
                return false; 
            }
            FindKeyboard();
            FindMouse();
            FindHeadset();
            return true;
        }

        private bool FindKeyboard()
        {
            sdkKeyboard = CueSDK.KeyboardSDK;
            if (!CheckForCUEError() || sdkKeyboard == null)
            {
                UpdateStatusMessage.NewMessage(3, "Could not connect to keyboard using SDK.");
                return false;
            }
            else
            { return true; }
        }

        private bool FindMouse()
        {
            sdkMouse = CueSDK.MouseSDK;
            if (!CheckForCUEError() || sdkMouse == null)
            {
                UpdateStatusMessage.NewMessage(3, "Could not connect to mouse using SDK.");
                return false;
            }
            else
            { return true; }
        }

        private bool FindHeadset()
        {
            sdkHeadset = CueSDK.HeadsetSDK;
            if (!CheckForCUEError() || sdkHeadset == null)
            {
                UpdateStatusMessage.NewMessage(3, "Could not connect to headset using SDK.");
                return false;
            }
            else
            { return true; }
        }

        private bool CheckForCUEError()
        {
            if (CueSDK.LastError != CorsairError.Success)
            { return false; }
            else
            { return true; }
        }

        public void UpdateKeyboard(KeyData[] Keys)
        {
            if (!CheckForCUEError()) { return; }
            CorsairKeyboardKeyId ckey;
            sdkKeyboard.Color = System.Drawing.Color.Transparent;
            
            for (int i = 0; i < 144; i++)
            {
                ckey = sdkKeys.GetKeyCodeFromDict(i);
                if (ckey != CorsairKeyboardKeyId.Invalid)
                {
                    double opacityMultiplier = (double)Keys[i].KeyColor.LightColor.A / (double)255;
                    System.Drawing.Color keyColor = System.Drawing.Color.FromArgb(255,
                        (byte)((double)Keys[i].KeyColor.LightColor.R * (opacityMultiplier)),
                        (byte)((double)Keys[i].KeyColor.LightColor.G * (opacityMultiplier)),
                        (byte)((double)Keys[i].KeyColor.LightColor.B * (opacityMultiplier)));
                    if (sdkKeyboard[ckey] != null)
                    {
                        if (ckey == CorsairKeyboardKeyId.M1 && Properties.Settings.Default.OptInvertM1)
                        {
                            keyColor = System.Drawing.Color.FromArgb(255, Math.Abs(255 - keyColor.R), Math.Abs(255 - keyColor.G), Math.Abs(255 - keyColor.B));
                        }
                        { sdkKeyboard[ckey].Led.Color = keyColor; }
                    }
                }
            }
            sdkKeyboard.UpdateLeds();
        }

        public void UpdateMouse(KeyData[] Keys)
        {
            if (!CheckForCUEError()) { return; }
            // TODO
            return;
        }

        public void UpdateHeadset(System.Drawing.Color color)
        {
            if (!CheckForCUEError()) { return; }
            // TODO
            return;
        }
    }
}

public class CorsairKeyCodes
{
    Dictionary<int, CorsairKeyboardKeyId> sdkKeyDict;

    public CorsairKeyCodes()
    {
        sdkKeyDict = new Dictionary<int,CorsairKeyboardKeyId>();

        sdkKeyDict.Add(0, CorsairKeyboardKeyId.Escape);
        sdkKeyDict.Add(1, CorsairKeyboardKeyId.GraveAccentAndTilde);
        sdkKeyDict.Add(2, CorsairKeyboardKeyId.Tab);
        sdkKeyDict.Add(3, CorsairKeyboardKeyId.CapsLock);
        sdkKeyDict.Add(4, CorsairKeyboardKeyId.LeftShift);
        sdkKeyDict.Add(5, CorsairKeyboardKeyId.LeftCtrl);
        sdkKeyDict.Add(6, CorsairKeyboardKeyId.F12);
        sdkKeyDict.Add(7, CorsairKeyboardKeyId.EqualsAndPlus);
        sdkKeyDict.Add(8, CorsairKeyboardKeyId.WinLock);
        sdkKeyDict.Add(9, CorsairKeyboardKeyId.Keypad7);
        sdkKeyDict.Add(10, CorsairKeyboardKeyId.G1);
        sdkKeyDict.Add(11, CorsairKeyboardKeyId.MR);
        sdkKeyDict.Add(12, CorsairKeyboardKeyId.F1);
        sdkKeyDict.Add(13, CorsairKeyboardKeyId.D1);
        sdkKeyDict.Add(14, CorsairKeyboardKeyId.Q);
        sdkKeyDict.Add(15, CorsairKeyboardKeyId.A);
        sdkKeyDict.Add(16, CorsairKeyboardKeyId.NonUsBackslash);
        sdkKeyDict.Add(17, CorsairKeyboardKeyId.LeftGui);
        sdkKeyDict.Add(18, CorsairKeyboardKeyId.PrintScreen);
        sdkKeyDict.Add(19, CorsairKeyboardKeyId.Invalid);
        sdkKeyDict.Add(20, CorsairKeyboardKeyId.Mute);
        sdkKeyDict.Add(21, CorsairKeyboardKeyId.Keypad8);
        sdkKeyDict.Add(22, CorsairKeyboardKeyId.G2);
        sdkKeyDict.Add(23, CorsairKeyboardKeyId.M1);
        sdkKeyDict.Add(24, CorsairKeyboardKeyId.F2);
        sdkKeyDict.Add(25, CorsairKeyboardKeyId.D2);
        sdkKeyDict.Add(26, CorsairKeyboardKeyId.W);
        sdkKeyDict.Add(27, CorsairKeyboardKeyId.S);
        sdkKeyDict.Add(28, CorsairKeyboardKeyId.Z);
        sdkKeyDict.Add(29, CorsairKeyboardKeyId.LeftAlt);
        sdkKeyDict.Add(30, CorsairKeyboardKeyId.ScrollLock);
        sdkKeyDict.Add(31, CorsairKeyboardKeyId.Backspace);
        sdkKeyDict.Add(32, CorsairKeyboardKeyId.Stop);
        sdkKeyDict.Add(33, CorsairKeyboardKeyId.Keypad9);
        sdkKeyDict.Add(34, CorsairKeyboardKeyId.G3);
        sdkKeyDict.Add(35, CorsairKeyboardKeyId.M2);
        sdkKeyDict.Add(36, CorsairKeyboardKeyId.F3);
        sdkKeyDict.Add(37, CorsairKeyboardKeyId.D3);
        sdkKeyDict.Add(38, CorsairKeyboardKeyId.E);
        sdkKeyDict.Add(39, CorsairKeyboardKeyId.D);
        sdkKeyDict.Add(40, CorsairKeyboardKeyId.X);
        sdkKeyDict.Add(41, CorsairKeyboardKeyId.Invalid);
        sdkKeyDict.Add(42, CorsairKeyboardKeyId.PauseBreak);
        sdkKeyDict.Add(43, CorsairKeyboardKeyId.Delete);
        sdkKeyDict.Add(44, CorsairKeyboardKeyId.ScanPreviousTrack);
        sdkKeyDict.Add(45, CorsairKeyboardKeyId.Invalid);
        sdkKeyDict.Add(46, CorsairKeyboardKeyId.G4);
        sdkKeyDict.Add(47, CorsairKeyboardKeyId.M3);
        sdkKeyDict.Add(48, CorsairKeyboardKeyId.F4);
        sdkKeyDict.Add(49, CorsairKeyboardKeyId.D4);
        sdkKeyDict.Add(50, CorsairKeyboardKeyId.R);
        sdkKeyDict.Add(51, CorsairKeyboardKeyId.F);
        sdkKeyDict.Add(52, CorsairKeyboardKeyId.C);
        sdkKeyDict.Add(53, CorsairKeyboardKeyId.Space);
        sdkKeyDict.Add(54, CorsairKeyboardKeyId.Insert);
        sdkKeyDict.Add(55, CorsairKeyboardKeyId.End);
        sdkKeyDict.Add(56, CorsairKeyboardKeyId.PlayPause);
        sdkKeyDict.Add(57, CorsairKeyboardKeyId.Keypad4);
        sdkKeyDict.Add(58, CorsairKeyboardKeyId.G5);
        sdkKeyDict.Add(59, CorsairKeyboardKeyId.G11);
        sdkKeyDict.Add(60, CorsairKeyboardKeyId.F5);
        sdkKeyDict.Add(61, CorsairKeyboardKeyId.D5);
        sdkKeyDict.Add(62, CorsairKeyboardKeyId.T);
        sdkKeyDict.Add(63, CorsairKeyboardKeyId.G);
        sdkKeyDict.Add(64, CorsairKeyboardKeyId.V);
        sdkKeyDict.Add(65, CorsairKeyboardKeyId.Invalid);
        sdkKeyDict.Add(66, CorsairKeyboardKeyId.Home);
        sdkKeyDict.Add(67, CorsairKeyboardKeyId.PageDown);
        sdkKeyDict.Add(68, CorsairKeyboardKeyId.ScanNextTrack);
        sdkKeyDict.Add(69, CorsairKeyboardKeyId.Keypad5);
        sdkKeyDict.Add(70, CorsairKeyboardKeyId.G6);
        sdkKeyDict.Add(71, CorsairKeyboardKeyId.G12);
        sdkKeyDict.Add(72, CorsairKeyboardKeyId.F6);
        sdkKeyDict.Add(73, CorsairKeyboardKeyId.D6);
        sdkKeyDict.Add(74, CorsairKeyboardKeyId.Y);
        sdkKeyDict.Add(75, CorsairKeyboardKeyId.H);
        sdkKeyDict.Add(76, CorsairKeyboardKeyId.B);
        sdkKeyDict.Add(77, CorsairKeyboardKeyId.Invalid);
        sdkKeyDict.Add(78, CorsairKeyboardKeyId.PageUp);
        sdkKeyDict.Add(79, CorsairKeyboardKeyId.RightShift);
        sdkKeyDict.Add(80, CorsairKeyboardKeyId.NumLock);
        sdkKeyDict.Add(81, CorsairKeyboardKeyId.Keypad6);
        sdkKeyDict.Add(82, CorsairKeyboardKeyId.G7);
        sdkKeyDict.Add(83, CorsairKeyboardKeyId.G13);
        sdkKeyDict.Add(84, CorsairKeyboardKeyId.F7);
        sdkKeyDict.Add(85, CorsairKeyboardKeyId.D7);
        sdkKeyDict.Add(86, CorsairKeyboardKeyId.U);
        sdkKeyDict.Add(87, CorsairKeyboardKeyId.J);
        sdkKeyDict.Add(88, CorsairKeyboardKeyId.N);
        sdkKeyDict.Add(89, CorsairKeyboardKeyId.RightAlt);
        sdkKeyDict.Add(90, CorsairKeyboardKeyId.BracketRight);
        sdkKeyDict.Add(91, CorsairKeyboardKeyId.RightCtrl);
        sdkKeyDict.Add(92, CorsairKeyboardKeyId.KeypadSlash);
        sdkKeyDict.Add(93, CorsairKeyboardKeyId.Keypad1);
        sdkKeyDict.Add(94, CorsairKeyboardKeyId.G8);
        sdkKeyDict.Add(95, CorsairKeyboardKeyId.G14);
        sdkKeyDict.Add(96, CorsairKeyboardKeyId.F8);
        sdkKeyDict.Add(97, CorsairKeyboardKeyId.D8);
        sdkKeyDict.Add(98, CorsairKeyboardKeyId.I);
        sdkKeyDict.Add(99, CorsairKeyboardKeyId.K);
        sdkKeyDict.Add(100, CorsairKeyboardKeyId.M);
        sdkKeyDict.Add(101, CorsairKeyboardKeyId.RightGui);
        sdkKeyDict.Add(102, CorsairKeyboardKeyId.Backslash);
        sdkKeyDict.Add(103, CorsairKeyboardKeyId.UpArrow);
        sdkKeyDict.Add(104, CorsairKeyboardKeyId.KeypadAsterisk);
        sdkKeyDict.Add(105, CorsairKeyboardKeyId.Keypad2);
        sdkKeyDict.Add(106, CorsairKeyboardKeyId.G9);
        sdkKeyDict.Add(107, CorsairKeyboardKeyId.G15);
        sdkKeyDict.Add(108, CorsairKeyboardKeyId.F9);
        sdkKeyDict.Add(109, CorsairKeyboardKeyId.D9);
        sdkKeyDict.Add(110, CorsairKeyboardKeyId.O);
        sdkKeyDict.Add(111, CorsairKeyboardKeyId.L);
        sdkKeyDict.Add(112, CorsairKeyboardKeyId.CommaAndLessThan);
        sdkKeyDict.Add(113, CorsairKeyboardKeyId.Application);
        sdkKeyDict.Add(114, CorsairKeyboardKeyId.Invalid);
        sdkKeyDict.Add(115, CorsairKeyboardKeyId.LeftArrow);
        sdkKeyDict.Add(116, CorsairKeyboardKeyId.KeypadMinus);
        sdkKeyDict.Add(117, CorsairKeyboardKeyId.Keypad3);
        sdkKeyDict.Add(118, CorsairKeyboardKeyId.G10);
        sdkKeyDict.Add(119, CorsairKeyboardKeyId.G16);
        sdkKeyDict.Add(120, CorsairKeyboardKeyId.F10);
        sdkKeyDict.Add(121, CorsairKeyboardKeyId.D0);
        sdkKeyDict.Add(122, CorsairKeyboardKeyId.P);
        sdkKeyDict.Add(123, CorsairKeyboardKeyId.SemicolonAndColon);
        sdkKeyDict.Add(124, CorsairKeyboardKeyId.PeriodAndBiggerThan);
        sdkKeyDict.Add(125, CorsairKeyboardKeyId.Invalid);
        sdkKeyDict.Add(126, CorsairKeyboardKeyId.Enter);
        sdkKeyDict.Add(127, CorsairKeyboardKeyId.DownArrow);
        sdkKeyDict.Add(128, CorsairKeyboardKeyId.KeypadPlus);
        sdkKeyDict.Add(129, CorsairKeyboardKeyId.Keypad0);
        sdkKeyDict.Add(130, CorsairKeyboardKeyId.Invalid);
        sdkKeyDict.Add(131, CorsairKeyboardKeyId.G17);
        sdkKeyDict.Add(132, CorsairKeyboardKeyId.F11);
        sdkKeyDict.Add(133, CorsairKeyboardKeyId.MinusAndUnderscore);
        sdkKeyDict.Add(134, CorsairKeyboardKeyId.BracketLeft);
        sdkKeyDict.Add(135, CorsairKeyboardKeyId.ApostropheAndDoubleQuote);
        sdkKeyDict.Add(136, CorsairKeyboardKeyId.SlashAndQuestionMark);
        sdkKeyDict.Add(137, CorsairKeyboardKeyId.Brightness);
        sdkKeyDict.Add(138, CorsairKeyboardKeyId.Invalid);
        sdkKeyDict.Add(139, CorsairKeyboardKeyId.RightArrow);
        sdkKeyDict.Add(140, CorsairKeyboardKeyId.KeypadEnter);
        sdkKeyDict.Add(141, CorsairKeyboardKeyId.KeypadPeriodAndDelete);
        sdkKeyDict.Add(142, CorsairKeyboardKeyId.Invalid);
        sdkKeyDict.Add(143, CorsairKeyboardKeyId.G18);
        sdkKeyDict.Add(144, CorsairKeyboardKeyId.Invalid);

    }

    public CorsairKeyboardKeyId GetKeyCodeFromDict(int key)
    { return (sdkKeyDict[key]); }
}
