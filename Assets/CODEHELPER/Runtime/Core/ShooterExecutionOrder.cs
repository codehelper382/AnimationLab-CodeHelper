namespace CodeHelper.ProceduralShooterFramework.Animation
{
    /// <summary>Shared ordering for our components. No dependency on a particular controller or framework.</summary>
    public static class ShooterExecutionOrder
    {
        public const int Spine = 200;
        public const int HandIK = 300; // compatibility for earlier components
        public const int WeaponAttachment = 400;
        public const int Camera = 500;
        public const int WeaponPose = 550; // local weapon pose after camera, before final hand IK
        public const int FinalHandIK = 600; // reads the current frame's camera/holder pose
    }
}
