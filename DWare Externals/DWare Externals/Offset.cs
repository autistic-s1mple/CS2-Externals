using System.Text.Json.Serialization;

namespace FurryWare
{
    public class Offsets
    {
        public int m_iTeamNum = 0x3EB;
        public int m_fFlags = 0x3F8;
        public int m_hPlayerPawn = 0x8FC;
        public int m_iszPlayerName = 0x6E8;
        public int m_iIDEntIndex = 0x3EDC;
        public int m_entitySpottedState = 0x2710;
        public int m_bSpotted = 0x8;
        public int m_iFOV = 0x288;
        public int m_bIsScoped = 0x2728;
        public int m_iHealth = 0x34C;
        public int m_ArmorValue = 0x820;
        public int m_flFlashBangTime = 0x160C;
        public int m_vOldOrigin = 0x15B0;
        public int m_vecViewOffset = 0xD98;
        public int m_lifeState = 0x350;
        public int m_modelState = 0x190;
        public int m_pGameSceneNode = 0x330;
        public int m_iShotsFired = 0x273C;
        public int m_pClippingWeapon = 0x3DF0;
        public int m_iItemDefinitionIndex = 0x1BA;
        public int m_AttributeManager = 0x13A0;
        public int m_Item = 0x50;
        public int m_bBombPlanted = 0x9A5;
        public int m_vecAbsVelocity = 0x3FC;
        public int m_iPing = 0x818;
        public int m_iCompTeammateColor = 0x838;
        public int m_pCameraServices = 0x1438;
        public int m_nBombSite = 0x1174;
        public int m_bBeingDefused = 0x11AC;
        public int m_flDefuseCountDown = 0x11C0;
        public int m_aimPunchAngle = 0x1734;
        public int m_totalHitsOnServer = 0x40;
        public int m_pBulletServices = 0x1688;
        public int m_pObserverServices = 0x1418;
        public int m_hObserverTarget = 0x44;

        public int dwEntityList = 0x1D15F88;
        public int dwLocalPlayerController = 0x1E1F1E8;
        public int dwLocalPlayerPawn = 0x1BF2490;
        public int dwPlantedC4 = 0x1E38160;
        public int dwViewAngles = 0x1E3DC20;
        public int dwViewMatrix = 0x1E330F0;

        public int forceDuck = 0x1BEBEB0;
    }
}