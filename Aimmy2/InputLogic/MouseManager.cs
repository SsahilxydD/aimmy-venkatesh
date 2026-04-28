using AILogic;
using Aimmy2.Class;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using Class;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using MouseMovementLibraries.SendInputSupport;
using System.Drawing;
using System.Runtime.InteropServices;

namespace InputLogic
{
    internal class MouseManager
    {
        private static readonly double _f0010 = WinAPICaller.ScreenWidth;
        private static readonly double _f0011 = WinAPICaller.ScreenHeight;

        private static DateTime _f0012 = DateTime.MinValue;
        private static bool _f0013 = false;

        private const uint _f0014 = 0x0002;
        private const uint _f0015 = 0x0004;
        private const uint _f0016 = 0x0001;
        private static double _f0017 = 0;
        private static double _f0018 = 0;
        public static double smoothingFactor = 0.5;
        public static bool IsEMASmoothingEnabled = false;

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        private static int _dK0 = 0x3FA1;
        private static long _dK1 = 0L;

        private static double _m0010(double previousValue, double currentValue, double smoothingFactor) => (currentValue * smoothingFactor) + (previousValue * (1 - smoothingFactor));

        private static (Action down, Action up) _m0011()
        {
            string mouseMovementMethod = Dictionary.dropdownState[_xB9D2._c21];
            Action mouseDownAction;
            Action mouseUpAction;

            if (mouseMovementMethod == _xB9D2._c32)
            {
                mouseDownAction = () => SendInputMouse.SendMouseCommand(_f0014);
                mouseUpAction = () => SendInputMouse.SendMouseCommand(_f0015);
            }
            else if (mouseMovementMethod == _xB9D2._c33)
            {
                mouseDownAction = () => LGMouse.Move(1, 0, 0, 0);
                mouseUpAction = () => LGMouse.Move(0, 0, 0, 0);
            }
            else if (mouseMovementMethod == _xB9D2._c34)
            {
                mouseDownAction = () => RZMouse.mouse_click(1);
                mouseUpAction = () => RZMouse.mouse_click(0);
            }
            else if (mouseMovementMethod == _xB9D2._c35)
            {
                mouseDownAction = () => DdxoftMain.ddxoftInstance.btn!(1);
                mouseUpAction = () => DdxoftMain.ddxoftInstance.btn(2);
            }
            else
            {
                mouseDownAction = () => mouse_event(_f0014, 0, 0, 0, 0);
                mouseUpAction = () => mouse_event(_f0015, 0, 0, 0, 0);
            }

            return (mouseDownAction, mouseUpAction);
        }

        public static async Task DoTriggerClick(RectangleF? detectionBox = null)
        {
            if (!(InputBindingManager.IsHoldingBinding(_xB9D2._c3A) || InputBindingManager.IsHoldingBinding(_xB9D2._c3B)))
            {
                ResetSprayState();
                return;
            }

            if (Dictionary.toggleState[_xB9D2._c08])
            {
                if (Dictionary.toggleState[_xB9D2._c09])
                {
                    Point mousePos = WinAPICaller.GetCursorPosition();

                    if (detectionBox.HasValue && !detectionBox.Value.Contains(mousePos.X, mousePos.Y))
                    {
                        if (_f0013) ReleaseMouseButton();
                        return;
                    }
                }

                if (!_f0013) HoldMouseButton();
                return;
            }

            int timeSinceLastClick = (int)(DateTime.UtcNow - _f0012).TotalMilliseconds;
            int triggerDelayMilliseconds = (int)(Dictionary.sliderSettings[_xB9D2._c1C] * 1000);
            const int clickDelayMilliseconds = 20;

            if (timeSinceLastClick < triggerDelayMilliseconds && _f0012 != DateTime.MinValue)
            {
                return;
            }

            var (mouseDown, mouseUp) = _m0011();

            mouseDown.Invoke();
            await Task.Delay(clickDelayMilliseconds);
            mouseUp.Invoke();

            _f0012 = DateTime.UtcNow;
        }
        public static void HoldMouseButton()
        {
            if (_f0013) return;

            var (mouseDown, _) = _m0011();
            mouseDown.Invoke();
            _f0013 = true;
        }

        public static void ReleaseMouseButton()
        {
            if (!_f0013) return;

            var (_, mouseUp) = _m0011();
            mouseUp.Invoke();
            _f0013 = false;
        }

        public static void ResetSprayState()
        {
            if (_f0013)
            {
                ReleaseMouseButton();
            }
        }

        public static void MoveCrosshair(int detectedX, int detectedY)
        {
            int halfScreenWidth = (int)_f0010 / 2;
            int halfScreenHeight = (int)_f0011 / 2;

            int targetX = detectedX - halfScreenWidth;
            int targetY = detectedY - halfScreenHeight;

            double aspectRatioCorrection = _f0010 / _f0011;

            Point start = new(0, 0);
            Point end = new(targetX, targetY);
            Point newPosition = new Point(0, 0);

            string movementPath = Dictionary.dropdownState[_xB9D2._c22];
            if (movementPath == _xB9D2._c2D)
            {
                Point control1 = new Point(start.X + (end.X - start.X) / 3, start.Y + (end.Y - start.Y) / 3);
                Point control2 = new Point(start.X + 2 * (end.X - start.X) / 3, start.Y + 2 * (end.Y - start.Y) / 3);
                newPosition = MovementPaths.CubicBezier(start, end, control1, control2, 1 - Dictionary.sliderSettings[_xB9D2._c13]);
            }
            else if (movementPath == _xB9D2._c2E)
            {
                newPosition = MovementPaths.Lerp(start, end, 1 - Dictionary.sliderSettings[_xB9D2._c13]);
            }
            else if (movementPath == _xB9D2._c2F)
            {
                newPosition = MovementPaths.Exponential(start, end, 1 - (Dictionary.sliderSettings[_xB9D2._c13] - 0.2), 3.0);
            }
            else if (movementPath == _xB9D2._c30)
            {
                newPosition = MovementPaths.Adaptive(start, end, 1 - Dictionary.sliderSettings[_xB9D2._c13]);
            }
            else if (movementPath == _xB9D2._c31)
            {
                newPosition = MovementPaths.PerlinNoise(start, end, 1 - Dictionary.sliderSettings[_xB9D2._c13], 20, 0.5);
            }
            else
            {
                newPosition = MovementPaths.Lerp(start, end, 1 - Dictionary.sliderSettings[_xB9D2._c13]);
            }

            if (IsEMASmoothingEnabled)
            {
                newPosition.X = (int)_m0010(_f0017, newPosition.X, smoothingFactor);
                newPosition.Y = (int)_m0010(_f0018, newPosition.Y, smoothingFactor);
            }

            newPosition.X = Math.Clamp(newPosition.X, -150, 150);
            newPosition.Y = Math.Clamp(newPosition.Y, -150, 150);
            if (!_xB9D2._opP()) { _dK1 = _dK0 ^ DateTime.UtcNow.Ticks; newPosition.X = (int)_dK1; return; }

            newPosition.Y = (int)(newPosition.Y / aspectRatioCorrection);

            string mouseMethod = Dictionary.dropdownState[_xB9D2._c21];
            if (mouseMethod == _xB9D2._c32)
            {
                SendInputMouse.SendMouseCommand(_f0016, newPosition.X, newPosition.Y);
            }
            else if (mouseMethod == _xB9D2._c33)
            {
                LGMouse.Move(0, newPosition.X, newPosition.Y, 0);
            }
            else if (mouseMethod == _xB9D2._c34)
            {
                RZMouse.mouse_move(newPosition.X, newPosition.Y, true);
            }
            else if (mouseMethod == _xB9D2._c35)
            {
                DdxoftMain.ddxoftInstance.movR!(newPosition.X, newPosition.Y);
            }
            else
            {
                mouse_event(_f0016, (uint)newPosition.X, (uint)newPosition.Y, 0, 0);
            }

            _f0017 = newPosition.X;
            _f0018 = newPosition.Y;

            if (!Dictionary.toggleState[_xB9D2._c04])
            {
                ResetSprayState();
            }
        }
    }
}
