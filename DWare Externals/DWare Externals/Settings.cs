using System.Numerics;
using System.Text.Json.Serialization;

namespace FurryWare
{
    public class Settings
    {
        public bool enableESP { get; set; }
        public bool enableBox { get; set; }
        public bool cornerBox { get; set; }
        public bool coloredBox { get; set; }
        public bool fillBox { get; set; }
        public bool skeletonESP { get; set; }
        public bool hpBar { get; set; }
        public bool weaponESP { get; set; }
        public bool playerDistance { get; set; }
        public bool playerName { get; set; }
        public bool boxLine { get; set; }
        public bool teamESP { get; set; }
        public bool radar { get; set; }


        public bool enableAim { get; set; }
        public int aimHotkey { get; set; }
        public bool enableAimFOV { get; set; }
        public int aimFOV { get; set; }
        public int smoothes { get; set; }
        public bool teamAim { get; set; }


        public bool enableTrigger { get; set; }
        public bool enableHotKey { get; set; }
        public int triggerHotkey { get; set; }
        public bool trggierDelay { get; set; }
        public int delay { get; set; }
        public bool disableWhileStrafing { get; set; }
        public bool teamTrigger { get; set; }


        public bool antiFlash { get; set; }
        public bool autoDuck { get; set; }
        public int playerFOV { get; set; }
        public bool enableHitSound { get; set; }


        // Colors
        [JsonIgnore] public Vector4 lineColor { get; set; }
        public float[] lineColorRgba
        {
            get => new float[] { lineColor.X, lineColor.Y, lineColor.Z, lineColor.W };
            set => lineColor = new Vector4(value[0], value[1], value[2], value[3]);
        }

        [JsonIgnore] public Vector4 skeletonColor { get; set; }
        public float[] skeletonColorRgba
        {
            get => new float[] { skeletonColor.X, skeletonColor.Y, skeletonColor.Z, skeletonColor.W };
            set => skeletonColor = new Vector4(value[0], value[1], value[2], value[3]);
        }

        [JsonIgnore] public Vector4 boxColor { get; set; }
        public float[] boxColorRgba
        {
            get => new float[] { boxColor.X, boxColor.Y, boxColor.Z, boxColor.W };
            set => boxColor = new Vector4(value[0], value[1], value[2], value[3]);
        }

        [JsonIgnore] public Vector4 boxOutlineColor { get; set; }
        public float[] boxOutlineColorRgba
        {
            get => new float[] { boxOutlineColor.X, boxOutlineColor.Y, boxOutlineColor.Z, boxOutlineColor.W };
            set => boxOutlineColor = new Vector4(value[0], value[1], value[2], value[3]);
        }

        [JsonIgnore] public Vector4 boxFillColor { get; set; }
        public float[] boxFillColorRgba
        {
            get => new float[] { boxFillColor.X, boxFillColor.Y, boxFillColor.Z, boxFillColor.W };
            set => boxFillColor = new Vector4(value[0], value[1], value[2], value[3]);
        }

        [JsonIgnore] public Vector4 textColor { get; set; }
        public float[] textColorRgba
        {
            get => new float[] { textColor.X, textColor.Y, textColor.Z, textColor.W };
            set => textColor = new Vector4(value[0], value[1], value[2], value[3]);
        }

        [JsonIgnore] public Vector4 textOutlineColor { get; set; }
        public float[] textOutlineColorRgba
        {
            get => new float[] { textOutlineColor.X, textOutlineColor.Y, textOutlineColor.Z, textOutlineColor.W };
            set => textOutlineColor = new Vector4(value[0], value[1], value[2], value[3]);
        }

        [JsonIgnore] public Vector4 aimFOVColor { get; set; }
        public float[] aimFOVColorRgba
        {
            get => new float[] { aimFOVColor.X, aimFOVColor.Y, aimFOVColor.Z, aimFOVColor.W };
            set => aimFOVColor = new Vector4(value[0], value[1], value[2], value[3]);
        }
    }
}
