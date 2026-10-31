
using static mpvnet.Global;

namespace mpvnet
{
    /// <summary>Hold-to-speed state machine; UI wires key events only.</summary>
    public sealed class HoldSpeedController
    {
        bool _repeated;
        double _original = 1;

        public void OnKeyDown(bool isRepeat)
        {
            if (!isRepeat)
            {
                _repeated = false;
                _original = Core.GetPropertyDouble("speed");
                App.SpeedBeforeBoost = _original;
                return;
            }

            if (!_repeated)
            {
                _repeated = true;
                App.SpeedBoosting = true;
                Core.SetPropertyDouble("speed", App.HoldSpeedRight);
            }

            Commands.ShowSpeedOsd();
        }

        public void OnKeyUp()
        {
            if (_repeated)
            {
                Core.SetPropertyDouble("speed", _original);
                App.SpeedBoosting = false;
                _repeated = false;
                Commands.ShowSpeedOsd();
                return;
            }

            Core.Command("osd-msg-bar seek 5");
        }
    }
}
