using System;
using SharpDX;

namespace PlefVersion
{
    class GPlayer
    {
        public string Name;
        public string VehicleName;
        public int Team;
        public Vector3 Origin;
        public Vector3 Velocity;
        public RDoll Bone;
        //public Vector3 BoneTarget;
        public int Pose;

        public Vector2 FoV;

        public Vector2 Sway;

        public int IsOccluded;
        public bool IsSpectator;

        public bool IsDriver;
        public bool InVehicle;

        public float Health;
        public float MaxHealth;

        public Gun CurrentWeapon;

        public float ShotsFired;
        public float ShotsHit;
        public float DamageCount;
        //public float Accuracy;

        public string LastEnemyNameAimed = "";
        public DateTime LastTimeEnimyAimed = DateTime.Now;

        public Matrix ViewProj;
        public Matrix MatrixInverse;

        //public float BreathControl;
        public bool NoBreathEnabled = false;

        public float Yaw;
        public float Distance;
        public float DistanceToCrosshair;

        // Vehicle
        public AxisAlignedBox VehicleAABB;
        public Matrix VehicleTranfsorm;
        public float VehicleHealth;
        public float VehicleMaxHealth;

        /*
        public float VehicleSpeed;
        public float VehicleWeaponHeatPercentage;
        public float VehicleRollAngle;

        public float DistanceToCrosshair;
        public float Pitch;
        public float TurretYaw;
        public float altitudeDifference;
        public float ZeroingDistanceRadians;
        */

        public bool IsValid()
        {
            return (Health > 0.1f && Health <= 100 && !Origin.IsZero);
        }

        public bool IsValidAimbotTarget(bool bTwoSecRule, string lastTargetName, DateTime lastTimeTargeted)
        {
            return IsValid() && (!bTwoSecRule || lastTargetName == Name || DateTime.Now.Subtract(lastTimeTargeted).Seconds >= 2);
        }

        public bool IsDead()
        {
            return !(Health > 0.1f);
        }

        public bool IsVisible()
        {
           return (IsOccluded == 0);
        }

        public bool IsSprinting()
        {
            return ((float)Math.Abs(Velocity.X + Velocity.Y + Velocity.Z) > 4.0f);
        }    

        public float GetShotsAccuracy()
        {
            if ((ShotsFired > 0f && ShotsHit > 0f))
                return (float)Math.Round((double)((float)((ShotsHit / ShotsFired) * 100.0f)), 2);
            else
                return 0.0f;
        }

        public AxisAlignedBox GetAABB()
        {
            AxisAlignedBox aabb = new AxisAlignedBox();
            if (this.Pose == 0) // standing
            {
                aabb.Min = new Vector4(-0.350000f, 0.000000f, -0.350000f, 0);
                aabb.Max = new Vector4(0.350000f, 1.700000f, 0.350000f, 0);
            }
            if (this.Pose == 1) // crouching
            {
                aabb.Min = new Vector4(-0.350000f, 0.000000f, -0.350000f, 0);
                aabb.Max = new Vector4(0.350000f, 1.150000f, 0.350000f, 0);
            }
            if (this.Pose == 2) // prone
            {
                aabb.Min = new Vector4(-0.350000f, 0.000000f, -0.350000f, 0);
                aabb.Max = new Vector4(0.350000f, 0.400000f, 0.350000f,0);
            }
            return aabb;
        }
    }
}
