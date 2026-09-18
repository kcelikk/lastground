using System;

namespace LastGround.Data.Crowd
{
    /// <summary>A baked clip: a contiguous frame range in the bone texture.</summary>
    [Serializable]
    public struct CrowdClip
    {
        public CrowdClipId Id;
        public int StartFrame;
        public int FrameCount;
        public bool Loop;

        public float Length(float frameRate) => FrameCount / frameRate;
    }
}
