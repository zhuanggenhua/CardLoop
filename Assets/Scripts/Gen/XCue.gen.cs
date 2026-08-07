///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

namespace GAS.Runtime
{
    public static class XCue
    {
        public const string CUE_CueLog = "CueLog";
        public const string CUE_CueLogging = "CueLogging";
        public const string CUE_CueMountPrefab = "CueMountPrefab";
        public const string CUE_CuePlayAnimator = "CuePlayAnimator";
        public const string CUE_CuePlaySound = "CuePlaySound";
        public const string CUE_CuePlayGameCoreAnimator = "CuePlayGameCoreAnimator";
        public const string CUE_CuePlayGameCoreAudio = "CuePlayGameCoreAudio";
        public const string CUE_CuePlayGameCoreFeedback = "CuePlayGameCoreFeedback";

        public static void LoadCueType()
        {
            var CueLog = typeof(GAS.Runtime.CueLog);
            CueHelper.RegisterCue(CUE_CueLog, CueLog, typeof(GAS.Runtime.XParamString));
            var CueLogging = typeof(GAS.Runtime.CueLogging);
            CueHelper.RegisterCue(CUE_CueLogging, CueLogging, typeof(GAS.Runtime.XParamLogging));
            var CueMountPrefab = typeof(GAS.Runtime.CueMountPrefab);
            CueHelper.RegisterCue(CUE_CueMountPrefab, CueMountPrefab, typeof(GAS.Runtime.XParamMountPrefab));
            var CuePlayAnimator = typeof(GAS.Runtime.CuePlayAnimator);
            CueHelper.RegisterCue(CUE_CuePlayAnimator, CuePlayAnimator, typeof(GAS.Runtime.XParamAnimator));
            var CuePlaySound = typeof(GAS.Runtime.CuePlaySound);
            CueHelper.RegisterCue(CUE_CuePlaySound, CuePlaySound, typeof(GAS.Runtime.XParamPlaySound));
            var CuePlayGameCoreAnimator = typeof(GameCore.CuePlayGameCoreAnimator);
            CueHelper.RegisterCue(CUE_CuePlayGameCoreAnimator, CuePlayGameCoreAnimator, typeof(GAS.Runtime.XParamAnimator));
            var CuePlayGameCoreAudio = typeof(GameCore.CuePlayGameCoreAudio);
            CueHelper.RegisterCue(CUE_CuePlayGameCoreAudio, CuePlayGameCoreAudio, typeof(GameCore.XParamGameCoreAudio));
            var CuePlayGameCoreFeedback = typeof(GameCore.CuePlayGameCoreFeedback);
            CueHelper.RegisterCue(CUE_CuePlayGameCoreFeedback, CuePlayGameCoreFeedback, typeof(GameCore.XParamGameCoreFeedback));
        }
    }
}
