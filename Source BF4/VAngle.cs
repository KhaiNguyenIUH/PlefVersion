using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlefVersion
{
    public class VAngle
    {
        public float Yaw;
        public float Pitch;

        public VAngle() { }

        public VAngle(float _Yaw, float _Pitch)
        {
            this.Yaw = _Yaw;
            this.Pitch = _Pitch;
        }

        public VAngle GetViewAngle { get { return this; } }
    }
}
