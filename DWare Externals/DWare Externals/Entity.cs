using System.Numerics;

namespace FurryWare;

public class Entity
{
    public IntPtr addres;
    public int healt;
    public int teamID;
    public string playerName;
    public int entityID;
    public bool isSpactating;
    public Vector3 position;
    public Vector3 head;
    public Vector2 head2D;
    public uint lifeState;
    public float distance;
    public Vector3 view;
    public Vector3 abs;
    public Vector2 orignScreenPosition;
    public Vector2 absScreenPosition;
    public List<Vector3> bones;
    public List<Vector2> bones2d;
    public Vector4 color;
    public float pixelDistance;
    public int armor;
    public short currentWeaponIndex;
    public string currentWeaponName;
    public bool spotted;
    public int money;
    public Vector4 teamColor;
}

enum BoneIds
{
    Waist = 0,
    Neck = 5,
    Head = 6,
    ShoulderLeft = 8,
    ForeLeft = 9,
    HandLeft = 11,
    ShoulderRight = 13,
    ForeRight = 14,
    HandRight = 16,
    KneeLeft = 23,
    FeetLeft = 24,
    KneeRight = 26,
    FeetRight = 27,
}

enum Weapon
{
    Deagle = 1,
    Elite = 2,
    Fiveseven = 3,
    Glock = 4,
    Ak47 = 7,
    Aug = 8,
    Awp = 9,
    Famas = 10,
    G3Sg1 = 11,
    Galil = 13,
    M249 = 14,
    Mac10 = 17,
    P90 = 19,
    Ump45 = 24,
    Xm1014 = 25,
    Bizon = 26,
    Mag7 = 27,
    Negev = 28,
    Sawedoff = 29,
    Tec9 = 30,
    Zeus = 31,
    P2000 = 32,
    Mp7 = 33,
    Mp9 = 34,
    Nova = 35,
    P250 = 36,
    Scar20 = 38,
    Sg556 = 39,
    Ssg08 = 40,
    Knife = 42 | 59,
    Flashbang = 43,
    Hegrenade = 44,
    Smokegrenade = 45,
    Molotov = 46,
    Decoy = 47,
    Incgrenede = 48,
    C4 = 49,
    M4A4 = 16,
    UspS = 61,
    M4A1Silencer = 60,
    Cz75A = 63,
    Revolver = 64,
}
