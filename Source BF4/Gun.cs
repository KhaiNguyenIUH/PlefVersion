using System;
using SharpDX;

namespace PlefVersion
{
    class Gun
    {
        public string Name = "";

        public Offsets.MKO_ClientSoldierWeaponsComponent.WeaponSlot Slot;
        //public float Accuracy;

        public int Ammo;
        public int AmmoClip;

        public float RateOfFire;
        public bool RateOfFireBoostEnabled = false;

        public float RecoilMultiplier;
        public float RecoilDecrease;
        public bool RipOfRecoilEnabled = false;

        public float DeviationZoom;
        public float GameplayDeviationZoom;
        public float DeviationNoZoom;
        public float GameplayDeviationNoZoom;
        public bool NoSpreadEnabled = false;

        public Vector3 BulletInitialPosition;
        public Vector3 BulletInitialSpeed;

        public float BulletSpeed;
        //public float BulletSpeedSuppressed;
        public bool SuperSpeedBulletEnabled;
        public float BulletGravity;
        public bool NoGravityEnabled = false;
        public int BulletsPerShell;
        public int BulletsPerShot;        
        public bool DoubleBulletsEnabled = false;

        public float ZeroingDistanceRadians;
        public float ZeroingDistanceDefault;

        public bool IsValid()
        {
            return Name.Length > 0;
        }

    }
}
