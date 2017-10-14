using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Windows.Forms;
using System.Collections.Generic;

using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;

using Factory = SharpDX.Direct2D1.Factory;
using FontFactory = SharpDX.DirectWrite.Factory;
using Format = SharpDX.DXGI.Format;

namespace PlefVersion
{
    public partial class Overlay : Form
    {
        public const string AppName = "Khai - test";
        public const string AppTitle = @"Khai - test";

        // Game Data
        private static GPlayer localPlayer = null;
        private static List<Gun> localWeapons = null;
        private static List<GPlayer> players = null;
        private static List<GPlayer> targetEnimies = null;

        private static Offsets.MKO_ClientSoldierWeaponsComponent.WeaponSlot currWeaponSlot;

        private const int proximityDeadline = 50;
        private int spectatorCount = 0;
        private int proximityCount = 0;

        // Handle
        private IntPtr handle;

        // Screen Size
        private Rectangle rect;

        #region MAIN : Overlay

        // Process
        private Process process = null;
        private Thread updateStream = null;
        private Thread windowStream = null;
        //private Thread aimbotStream = null;

        // Init
        public Overlay(Process process)
        {
            this.process = process;
            this.handle = Handle;

            int initialStyle = Manager.GetWindowLong(this.Handle, -20);
            Manager.SetWindowLong(this.Handle, -20, initialStyle | 0x80000 | 0x20);

            IntPtr HWND_TOPMOST = new IntPtr(-1);
            const UInt32 SWP_NOSIZE = 0x0001;
            const UInt32 SWP_NOMOVE = 0x0002;
            const UInt32 TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE;

            Manager.SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);
            OnResize(null);

            InitializeComponent();
        }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // Set the form click-through
                cp.ExStyle |= 0x80000 /* WS_EX_LAYERED */ | 0x20 /* WS_EX_TRANSPARENT */;
                return cp;
            }
        }
        // Set window style
        protected override void OnResize(EventArgs e)
        {
            int[] margins = new int[] { 0, 0, rect.Width, rect.Height };
            Manager.DwmExtendFrameIntoClientArea(this.Handle, ref margins);
        }

        // Get Window Rect
        private void OverlaySet(object sender)
        {
            while (true)
            {
                IntPtr targetWnd = IntPtr.Zero;
                targetWnd = Manager.FindWindow(null, "Battlefield 4");

                if (targetWnd != IntPtr.Zero)
                {
                    RECT targetSize = new RECT();
                    Manager.GetWindowRect(targetWnd, out targetSize);

                    // Game is Minimized
                    if (targetSize.Left < 0 && targetSize.Top < 0 && targetSize.Right < 0 && targetSize.Bottom < 0)
                    {
                        IsMinimized = true;
                        continue;
                    }

                    // Reset
                    IsMinimized = false;

                    RECT borderSize = new RECT();
                    Manager.GetClientRect(targetWnd, out borderSize);

                    int dwStyle = Manager.GetWindowLong(targetWnd, Manager.GWL_STYLE);

                    int windowheight;
                    int windowwidth;
                    int borderheight;
                    int borderwidth;

                    if (rect.Width != (targetSize.Bottom - targetSize.Top)
                        && rect.Width != (borderSize.Right - borderSize.Left))
                        IsResize = true;

                    rect.Width = targetSize.Right - targetSize.Left;
                    rect.Height = targetSize.Bottom - targetSize.Top;

                    if ((dwStyle & Manager.WS_BORDER) != 0)
                    {
                        windowheight = targetSize.Bottom - targetSize.Top;
                        windowwidth = targetSize.Right - targetSize.Left;

                        rect.Height = borderSize.Bottom - borderSize.Top;
                        rect.Width = borderSize.Right - borderSize.Left;

                        borderheight = (windowheight - borderSize.Bottom);
                        borderwidth = (windowwidth - borderSize.Right) / 2; //only want one side
                        borderheight -= borderwidth; //remove bottom

                        targetSize.Left += borderwidth;
                        targetSize.Top += borderheight;

                        rect.Left = targetSize.Left;
                        rect.Top = targetSize.Top;
                    }
                    Manager.MoveWindow(handle, targetSize.Left, targetSize.Top, rect.Width, rect.Height, true);
                }
                Thread.Sleep(300);
            }
        }

        // Close window event
        private void OverlayClose(object sender, FormClosingEventArgs e)
        {
            Quit();
        }

        // INIT
        private void OverlayLoad(object sender, EventArgs e)
        {
            this.TopMost = true;
            this.Visible = true;
            this.FormBorderStyle = FormBorderStyle.None;
            //this.WindowState = FormWindowState.Maximized;
            this.Width = rect.Width;
            this.Height = rect.Height;

            // Window name
            this.Name = Process.GetCurrentProcess().ProcessName + " - " + AppName;
            this.Text = Process.GetCurrentProcess().ProcessName + " - " + AppTitle;

            // Init factory
            factory = new Factory();
            fontFactory = new FontFactory();

            // Render settings
            renderProperties = new HwndRenderTargetProperties()
            {
                Hwnd = this.Handle,
                PixelSize = new Size2(rect.Width, rect.Height),
                PresentOptions = PresentOptions.None
            };

            // Init device
            device = new WindowRenderTarget(factory, new RenderTargetProperties(new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied)), renderProperties);

            // Init brush
            solidColorBrush = new SolidColorBrush(device, Color.White);

            // Init font's
            font = new TextFormat(fontFactory, fontFamily, fontSize);
            fontSmall = new TextFormat(fontFactory, fontFamily, fontSizeSmall);

            // Open process
            RPM.OpenProcess(process.Id);

            // Init player array
            localPlayer = new GPlayer();
            //localPlayer.CurrentWeapon = new Weapon();
            localWeapons = new List<Gun>();
            players = new List<GPlayer>();
            targetEnimies = new List<GPlayer>();

            // Init update thread
            updateStream = new Thread(new ParameterizedThreadStart(OverlayControl));
            updateStream.Start();

            // Init window thread (resize / position)
            windowStream = new Thread(new ParameterizedThreadStart(OverlaySet));
            windowStream.Start();

            //aimbotStream = new Thread(new ParameterizedThreadStart(AimbotControl));
            //aimbotStream.Start();

            // Init Key Listener
            KeyAssign();
        }

        // Update Thread
        private void OverlayControl(object sender)
        {
            while (IsGameRun())
            {
                try
                {
                    // Resize
                    if (IsResize)
                    {
                        device.Resize(new Size2(rect.Width, rect.Height));
                        IsResize = false;
                    }

                    // Begin Draw
                    device.BeginDraw();
                    device.Clear(new Color4(0.0f, 0.0f, 0.0f, 0.0f));

                    // Check Window State
                    if (!IsMinimized)
                    {
                        //RPM.ReadAngle(out lastAngle);

                        #region Scan Memory & Draw Players
                        MainScan();
                        #endregion

                        #region Aimbot Watch
                        if (bAimbot)
                            AimUpdateKeys();
                        #endregion

                        #region Drawing Shortcut Menu
                        DrawShortcutMenu(5, 5);
                        #endregion

                        #region Drawing Menu
                        if (bMenuControl)
                            DrawMenu(10, 300);
                        #endregion

                        #region Drawing DrawShotAccuracy
                        DrawShotAccuracy(rect.Width / 2 - 125, rect.Height - 5);
                        #endregion

                        #region Drawing Proximity Alert
                        if (bEspSpotline && proximityCount > 0)
                            DrawProximityAlert(rect.Width / 2 + 300, rect.Height - 80, 155, 50);
                        #endregion

                        #region Drawing Hardcore Mode HUD
                        if (bHardcoreMode)
                        {
                            //if (bCrosshairHUD)
                            DrawCrosshairHUD(base.Width / 2, base.Height / 2, 30, 30, new Color(0, 0, 255, 255));
                            //if (bRadarHUD)
                            DrawRadarHUD(currMnHC == mnHardCoreMode.RIGHT ? base.Width - 220 : 20, base.Height - 432, 200, 200);
                            //if (bAmmoHealthHUD)
                            //{
                            DrawAmmoHealthHUD(currMnHC == mnHardCoreMode.RIGHT ? base.Width - 220 : 20, base.Height - 231, 200, 30);
                            DrawHealthBarHUD(currMnHC == mnHardCoreMode.RIGHT ? base.Width - 220 : 20, base.Height - 200, 200, 15);
                            //}
                        }
                        #endregion

                        #region Drawing Spectator Count
                        DrawTextCenter(rect.Width / 2 - 100, rect.Height - (int)font.FontSize, 200, (int)font.FontSize, spectatorCount + " SPECTATOR(S) ON SERVER", new Color(255, 214, 0, 255), true);
                        #endregion

                        #region Drawing Spectator Warning
                        if (bSpectatorWarn && spectatorCount > 0)
                            DrawSpectatorWarn(rect.Center.X - 125, 25, 350, 55);
                        #endregion

                        #region Drawing Credits
                        //DrawTextCenter(rect.Width / 2 - 125, 5, 250, (int)font.FontSize, AppTitle, new Color(255, 214, 0, 255), true);
                        #endregion

                    }

                    device.EndDraw();

                    CalculateFrameRate();
                    //Thread.Sleep(Interval);
                }
                catch (Exception ex)
                {
                    WriteOnLogFile(DateTime.Now.ToString() + " - OVERLAY ERROR : " + ex);
                }

            }
            RPM.CloseProcess();
            Environment.Exit(0);
        }

        #endregion

        #region Scan Game Memory Stuff

        private bool bBoneOk = false;

        private Int64 GetLocalSoldier()
        {
            Int64 pGContext = RPM.Read<Int64>(Offsets.OFFSET_CLIENTGAMECONTEXT);
            if (!RPM.IsValid(pGContext))
                return 0x000F000000000000;

            Int64 pPlayerManager = RPM.Read<Int64>(pGContext + Offsets.MKO_ClientGameContext.m_pPlayerManager);
            if (!RPM.IsValid(pPlayerManager))
                return 0x000F000000000000;

            Int64 plocalPlayer = RPM.Read<Int64>(pPlayerManager + Offsets.MKO_ClientPlayerManager.m_pLocalPlayer);
            if (!RPM.IsValid(plocalPlayer))
                return 0x000F000000000000;

            Int64 pLocalSoldier = GetClientSoldierEntity(plocalPlayer, localPlayer);
            if (!RPM.IsValid(pLocalSoldier))
                return 0x000F000000000000;
            else
                return pLocalSoldier;
        }

        private Int64 GetSoldierWeapon()
        {
            Int64 pSoldierWeapon = 0x000F000000000000;

            Int64 pLocalSoldier = GetLocalSoldier();
            if (!RPM.IsValid(pLocalSoldier))
                return pSoldierWeapon;

            if (localPlayer.IsDead() || localPlayer.InVehicle)
                return pSoldierWeapon;

            Int64 pClientWeaponComponent = RPM.Read<Int64>(pLocalSoldier + Offsets.MKO_ClientSoldierEntity.m_soldierWeaponsComponent);
            if (RPM.IsValid(pClientWeaponComponent))
            {
                Int64 pWeaponHandle = RPM.Read<Int64>(pClientWeaponComponent + Offsets.MKO_ClientSoldierWeaponsComponent.m_handler);
                Int32 ActiveSlot = RPM.Read<Int32>(pClientWeaponComponent + Offsets.MKO_ClientSoldierWeaponsComponent.m_activeSlot);
                if (RPM.IsValid(pWeaponHandle))
                    pSoldierWeapon = RPM.Read<Int64>(pWeaponHandle + ActiveSlot * 0x8);
            }

            return pSoldierWeapon;
        }

        private Int32 GetActiveSlot()
        {
            Int64 pLocalSoldier = GetLocalSoldier();
            if (!RPM.IsValid(pLocalSoldier))
                return 99999;

            Int64 pClientWeaponComponent = RPM.Read<Int64>(pLocalSoldier + Offsets.MKO_ClientSoldierEntity.m_soldierWeaponsComponent);
            if (RPM.IsValid(pClientWeaponComponent))
                return RPM.Read<Int32>(pClientWeaponComponent + Offsets.MKO_ClientSoldierWeaponsComponent.m_activeSlot);
            else
                return 99999;
        }

        private Int64 GetClientSoldierEntity(Int64 pClientPlayer, GPlayer player)
        {
            player.InVehicle = false;
            player.IsDriver = false;


            Int64 pAttached = RPM.Read<Int64>(pClientPlayer + Offsets.MKO_ClientPlayer.m_pAttachedControllable);
            if (RPM.IsValid(pAttached))
            {
                Int64 m_ClientSoldier = RPM.Read<Int64>(RPM.Read<Int64>(pClientPlayer + Offsets.MKO_ClientPlayer.m_character)) - sizeof(Int64);
                if (RPM.IsValid(m_ClientSoldier))
                {
                    player.InVehicle = true;

                    Int64 pVehicleEntity = RPM.Read<Int64>(pClientPlayer + Offsets.MKO_ClientPlayer.m_pAttachedControllable);
                    if (RPM.IsValid(pVehicleEntity))
                    {
                        // Driver
                        if (RPM.Read<Int32>(pClientPlayer + Offsets.MKO_ClientPlayer.m_attachedEntryId) == 0)
                        {
                            // Vehicle AABB
                            if (ESP_Vehicle)
                            {
                                Int64 pDynamicPhysicsEntity = RPM.Read<Int64>(pVehicleEntity + Offsets.MKO_ClientVehicleEntity.m_pPhysicsEntity);
                                if (RPM.IsValid(pDynamicPhysicsEntity))
                                {
                                    Int64 pPhysicsEntity = RPM.Read<Int64>(pDynamicPhysicsEntity + Offsets.MKO_DynamicPhysicsEntity.m_EntityTransform);
                                    player.VehicleTranfsorm = RPM.Read<Matrix>(pPhysicsEntity + Offsets.MKO_PhysicsEntityTransform.m_Transform);
                                    player.VehicleAABB = RPM.Read<AxisAlignedBox>(pVehicleEntity + Offsets.MKO_ClientVehicleEntity.m_childrenAABB);
                                }
                            }
                            Int64 _EntityData = RPM.Read<Int64>(pVehicleEntity + Offsets.MKO_ClientSoldierEntity.m_data);
                            if (RPM.IsValid(_EntityData))
                            {
                                Int64 _NameSid = RPM.Read<Int64>(_EntityData + Offsets.MKO_VehicleEntityData.m_NameSid);

                                string strName = RPM.ReadName(_NameSid, 20);
                                if (strName.Length > 11)
                                {
                                    Int64 pAttachedClient = RPM.Read<Int64>(m_ClientSoldier + Offsets.MKO_ClientSoldierEntity.m_pPlayer);
                                    // AttachedControllable Max Health
                                    Int64 p = RPM.Read<Int64>(pAttachedClient + Offsets.MKO_ClientPlayer.m_pAttachedControllable);
                                    Int64 p2 = RPM.Read<Int64>(p + Offsets.MKO_ClientSoldierEntity.m_pHealthComponent);
                                    player.VehicleHealth = RPM.Read<float>(p2 + Offsets.MKO_HealthComponent.m_vehicleHealth);

                                    // AttachedControllable Health
                                    player.VehicleMaxHealth = RPM.Read<float>(_EntityData + Offsets.MKO_VehicleEntityData.m_FrontMaxHealth);

                                    // AttachedControllable Name
                                    player.VehicleName = strName.Remove(0, 11);
                                    player.IsDriver = true;
                                }
                            }
                        }
                    }
                }
                return m_ClientSoldier;
            }
            return RPM.Read<Int64>(pClientPlayer + Offsets.MKO_ClientPlayer.m_pControlledControllable);
        }

        private bool GetBoneById(Int64 pEnemySoldier, int Id, out Vector3 _World)
        {
            _World = new Vector3();

            Int64 pRagdollComp = RPM.Read<Int64>(pEnemySoldier + Offsets.MKO_ClientSoldierEntity.m_ragdollComponent);
            if (!RPM.IsValid(pRagdollComp))
                return false;

            byte m_ValidTransforms = RPM.Read<Byte>(pRagdollComp + (Offsets.MKO_ClientRagDollComponent.m_ragdollTransforms + Offsets.MKO_UpdatePoseResultData.m_ValidTransforms));
            if (m_ValidTransforms != 1)
                return false;

            Int64 pQuatTransform = RPM.Read<Int64>(pRagdollComp + (Offsets.MKO_ClientRagDollComponent.m_ragdollTransforms + Offsets.MKO_UpdatePoseResultData.m_ActiveWorldTransforms));
            if (!RPM.IsValid(pQuatTransform))
                return false;

            _World = RPM.Read<Vector3>(pQuatTransform + Id * 0x20);
            return true;
        }

        private void MainScan()
        {
            //localPlayer.CurrentWeapon = new Weapon();
            players.Clear();
            targetEnimies.Clear();

            // Read Local
            #region Get Local Player

            Int64 pShotsStats = RPM.Read<Int64>(Offsets.OFFSET_SHOTSTATS);
            if (RPM.IsValid(pShotsStats))
            {
                localPlayer.ShotsFired = (float)RPM.Read<Int32>(pShotsStats + Offsets.MKO_ShotStats.m_shotsFired);
                localPlayer.ShotsHit = (float)RPM.Read<Int32>(pShotsStats + Offsets.MKO_ShotStats.m_shotsHit);
                localPlayer.DamageCount = (float)RPM.Read<Int32>(pShotsStats + Offsets.MKO_ShotStats.m_damageCount);
            }

            // Render View
            Int64 pGameRenderer = RPM.Read<Int64>(Offsets.OFFSET_GAMERENDERER);
            Int64 pRenderView = RPM.Read<Int64>(pGameRenderer + Offsets.MKO_GameRenderer.m_pRenderView);

            // Read Field of View
            localPlayer.FoV.X = RPM.Read<float>(pRenderView + Offsets.MKO_RenderView.m_fovX);
            localPlayer.FoV.Y = RPM.Read<float>(pRenderView + Offsets.MKO_RenderView.m_fovY);

            // Read Screen Matrix
            localPlayer.ViewProj = RPM.Read<Matrix>(pRenderView + Offsets.MKO_RenderView.m_viewProj);
            localPlayer.MatrixInverse = RPM.Read<Matrix>(pRenderView + Offsets.MKO_RenderView.m_viewMatrixInverse);

            Int64 pGContext = RPM.Read<Int64>(Offsets.OFFSET_CLIENTGAMECONTEXT);
            if (!RPM.IsValid(pGContext))
                return;

            Int64 pPlayerManager = RPM.Read<Int64>(pGContext + Offsets.MKO_ClientGameContext.m_pPlayerManager);
            if (!RPM.IsValid(pPlayerManager))
                return;

            Int64 plocalPlayer = RPM.Read<Int64>(pPlayerManager + Offsets.MKO_ClientPlayerManager.m_pLocalPlayer);
            if (!RPM.IsValid(plocalPlayer))
                return;

            localPlayer.Team = RPM.Read<Int32>(plocalPlayer + Offsets.MKO_ClientPlayer.m_teamId);
            //localPlayer.Name = RPM.ReadString(plocalPlayer + Offsets.ClientPlayer.szName, 10);

            Int64 pLocalSoldier = GetClientSoldierEntity(plocalPlayer, localPlayer);
            if (!RPM.IsValid(pLocalSoldier))
                return;

            Int64 pHealthComponent = RPM.Read<Int64>(pLocalSoldier + Offsets.MKO_ClientSoldierEntity.m_pHealthComponent);
            if (!RPM.IsValid(pHealthComponent))
                return;

            Int64 pPredictedController = RPM.Read<Int64>(pLocalSoldier + Offsets.MKO_ClientSoldierEntity.m_pPredictedController);
            if (!RPM.IsValid(pPredictedController))
                return;

            // Health
            localPlayer.Health = RPM.Read<float>(pHealthComponent + Offsets.MKO_HealthComponent.m_Health);
            localPlayer.MaxHealth = RPM.Read<float>(pHealthComponent + Offsets.MKO_HealthComponent.m_MaxHealth);

            if (localPlayer.IsDead())
                return;

            // Origin
            localPlayer.Origin = RPM.Read<Vector3>(pPredictedController + Offsets.MKO_ClientSoldierPrediction.m_Position);
            localPlayer.Velocity = RPM.Read<Vector3>(pPredictedController + Offsets.MKO_ClientSoldierPrediction.m_Velocity);

            // Other
            localPlayer.Pose = RPM.Read<Int32>(pLocalSoldier + Offsets.MKO_ClientSoldierEntity.m_poseType);
            localPlayer.Yaw = RPM.Read<float>(pLocalSoldier + Offsets.MKO_ClientSoldierEntity.m_authorativeYaw);

            localPlayer.IsOccluded = RPM.Read<Byte>(pLocalSoldier + Offsets.MKO_ClientSoldierEntity.m_occluded);

            if (!localPlayer.InVehicle)
            {
                Int64 pClientWeaponComponent = RPM.Read<Int64>(pLocalSoldier + Offsets.MKO_ClientSoldierEntity.m_soldierWeaponsComponent);
                if (RPM.IsValid(pClientWeaponComponent))
                {
                    Int64 pWeaponHandle = RPM.Read<Int64>(pClientWeaponComponent + Offsets.MKO_ClientSoldierWeaponsComponent.m_handler);
                    Int32 ActiveSlot = RPM.Read<Int32>(pClientWeaponComponent + Offsets.MKO_ClientSoldierWeaponsComponent.m_activeSlot);
                    Int32 ZeroingDistanceLevel = RPM.Read<Int32>(pClientWeaponComponent + Offsets.MKO_ClientSoldierWeaponsComponent.m_zeroingDistanceLevel);

                    if (RPM.IsValid(pWeaponHandle))
                    {
                        Int64 pSoldierWeapon = RPM.Read<Int64>(pWeaponHandle + ActiveSlot * 0x8);
                        if (RPM.IsValid(pSoldierWeapon))
                        {
                            currWeaponSlot = (Offsets.MKO_ClientSoldierWeaponsComponent.WeaponSlot)ActiveSlot;

                            Int64 pAimingSimulation = RPM.Read<Int64>(pSoldierWeapon + Offsets.MKO_ClientSoldierWeapon.m_authorativeAiming);
                            if (RPM.IsValid(pAimingSimulation))
                                localPlayer.Sway = RPM.Read<Vector2>(pAimingSimulation + Offsets.MKO_ClientSoldierAimingSimulation.m_sway);

                            #region Weapon Data
                            Int64 pWeaponEntityData = RPM.Read<Int64>(pSoldierWeapon + Offsets.MKO_ClientSoldierWeapon.m_data);
                            if (RPM.IsValid(pWeaponEntityData))
                            {
                                Int64 pWeaponName = RPM.Read<Int64>(pWeaponEntityData + Offsets.MKO_WeaponEntityData.m_name);
                                if (RPM.IsValid(pWeaponName))
                                {
                                    Int64 pCorrectedFiring = RPM.Read<Int64>(pSoldierWeapon + Offsets.MKO_ClientSoldierWeapon.m_pPrimary);
                                    if (RPM.IsValid(pCorrectedFiring))
                                    {
                                        localPlayer.CurrentWeapon = new Gun();
                                        string weaponName = RPM.ReadString(pWeaponName, 12).Trim().ToUpper();

                                        if (((int)currWeaponSlot >= 0 && (int)currWeaponSlot < 2) ||
                                            weaponName.Contains("M82") || weaponName.Contains("AMR") ||
                                            weaponName.Contains("SMAW") || weaponName.Contains("SRAW") || weaponName.Contains("RPG"))
                                        {
                                            if (localWeapons.Exists(x => x.Name == weaponName))
                                            {
                                                localPlayer.CurrentWeapon = localWeapons.Find(x => x.Name == weaponName);
                                            }
                                            else
                                            {
                                                localPlayer.CurrentWeapon.Name = weaponName;
                                                localPlayer.CurrentWeapon.Slot = currWeaponSlot;

                                                Int64 pPrimaryFire = RPM.Read<Int64>(pCorrectedFiring + Offsets.MKO_WeaponFiring.m_pPrimaryFire);
                                                if (RPM.IsValid(pPrimaryFire))
                                                {
                                                    Int64 pShotConfigData = RPM.Read<Int64>(pPrimaryFire + Offsets.MKO_PrimaryFire.m_shotConfigData);
                                                    if (RPM.IsValid(pShotConfigData))
                                                    {
                                                        localPlayer.CurrentWeapon.RateOfFire = RPM.Read<float>(pShotConfigData + Offsets.MKO_ShotConfigData.m_FireLogic);
                                                        localPlayer.CurrentWeapon.BulletInitialPosition = RPM.Read<Vector3>(pShotConfigData + Offsets.MKO_ShotConfigData.m_PositionOffset);
                                                        localPlayer.CurrentWeapon.BulletInitialSpeed = RPM.Read<Vector3>(pShotConfigData + Offsets.MKO_ShotConfigData.m_initialSpeed);
                                                        localPlayer.CurrentWeapon.BulletSpeed = RPM.Read<float>(pShotConfigData + Offsets.MKO_ShotConfigData.m_Speed);
                                                        localPlayer.CurrentWeapon.BulletsPerShell = RPM.Read<int>(pShotConfigData + Offsets.MKO_ShotConfigData.m_numberOfBulletsPerShell);
                                                        localPlayer.CurrentWeapon.BulletsPerShot = RPM.Read<int>(pShotConfigData + Offsets.MKO_ShotConfigData.m_numberOfBulletsPerShot);

                                                        Int64 pProjectileData = RPM.Read<Int64>(pShotConfigData + Offsets.MKO_ShotConfigData.m_pProjectileData);
                                                        if (RPM.IsValid(pProjectileData))
                                                            localPlayer.CurrentWeapon.BulletGravity = RPM.Read<float>(pProjectileData + Offsets.MKO_BulletEntityData.m_Gravity);
                                                    }
                                                }

                                                Int64 pSway = RPM.Read<Int64>(pCorrectedFiring + Offsets.MKO_WeaponFiring.m_pSway);
                                                if (RPM.IsValid(pSway))
                                                {
                                                    Int64 pSwayData = RPM.Read<Int64>(pSway + Offsets.MKO_WeaponSway.m_pSwayData);
                                                    if (RPM.IsValid(pSwayData))
                                                    {
                                                        localPlayer.CurrentWeapon.RecoilMultiplier = RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_FirstShotRecoilMultiplier);
                                                        localPlayer.CurrentWeapon.RecoilDecrease = RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_ShootingRecoilDecreaseScale);

                                                        localPlayer.CurrentWeapon.DeviationZoom = RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_DeviationScaleFactorZoom);
                                                        localPlayer.CurrentWeapon.GameplayDeviationZoom = RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_GameplayDeviationScaleFactorZoom);
                                                        localPlayer.CurrentWeapon.DeviationNoZoom = RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_DeviationScaleFactorNoZoom);
                                                        localPlayer.CurrentWeapon.GameplayDeviationNoZoom = RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_GameplayDeviationScaleFactorNoZoom);
                                                    }
                                                }

                                                if (localPlayer.CurrentWeapon.IsValid())
                                                    localWeapons.Add(localPlayer.CurrentWeapon);
                                            }
                                        }

                                        if (localPlayer.CurrentWeapon.IsValid())
                                        {
                                            localPlayer.CurrentWeapon.Ammo = RPM.Read<Int32>(pCorrectedFiring + Offsets.MKO_WeaponFiring.m_projectilesLoaded);
                                            localPlayer.CurrentWeapon.AmmoClip = RPM.Read<Int32>(pCorrectedFiring + Offsets.MKO_WeaponFiring.m_projectilesInMagazines);

                                            if (ZeroingDistanceLevel == -1)
                                                localPlayer.CurrentWeapon.ZeroingDistanceRadians = 0.0f;
                                            else
                                            {
                                                Int64 pWeaponModifier = RPM.Read<Int64>(pCorrectedFiring + Offsets.MKO_WeaponFiring.m_pWeaponModifier);
                                                if (RPM.IsValid(pWeaponModifier))
                                                {
                                                    Int64 pWeaponZeroingModifier = RPM.Read<Int64>(pWeaponModifier + Offsets.MKO_WeaponModifier.m_pWeaponZeroingModifier);
                                                    if (RPM.IsValid(pWeaponZeroingModifier))
                                                    {
                                                        localPlayer.CurrentWeapon.ZeroingDistanceDefault = RPM.Read<float>(pWeaponZeroingModifier + Offsets.MKO_WeaponZeroingModifier.m_defaultZeroingDistance);
                                                        Int64 pModes = RPM.Read<Int64>(pWeaponZeroingModifier + Offsets.MKO_WeaponZeroingModifier.m_Modes);
                                                        if (RPM.IsValid(pModes))
                                                        {
                                                            Vector2 Vec2 = RPM.Read<Vector2>(pModes + ZeroingDistanceLevel * 0x8);
                                                            if (Vec2.X != 0 && Vec2.Y != 0)
                                                                localPlayer.CurrentWeapon.ZeroingDistanceRadians = (float)Vec2.Y * (float)(Math.PI / 180.0f);
                                                        }
                                                    }
                                                }
                                            }

                                            #region No Breath, No Recoil, No Spread, No Gravity, RoF

                                            NoBreathControl();

                                            RipOfRecoilControl();

                                            NoSpreadControl();

                                            RateOfFireControl();

                                            NoGravityControl();

                                            SuperSpeedBulletControl();

                                            DoubleBulletsControl();

                                            #endregion
                                        }
                                    }
                                }
                            }
                            #endregion
                        }
                    }
                }
            }
            else
            {
                Int64 pCurrentWeaponFiring =  RPM.Read<Int64>(Offsets.OFFSET_CURRENT_WEAPONFIRING);
                if (RPM.IsValid(pCurrentWeaponFiring))
                {
                    if (localPlayer.CurrentWeapon == null)
                        localPlayer.CurrentWeapon = new Gun();
                    localPlayer.CurrentWeapon.Ammo = RPM.Read<Int32>(pCurrentWeaponFiring + Offsets.MKO_WeaponFiring.m_projectilesLoaded);
                    localPlayer.CurrentWeapon.AmmoClip = RPM.Read<Int32>(pCurrentWeaponFiring + Offsets.MKO_WeaponFiring.m_projectilesInMagazines);
                }
            }
            #endregion

            // Pointer to Players Array
            Int64 m_ppPlayer = RPM.Read<Int64>(pPlayerManager + Offsets.MKO_ClientPlayerManager.m_ppPlayer);
            if (!RPM.IsValid(m_ppPlayer))
                return;

            // Reset
            spectatorCount = 0;
            proximityCount = 0;

            #region Get Other Players by Id
            for (uint i = 0; i < 70; i++)
            {
                // Create new Player
                GPlayer player = new GPlayer();

                // Pointer to ClientPlayer class (Player Array + (Id * Size of Pointer))
                Int64 pEnemyPlayer = RPM.Read<Int64>(m_ppPlayer + (i * sizeof(Int64)));
                if (!RPM.IsValid(pEnemyPlayer))
                    continue;

                //if (pEnemyPlayer == plocalPlayer)
                //    continue;

                player.IsSpectator = Convert.ToBoolean(RPM.Read<Byte>(pEnemyPlayer + Offsets.MKO_ClientPlayer.m_isSpectator));

                if (player.IsSpectator)
                    spectatorCount++;

                // Name
                player.Name = RPM.ReadString(pEnemyPlayer + Offsets.MKO_ClientPlayer.szName, 10);

                // RPM.Read<Int64>(pEnemyPlayer + Offsets.ClientPlayer.m_pControlledControllable);
                Int64 pEnemySoldier = GetClientSoldierEntity(pEnemyPlayer, player);
                if (!RPM.IsValid(pEnemySoldier))
                    continue;

                Int64 pEnemyHealthComponent = RPM.Read<Int64>(pEnemySoldier + Offsets.MKO_ClientSoldierEntity.m_pHealthComponent);
                if (!RPM.IsValid(pEnemyHealthComponent))
                    continue;

                Int64 pEnemyPredictedController = RPM.Read<Int64>(pEnemySoldier + Offsets.MKO_ClientSoldierEntity.m_pPredictedController);
                if (!RPM.IsValid(pEnemyPredictedController))
                    continue;

                // Health
                player.Health = RPM.Read<float>(pEnemyHealthComponent + Offsets.MKO_HealthComponent.m_Health);
                player.MaxHealth = RPM.Read<float>(pEnemyHealthComponent + Offsets.MKO_HealthComponent.m_MaxHealth);

                if (player.Health <= 0.1f) // DEAD
                    continue;

                // Origin (Position in Game X, Y, Z)
                player.Origin = RPM.Read<Vector3>(pEnemyPredictedController + Offsets.MKO_ClientSoldierPrediction.m_Position);
                player.Velocity = RPM.Read<Vector3>(pEnemyPredictedController + Offsets.MKO_ClientSoldierPrediction.m_Velocity);

                // Other
                player.Team = RPM.Read<Int32>(pEnemyPlayer + Offsets.MKO_ClientPlayer.m_teamId);
                player.Pose = RPM.Read<Int32>(pEnemySoldier + Offsets.MKO_ClientSoldierEntity.m_poseType);
                player.Yaw = RPM.Read<float>(pEnemySoldier + Offsets.MKO_ClientSoldierEntity.m_authorativeYaw);
                player.IsOccluded = RPM.Read<Byte>(pEnemySoldier + Offsets.MKO_ClientSoldierEntity.m_occluded);

                // Distance to You
                player.Distance = Vector3.Distance(localPlayer.Origin, player.Origin);
                player.DistanceToCrosshair = AimCrosshairDistance(player);

                players.Add(player);

                if (player.IsValid())
                {
                    // Player Bone
                    bBoneOk = (GetBoneById(pEnemySoldier, (int)Offsets.MKO_UpdatePoseResultData.BONES.BONE_HEAD, out player.Bone.BONE_HEAD)
                            && GetBoneById(pEnemySoldier, (int)Offsets.MKO_UpdatePoseResultData.BONES.BONE_LEFTELBOWROLL, out player.Bone.BONE_LEFTELBOWROLL)
                            && GetBoneById(pEnemySoldier, (int)Offsets.MKO_UpdatePoseResultData.BONES.BONE_LEFTFOOT, out player.Bone.BONE_LEFTFOOT)
                            && GetBoneById(pEnemySoldier, (int)Offsets.MKO_UpdatePoseResultData.BONES.BONE_LEFTHAND, out player.Bone.BONE_LEFTHAND)
                            && GetBoneById(pEnemySoldier, (int)Offsets.MKO_UpdatePoseResultData.BONES.BONE_LEFTKNEEROLL, out player.Bone.BONE_LEFTKNEEROLL)
                            && GetBoneById(pEnemySoldier, (int)Offsets.MKO_UpdatePoseResultData.BONES.BONE_LEFTSHOULDER, out player.Bone.BONE_LEFTSHOULDER)
                            && GetBoneById(pEnemySoldier, (int)Offsets.MKO_UpdatePoseResultData.BONES.BONE_NECK, out player.Bone.BONE_NECK)
                            && GetBoneById(pEnemySoldier, (int)Offsets.MKO_UpdatePoseResultData.BONES.BONE_RIGHTELBOWROLL, out player.Bone.BONE_RIGHTELBOWROLL)
                            && GetBoneById(pEnemySoldier, (int)Offsets.MKO_UpdatePoseResultData.BONES.BONE_RIGHTFOOT, out player.Bone.BONE_RIGHTFOOT)
                            && GetBoneById(pEnemySoldier, (int)Offsets.MKO_UpdatePoseResultData.BONES.BONE_RIGHTHAND, out player.Bone.BONE_RIGHTHAND)
                            && GetBoneById(pEnemySoldier, (int)Offsets.MKO_UpdatePoseResultData.BONES.BONE_RIGHTKNEEROLL, out player.Bone.BONE_RIGHTKNEEROLL)
                            && GetBoneById(pEnemySoldier, (int)Offsets.MKO_UpdatePoseResultData.BONES.BONE_RIGHTSHOULDER, out player.Bone.BONE_RIGHTSHOULDER)
                            && GetBoneById(pEnemySoldier, (int)Offsets.MKO_UpdatePoseResultData.BONES.BONE_SPINE, out player.Bone.BONE_SPINE)
                            && GetBoneById(pEnemySoldier, (int)Offsets.MKO_UpdatePoseResultData.BONES.BONE_SPINE1, out player.Bone.BONE_SPINE1)
                            && GetBoneById(pEnemySoldier, (int)Offsets.MKO_UpdatePoseResultData.BONES.BONE_SPINE2, out player.Bone.BONE_SPINE2));

                    #region Get Valid Target for Aimbot
                    if (bAimbot /*&& bBoneOk*/ && player.Team != localPlayer.Team && (player.IsVisible() || (bAimAtVehicles && player.InVehicle)))
                        targetEnimies.Add(player);
                    #endregion

                    #region Drawing ESP on Overlay

                    // Desconsidera Aliados
                    if (!bEspAllies && (player.Team == localPlayer.Team))
                        continue;

                    // Desconsidera os "Não Visíveis"
                    if (bEspVisiblesOnly && (!player.IsVisible() || player.Distance > 75) && !player.InVehicle)
                        continue;

                    #region ESP Bone
                    if (bBoneOk && ESP_Bone)
                        DrawBone(player);
                    #endregion

                    Vector3 w2sFoot, w2sHead;
                    if (WorldToScreen(player.Origin, out w2sFoot) && WorldToScreen(player.Origin, player.Pose, out w2sHead))
                    {
                        float H = w2sFoot.Y - w2sHead.Y;
                        float W = H / 2;
                        float X = w2sHead.X - W / 2;
                        int iAux;

                        #region ESP Color
                        Color color;
                        color = (player.Team == localPlayer.Team) ? friendlyColor : player.IsVisible() ? enemyColorVisible : enemyColor;
                        #endregion

                        #region ESP Box
                        // ESP Box
                        if (ESP_Box && !bEspVisiblesOnly)
                            if (bEsp3D)
                                DrawAABB(player.GetAABB(), player.Origin, player.Yaw, color); // 3D Box
                            else
                                DrawRect((int)X, (int)w2sHead.Y, (int)W, (int)H, color); // 2D Box
                        #endregion

                        #region ESP Vehicle
                        if (ESP_Vehicle)
                            //if (bEsp3D)
                            DrawAABB(player.VehicleAABB, player.VehicleTranfsorm, player.Team == localPlayer.Team ? friendlyColorVehicle : enemyColorVehicle);
                        //else
                        //    DrawRect((int)X, (int)w2sHead.Y, (int)W, (int)H, color); // 2D Box
                        #endregion

                        #region ESP Name
                        if (ESP_Name && !bEspVisiblesOnly)
                            DrawText((int)X, (int)w2sFoot.Y, player.Name, Color.Orange, true, fontSmall);
                        #endregion

                        #region ESP Distance
                        if (ESP_Distance && !bEspVisiblesOnly)
                        {
                            iAux = (int)w2sFoot.Y;
                            if (ESP_Name)
                                iAux = iAux + 13;
                            DrawText((int)X, iAux, (int)player.Distance + "m", Color.Orange, true, fontSmall);
                        }
                        #endregion

                        #region ESP Health
                        if (ESP_Health && !bEspVisiblesOnly)
                        {
                            DrawHealth((int)X, (int)w2sHead.Y - 6, (int)W, 3, (int)player.Health, (int)player.MaxHealth);
                            if (player.InVehicle && player.IsDriver)
                                DrawHealth((int)X, (int)w2sHead.Y - 10, (int)W, 3, (int)player.VehicleHealth, (int)player.VehicleMaxHealth);
                        }
                        #endregion

                        #region ESP Spotline
                        if (bEspSpotline && player.Distance <= proximityDeadline && player.Team != localPlayer.Team)
                        {
                            proximityCount++;
                            DrawSpotline((int)w2sFoot.X, (int)w2sFoot.Y, player.IsVisible());
                        }
                        #endregion

                    }
                    #endregion

                }
            }
            #endregion
        }

            #region No Breath Control
        private bool NoBreathControl()
        {
            //if ((bNoBreath && localPlayer.NoBreathEnabled) || (!bNoBreath && !localPlayer.NoBreathEnabled))
            //    return true;

            if (!localPlayer.CurrentWeapon.IsValid() || (int)localPlayer.CurrentWeapon.Slot >= 2 || localPlayer.IsDead() || localPlayer.InVehicle)
                return false;

            Int64 pLocalSoldier = GetLocalSoldier();
            if (!RPM.IsValid(pLocalSoldier))
                return false;

            Int64 pBreathControlHandler = RPM.Read<Int64>(pLocalSoldier + Offsets.MKO_ClientSoldierEntity.m_breathControlHandler);
            if (!RPM.IsValid(pBreathControlHandler))
                return false;

            if (bNoBreath /*&& !localPlayer.NoBreathEnabled*/)
            {
                if (RPM.Read<float>(pBreathControlHandler + Offsets.MKO_BreathControlHandler.m_Enabled) != 0.0f)
                    RPM.Write<float>(pBreathControlHandler + Offsets.MKO_BreathControlHandler.m_Enabled, 0.0f);

                localPlayer.NoBreathEnabled = true;

                return true;
            }
            else/* if (localPlayer.NoBreathEnabled)*/
            { 
                if (RPM.Read<float>(pBreathControlHandler + Offsets.MKO_BreathControlHandler.m_Enabled) == 0.0f)
                    RPM.Write<float>(pBreathControlHandler + Offsets.MKO_BreathControlHandler.m_Enabled, 1.0f);

                if (localPlayer.NoBreathEnabled)
                    localPlayer.NoBreathEnabled = false;

                return true;
            }

            //return false;
        }
        #endregion

            #region No Rate Of Fire Control
        private bool RateOfFireControl()
        {
            if (!localPlayer.CurrentWeapon.IsValid() || (int)localPlayer.CurrentWeapon.Slot >= 2 || localPlayer.IsDead() || localPlayer.InVehicle)
                return false;

            if (localPlayer.CurrentWeapon.RateOfFire <= 0.0f)
                return false;

            Int64 pSoldierWeapon = GetSoldierWeapon();
            if (!RPM.IsValid(pSoldierWeapon))
                return false;

            Int64 pCorrectedFiring = RPM.Read<Int64>(pSoldierWeapon + Offsets.MKO_ClientSoldierWeapon.m_pPrimary);
            if (!RPM.IsValid(pCorrectedFiring))
                return false;

            Int64 pPrimaryFire = RPM.Read<Int64>(pCorrectedFiring + Offsets.MKO_WeaponFiring.m_pPrimaryFire);
            if (!RPM.IsValid(pPrimaryFire))
                return false;

            Int64 pShotConfigData = RPM.Read<Int64>(pPrimaryFire + Offsets.MKO_PrimaryFire.m_shotConfigData);
            if (!RPM.IsValid(pShotConfigData))
                return false;

            float actualRoF = RPM.Read<float>(pShotConfigData + Offsets.MKO_ShotConfigData.m_FireLogic);
            float newRoF = 0.0f;
            switch (currMnRoF)
            {
                case mnRateOfFire.DEFAULT:
                    newRoF = localPlayer.CurrentWeapon.RateOfFire;
                    break;
                case mnRateOfFire.PERFIVE:
                    newRoF = (float)(Math.Truncate((localPlayer.CurrentWeapon.RateOfFire * (1 + (1.0f / 5.0f))) / 10) * 10);
                    break;
                case mnRateOfFire.PERTHREE:
                    newRoF = (float)(Math.Truncate((localPlayer.CurrentWeapon.RateOfFire * (1 + (1.0f / 3.0f))) / 10) * 10);
                    break;
                case mnRateOfFire.PERTWO:
                    newRoF = (float)(Math.Truncate((localPlayer.CurrentWeapon.RateOfFire * (1 + (1.0f / 2.0f))) / 10) * 10);
                    break;
                case mnRateOfFire.DOUBLE:
                    newRoF = (float)Math.Truncate((localPlayer.CurrentWeapon.RateOfFire * 2.0f));
                    break;
            }

            if ((bRateOfFire && localPlayer.CurrentWeapon.RateOfFireBoostEnabled && actualRoF == newRoF)
             || (!bRateOfFire && !localPlayer.CurrentWeapon.RateOfFireBoostEnabled))
                return true;

            if (actualRoF != newRoF)
            {
                RPM.Write<float>(pShotConfigData + Offsets.MKO_ShotConfigData.m_FireLogic, newRoF);
                localPlayer.CurrentWeapon.RateOfFireBoostEnabled = !(currMnRoF == mnRateOfFire.DEFAULT);
                return true;
            }

            return false;
        }
        #endregion

            #region No Gravity Control
        private bool NoGravityControl()
        {
            if ((bNoGravity && localPlayer.CurrentWeapon.NoGravityEnabled) || (!bNoGravity && !localPlayer.CurrentWeapon.NoGravityEnabled))
                return true;

            if (!localPlayer.CurrentWeapon.IsValid() || (int)localPlayer.CurrentWeapon.Slot >= 2 || localPlayer.IsDead() || localPlayer.InVehicle)
                return false;

            Int64 pSoldierWeapon = GetSoldierWeapon();
            if (!RPM.IsValid(pSoldierWeapon))
                return false;

            Int64 pCorrectedFiring = RPM.Read<Int64>(pSoldierWeapon + Offsets.MKO_ClientSoldierWeapon.m_pPrimary);
            if (!RPM.IsValid(pCorrectedFiring))
                return false;

            Int64 pPrimaryFire = RPM.Read<Int64>(pCorrectedFiring + Offsets.MKO_WeaponFiring.m_pPrimaryFire);
            if (!RPM.IsValid(pPrimaryFire))
                return false;

            Int64 pShotConfigData = RPM.Read<Int64>(pPrimaryFire + Offsets.MKO_PrimaryFire.m_shotConfigData);
            if (!RPM.IsValid(pShotConfigData))
                return false;

            Int64 pProjectileData = RPM.Read<Int64>(pShotConfigData + Offsets.MKO_ShotConfigData.m_pProjectileData);
            if (!RPM.IsValid(pProjectileData))
                return false;

            if (bNoGravity && !localPlayer.CurrentWeapon.NoGravityEnabled)
            {
                if (RPM.Read<float>(pProjectileData + Offsets.MKO_BulletEntityData.m_Gravity) != 0.0f)
                    RPM.Write<float>(pProjectileData + Offsets.MKO_BulletEntityData.m_Gravity, 0.0f);

                localPlayer.CurrentWeapon.NoGravityEnabled = true;

                return true;
            }
            else if (!bNoGravity && localPlayer.CurrentWeapon.NoGravityEnabled)
            {
                if (localPlayer.CurrentWeapon.BulletGravity > 0)
                {
                    if (RPM.Read<float>(pProjectileData + Offsets.MKO_BulletEntityData.m_Gravity) != localPlayer.CurrentWeapon.BulletGravity)
                        RPM.Write<float>(pProjectileData + Offsets.MKO_BulletEntityData.m_Gravity, localPlayer.CurrentWeapon.BulletGravity);

                    localPlayer.CurrentWeapon.NoGravityEnabled = false;

                    return true;
                }
            }

            return false;
        }
        #endregion

            #region Super Speed Bullet Control
        private bool SuperSpeedBulletControl()
        {
            if ((bSuperBullet && localPlayer.CurrentWeapon.SuperSpeedBulletEnabled) || (!bSuperBullet && !localPlayer.CurrentWeapon.SuperSpeedBulletEnabled))
                return true;

            if (!localPlayer.CurrentWeapon.IsValid() || (int)localPlayer.CurrentWeapon.Slot >= 2 || localPlayer.IsDead() || localPlayer.InVehicle)
                return false;

            Int64 pSoldierWeapon = GetSoldierWeapon();
            if (!RPM.IsValid(pSoldierWeapon))
                return false;

            Int64 pCorrectedFiring = RPM.Read<Int64>(pSoldierWeapon + Offsets.MKO_ClientSoldierWeapon.m_pPrimary);
            if (!RPM.IsValid(pCorrectedFiring))
                return false;

            Int64 pPrimaryFire = RPM.Read<Int64>(pCorrectedFiring + Offsets.MKO_WeaponFiring.m_pPrimaryFire);
            if (!RPM.IsValid(pPrimaryFire))
                return false;

            Int64 pShotConfigData = RPM.Read<Int64>(pPrimaryFire + Offsets.MKO_PrimaryFire.m_shotConfigData);
            if (!RPM.IsValid(pShotConfigData))
                return false;

            if (bSuperBullet && !localPlayer.CurrentWeapon.SuperSpeedBulletEnabled)
            {
                if (RPM.Read<float>(pShotConfigData + Offsets.MKO_ShotConfigData.m_Speed) != 1500f)
                    RPM.Write<float>(pShotConfigData + Offsets.MKO_ShotConfigData.m_Speed, 1500f);

                localPlayer.CurrentWeapon.SuperSpeedBulletEnabled = true;

                return true;
            }
            else if (!bSuperBullet && localPlayer.CurrentWeapon.SuperSpeedBulletEnabled)
            {
                if (localPlayer.CurrentWeapon.BulletSpeed > 0)
                {
                    if (RPM.Read<float>(pShotConfigData + Offsets.MKO_ShotConfigData.m_Speed) != localPlayer.CurrentWeapon.BulletSpeed)
                        RPM.Write<float>(pShotConfigData + Offsets.MKO_ShotConfigData.m_Speed, localPlayer.CurrentWeapon.BulletSpeed);

                    localPlayer.CurrentWeapon.SuperSpeedBulletEnabled = false;

                    return true;
                }
            }

            return false;
        }
        #endregion

            #region Double Bullets Control
        private bool DoubleBulletsControl()
        {
            if ((bDoubleBullets && localPlayer.CurrentWeapon.DoubleBulletsEnabled) || (!bDoubleBullets && !localPlayer.CurrentWeapon.DoubleBulletsEnabled))
                return true;

            if (!localPlayer.CurrentWeapon.IsValid() || (int)localPlayer.CurrentWeapon.Slot >= 2 || localPlayer.IsDead() || localPlayer.InVehicle)
                return false;

            Int64 pSoldierWeapon = GetSoldierWeapon();
            if (!RPM.IsValid(pSoldierWeapon))
                return false;

            Int64 pCorrectedFiring = RPM.Read<Int64>(pSoldierWeapon + Offsets.MKO_ClientSoldierWeapon.m_pPrimary);
            if (!RPM.IsValid(pCorrectedFiring))
                return false;

            Int64 pPrimaryFire = RPM.Read<Int64>(pCorrectedFiring + Offsets.MKO_WeaponFiring.m_pPrimaryFire);
            if (!RPM.IsValid(pPrimaryFire))
                return false;

            Int64 pShotConfigData = RPM.Read<Int64>(pPrimaryFire + Offsets.MKO_PrimaryFire.m_shotConfigData);
            if (!RPM.IsValid(pShotConfigData))
                return false;

            if (bDoubleBullets && !localPlayer.CurrentWeapon.DoubleBulletsEnabled)
            {
                if (localPlayer.CurrentWeapon.BulletsPerShot != 0)
                {
                    //if (RPM.Read<int>(pShotConfigData + Offsets.ShotConfigData.m_numberOfBulletsPerShell) != localPlayer.CurrentWeapon.BulletsPerShell * 2)
                      //  RPM.Write<int>(pShotConfigData + Offsets.ShotConfigData.m_numberOfBulletsPerShell, localPlayer.CurrentWeapon.BulletsPerShell * 2);

                    if (RPM.Read<int>(pShotConfigData + Offsets.MKO_ShotConfigData.m_numberOfBulletsPerShot) != localPlayer.CurrentWeapon.BulletsPerShot * 2)
                        RPM.Write<int>(pShotConfigData + Offsets.MKO_ShotConfigData.m_numberOfBulletsPerShot, localPlayer.CurrentWeapon.BulletsPerShot * 2);

                    localPlayer.CurrentWeapon.DoubleBulletsEnabled = true;

                    return true;
                }
                else
                    return false;
            }
            else if (!bDoubleBullets && localPlayer.CurrentWeapon.DoubleBulletsEnabled)
            {
                if (localPlayer.CurrentWeapon.BulletsPerShot != 0)
                {
                    //if (RPM.Read<int>(pShotConfigData + Offsets.ShotConfigData.m_numberOfBulletsPerShell) != localPlayer.CurrentWeapon.BulletsPerShell)
                    //    RPM.Write<int>(pShotConfigData + Offsets.ShotConfigData.m_numberOfBulletsPerShell, localPlayer.CurrentWeapon.BulletsPerShell);

                    if (RPM.Read<int>(pShotConfigData + Offsets.MKO_ShotConfigData.m_numberOfBulletsPerShot) != localPlayer.CurrentWeapon.BulletsPerShot)
                        RPM.Write<int>(pShotConfigData + Offsets.MKO_ShotConfigData.m_numberOfBulletsPerShot, localPlayer.CurrentWeapon.BulletsPerShot);

                    localPlayer.CurrentWeapon.DoubleBulletsEnabled = false;

                    return true;
                }
                else
                    return false;
            }

            return false;
        }
        #endregion

            #region No Recoil Control
        private bool RipOfRecoilControl()
        {
            if ((bRipOfRecoil && localPlayer.CurrentWeapon.RipOfRecoilEnabled) || (!bRipOfRecoil && !localPlayer.CurrentWeapon.RipOfRecoilEnabled))
                return true;

            if (!localPlayer.CurrentWeapon.IsValid() || (int)localPlayer.CurrentWeapon.Slot >= 2 || localPlayer.IsDead() || localPlayer.InVehicle)
                return false;

            Int64 pSoldierWeapon = GetSoldierWeapon();
            if (!RPM.IsValid(pSoldierWeapon))
                return false;

            Int64 pCorrectedFiring = RPM.Read<Int64>(pSoldierWeapon + Offsets.MKO_ClientSoldierWeapon.m_pPrimary);
            if (!RPM.IsValid(pCorrectedFiring))
                return false;

            Int64 pSway = RPM.Read<Int64>(pCorrectedFiring + Offsets.MKO_WeaponFiring.m_pSway);
            if (!RPM.IsValid(pSway))
                return false;

            Int64 pSwayData = RPM.Read<Int64>(pSway + Offsets.MKO_WeaponSway.m_pSwayData);
            if (!RPM.IsValid(pSwayData))
                return false;

            if (bRipOfRecoil && !localPlayer.CurrentWeapon.RipOfRecoilEnabled)
            {
                if (RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_FirstShotRecoilMultiplier) != 0.0f)
                    RPM.Write<float>(pSwayData + Offsets.MKO_GunSwayData.m_FirstShotRecoilMultiplier, 0.0f);

                if (RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_ShootingRecoilDecreaseScale) != 100.0f)
                    RPM.Write<float>(pSwayData + Offsets.MKO_GunSwayData.m_ShootingRecoilDecreaseScale, 100.0f);

                localPlayer.CurrentWeapon.RipOfRecoilEnabled = true;

                return true;
            }
            else if (localPlayer.CurrentWeapon.RipOfRecoilEnabled)
            {
                if (localPlayer.CurrentWeapon.RecoilMultiplier > 0 || localPlayer.CurrentWeapon.RecoilDecrease > 0)
                {
                    if (RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_FirstShotRecoilMultiplier) != localPlayer.CurrentWeapon.RecoilMultiplier)
                        RPM.Write<float>(pSwayData + Offsets.MKO_GunSwayData.m_FirstShotRecoilMultiplier, localPlayer.CurrentWeapon.RecoilMultiplier);

                    if (RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_ShootingRecoilDecreaseScale) != localPlayer.CurrentWeapon.RecoilDecrease)
                        RPM.Write<float>(pSwayData + Offsets.MKO_GunSwayData.m_ShootingRecoilDecreaseScale, localPlayer.CurrentWeapon.RecoilDecrease);

                    localPlayer.CurrentWeapon.RipOfRecoilEnabled = false;

                    return true;
                }
            }

            return false;
        }
        #endregion

            #region No Spread Control
        private bool NoSpreadControl()
        {
            if ((bNoSpread && localPlayer.CurrentWeapon.NoSpreadEnabled) || (!bNoSpread && !localPlayer.CurrentWeapon.NoSpreadEnabled))
                return true;

            if (!localPlayer.CurrentWeapon.IsValid() || (int)localPlayer.CurrentWeapon.Slot >= 2 || localPlayer.IsDead() || localPlayer.InVehicle)
                return false;

            Int64 pSoldierWeapon = GetSoldierWeapon();
            if (!RPM.IsValid(pSoldierWeapon))
                return false;

            Int64 pCorrectedFiring = RPM.Read<Int64>(pSoldierWeapon + Offsets.MKO_ClientSoldierWeapon.m_pPrimary);
            if (!RPM.IsValid(pCorrectedFiring))
                return false;

            Int64 pSway = RPM.Read<Int64>(pCorrectedFiring + Offsets.MKO_WeaponFiring.m_pSway);
            if (!RPM.IsValid(pSway))
                return false;

            Int64 pSwayData = RPM.Read<Int64>(pSway + Offsets.MKO_WeaponSway.m_pSwayData);
            if (!RPM.IsValid(pSwayData))
                return false;

            if (bNoSpread && !localPlayer.CurrentWeapon.NoSpreadEnabled)
            {
                // ADS Spread
                if (RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_DeviationScaleFactorZoom) != 0.0f)
                    RPM.Write<float>(pSwayData + Offsets.MKO_GunSwayData.m_DeviationScaleFactorZoom, 0.0f);
                if (RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_GameplayDeviationScaleFactorZoom) != 0.0f)
                    RPM.Write<float>(pSwayData + Offsets.MKO_GunSwayData.m_GameplayDeviationScaleFactorZoom, 0.0f);

                // Hip Fire Spread
                if (RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_DeviationScaleFactorNoZoom) != 0.0f)
                    RPM.Write<float>(pSwayData + Offsets.MKO_GunSwayData.m_DeviationScaleFactorNoZoom, 0.0f);
                if (RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_GameplayDeviationScaleFactorNoZoom) != 0.0f)
                    RPM.Write<float>(pSwayData + Offsets.MKO_GunSwayData.m_GameplayDeviationScaleFactorNoZoom, 0.0f);

                localPlayer.CurrentWeapon.NoSpreadEnabled = true;

                return true;
            }
            else if (localPlayer.CurrentWeapon.NoSpreadEnabled)
            {
                // ADS Spread
                if (localPlayer.CurrentWeapon.DeviationZoom > 0 || localPlayer.CurrentWeapon.GameplayDeviationZoom > 0)
                {
                    if (RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_DeviationScaleFactorZoom) != localPlayer.CurrentWeapon.DeviationZoom)
                        RPM.Write<float>(pSwayData + Offsets.MKO_GunSwayData.m_DeviationScaleFactorZoom, localPlayer.CurrentWeapon.DeviationZoom);
                    if (RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_GameplayDeviationScaleFactorZoom) != localPlayer.CurrentWeapon.GameplayDeviationZoom)
                        RPM.Write<float>(pSwayData + Offsets.MKO_GunSwayData.m_GameplayDeviationScaleFactorZoom, localPlayer.CurrentWeapon.GameplayDeviationZoom);
                }

                // Hip Fire Spread
                if (localPlayer.CurrentWeapon.DeviationNoZoom > 0 || localPlayer.CurrentWeapon.GameplayDeviationNoZoom > 0)
                {
                    if (RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_DeviationScaleFactorNoZoom) != localPlayer.CurrentWeapon.DeviationNoZoom)
                        RPM.Write<float>(pSwayData + Offsets.MKO_GunSwayData.m_DeviationScaleFactorNoZoom, localPlayer.CurrentWeapon.DeviationNoZoom);
                    if (RPM.Read<float>(pSwayData + Offsets.MKO_GunSwayData.m_GameplayDeviationScaleFactorNoZoom) != localPlayer.CurrentWeapon.GameplayDeviationNoZoom)
                        RPM.Write<float>(pSwayData + Offsets.MKO_GunSwayData.m_GameplayDeviationScaleFactorNoZoom, localPlayer.CurrentWeapon.GameplayDeviationNoZoom);
                }

                if (localPlayer.CurrentWeapon.DeviationZoom > 0 || localPlayer.CurrentWeapon.GameplayDeviationZoom > 0 || localPlayer.CurrentWeapon.DeviationNoZoom > 0 || localPlayer.CurrentWeapon.GameplayDeviationNoZoom > 0)
                {
                    localPlayer.CurrentWeapon.NoSpreadEnabled = false;
                    return true;
                }
            }

            return false;
        }
        #endregion

            #region Unlock Attachments Control
        private bool UnlockAttachmentsControl()
        {
            Int64 pSysSettings = RPM.Read<Int64>(Offsets.OFFSET_SETTINGS);
            if (!RPM.IsValid(pSysSettings))
                return false;

            if (bUnlockAttachments)
            {
                if (RPM.Read<byte>(pSysSettings + Offsets.MKO_SystemSettings.m_AllUnlocksUnlocked) != 1)
                    RPM.Write<byte>(pSysSettings + Offsets.MKO_SystemSettings.m_AllUnlocksUnlocked, 1);

                return true;
            }
            else
            {
                if (RPM.Read<byte>(pSysSettings + Offsets.MKO_SystemSettings.m_AllUnlocksUnlocked) != 0)
                    RPM.Write<byte>(pSysSettings + Offsets.MKO_SystemSettings.m_AllUnlocksUnlocked, 0);
                return true;
            }

        }
        #endregion

        #endregion

        #region Keys Stuff
        public void KeyAssign()
        {
            KeysMgr keyMgr = new KeysMgr();
            keyMgr.AddKey(Keys.Home);     // MENU
            keyMgr.AddKey(Keys.Up);       // UP
            keyMgr.AddKey(Keys.Down);     // DOWN
            keyMgr.AddKey(Keys.Right);    // CHANGE OPTION
            keyMgr.AddKey(Keys.Delete);   // QUIT

            keyMgr.AddKey(Keys.F6);       // Clear Weapon Data Bank (Collection)

            keyMgr.AddKey(Keys.F9);       // ATALHO 1
            keyMgr.AddKey(Keys.F10);      // ATALHO 2
            keyMgr.AddKey(Keys.F11);      // ATALHO 3
            keyMgr.AddKey(Keys.F12);      // ATALHO 4

            keyMgr.AddKey(Keys.LShiftKey);  // Aimbot Activate 1
            keyMgr.AddKey(Keys.RButton);   // Aimbot Activate 2

            keyMgr.AddKey(Keys.PageUp);    // Optimized Settings
            keyMgr.AddKey(Keys.PageDown);  // Default Settings

            keyMgr.KeyDownEvent += new KeysMgr.KeyHandler(KeyDownEvent);
        }

        public static bool IsKeyDown(int key)
        {
            return Convert.ToBoolean(Manager.GetKeyState(key) & Manager.KEY_PRESSED);
        }

        private void KeyDownEvent(int Id, string Name)
        {
            switch ((Keys)Id)
            {
                case Keys.Home:
                    this.bMenuControl = !this.bMenuControl;
                    break;
                case Keys.Delete:
                    Quit();
                    break;
                case Keys.Right:
                    SelectMenuItem();
                    break;
                case Keys.Up:
                    CycleMenuUp();
                    break;
                case Keys.Down:
                    CycleMenuDown();
                    break;
                case Keys.F6:
                    localWeapons.Clear();
                    break;
                case Keys.F9:
                    bNoBreath = !bNoBreath;
                    break;
                case Keys.F10:
                    if (bRipOfRecoil && bNoSpread)
                    {
                        bRipOfRecoil = false;
                        bNoSpread = false;
                    }
                    else
                    {
                        bRipOfRecoil = true;
                        bNoSpread = true;
                    }
                    break;
                case Keys.F11:
                    switch (currMnAimTarget)
                    {
                        case mnAimTarget.HEAD:
                            currMnAimTarget = mnAimTarget.NECK;
                            break;
                        case mnAimTarget.NECK:
                            currMnAimTarget = mnAimTarget.BODY;
                            break;
                        case mnAimTarget.BODY:
                            currMnAimTarget = mnAimTarget.HEAD;
                            break;
                    }
                    break;
                case Keys.F12:
                    switch (currMnAimMode)
                    {
                        case mnAimClosestTo.OFF:
                            currMnAimMode = mnAimClosestTo.PLAYER;
                            break;
                        case mnAimClosestTo.PLAYER:
                            currMnAimMode = mnAimClosestTo.CROSSHAIR;
                            break;
                        case mnAimClosestTo.CROSSHAIR:
                            currMnAimMode = mnAimClosestTo.OFF;
                            break;
                    }
                    bAimbot = !(currMnAimMode == mnAimClosestTo.OFF);
                    break;
                case Keys.PageUp:
                    currMnEspMode = mnEspMode.FULL;
                    ESP_Box = true;
                    ESP_Bone = true;
                    ESP_Name = true;
                    ESP_Health = true;
                    ESP_Distance = true;
                    ESP_Vehicle = true;

                    bEspVisiblesOnly = false;
                    bEsp3D = false;
                    bEspAllies = false;
                    bEspSpotline = true;

                    currMnAimMode = mnAimClosestTo.PLAYER;
                    currMnAimTarget = mnAimTarget.HEAD;
                    bAimbot = true;
                    bAimKeyDefault = true;
                    bAimTwoSecondsRule = false;
                    bAimAtVehicles = true;

                    bRipOfRecoil = true;
                    bNoSpread = true;
                    //bNoGravity = true;
                    //bSuperBullet = true;
                    bDoubleBullets = false;
                    currMnRoF = mnRateOfFire.DEFAULT;
                    bRateOfFire = false;
                    bNoBreath = true;

                    currMnHC = mnHardCoreMode.LEFT;
                    bHardcoreMode = true;
                    bSpectatorWarn = true;
                    bUnlockAttachments = true;
                    if (!UnlockAttachmentsControl())
                        bUnlockAttachments = false;

                    break;
                case Keys.PageDown:
                    currMnEspMode = mnEspMode.FULL;
                    ESP_Box = true;
                    ESP_Bone = true;
                    ESP_Name = true;
                    ESP_Health = true;
                    ESP_Distance = true;
                    ESP_Vehicle = true;

                    bEspVisiblesOnly = false;
                    bEsp3D = false;
                    bEspAllies = false;
                    bEspSpotline = false;

                    currMnAimMode = mnAimClosestTo.OFF;
                    currMnAimTarget = mnAimTarget.HEAD;
                    bAimbot = false;
                    bAimKeyDefault = true;
                    bAimTwoSecondsRule = true;
                    bAimAtVehicles = false;

                    bRipOfRecoil = false;
                    bNoSpread = false;
                    //bNoGravity = false;
                    //bSuperBullet = false;
                    bDoubleBullets = false;
                    currMnRoF = mnRateOfFire.DEFAULT;
                    bRateOfFire = false;
                    bNoBreath = false;

                    currMnHC = mnHardCoreMode.OFF;
                    bHardcoreMode = false;
                    bSpectatorWarn = true;
                    bUnlockAttachments = false;
                    if (!UnlockAttachmentsControl())
                        bUnlockAttachments = true;
                    break;
            }
        }
        #endregion

        #region Menu Stuff

        private bool bMenuControl = true;

        private bool bEspVisiblesOnly = false;
        private bool bEsp3D = false;
        private bool bEspAllies = false;
        private bool bEspSpotline = false;

        private bool bAimbot = false;
        private bool bAimKeyDefault = true;
        private bool bAimTwoSecondsRule = true;
        private bool bAimAtVehicles = false;
        
        private bool bRipOfRecoil = false;
        private bool bNoSpread = false;
        private bool bNoGravity = false;
        private bool bSuperBullet = false;
        private bool bDoubleBullets = false;
        private bool bRateOfFire = false;
        private bool bNoBreath = false;

        private bool bHardcoreMode = false;
        private bool bUnlockAttachments = false;
        private bool bSpectatorWarn = true;

        private bool bCrosshairHUD = false;
        private bool bRadarHUD = false;
        private bool bAmmoHealthHUD = false;

        private enum mnIndex
        {
            MN_ESP_ESPMODE = 0,
            MN_ESP_VISIBLES_ONLY = 1,
            MN_ESP_ALLIES = 2,
            MN_ESP_3D = 3,
            MN_ESP_SPOTLINE = 4,
            MN_AIMBOT_MODE = 5,
            MN_AIM_TARGET = 6,
            MN_AIM_TWO_SEC_RULE = 7,
            MN_AIM_AT_VEHICLE = 8,
            MN_AIM_KEY = 9,
            MN_NO_RECOIL = 10,
            MN_NO_SPREAD = 11,
            MN_ROF_MODE = 12,
            MN_NO_BREATH = 13,
            MN_DOUBLE_BULLETS = 14,
            MN_HC_MODE = 15,
            MN_UNLOCK_ATTACHMENTS = 16,
            MN_SPECTATOR_WARN = 17
            //MN_NO_GRAVITY
            //MN_SUPER_BULLET
            //MN_AIM_SMOOTH

        };
        private mnIndex currMnIndex = mnIndex.MN_ESP_ESPMODE;
        private int LastMenuIndex = Enum.GetNames(typeof(mnIndex)).Length - 1;

        private enum mnRateOfFire
        {
            DEFAULT,
            PERFIVE,
            PERTHREE,
            PERTWO,
            DOUBLE
        };
        private mnRateOfFire currMnRoF = mnRateOfFire.DEFAULT;

        private enum mnEspMode
        {
            NONE,
            MINIMAL,
            PARTIAL,
            FULL
        };
        private mnEspMode currMnEspMode = mnEspMode.FULL;

        private enum mnHardCoreMode
        {
            OFF,
            LEFT,
            RIGHT
        };
        private mnHardCoreMode currMnHC = mnHardCoreMode.OFF;

        private enum mnAimClosestTo
        {
            OFF = 0,
            PLAYER = 1,
            CROSSHAIR = 2
        };
        mnAimClosestTo currMnAimMode = mnAimClosestTo.OFF;

        private enum mnAimTarget
        {
            HEAD = 0,
            NECK = 1,
            BODY = 2
            //,CYCLIC = 3
        }
        mnAimTarget currMnAimTarget = mnAimTarget.NECK;

        private void CycleMenuDown()
        {
            if (bMenuControl)
                currMnIndex = (mnIndex)((int)currMnIndex >= LastMenuIndex ? 0 : (int)currMnIndex + 1);
        }

        private void CycleMenuUp()
        {
            if (bMenuControl)
                currMnIndex = (mnIndex)((int)currMnIndex <= 0 ? LastMenuIndex : (int)currMnIndex - 1);
        }

        private void SelectMenuItem()
        {
            switch (currMnIndex)
            {
                case mnIndex.MN_ESP_ESPMODE:
                    switch (currMnEspMode)
                    {
                        case mnEspMode.NONE:
                            currMnEspMode = mnEspMode.MINIMAL;
                            ESP_Box = true;
                            ESP_Bone = false;
                            ESP_Name = false;
                            ESP_Health = false;
                            ESP_Distance = false;
                            ESP_Vehicle = false;
                            break;
                        case mnEspMode.MINIMAL:
                            currMnEspMode = mnEspMode.PARTIAL;
                            ESP_Box = true;
                            ESP_Bone = true;
                            ESP_Name = false;
                            ESP_Health = false;
                            ESP_Distance = false;
                            ESP_Vehicle = true;
                            break;
                        case mnEspMode.PARTIAL:
                            currMnEspMode = mnEspMode.FULL;
                            ESP_Box = true;
                            ESP_Bone = true;
                            ESP_Name = true;
                            ESP_Health = true;
                            ESP_Distance = true;
                            ESP_Vehicle = true;
                            break;
                        case mnEspMode.FULL:
                            currMnEspMode = mnEspMode.NONE;
                            ESP_Box = false;
                            ESP_Bone = false;
                            ESP_Name = false;
                            ESP_Health = false;
                            ESP_Distance = false;
                            ESP_Vehicle = false;
                            break;
                    }
                    break;
                case mnIndex.MN_ESP_VISIBLES_ONLY:
                    bEspVisiblesOnly = !bEspVisiblesOnly;
                    break;
                case mnIndex.MN_ESP_ALLIES:
                    bEspAllies = !bEspAllies;
                    break;
                case mnIndex.MN_ESP_3D:
                    bEsp3D = !bEsp3D;
                    break;
                case mnIndex.MN_ESP_SPOTLINE:
                    bEspSpotline = !bEspSpotline;
                    break;
                case mnIndex.MN_AIMBOT_MODE:
                    switch (currMnAimMode)
                    {
                        case mnAimClosestTo.OFF:
                            currMnAimMode = mnAimClosestTo.PLAYER;
                            break;
                        case mnAimClosestTo.PLAYER:
                            currMnAimMode = mnAimClosestTo.CROSSHAIR;
                            break;
                        case mnAimClosestTo.CROSSHAIR:
                            currMnAimMode = mnAimClosestTo.OFF;
                            break;
                    }
                    bAimbot = !(currMnAimMode == mnAimClosestTo.OFF);
                    break;
                case mnIndex.MN_AIM_TARGET:
                    switch (currMnAimTarget)
                    {
                        case mnAimTarget.HEAD:
                            currMnAimTarget = mnAimTarget.NECK;
                            break;
                        case mnAimTarget.NECK:
                            currMnAimTarget = mnAimTarget.BODY;
                            break;
                        case mnAimTarget.BODY:
                            currMnAimTarget = mnAimTarget.HEAD;
                            break;
                    }
                    break;
                case mnIndex.MN_AIM_KEY:
                    bAimKeyDefault = !bAimKeyDefault;
                    break;
                case mnIndex.MN_AIM_TWO_SEC_RULE:
                    bAimTwoSecondsRule = !bAimTwoSecondsRule;
                    break;
                case mnIndex.MN_AIM_AT_VEHICLE:
                    bAimAtVehicles = !bAimAtVehicles;
                    break;
                case mnIndex.MN_NO_RECOIL:
                    bRipOfRecoil = !bRipOfRecoil;
                    break;
                case mnIndex.MN_NO_SPREAD:
                    bNoSpread = !bNoSpread;
                    break;
                //case mnIndex.MN_NO_GRAVITY:
                //    bNoGravity = !bNoGravity;
                //    break;
                //case mnIndex.MN_SUPER_BULLET:
                //    bSuperBullet = !bSuperBullet;
                //    break;
                case mnIndex.MN_DOUBLE_BULLETS:
                    bDoubleBullets = !bDoubleBullets;
                    break;
                case mnIndex.MN_ROF_MODE:
                    mnRateOfFire cacheMnRoF = currMnRoF;
                    switch (currMnRoF)
                    {
                        case mnRateOfFire.DEFAULT:
                            currMnRoF = mnRateOfFire.PERFIVE;
                            break;
                        case mnRateOfFire.PERFIVE:
                            currMnRoF = mnRateOfFire.PERTHREE;
                            break;
                        case mnRateOfFire.PERTHREE:
                            currMnRoF = mnRateOfFire.PERTWO;
                            break;
                        case mnRateOfFire.PERTWO:
                            currMnRoF = mnRateOfFire.DOUBLE;
                            break;
                        case mnRateOfFire.DOUBLE:
                            currMnRoF = mnRateOfFire.DEFAULT;
                            break;
                    }
                    bRateOfFire = !(currMnRoF == mnRateOfFire.DEFAULT);
                    break;
                case mnIndex.MN_NO_BREATH:
                    bNoBreath = !bNoBreath;
                    break;
                case mnIndex.MN_HC_MODE:
                    switch (currMnHC)
                    {
                        case mnHardCoreMode.OFF:
                            currMnHC = mnHardCoreMode.LEFT;
                            break;
                        case mnHardCoreMode.LEFT:
                            currMnHC = mnHardCoreMode.RIGHT;
                            break;
                        case mnHardCoreMode.RIGHT:
                            currMnHC = mnHardCoreMode.OFF;
                            break;
                    }
                    bHardcoreMode = !(currMnHC == mnHardCoreMode.OFF);
                    bCrosshairHUD = bHardcoreMode;
                    bRadarHUD = bHardcoreMode;
                    bAmmoHealthHUD = bHardcoreMode;
                    break;
                case mnIndex.MN_UNLOCK_ATTACHMENTS:
                    bUnlockAttachments = !bUnlockAttachments;
                    if (!UnlockAttachmentsControl())
                        bUnlockAttachments = !bUnlockAttachments;
                    break;
                case mnIndex.MN_SPECTATOR_WARN:
                    bSpectatorWarn = !bSpectatorWarn;
                    break;
            }
        }

        private string GetMenuString(mnIndex idx)
        {
            string result = "";

            switch (idx)
            {
                case mnIndex.MN_ESP_ESPMODE:
                    result = "Nhìn Xuyên Tường : " + ((currMnEspMode == mnEspMode.NONE) ? "[ Tắt ]" : ((currMnEspMode == mnEspMode.MINIMAL) ? "[ MINIMAL ]" : ((currMnEspMode == mnEspMode.PARTIAL) ? "[ PARTIAL ]" : "[ FULL ]")));
                    break;
                case mnIndex.MN_ESP_VISIBLES_ONLY:
                    result = "Nhìn Xuyên Tường 2 : " + (((currMnEspMode != mnEspMode.NONE) && bEspVisiblesOnly) ? "[ Mở ]" : "[ Tắt ]");
                    break;
                case mnIndex.MN_ESP_ALLIES:
                    result = "Nhìn Xuyên Tường Đồng đội : " + (((currMnEspMode != mnEspMode.NONE) && bEspAllies) ? "[ Mở ]" : "[ Tắt ]");
                    break;
                case mnIndex.MN_ESP_3D:
                    result = "Nhìn Xuyên Tường 2D/3D : " + ((currMnEspMode == mnEspMode.NONE) ? "[ Tắt ]" : (bEsp3D) ? "[ 3D ]" : "[ 2D ]");
                    break;
                case mnIndex.MN_ESP_SPOTLINE:
                    result = "Nhìn Xuyên Tường ( Đường kẻ) : " + (bEspSpotline ? "[ Mở ]" : "[ Tắt ]");
                    break;
                case mnIndex.MN_AIMBOT_MODE:
                    result = "Tự Động Ngắm : " + (currMnAimMode == mnAimClosestTo.PLAYER ? "[ Thằng Gần Nhất ]" : currMnAimMode == mnAimClosestTo.CROSSHAIR ? "[ Thằng Gần Tâm Ngắm ]" : "[ Tắt ]");
                    break;
                case mnIndex.MN_AIM_TARGET:
                    result = "AIM TARGET " + (currMnAimTarget == mnAimTarget.HEAD ? "[ Đầu ]" : currMnAimTarget == mnAimTarget.NECK ? "[ Cổ ]" : /*currMnAimTarget == mnAimTarget.BODY ?*/ "[ BODY ]" /*: "[ CYCLIC ]"*/);
                    break;
                case mnIndex.MN_AIM_TWO_SEC_RULE:
                    result = "AIM TWO SECONDS RULE " + (bAimTwoSecondsRule ? "[ Mở ]" : "[ Tắt ]");
                    break;
                case mnIndex.MN_AIM_AT_VEHICLE:
                    result = "AIM AT VEHICLES " + (bAimAtVehicles ? "[ Mở ]" : "[ Tắt ]");
                    break;
                case mnIndex.MN_AIM_KEY:
                    result = "AIM KEY " + (bAimKeyDefault ? "[ Shift trái ]" : "[ Chuột Phải ]");
                    break;
                case mnIndex.MN_NO_RECOIL:
                    result = "NO RECOIL : " + (bRipOfRecoil ? "[ Mở ]" : "[ Tắt ]");
                    break;
                case mnIndex.MN_NO_SPREAD:
                    result = "NO SPREAD : " + (bNoSpread ? "[ Mở ]" : "[ Tắt ]");
                    break;
                case mnIndex.MN_ROF_MODE:
                    result = "FIRERATE MODE : " + (currMnRoF == mnRateOfFire.DEFAULT ? "[ NORMAL ]" : currMnRoF == mnRateOfFire.PERFIVE ? "[ +1/5 ]" : currMnRoF == mnRateOfFire.PERTHREE ? "[ +1/3 ]" : currMnRoF == mnRateOfFire.PERTWO ? "[ +1/2 ]" : "[ DOUBLE ]");
                    break;
                case mnIndex.MN_NO_BREATH:
                    result = "NO BREATH : " + (bNoBreath ? "[ Mở ]" : "[ Tắt ]");
                    break;
                //case mnIndex.MN_NO_GRAVITY:
                //    result = "NO GRAVITY : " + (bNoGravity ? "[ ON ]" : "[ OFF ]");
                //    break;
                //case mnIndex.MN_SUPER_BULLET:
                //    result = "SUPER BULLET : " + (bSuperBullet ? "[ ON ]" : "[ OFF ]");
                //    break;
                case mnIndex.MN_DOUBLE_BULLETS:
                    result = "DOUBLE BULLETS : " + (bDoubleBullets ? "[ Mở ]" : "[ Tắt ]");
                    break;                    
                case mnIndex.MN_HC_MODE:
                    result = "ANTI HARDCORE HUD" + (currMnHC == mnHardCoreMode.OFF ? "[ Tắt ]" : currMnHC == mnHardCoreMode.LEFT ? "[ LEFT ]" : "[ RIGHT ]");
                    break;
                case mnIndex.MN_UNLOCK_ATTACHMENTS:
                    result = "UNLOCK ATTACHMENTS: " + (bUnlockAttachments ? "[ Mở ]" : "[ Tắt ]");
                    break;
                case mnIndex.MN_SPECTATOR_WARN:
                    result = "SPECTATOR WARNING: " + (bSpectatorWarn ? "[ Mở ]" : "[ Tắt ]");
                    break;
            }

            return result;
        }

        #endregion

        #region World to Screen
        private bool WorldToScreen(Vector3 _Enemy, int _Pose, out Vector3 _Screen)
        {
            _Screen = new Vector3(0, 0, 0);
            float HeadHeight = _Enemy.Y;

            #region HeadHeight
            if (_Pose == 0)
            {
                HeadHeight += 1.7f;
            }
            if (_Pose == 1)
            {
                HeadHeight += 1.15f;
            }
            if (_Pose == 2)
            {
                HeadHeight += 0.4f;
            }
            #endregion

            float ScreenW = (localPlayer.ViewProj.M14 * _Enemy.X) + (localPlayer.ViewProj.M24 * HeadHeight) + (localPlayer.ViewProj.M34 * _Enemy.Z + localPlayer.ViewProj.M44);

            if (ScreenW < 0.0001f)
                return false;

            float ScreenX = (localPlayer.ViewProj.M11 * _Enemy.X) + (localPlayer.ViewProj.M21 * HeadHeight) + (localPlayer.ViewProj.M31 * _Enemy.Z + localPlayer.ViewProj.M41);
            float ScreenY = (localPlayer.ViewProj.M12 * _Enemy.X) + (localPlayer.ViewProj.M22 * HeadHeight) + (localPlayer.ViewProj.M32 * _Enemy.Z + localPlayer.ViewProj.M42);

            _Screen.X = (rect.Width / 2) + (rect.Width / 2) * ScreenX / ScreenW;
            _Screen.Y = (rect.Height / 2) - (rect.Height / 2) * ScreenY / ScreenW;
            _Screen.Z = ScreenW;
            return true;
        }

        private bool WorldToScreen(Vector3 _Enemy, out Vector3 _Screen)
        {
            _Screen = new Vector3(0, 0, 0);
            float ScreenW = (localPlayer.ViewProj.M14 * _Enemy.X) + (localPlayer.ViewProj.M24 * _Enemy.Y) + (localPlayer.ViewProj.M34 * _Enemy.Z + localPlayer.ViewProj.M44);

            if (ScreenW < 0.0001f)
                return false;

            float ScreenX = (localPlayer.ViewProj.M11 * _Enemy.X) + (localPlayer.ViewProj.M21 * _Enemy.Y) + (localPlayer.ViewProj.M31 * _Enemy.Z + localPlayer.ViewProj.M41);
            float ScreenY = (localPlayer.ViewProj.M12 * _Enemy.X) + (localPlayer.ViewProj.M22 * _Enemy.Y) + (localPlayer.ViewProj.M32 * _Enemy.Z + localPlayer.ViewProj.M42);

            _Screen.X = (rect.Width / 2) + (rect.Width / 2) * ScreenX / ScreenW;
            _Screen.Y = (rect.Height / 2) - (rect.Height / 2) * ScreenY / ScreenW;
            _Screen.Z = ScreenW;
            return true;
        }
        #endregion

        #region Draw Stuff

        #region Draw - Variables

        // Color
        private Color enemyColor = new Color(255, 0, 0, 200),
            enemyColorVisible = new Color(255, 255, 0, 220),
            enemyColorVehicle = new Color(255, 129, 72, 200),
            enemySkeletonColor = new Color(245, 114, 0, 255),
            friendlyColor = new Color(0, 255, 0, 200),
            friendlyColorVehicle = new Color(64, 154, 200, 255),
            friendSkeletonColor = new Color(46, 228, 213, 255);

        // ESP OPTIONS
        private bool ESP_Box = true,
            ESP_Bone = true,
            ESP_Name = true,
            ESP_Health = true,
            ESP_Distance = true,
            ESP_Vehicle = true;

        // SharpDX
        private WindowRenderTarget device;
        private HwndRenderTargetProperties renderProperties;
        private SolidColorBrush solidColorBrush;
        private Factory factory;
        private bool IsResize = false;
        private bool IsMinimized = false;

        // SharpDX Font
        private TextFormat font, fontSmall;
        private FontFactory fontFactory;
        private const string fontFamily = "Calibri";
        private const float fontSizeLarge = 20.0f;
        private const float fontSize = 18.0f;
        private const float fontSizeSmall = 14.0f;

        #endregion

            #region Draw - Info

        private void DrawShotAccuracy(int X, int Y)
        {
            float accuracy = localPlayer.GetShotsAccuracy();
            Color color = Color.WhiteSmoke;
            if (accuracy >= 50.0f)
                color = Color.Yellow;
            else if (accuracy >= 25.0f)
                color = new Color(255, 214, 0, 255);

            DrawTextCenter(rect.Width / 2 - 125, 5, 250, (int)font.FontSize, "Chính xác : " + accuracy.ToString() + "%", color, true);
        }

        private void DrawShortcutMenu(int X, int Y)
        {
            Color selectedColor = new Color(255, 214, 0, 255);
            DrawText(X, Y, "F9: Nín thở", bNoBreath ? selectedColor : Color.White, true, fontSmall);
            DrawText(X + 100, Y, "F10: Không giật", bRipOfRecoil && bNoSpread ? selectedColor : Color.White, true, fontSmall);
            switch (currMnAimTarget)
            {
                case mnAimTarget.HEAD:
                    DrawText(X + 200, Y, "F11: AIM : [ Đầu ]", currMnAimMode == mnAimClosestTo.OFF ? Color.White : selectedColor, true, fontSmall);
                    break;
                case mnAimTarget.NECK:
                    DrawText(X + 200, Y, "F11: AIM : [ Cổ ]", currMnAimMode == mnAimClosestTo.OFF ? Color.White : selectedColor, true, fontSmall);
                    break;
                case mnAimTarget.BODY:
                    DrawText(X + 200, Y, "F11: AIM : [ Thân ]", currMnAimMode == mnAimClosestTo.OFF ? Color.White : selectedColor, true, fontSmall);
                    break;                    
            }
            switch (currMnAimMode)
            {
                case mnAimClosestTo.OFF:
                    DrawText(X + 300, Y, "F12: AIMBOT MODE : [ Tắt ]", Color.White, true, fontSmall);
                    break;
                case mnAimClosestTo.PLAYER:
                    DrawText(X + 300, Y, "F12: AIMBOT MODE : [ Aim thằng gần ]", selectedColor, true, fontSmall);
                    break;
                case mnAimClosestTo.CROSSHAIR:
                    DrawText(X + 300, Y, "F12: AIMBOT MODE : [ Aim theo tâm nhắm ]", selectedColor, true, fontSmall);
                    break;
            }
            DrawText(base.Width - 120, base.Height-50, "PlefVersion v1.1",Color.White, true, fontSmall);
            DrawText(base.Width - 220, Y, "Modify by Khải", bMenuControl ? selectedColor : Color.Red, true, fontSmall);

            if (bMenuControl)
            {
                DrawText(base.Width - 220, Y + 20, "UP/DOWN: Điều Hướng", Color.White, true, fontSmall);
                DrawText(base.Width - 220, Y + 40, "RIGHT: Chọn", Color.White, true, fontSmall);
                DrawText(base.Width - 220, Y + 60, "PAGE UP: Tối Ưu setting", Color.White, true, fontSmall);
                DrawText(base.Width - 220, Y + 80, "PAGE DOWN: Về Ban Đầu", Color.White, true, fontSmall);
                DrawText(base.Width - 220, Y + 100, "DELETE: Thoát", Color.White, true, fontSmall);
            }
        }

        private void DrawMenu(int x, int y)
        {
            DrawFillRect(x - 5, y - 22, 260, 405, new Color(11, 11, 11, 150));
            DrawTextCenter(x + 10, y - 15, 195, 20, AppTitle, Color.Red, true);

            foreach (mnIndex MnIdx in Enum.GetValues(typeof(mnIndex)))
            {
                Color color = Color.WhiteSmoke;
                if (currMnIndex == MnIdx)
                    color = new Color(255, 214, 0, 255);

                DrawText(x, y = y + 20, GetMenuString(MnIdx), color, true, fontSmall);
            }
        }

        private void DrawSpectatorWarn(int X, int Y, int W, int H)
        {
            RoundedRectangle rect = new RoundedRectangle();
            rect.RadiusX = 4;
            rect.RadiusY = 4;
            rect.Rect = new RectangleF(X, Y, W, H);

            solidColorBrush.Color = new Color(196, 26, 31, 210);
            device.FillRoundedRectangle(ref rect, solidColorBrush);

            DrawText(X + 20, Y + 5, "###Có người đang trong chế độ theo dõi###", Color.White, true);
        }

        private void DrawProximityAlert(int X, int Y, int W, int H)
        {
            RoundedRectangle rect = new RoundedRectangle();
            rect.RadiusX = 4;
            rect.RadiusY = 4;
            rect.Rect = new RectangleF(X, Y, W, H);

            solidColorBrush.Color = new Color(196, 26, 31, 210);
            device.FillRoundedRectangle(ref rect, solidColorBrush);

            DrawText(X + 12 - (int)(font.FontSize / 2), Y + 15, "## ENEMY CLOSE ##", Color.White);
        }
            
            #endregion

            #region Draw - ESP

        private void DrawBone(GPlayer player)
        {
            Vector3 BONE_HEAD,
            BONE_NECK,
            BONE_SPINE2,
            BONE_SPINE1,
            BONE_SPINE,
            BONE_LEFTSHOULDER,
            BONE_RIGHTSHOULDER,
            BONE_LEFTELBOWROLL,
            BONE_RIGHTELBOWROLL,
            BONE_LEFTHAND,
            BONE_RIGHTHAND,
            BONE_LEFTKNEEROLL,
            BONE_RIGHTKNEEROLL,
            BONE_LEFTFOOT,
            BONE_RIGHTFOOT;

            if (WorldToScreen(player.Bone.BONE_HEAD, out BONE_HEAD) &&
            WorldToScreen(player.Bone.BONE_NECK, out BONE_NECK) &&
            WorldToScreen(player.Bone.BONE_SPINE2, out BONE_SPINE2) &&
            WorldToScreen(player.Bone.BONE_SPINE1, out BONE_SPINE1) &&
            WorldToScreen(player.Bone.BONE_SPINE, out BONE_SPINE) &&
            WorldToScreen(player.Bone.BONE_LEFTSHOULDER, out BONE_LEFTSHOULDER) &&
            WorldToScreen(player.Bone.BONE_RIGHTSHOULDER, out BONE_RIGHTSHOULDER) &&
            WorldToScreen(player.Bone.BONE_LEFTELBOWROLL, out BONE_LEFTELBOWROLL) &&
            WorldToScreen(player.Bone.BONE_RIGHTELBOWROLL, out BONE_RIGHTELBOWROLL) &&
            WorldToScreen(player.Bone.BONE_LEFTHAND, out BONE_LEFTHAND) &&
            WorldToScreen(player.Bone.BONE_RIGHTHAND, out BONE_RIGHTHAND) &&
            WorldToScreen(player.Bone.BONE_LEFTKNEEROLL, out BONE_LEFTKNEEROLL) &&
            WorldToScreen(player.Bone.BONE_RIGHTKNEEROLL, out BONE_RIGHTKNEEROLL) &&
            WorldToScreen(player.Bone.BONE_LEFTFOOT, out BONE_LEFTFOOT) &&
            WorldToScreen(player.Bone.BONE_RIGHTFOOT, out BONE_RIGHTFOOT))
            {
                int stroke = 3;
                int strokeW = stroke % 2 == 0 ? stroke / 2 : (stroke - 1) / 2;

                // Color
                Color skeletonColor = player.Team == localPlayer.Team ? friendSkeletonColor : enemySkeletonColor;

                // RECT's
                DrawFillRect((int)BONE_HEAD.X - strokeW, (int)BONE_HEAD.Y - strokeW, stroke, stroke, skeletonColor);
                DrawFillRect((int)BONE_NECK.X - strokeW, (int)BONE_NECK.Y - strokeW, stroke, stroke, skeletonColor);
                DrawFillRect((int)BONE_LEFTSHOULDER.X - strokeW, (int)BONE_LEFTSHOULDER.Y - strokeW, stroke, stroke, skeletonColor);
                DrawFillRect((int)BONE_LEFTELBOWROLL.X - strokeW, (int)BONE_LEFTELBOWROLL.Y - strokeW, stroke, stroke, skeletonColor);
                DrawFillRect((int)BONE_LEFTHAND.X - strokeW, (int)BONE_LEFTHAND.Y - strokeW, stroke, stroke, skeletonColor);
                DrawFillRect((int)BONE_RIGHTSHOULDER.X - strokeW, (int)BONE_RIGHTSHOULDER.Y - strokeW, stroke, stroke, skeletonColor);
                DrawFillRect((int)BONE_RIGHTELBOWROLL.X - strokeW, (int)BONE_RIGHTELBOWROLL.Y - strokeW, stroke, stroke, skeletonColor);
                DrawFillRect((int)BONE_RIGHTHAND.X - strokeW, (int)BONE_RIGHTHAND.Y - strokeW, stroke, stroke, skeletonColor);
                DrawFillRect((int)BONE_SPINE2.X - strokeW, (int)BONE_SPINE2.Y - strokeW, stroke, stroke, skeletonColor);
                DrawFillRect((int)BONE_SPINE1.X - strokeW, (int)BONE_SPINE1.Y - strokeW, stroke, stroke, skeletonColor);
                DrawFillRect((int)BONE_SPINE.X - strokeW, (int)BONE_SPINE.Y - strokeW, stroke, stroke, skeletonColor);
                DrawFillRect((int)BONE_LEFTKNEEROLL.X - strokeW, (int)BONE_LEFTKNEEROLL.Y - strokeW, stroke, stroke, skeletonColor);
                DrawFillRect((int)BONE_RIGHTKNEEROLL.X - strokeW, (int)BONE_RIGHTKNEEROLL.Y - strokeW, 2, 2, skeletonColor);
                DrawFillRect((int)BONE_LEFTFOOT.X - strokeW, (int)BONE_LEFTFOOT.Y - strokeW, 2, 2, skeletonColor);
                DrawFillRect((int)BONE_RIGHTFOOT.X - strokeW, (int)BONE_RIGHTFOOT.Y - strokeW, 2, 2, skeletonColor);

                // Head -> Neck
                DrawLine((int)BONE_HEAD.X, (int)BONE_HEAD.Y, (int)BONE_NECK.X, (int)BONE_NECK.Y, skeletonColor);

                // Neck -> Left
                DrawLine((int)BONE_NECK.X, (int)BONE_NECK.Y, (int)BONE_LEFTSHOULDER.X, (int)BONE_LEFTSHOULDER.Y, skeletonColor);
                DrawLine((int)BONE_LEFTSHOULDER.X, (int)BONE_LEFTSHOULDER.Y, (int)BONE_LEFTELBOWROLL.X, (int)BONE_LEFTELBOWROLL.Y, skeletonColor);
                DrawLine((int)BONE_LEFTELBOWROLL.X, (int)BONE_LEFTELBOWROLL.Y, (int)BONE_LEFTHAND.X, (int)BONE_LEFTHAND.Y, skeletonColor);

                // Neck -> Right
                DrawLine((int)BONE_NECK.X, (int)BONE_NECK.Y, (int)BONE_RIGHTSHOULDER.X, (int)BONE_RIGHTSHOULDER.Y, skeletonColor);
                DrawLine((int)BONE_RIGHTSHOULDER.X, (int)BONE_RIGHTSHOULDER.Y, (int)BONE_RIGHTELBOWROLL.X, (int)BONE_RIGHTELBOWROLL.Y, skeletonColor);
                DrawLine((int)BONE_RIGHTELBOWROLL.X, (int)BONE_RIGHTELBOWROLL.Y, (int)BONE_RIGHTHAND.X, (int)BONE_RIGHTHAND.Y, skeletonColor);

                // Neck -> Center
                DrawLine((int)BONE_NECK.X, (int)BONE_NECK.Y, (int)BONE_SPINE2.X, (int)BONE_SPINE2.Y, skeletonColor);
                DrawLine((int)BONE_SPINE2.X, (int)BONE_SPINE2.Y, (int)BONE_SPINE1.X, (int)BONE_SPINE1.Y, skeletonColor);
                DrawLine((int)BONE_SPINE1.X, (int)BONE_SPINE1.Y, (int)BONE_SPINE.X, (int)BONE_SPINE.Y, skeletonColor);

                // Spine -> Left
                DrawLine((int)BONE_SPINE.X, (int)BONE_SPINE.Y, (int)BONE_LEFTKNEEROLL.X, (int)BONE_LEFTKNEEROLL.Y, skeletonColor);
                DrawLine((int)BONE_LEFTKNEEROLL.X, (int)BONE_LEFTKNEEROLL.Y, (int)BONE_LEFTFOOT.X, (int)BONE_LEFTFOOT.Y, skeletonColor);

                // Spine -> Right
                DrawLine((int)BONE_SPINE.X, (int)BONE_SPINE.Y, (int)BONE_RIGHTKNEEROLL.X, (int)BONE_RIGHTKNEEROLL.Y, skeletonColor);
                DrawLine((int)BONE_RIGHTKNEEROLL.X, (int)BONE_RIGHTKNEEROLL.Y, (int)BONE_RIGHTFOOT.X, (int)BONE_RIGHTFOOT.Y, skeletonColor);
            }
        }

        private void DrawHealth(int X, int Y, int W, int H, int Health, int MaxHealth)
        {
            if (Health <= 0)
                Health = 1;

            if (MaxHealth < Health)
                MaxHealth = 100;

            int progress = (int)((float)Health / ((float)MaxHealth / 100));
            int w = (int)((float)W / 100 * progress);

            if (w <= 2)
                w = 3;

            Color color = new Color(255, 0, 0, 255);
            if (progress >= 20) color = new Color(255, 165, 0, 255);
            if (progress >= 40) color = new Color(255, 255, 0, 255);
            if (progress >= 60) color = new Color(173, 255, 47, 255);
            if (progress >= 80) color = new Color(0, 255, 0, 255);

            DrawFillRect(X, Y - 1, W + 1, H + 2, Color.Black);
            DrawFillRect(X + 1, Y, w - 1, H, color);
        }

        private void DrawProgress(int X, int Y, int W, int H, int Value, int MaxValue)
        {
            int progress = (int)((float)Value / ((float)MaxValue / 100));
            int w = (int)((float)W / 100 * progress);

            Color color = new Color(0, 255, 0, 255);
            if (progress >= 20) color = new Color(173, 255, 47, 255);
            if (progress >= 40) color = new Color(255, 255, 0, 255);
            if (progress >= 60) color = new Color(255, 165, 0, 255);
            if (progress >= 80) color = new Color(255, 0, 0, 255);

            DrawFillRect(X, Y - 1, W + 1, H + 2, Color.Black);
            if (w >= 2)
            {
                DrawFillRect(X + 1, Y, w - 1, H, color);
            }
        }

        private void DrawSpotline(int X, int Y, bool playerIsVisible)
        {
            solidColorBrush.Color = playerIsVisible ? Color.Red : Color.Yellow;
            device.DrawLine(new Vector2((float)(MultiWidth / 2), (float)MultiHeight), new Vector2((float)(X), (float)Y), solidColorBrush);
        }
            
            #endregion

            #region Draw - Local HUD

                #region HUD: Draw Radar
        public void DrawRadarHUD(int xAxis, int yAxis, int width, int height)
        {
            Rectangle rectangle = new Rectangle()
            {
                X = xAxis - 1,
                Y = yAxis - 1,
                Width = width + 1,
                Height = height + 1
            };
            solidColorBrush.Color = new Color(11, 11, 11, 150); // Color();
            device.DrawRectangle(rectangle, this.solidColorBrush);
            DrawRect(xAxis, yAxis, width, height, new Color(11, 11, 11, 120));
            //DrawCrosshairHUD(xAxis + height / 2, yAxis + width / 2, width, height, new Color(169, 169, 169, 120));
            float Y_Axis = localPlayer.FoV.Y;
            Y_Axis = Y_Axis / 1.34f;
            Y_Axis = Y_Axis - 1.57079637f;
            int xis = xAxis + width / 2;
            int ypisilum = yAxis + height / 2;
            int pClientPlayerOwner = (int)Math.Sqrt((double)((xis - xis) * (xis - xis) + (ypisilum - ypisilum - width / 2) * (ypisilum - ypisilum - height / 2)));
            int GameContext = (int)((float)pClientPlayerOwner * (float)Math.Cos((double)Y_Axis) + (float)xis);
            int pManager = (int)((float)pClientPlayerOwner * (float)Math.Sin((double)Y_Axis) + (float)ypisilum);
            Y_Axis = Y_Axis + 3.14159274f;
            int lClient = (int)((float)pClientPlayerOwner * (float)Math.Cos((double)(-Y_Axis)) + (float)xis);
            int lSoldier = (int)((float)pClientPlayerOwner * (float)Math.Sin((double)(-Y_Axis)) + (float)ypisilum);
            DrawLine(xis, ypisilum, GameContext, pManager, new Color(247, 244, 9, 120), 1f);
            DrawLine(xis, ypisilum, lClient, lSoldier, new Color(247, 244, 9, 120), 1f);
            DrawRadarHUD(100, 100, xAxis + width / 2, yAxis + height / 2, new Color(169, 169, 169, 120));
            DrawRadarHUD(70, 70, xAxis + width / 2, yAxis + height / 2, new Color(169, 169, 169, 120));
            DrawRadarHUD(40, 40, xAxis + width / 2, yAxis + height / 2, new Color(169, 169, 169, 120));
            DrawRadarHUD(2, 2, xAxis + width / 2, yAxis + height / 2, new Color(247, 244, 9, 120));
            foreach (GPlayer player in players)
            {
                if (player.IsValid())
                {
                    float zs = localPlayer.Origin.Z - player.Origin.Z;
                    float xs = localPlayer.Origin.X - player.Origin.X;
                    double Yaw = -(double)localPlayer.Yaw;
                    float single = xs * (float)Math.Cos(Yaw) - zs * (float)Math.Sin(Yaw);
                    float ypisilum1 = xs * (float)Math.Sin(Yaw) + zs * (float)Math.Cos(Yaw);
                    single = single * 2f;
                    single = single + (float)(xAxis + width / 2);
                    ypisilum1 = ypisilum1 * 2f;
                    ypisilum1 = ypisilum1 + (float)(yAxis + height / 2);
                    if (single < (float)xAxis)
                        single = (float)xAxis;
                    if (ypisilum1 < (float)yAxis)
                        ypisilum1 = (float)yAxis;
                    if (single > (float)(xAxis + width - 3))
                        single = (float)(xAxis + width - 3);
                    if (ypisilum1 > (float)(yAxis + height - 3))
                        ypisilum1 = (float)(yAxis + height - 3);
                    if (player.Distance >= 0f && player.Distance < 500f)
                    {
                        Color white = Color.White;
                        if (player.Team == localPlayer.Team)
                            white = Color.SkyBlue;
                        else if (!player.InVehicle)
                            white = (player.IsVisible() ? Color.Red : Color.LimeGreen);
                        else
                            white = Color.Orange;
                        DrawRect((int)single, (int)ypisilum1, 3, 3, white);
                    }
                }
            }
        }
                #endregion

                #region HUD: Draw Radar Circle
        private void DrawRadarHUD(int RadiusX, int RadiusY, int X, int Y, Color color)
        {
            Ellipse X_Axis = new Ellipse()
            {
                RadiusX = (float)RadiusX,
                RadiusY = (float)RadiusY
            };
            X_Axis.Point.X = (float)X;
            X_Axis.Point.Y = (float)Y;
            solidColorBrush.Color = color;
            device.DrawEllipse(X_Axis, this.solidColorBrush);
        }
                #endregion

                #region HUD: Draw Ammo and Health
        public void DrawAmmoHealthHUD(int X_Axis, int Y_Axis, int width, int height)
        {
            Rectangle rectangle = new Rectangle()
            {
                X = X_Axis - 1,
                Y = Y_Axis - 1,
                Width = width + 1,
                Height = height + 1
            };

            solidColorBrush.Color = new Color();
            device.DrawRectangle(rectangle, this.solidColorBrush);
            DrawRect(X_Axis, Y_Axis, width, height, new Color(11, 11, 11, 120));
            Color yellowGreen = Color.YellowGreen;

            int Health = 0;
            int maxHealth = 100;

            if (localPlayer != null && localPlayer.IsValid())
            {
                Health = (int)localPlayer.Health;
                maxHealth = (int)localPlayer.MaxHealth;

                if (Health < 0 || Health > 100)
                    Health = 1;

                if (maxHealth <= 0 || maxHealth > 100)
                    maxHealth = 100;
            }

            string sAmmo = "000";
            string sAmmoClip = "000";

            if (localPlayer.CurrentWeapon != null && localPlayer.CurrentWeapon.IsValid())
            {
                sAmmo = localPlayer.CurrentWeapon.Ammo < 0 ? "000" : localPlayer.CurrentWeapon.Ammo.ToString();
                sAmmoClip = localPlayer.CurrentWeapon.Ammo < 0 ? "000" : localPlayer.CurrentWeapon.AmmoClip.ToString();
            }

            DrawText(string.Concat(sAmmo, "/", sAmmoClip), X_Axis + 10, Y_Axis, 100, 20, true, false, Color.White);
            if (localPlayer.Health < 55f)
                yellowGreen = new Color(240, 146, 0, 200);
            if (localPlayer.Health < 35f)
                yellowGreen = new Color(0xff, 0, 0, 200);

            string str2 = string.Concat("+", Health.ToString());

            int pClientPlayerOwner;
            int xis = X_Axis;

            if (Health < 100)
                pClientPlayerOwner = (Health < 10 ? 155 : 145);
            else
                pClientPlayerOwner = 135;

            DrawText(str2, xis + pClientPlayerOwner, Y_Axis, 100, 20, true, false, yellowGreen);
        }
                #endregion

                #region HUD: Draw Health Bar
        public void DrawHealthBarHUD(int X, int Y, int W, int H)
        {
            int Health = (int)localPlayer.Health;
            int MaxHealth = (int)localPlayer.MaxHealth;

            if (!(Health > 0))
                Health = 0;
            else if (Health > 100)
                Health = 100;

            if (!(MaxHealth >= Health))
                MaxHealth = 100;
            
            int HP = (int)((float)Health / ((float)MaxHealth / 100f));

            Color color = Color.Red;
            if (HP >= 65)
                color = Color.YellowGreen;
            else if (HP >= 40)
                color = Color.Yellow;
            else if (HP >= 30)
                color = Color.Orange;

            DrawRect(X, Y, W, H, Color.Black);

            int wBar = (int)((float)(W - 2) / 100f * (float)HP);

            wBar = wBar < 0 ? 0 : wBar;

            RoundedRectangle rect = new RoundedRectangle();
            rect.RadiusX = 1;
            rect.RadiusY = 1;
            rect.Rect = new RectangleF(X + 1, Y + 1, wBar, H - 2);

            solidColorBrush.Color = color;
            device.FillRoundedRectangle(ref rect, solidColorBrush);
        }
                #endregion

                #region HUD: Draw Crosshair
        public void DrawCrosshairHUD(int X_Axis, int Ypilisum, int awWidsth, int aHaeighttt, Color color)
        {
            this.solidColorBrush.Color = color;
            this.device.DrawLine(new Vector2((float)(X_Axis - awWidsth / 2), (float)Ypilisum), new Vector2((float)(X_Axis + awWidsth / 2), (float)Ypilisum), this.solidColorBrush);
            this.device.DrawLine(new Vector2((float)X_Axis, (float)(Ypilisum - aHaeighttt / 2)), new Vector2((float)X_Axis, (float)(Ypilisum + aHaeighttt / 2)), this.solidColorBrush);
        }
                #endregion

            #endregion

            #region Draw - 3D In 2D
        public Vector3 Multiply(Vector3 vector, Matrix mat)
        {
            return new Vector3(mat.M11 * vector.X + mat.M21 * vector.Y + mat.M31 * vector.Z,
                                   mat.M12 * vector.X + mat.M22 * vector.Y + mat.M32 * vector.Z,
                                   mat.M13 * vector.X + mat.M23 * vector.Y + mat.M33 * vector.Z);
        }

        private void DrawAABB(AxisAlignedBox aabb, Matrix tranform, Color color)
        {
            Vector3 m_Position = new Vector3(tranform.M41, tranform.M42, tranform.M43);
            Vector3 fld = Multiply(new Vector3(aabb.Min.X, aabb.Min.Y, aabb.Min.Z), tranform) + m_Position;
            Vector3 brt = Multiply(new Vector3(aabb.Max.X, aabb.Max.Y, aabb.Max.Z), tranform) + m_Position;
            Vector3 bld = Multiply(new Vector3(aabb.Min.X, aabb.Min.Y, aabb.Max.Z), tranform) + m_Position;
            Vector3 frt = Multiply(new Vector3(aabb.Max.X, aabb.Max.Y, aabb.Min.Z), tranform) + m_Position;
            Vector3 frd = Multiply(new Vector3(aabb.Max.X, aabb.Min.Y, aabb.Min.Z), tranform) + m_Position;
            Vector3 brb = Multiply(new Vector3(aabb.Max.X, aabb.Min.Y, aabb.Max.Z), tranform) + m_Position;
            Vector3 blt = Multiply(new Vector3(aabb.Min.X, aabb.Max.Y, aabb.Max.Z), tranform) + m_Position;
            Vector3 flt = Multiply(new Vector3(aabb.Min.X, aabb.Max.Y, aabb.Min.Z), tranform) + m_Position;

            #region WorldToScreen
            if (!WorldToScreen(fld, out fld) || !WorldToScreen(brt, out brt)
                || !WorldToScreen(bld, out bld) || !WorldToScreen(frt, out frt)
                || !WorldToScreen(frd, out frd) || !WorldToScreen(brb, out brb)
                || !WorldToScreen(blt, out blt) || !WorldToScreen(flt, out flt))
                return;
            #endregion

            #region DrawLines
            DrawLine(fld, flt, color);
            DrawLine(flt, frt, color);
            DrawLine(frt, frd, color);
            DrawLine(frd, fld, color);
            DrawLine(bld, blt, color);
            DrawLine(blt, brt, color);
            DrawLine(brt, brb, color);
            DrawLine(brb, bld, color);
            DrawLine(fld, bld, color);
            DrawLine(frd, brb, color);
            DrawLine(flt, blt, color);
            DrawLine(frt, brt, color);
            #endregion
        }

        private void DrawAABB(AxisAlignedBox aabb, Vector3 m_Position, float Yaw, Color color)
        {
            float cosY = (float)Math.Cos(Yaw);
            float sinY = (float)Math.Sin(Yaw);

            Vector3 fld = new Vector3(aabb.Min.Z * cosY - aabb.Min.X * sinY, aabb.Min.Y, aabb.Min.X * cosY + aabb.Min.Z * sinY) + m_Position; // 0
            Vector3 brt = new Vector3(aabb.Min.Z * cosY - aabb.Max.X * sinY, aabb.Min.Y, aabb.Max.X * cosY + aabb.Min.Z * sinY) + m_Position; // 1
            Vector3 bld = new Vector3(aabb.Max.Z * cosY - aabb.Max.X * sinY, aabb.Min.Y, aabb.Max.X * cosY + aabb.Max.Z * sinY) + m_Position; // 2
            Vector3 frt = new Vector3(aabb.Max.Z * cosY - aabb.Min.X * sinY, aabb.Min.Y, aabb.Min.X * cosY + aabb.Max.Z * sinY) + m_Position; // 3
            Vector3 frd = new Vector3(aabb.Max.Z * cosY - aabb.Min.X * sinY, aabb.Max.Y, aabb.Min.X * cosY + aabb.Max.Z * sinY) + m_Position; // 4
            Vector3 brb = new Vector3(aabb.Min.Z * cosY - aabb.Min.X * sinY, aabb.Max.Y, aabb.Min.X * cosY + aabb.Min.Z * sinY) + m_Position; // 5
            Vector3 blt = new Vector3(aabb.Min.Z * cosY - aabb.Max.X * sinY, aabb.Max.Y, aabb.Max.X * cosY + aabb.Min.Z * sinY) + m_Position; // 6
            Vector3 flt = new Vector3(aabb.Max.Z * cosY - aabb.Max.X * sinY, aabb.Max.Y, aabb.Max.X * cosY + aabb.Max.Z * sinY) + m_Position; // 7

            #region WorldToScreen
            if (!WorldToScreen(fld, out fld) || !WorldToScreen(brt, out brt)
                || !WorldToScreen(bld, out bld) || !WorldToScreen(frt, out frt)
                || !WorldToScreen(frd, out frd) || !WorldToScreen(brb, out brb)
                || !WorldToScreen(blt, out blt) || !WorldToScreen(flt, out flt))
                return;
            #endregion

            #region DrawLines
            DrawLine(fld, brt, color);
            DrawLine(brb, blt, color);
            DrawLine(fld, brb, color);
            DrawLine(brt, blt, color);

            DrawLine(frt, bld, color);
            DrawLine(frd, flt, color);
            DrawLine(frt, frd, color);
            DrawLine(bld, flt, color);

            DrawLine(frt, fld, color);
            DrawLine(frd, brb, color);
            DrawLine(brt, bld, color);
            DrawLine(blt, flt, color);
            #endregion
        }

            #endregion

            #region Draw - Functions

        private void DrawRect(int X, int Y, int W, int H, Color color)
        {
            solidColorBrush.Color = color;
            device.DrawRectangle(new Rectangle(X, Y, W, H), solidColorBrush);
        }

        private void DrawRect(int X, int Y, int W, int H, Color color, float stroke)
        {
            solidColorBrush.Color = color;
            device.DrawRectangle(new Rectangle(X, Y, W, H), solidColorBrush, stroke);
        }

        private void DrawFillRect(int X, int Y, int W, int H, Color color)
        {
            solidColorBrush.Color = color;
            device.FillRectangle(new RectangleF(X, Y, W, H), solidColorBrush);
        }

        private void DrawText(int X, int Y, string text, Color color)
        {
            solidColorBrush.Color = color;
            device.DrawText(text, font, new RectangleF(X, Y, font.FontSize * text.Length, font.FontSize), solidColorBrush);
        }

        private void DrawText(int X, int Y, string text, Color color, bool outline)
        {
            if (outline)
            {
                solidColorBrush.Color = Color.Black;
                device.DrawText(text, font, new RectangleF(X + 1, Y + 1, font.FontSize * text.Length, font.FontSize), solidColorBrush);
            }

            solidColorBrush.Color = color;
            device.DrawText(text, font, new RectangleF(X, Y, font.FontSize * text.Length, font.FontSize), solidColorBrush);
        }

        private void DrawText(int X, int Y, string text, Color color, bool outline, TextFormat format)
        {
            if (outline)
            {
                solidColorBrush.Color = Color.Black;
                device.DrawText(text, format, new RectangleF(X + 1, Y + 1, format.FontSize * text.Length, format.FontSize), solidColorBrush);
            }

            solidColorBrush.Color = color;
            device.DrawText(text, format, new RectangleF(X, Y, format.FontSize * text.Length, format.FontSize), solidColorBrush);
        }

        private void DrawText(string message, int X, int Y, int Width, int Height, bool largeText, bool center, Color color)
        {
            int x = center ? (int)((float)X - (float)message.Length * 12f / 2f + 0.5f) : X;
            int y = center ? (int)((float)Y + 3f) : Y;

            RectangleF rectangleF = new RectangleF()
            {
                Height = (float)Height,
                Width = (float)Width,
                X = (float)x,
                Y = (float)y
            };

            solidColorBrush.Color = color;
            device.DrawText(message, largeText ? font : fontSmall, rectangleF, solidColorBrush);
        }

        private void DrawTextCenter(int X, int Y, int W, int H, string text, Color color)
        {
            solidColorBrush.Color = color;
            TextLayout layout = new TextLayout(fontFactory, text, fontSmall, W, H);
            layout.TextAlignment = TextAlignment.Center;
            device.DrawTextLayout(new Vector2(X, Y), layout, solidColorBrush);
            layout.Dispose();
        }

        private void DrawTextCenter(int X, int Y, int W, int H, string text, Color color, bool outline)
        {
            TextLayout layout = new TextLayout(fontFactory, text, fontSmall, W, H);
            layout.TextAlignment = TextAlignment.Center;

            if (outline)
            {
                solidColorBrush.Color = Color.Black;
                device.DrawTextLayout(new Vector2(X + 1, Y + 1), layout, solidColorBrush);
            }

            solidColorBrush.Color = color;
            device.DrawTextLayout(new Vector2(X, Y), layout, solidColorBrush);
            layout.Dispose();
        }

        private void DrawLine(int X, int Y, int XX, int YY, Color color)
        {
            solidColorBrush.Color = color;
            device.DrawLine(new Vector2(X, Y), new Vector2(XX, YY), solidColorBrush);
        }

        private void DrawLine(int X, int Y, int Width, int Height, Color color, float lineWidth)
        {
            solidColorBrush.Color = color;
            device.DrawLine(new Vector2((float)X, (float)Y), new Vector2((float)Width, (float)Height), this.solidColorBrush, lineWidth);
        }

        private void DrawLine(Vector3 w2s, Vector3 _w2s, Color color)
        {
            solidColorBrush.Color = color;
            device.DrawLine(new Vector2(w2s.X, w2s.Y), new Vector2(_w2s.X, _w2s.Y), solidColorBrush);
        }

        private void DrawCircle(int X, int Y, int W, Color color)
        {
            solidColorBrush.Color = color;
            device.DrawEllipse(new Ellipse(new Vector2(X, Y), W, W), solidColorBrush);
        }

        private void DrawFillCircle(int X, int Y, int W, Color color)
        {
            solidColorBrush.Color = color;
            device.FillEllipse(new Ellipse(new Vector2(X, Y), W, W), solidColorBrush);
        }

        private void DrawImage(int X, int Y, int W, int H, Bitmap bitmap)
        {
            device.DrawBitmap(bitmap, new RectangleF(X, Y, W, H), 1.0f, BitmapInterpolationMode.Linear);
        }

        private void DrawImage(int X, int Y, int W, int H, Bitmap bitmap, float angle)
        {
            device.Transform = Matrix3x2.Rotation(angle, new Vector2(X + (H / 2), Y + (H / 2)));
            device.DrawBitmap(bitmap, new RectangleF(X, Y, W, H), 1.0f, BitmapInterpolationMode.Linear);
            device.Transform = Matrix3x2.Rotation(0);
        }

        private void DrawSprite(RectangleF destinationRectangle, Bitmap bitmap, RectangleF sourceRectangle)
        {
            device.DrawBitmap(bitmap, destinationRectangle, 1.0f, BitmapInterpolationMode.Linear, sourceRectangle);
        }

        private void DrawSprite(RectangleF destinationRectangle, Bitmap bitmap, RectangleF sourceRectangle, float angle)
        {
            Vector2 center = new Vector2();
            center.X = destinationRectangle.X + destinationRectangle.Width / 2;
            center.Y = destinationRectangle.Y + destinationRectangle.Height / 2;

            device.Transform = Matrix3x2.Rotation(angle, center);
            device.DrawBitmap(bitmap, destinationRectangle, 1.0f, BitmapInterpolationMode.Linear, sourceRectangle);
            device.Transform = Matrix3x2.Rotation(0);
        }

        #endregion

        #endregion

        #region Aimbot Stuff

            #region Aimbot: Key Updates
        private void AimUpdateKeys()
        {
            if (currMnAimMode == mnAimClosestTo.OFF)
                return;

            if ((bAimKeyDefault && IsKeyDown(Manager.VK_CAPSLOCK)) || (!bAimKeyDefault && IsKeyDown(Manager.VK_RIGHTBUTTON)))
            {

                if (localPlayer.CurrentWeapon == null || !localPlayer.CurrentWeapon.IsValid())
                    return;

                if (localPlayer.InVehicle)
                    return;

                if (!(targetEnimies.Count > 0))
                    return;

                GPlayer ClosestEnemy = GetClosestEnimy(targetEnimies, currMnAimMode);
                if (ClosestEnemy.IsValidAimbotTarget(bAimTwoSecondsRule, localPlayer.LastEnemyNameAimed, localPlayer.LastTimeEnimyAimed))
                {
                    VAngle viewAngle = AimLockAtEnemy(ClosestEnemy, currMnAimTarget);

                    if (viewAngle != null)
                    {
                        System.Drawing.PointF newAngle = new System.Drawing.PointF(viewAngle.Yaw, viewAngle.Pitch);
                        System.Drawing.PointF lastAngle = RPM.ReadAngle();

                        if (!CheckPointDifference(lastAngle, newAngle))
                            return;

                        RPM.WriteAngle(viewAngle.Yaw, viewAngle.Pitch);
                        localPlayer.LastEnemyNameAimed = ClosestEnemy.Name;
                        localPlayer.LastTimeEnimyAimed = DateTime.Now;
                    }
                }
            }
        }
            #endregion

            #region Aimbot: Get Closest Enimy
            private GPlayer GetClosestEnimy(List<GPlayer> _Players, mnAimClosestTo _AimMode)
            {
                if (_AimMode == mnAimClosestTo.PLAYER)
                    return _Players.Where(p => p.IsValid()).OrderBy(p => p.Distance).ThenBy(p => p.DistanceToCrosshair).ThenBy(p => p.Name).First();
                else if (_AimMode == mnAimClosestTo.CROSSHAIR)
                    return _Players.Where(p => p.IsValid()).OrderBy(p => p.DistanceToCrosshair).ThenBy(p => p.Distance).ThenBy(p => p.Name).First();

                return null;
            }
            #endregion

            #region Aimbot: Aim At Enemy
        private VAngle AimLockAtEnemy(GPlayer _Enemy, mnAimTarget _LockAt)
        {
            Vector3 LockAt = new Vector3();
            float correciton = 0.0f;
            switch (_LockAt)
            {
                case mnAimTarget.HEAD:
                    LockAt = _Enemy.Bone.BONE_HEAD;
                    //correciton = -0.01f;
                    break;
                case mnAimTarget.NECK:
                    LockAt = _Enemy.Bone.BONE_NECK;
                    correciton = -0.04f; //-0.03
                    break;
                case mnAimTarget.BODY:
                    LockAt = _Enemy.Bone.BONE_SPINE;
                    break;
            }

            if (_Enemy.Distance > 100.0f)
                correciton += localPlayer.CurrentWeapon.BulletInitialPosition.Y;

            if (LockAt.X != 0f && LockAt.Y != 0f && LockAt.Z != 0f)
            {
                Vector3 shootSpace = new Vector3();
                Vector3 Origin = AimCorrection(localPlayer.Velocity, _Enemy.Velocity, LockAt, _Enemy.Distance, localPlayer.CurrentWeapon.BulletSpeed, localPlayer.CurrentWeapon.BulletGravity);

                shootSpace.X = Origin.X - localPlayer.MatrixInverse.M41;
                shootSpace.Y = Origin.Y - localPlayer.MatrixInverse.M42 + correciton;
                shootSpace.Z = Origin.Z - localPlayer.MatrixInverse.M43;
                shootSpace = NormalizesVector(shootSpace);

                VAngle GameAngle = new VAngle();
                GameAngle.Yaw = (float)(-Math.Atan2((double)shootSpace.X, (double)shootSpace.Z));
                GameAngle.Pitch = (float)Math.Atan2((double)shootSpace.Y, Math.Sqrt((double)(shootSpace.X * shootSpace.X + shootSpace.Z * shootSpace.Z)));
                GameAngle.Yaw -= localPlayer.Sway.X;
                GameAngle.Pitch -= localPlayer.Sway.Y;

                if (_Enemy.Distance > 100.0f)
                {
                    VAngle Y = new VAngle();
                    Y.Pitch = (float)Math.Atan2(localPlayer.CurrentWeapon.BulletInitialSpeed.Y, localPlayer.CurrentWeapon.BulletInitialSpeed.Z);

                    if (localPlayer.CurrentWeapon.ZeroingDistanceRadians > 0.0f)
                        Y.Pitch += localPlayer.CurrentWeapon.ZeroingDistanceRadians;

                    GameAngle.Pitch -= Y.Pitch;
                }

                return GameAngle;
            }
            return null;
        }

        #endregion

            #region Aimbot : Utilities

        private Vector3 AimCorrection(Vector3 playerSpeed, Vector3 enemySpeed, Vector3 boneLocked, float enemyDistance, float bulletSpeed, float bulletGravity)
        {
            boneLocked = boneLocked + (enemySpeed * (enemyDistance / Math.Abs(bulletSpeed)));
            boneLocked = boneLocked - (playerSpeed * (enemyDistance / Math.Abs(bulletSpeed)));
            float gravity = Math.Abs(bulletGravity);
            float distance = enemyDistance / Math.Abs(bulletSpeed);
            boneLocked.Y = boneLocked.Y + 0.5f * gravity * distance * distance;
            return boneLocked;
        }

        private float AimCrosshairDistance(GPlayer Enimy)
        {
            Vector3 ShootSpace = new Vector3();
            Matrix auxMatrix = localPlayer.MatrixInverse;

            ShootSpace.X = Enimy.Origin.X - auxMatrix.M41;
            ShootSpace.Y = Enimy.Origin.Y - auxMatrix.M42;
            ShootSpace.Z = Enimy.Origin.Z - auxMatrix.M43;

            ShootSpace = NormalizesVector(ShootSpace);

            Vector3 vecUp = new Vector3(auxMatrix.M21, auxMatrix.M22, auxMatrix.M23);
            float Pitch = (float)Math.Asin(vecUp.X * ShootSpace.X + vecUp.Y * ShootSpace.Y + vecUp.Z * ShootSpace.Z); // Y

            double YawDifference = (double)localPlayer.FoV.Y * Pitch;
            double RealDistance = Math.Abs(Math.Sin(YawDifference) * (double)Enimy.Distance);

            return (float)RealDistance;
        }

        private Vector3 NormalizesVector(Vector3 _space)
        {
            Vector3 Vec = new Vector3();
            float single = (float)Math.Sqrt((double)(_space.X * _space.X + _space.Y * _space.Y + _space.Z * _space.Z));
            Vec.X = _space.X / single;
            Vec.Y = _space.Y / single;
            Vec.Z = _space.Z / single;
            return Vec;
        }

        public static bool CheckPointDifference(System.Drawing.PointF p1, System.Drawing.PointF p2)
        {
            float XMax = (float)Math.PI * 2;
            if (p1.X == p2.X && p1.Y == p2.Y)
                return true;

            if (p1.X > 0 && p2.X < 0)
                p2.X = XMax - Math.Abs(p2.X);
            else if (p2.X > 0 && p1.X < 0)
                p1.X = XMax - Math.Abs(p1.X);
            else if (p1.X < 0 && p2.X < 0)
            {
                p1.X = XMax - Math.Abs(p1.X);
                p2.X = XMax - Math.Abs(p2.X);
            }
            else if (!(p1.X > 0 && p2.X > 0))
                return false;

            float pointDifferenceX = 0.00f;

            if (p1.X > p2.X)
                pointDifferenceX = p1.X - p2.X;
            else
                pointDifferenceX = p2.X - p1.X;

            float TargetAcquisitionAngle = 20.00f; // maximum target acquisition angle in degrees (FOV)
            float FortyFiveDegreePoints = (float)Math.PI / 4;

            float MaxPointDifference = TargetAcquisitionAngle * FortyFiveDegreePoints / 45.00f;

            if (pointDifferenceX <= MaxPointDifference)
                return true;
            else
                return false;
        }

        public static Dictionary<int, System.Drawing.PointF> createPoints(System.Drawing.PointF p1, System.Drawing.PointF p2, float Distance, bool IsSprinting)
        {
            Dictionary<int, System.Drawing.PointF> results = new Dictionary<int, System.Drawing.PointF>();

            // one pixel movement is the same as 0.0011 in game angle

            float Xmod = 0.00025f;
            float Ymod = 0.00105f;

            float minModX = 0.00105f;
            float minModY = 0.00105f;

            int MaxPoints = 525;
            float minDifference = 0.005f;

            float dividerY = 100.0f;

            float XMax = (float)Math.PI * 2;

            List<float> Xpoint = new List<float>();
            List<float> Ypoint = new List<float>();

            if (p1.X == p2.X && p1.Y == p2.Y)
            {
                Xpoint.Add(p2.X);
                Ypoint.Add(p2.Y);
                return results;
            }

            bool positiveXmod = true;

            if (p1.X > 0 && p2.X < 0)
            {
                positiveXmod = false;
                p2.X = XMax - Math.Abs(p2.X);
            }
            else if (p2.X > 0 && p1.X < 0)
            {
                p1.X = XMax - Math.Abs(p1.X);
                positiveXmod = true;
            }
            else if (p1.X > 0 && p2.X > 0)
            {
                if (p1.X > p2.X)
                    positiveXmod = false;
                else
                    positiveXmod = true;

                if (XMax - p1.X <= 0.75f && p2.X <= 0.75f)
                    p1.X = 0.00f;
            }
            else if (p1.X < 0 && p2.X < 0)
            {
                p1.X = XMax - Math.Abs(p1.X);
                p2.X = XMax - Math.Abs(p2.X);
            }
            //else { WriteOnLogFile(String.Format("unhandled --> p1.X: {0}, p2.X: {1}", p1.X, p2.X)); }

            float pointDifferenceX = 0.00f;

            if (p1.X > p2.X)
                pointDifferenceX = p1.X - p2.X;
            else
                pointDifferenceX = p2.X - p1.X;

            if (IsSprinting)
                Xmod = Ymod * 2.0f;

            // X-axis
            if (p1.X < p2.X)
            {
                if (p2.X - p1.X < minDifference)
                    Xpoint.Add(p2.X);
                else
                {
                    int points = 0;
                    while (p1.X < p2.X)
                    {
                        p1.X += Xmod;
                        Xpoint.Add(p1.X);
                        points++;

                        if (points > MaxPoints)
                            break;
                    }
                    Xpoint[Xpoint.Count - 1] = p2.X;
                }
            }
            else
            {
                if (p1.X - p2.X < minDifference)
                    Xpoint.Add(p2.X);
                else
                {
                    if (positiveXmod)
                    {
                        if (minModX > Xmod)
                            Xmod = minModX;
                    }
                    else
                    {
                        Xmod = -Xmod;
                        if (-minModX > Xmod)
                            Xmod = -minModX;
                    }

                    int points = 0;

                    while (p1.X > p2.X)
                    {
                        p1.X += Xmod;
                        Xpoint.Add(p1.X);
                        points++;

                        if (points > MaxPoints)
                            break;
                    }
                    Xpoint[Xpoint.Count - 1] = p2.X;
                }
            }

            // Y-axis
            if (p1.Y < p2.Y)
            {
                if (p2.Y - p1.Y < minDifference)
                    Ypoint.Add(p2.Y);
                else
                {
                    Ymod = (p1.Y / dividerY);
                    if (Ymod < minModY)
                        Ymod = minModY;

                    int points = 0;
                    while (p1.Y < p2.Y)
                    {
                        p1.Y += Ymod;
                        Ypoint.Add(p1.Y);
                        points++;

                        if (points > MaxPoints)
                            break;
                    }
                    Ypoint[Ypoint.Count - 1] = p2.Y;
                }
            }
            else
            {
                if (p1.Y - p2.Y < minDifference || p1.Y < minDifference)
                    Ypoint.Add(p2.Y);
                else
                {
                    Ymod = -(p1.Y / dividerY);
                    if (-minModY > Ymod)
                        Ymod = -minModY;

                    int points = 0;
                    while (p1.Y > p2.Y)
                    {
                        p1.Y += Ymod;
                        Ypoint.Add(p1.Y);
                        points++;

                        if (points > MaxPoints)
                            break;
                    }
                    Ypoint[Ypoint.Count - 1] = p2.Y;
                }
            }

            int i = 0;
            int j = 0;
            int k = 0;

            while (i < Xpoint.Count || i < Ypoint.Count)
            {
                results.Add(i, new System.Drawing.PointF(Xpoint[j], Ypoint[k])); i++;

                if (j < Xpoint.Count - 1)
                    j++;

                if (k < Ypoint.Count - 1)
                    k++;
            }

            return results;
        }
            
            #endregion

        #endregion

        #region Utilities Stuff

        // FPS Stats
        private static int lastTick;
        private static int lastFrameRate;
        private static int frameRate;

        // Get FPS
        public int CalculateFrameRate()
        {
            int tickCount = Environment.TickCount;
            if (tickCount - lastTick >= 1000)
            {
                lastFrameRate = frameRate;
                frameRate = 0;
                lastTick = tickCount;
            }
            frameRate++;
            return lastFrameRate;
        }

        // Quit Application
        private void Quit()
        {
            updateStream.Abort();
            windowStream.Abort();
            //aimbotStream.Abort();
            RPM.CloseProcess();

            // Close main process
            Environment.Exit(0);
        }

        public int MultiHeight
        {
            get
            {
                Int64 pScreen = RPM.Read<Int64>(Offsets.OFFSET_DXRENDERER) == 0 ? 0 : RPM.Read<Int64>(RPM.Read<Int64>(Offsets.OFFSET_DXRENDERER) + 56);
                return (int)(pScreen == 0 ? 0 : RPM.Read<Int32>(pScreen + 92));
            }
        }

        public int MultiWidth
        {
            get
            {
                Int64 pScreen = RPM.Read<Int64>(Offsets.OFFSET_DXRENDERER) == 0 ? 0 : RPM.Read<Int64>(RPM.Read<Int64>(Offsets.OFFSET_DXRENDERER) + 56);
                return (int)(pScreen == 0 ? 0 : RPM.Read<Int32>(pScreen + 88));
            }
        }

        // Check is Game Run
        private bool IsGameRun()
        {
            foreach (Process p in Process.GetProcesses())
            {
                if (p.ProcessName == process.ProcessName)
                    return true;
            }
            return false;
        }

        private void WriteOnLogFile(string txt)
        {
            WriteOnFile(txt, "Log");
        }

        private void WriteOnFile(string txt, string name)
        {
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(AppDomain.CurrentDomain.BaseDirectory + @"\" + name + ".txt", true))
            {
                file.WriteLine(txt);
            }
        }

        #endregion

    }
}