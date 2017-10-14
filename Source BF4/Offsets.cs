using System;

namespace PlefVersion
{
    public struct Offsets
    {
        public static Int64 OFFSET_DXRENDERER = 0x142738080; //0x142572fa0;
        public static Int64 OFFSET_CLIENTGAMECONTEXT = 0x142670d80; //0x1424abd20;
        public static Int64 OFFSET_GAMERENDERER = 0x142672378; //0x1424ad330;
        public static Int64 OFFSET_ANGLES = 0x1423b2ec0; //0x1421caee0;        
        public static Int64 OFFSET_CURRENT_WEAPONFIRING = OFFSET_ANGLES + 0x8;
        public static Int64 OFFSET_SETTINGS = 0x1423717C0; //0x1421AC5A0;
        public static Int64 OFFSET_SHOTSTATS = 0x142737A40; //0x142572950;

        public struct MKO_GameRenderer
        {
            public static Int64 m_pRenderView = 0x60; // RenderView
        }

        public struct MKO_RenderView
        {
            public static Int64 m_Transform = 0x0040;         // D3DXMATRIX
            public static Int64 m_fovY = 0x00B4;              // FLOAT
            public static Int64 m_fovX = 0x0250;              // FLOAT
            public static Int64 m_viewProj = 0x0420;          // D3DXMATRIX
            public static Int64 m_viewMatrixInverse = 0x02E0; // D3DXMATRIX
            public static Int64 m_viewProjInverse = 0x04A0;   // D3DXMATRIX
        }

        public struct MKO_DynamicPhysicsEntity
        {
            public static Int64 m_EntityTransform = 0xA0;  // PhysicsEntityTransform
        }

        public struct MKO_PhysicsEntityTransform
        {
            public static Int64 m_Transform = 0x00;       // D3DXMATRIX
        }

        public struct MKO_VehicleEntityData
        {
            public static Int64 m_FrontMaxHealth = 0x148; // FLOAT
            public static Int64 m_NameSid = 0x0248;       // char* ID_P_VNAME_9K22
        }

        public struct MKO_ClientSoldierEntity
        {
            public static Int64 m_data = 0x0030;         // VehicleEntityData
            public static Int64 m_pPlayer = 0x01E0;          // ClientPlayer
            public static Int64 m_pHealthComponent = 0x0140; // HealthComponent
            public static Int64 m_authorativeYaw = 0x04D8;   // FLOAT
            public static Int64 m_authorativePitch = 0x04DC; // FLOAT 
            public static Int64 m_poseType = 0x04F0;         // INT32
            public static Int64 m_RenderFlags = 0x04F4;      // INT32
            public static Int64 m_pPhysicsEntity = 0x0238;   // VehicleDynamicPhysicsEntity
            public static Int64 m_pPredictedController = 0x0490;    // ClientSoldierPrediction
            public static Int64 m_soldierWeaponsComponent = 0x0570; // ClientSoldierWeaponsComponent
            public static Int64 m_ragdollComponent = 0x0580;        // ClientRagDollComponent 
            public static Int64 m_breathControlHandler = 0x0588;    // BreathControlHandler 
            public static Int64 m_sprinting = 0x5B0;  // BYTE 
            public static Int64 m_occluded = 0x05B1;  // BYTE
            public static Int64 m_pVaultComp = 0x0D30; // VaultComponent
        }

        public struct MKO_HealthComponent
        {
            public static Int64 m_Health = 0x0020;        // FLOAT
            public static Int64 m_MaxHealth = 0x0024;     // FLOAT
            public static Int64 m_vehicleHealth = 0x0038; // FLOAT (pLocalSoldier + 0x1E0 + 0x14C0 + 0x140 + 0x38)
        }

        public struct MKO_SystemSettings
        { 
            public static Int64 m_AllUnlocksUnlocked = 0x0054; // BYTE
            public static Int64 m_NoMinimap = 0x0055; // BYTE
            public static Int64 m_NoHud = 0x0056; // BYTE
            public static Int64 m_NoMinimapSpotting = 0x0057; // BYTE
            public static Int64 m_No3dSpotting = 0x0058; // BYTE
            public static Int64 m_NoNameTag = 0x0059; // BYTE
            public static Int64 m_OnlySquadLeaderSpawn = 0x005A; // BYTE
            public static Int64 m_TeamSwitchingAllowed = 0x005B; // BYTE
            public static Int64 m_RevertBackToBF3KillerCamera = 0x005C; // BYTE
            public static Int64 m_DisableHitIndicators = 0x005D; // BYTE
            public static Int64 m_DisableVehicleCommanderActions = 0x005E; // BYTE
            public static Int64 m_CommanderEnabled = 0x005F; // BYTE
            public static Int64 m_FieldUpgradeSystemActive = 0x0060; // BYTE
        };

        public struct MKO_ClientSoldierPrediction
        {
            public static Int64 m_Position = 0x0030; // D3DXVECTOR3
            public static Int64 m_Velocity = 0x0050; // D3DXVECTOR3
        }

        public struct MKO_ClientSoldierWeaponsComponent
        {
            public enum WeaponSlot
            {
                M_PRIMARY = 0,
                M_SECONDARY = 1,
                M_GADGET = 2,
                M_GRENADE = 6,
                M_KNIFE = 7
            };

            public static Int64 m_handler = 0x0890;      // m_handler + m_activeSlot * 0x8 = ClientSoldierWeapon
            public static Int64 m_activeSlot = 0x0A98;   // INT32 (WeaponSlot)
            public static Int64 m_activeHandler = 0x08D0; // ClientActiveWeaponHandler 
            public static Int64 m_zeroingDistanceLevel = 0x0AC8; // INT32
        }

        public struct MKO_ClientGameContext
        {
            public static Int64 m_pPhysicsManager = 0x28; // HavokPhysicsManager
            public static Int64 m_pPlayerManager = 0x60;  // ClientPlayerManager
        }

        public struct MKO_ClientPlayerManager
        {
            public static Int64 m_pLocalPlayer = 0x540; // ClientPlayer
            public static Int64 m_ppPlayer = 0x548;     // ClientPlayer
        }

        public struct MKO_ClientPlayer
        {
            public static Int64 szName = 0x40;            // 10 CHARS
            public static Int64 m_isSpectator = 0x13C9;   // BYTE
            public static Int64 m_teamId = 0x13CC;        // INT32
            public static Int64 m_character = 0x14B0;     // ClientSoldierEntity 
            public static Int64 m_ownPlayerView = 0x1510; // ClientPlayerView
            public static Int64 m_PlayerView = 0x1520;    // ClientPlayerView
            public static Int64 m_pAttachedControllable = 0x14C0;   // ClientSoldierEntity (ClientVehicleEntity)
            public static Int64 m_pControlledControllable = 0x14D0; // ClientSoldierEntity
            public static Int64 m_attachedEntryId = 0x14C8; // INT32
        }

        public struct MKO_ClientVehicleEntity
        {
            public static Int64 m_data = 0x0030;           // VehicleEntityData
            public static Int64 m_pPhysicsEntity = 0x0238; // DynamicPhysicsEntity
            public static Int64 m_Velocity = 0x0280;       // D3DXVECTOR3 
            public static Int64 m_prevVelocity = 0x0290;   // D3DXVECTOR3 
            public static Int64 m_Chassis = 0x03E0;        // ClientChassisComponent
            public static Int64 m_childrenAABB = 0x0250;   // AxisAlignedBox
        }

        public struct MKO_UpdatePoseResultData
        {
            public enum BONES
            {
                BONE_HEAD = 104,
                BONE_NECK = 142,
                BONE_SPINE2 = 7,
                BONE_SPINE1 = 6,
                BONE_SPINE = 5,
                BONE_LEFTSHOULDER = 9,
                BONE_RIGHTSHOULDER = 109,
                BONE_LEFTELBOWROLL = 11,
                BONE_RIGHTELBOWROLL = 111,
                BONE_LEFTHAND = 15,
                BONE_RIGHTHAND = 115,
                BONE_LEFTKNEEROLL = 188,
                BONE_RIGHTKNEEROLL = 197,
                BONE_LEFTFOOT = 184,
                BONE_RIGHTFOOT = 198
            };

            public static Int64 m_ActiveWorldTransforms = 0x0028; // QuatTransform
            public static Int64 m_ValidTransforms = 0x0040;       // BYTE
        }

        public struct MKO_ClientRagDollComponent
        {
            public static Int64 m_ragdollTransforms = 0x0088; // UpdatePoseResultData
            public static Int64 m_Transform = 0x05D0;         // D3DXMATRIX
        }

        public struct MKO_ClientSoldierWeapon
        {
            public static Int64 m_data = 0x0030;              // WeaponEntityData
            public static Int64 m_authorativeAiming = 0x4988; // ClientSoldierAimingSimulation
            public static Int64 m_pWeapon = 0x49A8;           // ClientWeapon
            public static Int64 m_pPrimary = 0x49C0;          // WeaponFiring
        }

        public struct MKO_WeaponEntityData
        {
            public static Int64 m_name = 0x0130; // char*
        }

        public struct MKO_ClientSoldierAimingSimulation
        {
            public static Int64 m_fpsAimer = 0x0010;  // AimAssist
            public static Int64 m_yaw = 0x0018;       // FLOAT
            public static Int64 m_pitch = 0x001C;     // FLOAT
            public static Int64 m_sway = 0x0028;      // D3DXVECTOR2
            public static Int64 m_zoomLevel = 0x0068; // FLOAT
        }

        public struct MKO_WeaponFiring
        {
            public static Int64 m_pSway = 0x0078;                  // WeaponSway
            public static Int64 m_pPrimaryFire = 0x0128;           // PrimaryFire
            public static Int64 m_pWeaponModifier = 0x01F0;        // WeaponModifier
            public static Int64 m_pFiringHolderData = 0x01C8;      // FireLogicData
            public static Int64 m_projectilesLoaded = 0x01A0;      // INT32 
            public static Int64 m_projectilesInMagazines = 0x01A4; // INT32 
            public static Int64 m_overheatPenaltyTimer = 0x01B0;   // FLOAT


            public static Int64 m_RecoilTimer = 0x0168; //float RecoilTimer; //0x0168 
            public static Int64 m_recoilAngleX = 0x016c;//float RecoilAngleX; //0x016C 
            public static Int64 m_RecoilAngleY = 0x0170;//float RecoilAngleY; //0x0170 
            public static Int64 m_RecoilAngleZ = 0x0174;//float RecoilAngleZ; //0x0174 
            public static Int64 m_RecoilFOVAngle = 0x0178;//float RecoilFOVAngle; //0x0178
            public static Int64 m_recoilTimeMultiplier = 0x017c;//float RecoilTimeMultiplier; //0x017C 
                                                                //NEW
        }

        public struct MKO_WeaponSway
        {
            public static Int64 m_pSwayData = 0x0008;      // GunSwayData
            public static Int64 m_deviationPitch = 0x0130; // FLOAT 
            public static Int64 m_deviationYaw = 0x0134;   // FLOAT

            public static Int64 m_devRoll = 0x0138;        //float //new
            public static Int64 m_devTransY = 0x013C;      //float

            public static Int64 m_CurDispersionPitch = 0x01AC;
            public static Int64 m_CurDispersionYaw = 0x01B0;
            public static Int64 m_CurDispersionRoll = 0x01B4;
            public static Int64 m_CurDispersionTransY = 0x01B8;
            public static Int64 m_RipOfRecoil = 0x0208;

        }

        public struct MKO_GunSwayData
        {
            public static Int64 m_DeviationScaleFactorZoom = 0x0430;           // FLOAT 
            public static Int64 m_GameplayDeviationScaleFactorZoom = 0x0434;   // FLOAT 
            public static Int64 m_DeviationScaleFactorNoZoom = 0x438;         // FLOAT 
            public static Int64 m_GameplayDeviationScaleFactorNoZoom = 0x043c; // FLOAT 

            public static Int64 m_ShootingRecoilDecreaseScale = 0x0440; // FLOAT 
            public static Int64 m_FirstShotRecoilMultiplier = 0x0444;   // FLOAT 
        }

        public struct MKO_PrimaryFire
        {
            public static Int64 m_shotConfigData = 0x0010; // ShotConfigData
        }

        public struct MKO_ShotConfigData
        {
            public static Int64 m_pProjectileData = 0x00B0;   // BulletEntityData
            public static Int64 m_FireLogic = 0x01C8;         // FireLogicData
            public static Int64 m_PositionOffset = 0x0060;    // VECTOR3
            public static Int64 m_initialSpeed = 0x0080;      // VECTOR3
            public static Int64 m_Speed = 0x0088;             // FLOAT
            public static Int64 m_numberOfBulletsPerShell = 0x00D4;   // INT
            public static Int64 m_numberOfBulletsPerShot = 0x00D8;    // INT
            public static Int64 m_numberOfBulletsPerBurst = 0x00DC;   // INT
        }

        public class MKO_WeaponModifier
        {
            public static Int64 m_pWeaponZeroingModifier = 0x00C0; // WeaponZeroingModifier
            public static Int64 m_pSoldierWeaponUnlockAsset = 0x0038; // SoldierWeaponUnlockAsset
            public static Int64 m_pWeaponFiringEffectsModifier = 0x0058; // WeaponFiringEffectsModifier
            public static Int64 m_WeaponMiscModifierSettings = 0x040; // 
        }

        public class MKO_WeaponZeroingModifier
        {
            public static Int64 m_Modes = 0x0018; // VECTOR2
            public static Int64 m_defaultZeroingDistance = 0x0020; // FLOAT
        }

        public struct MKO_ShotStats
        {
            public static Int64 m_shotsFired = 0x004C;  // 0x0048; // 0x0040; // INT
            public static Int64 m_shotsHit = 0x0054;    // 0x004C; // 0x0044; // INT
            public static Int64 m_damageCount = 0x005C; // 0x0054; // 0x0048; // INT

        }

        public struct MKO_BulletEntityData
        {
            public static Int64 m_Gravity = 0x0130;     // FLOAT
            public static Int64 m_StartDamage = 0x0154; // FLOAT
            public static Int64 m_EndDamage = 0x0158;   // FLOAT
        }

        public struct MKO_AimAssist
        {
            public static Int64 m_yaw = 0x0014;   // FLOAT
            public static Int64 m_pitch = 0x0018; // FLOAT
        }

        public struct MKO_BreathControlHandler
        {
            public static Int64 m_breathControlTimer = 0x0038; // FLOAT
            public static Int64 m_breathControlMultiplier = 0x003C; // FLOAT  
            public static Int64 m_breathControlPenaltyTimer = 0x0040; // FLOAT  
            public static Int64 m_breathControlpenaltyMultiplier = 0x0044; // FLOAT  
            public static Int64 m_breathControlActive = 0x0048; // FLOAT  
            public static Int64 m_breathControlInput = 0x004C; // FLOAT  
            public static Int64 m_breathActive = 0x0050; // FLOAT  
            public static Int64 m_Enabled = 0x0058; // FLOAT  
        }
    }
}
