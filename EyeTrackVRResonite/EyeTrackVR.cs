using System;
using System.Collections.Generic;
using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;

namespace EyeTrackVRResonite
{
    public class EyeTrackVR : ResoniteMod
    {
        public override string Name => "EyeTrackVRResonite";
        public override string Author => "PLYSHKA + dfgHiatus";
        public override string Version => "2.0.0";
        public override string Link => "https://github.com/Meister1593/EyeTrackVRResonite";

        public override void OnEngineInit()
        {
            _config = GetConfiguration();
            new Harmony("net.plyshka.EyeTrackVRResonite").PatchAll();
            Engine.Current.OnShutdown += ETVROSC.Teardown;
        }

        private static ETVROSC _etvr;
        private static ModConfiguration _config;

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> ModEnabled = new("enabled", "Mod Enabled", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> Alpha = new("alpha", "Eye Swing Multiplier X", () => 1.0f);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> Beta = new("beta", "Eye Swing Multiplier Y", () => 1.0f);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<int> OscPort = new("osc_port", "EyeTrackVR OSC port", () => 9000);

        public static ValueStream<float> CreateStream(World world, string parameter)
        {
            return world.LocalUser.GetStreamOrAdd<ValueStream<float>>(parameter, stream =>
            {
                stream.Name = parameter;
                stream.SetUpdatePeriod(0, 0);
                stream.Encoding = ValueEncoding.Quantized;
                stream.FullFrameBits = 10;
                stream.FullFrameMin = -1;
                stream.FullFrameMax = 1;
            });
        }

        public static void CreateVariable(Slot dvslot, string parameter, ValueStream<float> stream)
        {
            var dv = dvslot.AttachComponent<DynamicValueVariable<float>>();
            dv.VariableName.Value = "User/" + parameter;
            var dvdriver = dvslot.AttachComponent<ValueDriver<float>>();
            dvdriver.ValueSource.Target = stream;
            dvdriver.DriveTarget.Target = dv.Value;
        }

        [HarmonyPatch(typeof(InputInterface), MethodType.Constructor)]
        [HarmonyPatch(new[] { typeof(Engine) })]
        public class InputInterfaceCtorPatch
        {
            public static void Postfix(InputInterface __instance)
            {
                try
                {
                    _etvr = new ETVROSC(_config.GetValue(OscPort));
                    var gen = new EyeTrackVRInterface();
                    __instance.RegisterInputDriver(gen);
                }
                catch (Exception e)
                {
                    Warn("Module failed to initialize.");
                    Warn(e.ToString());
                }
            }
        }

        [HarmonyPatch(typeof(UserRoot), "OnStart")]
        class VRCFTReceiverPatch
        {
            public static void Postfix(UserRoot __instance)
            {
                if (!__instance.ActiveUser.IsLocalUser) return;

                var dvslot = __instance.Slot.FindChildOrAdd("VRCFTReceiver", true);

                if (!EyeTrackVRInterface.VRCFTDictionary.TryGetValue(__instance.World, out var lookup))
                {
                    lookup = new();
                    EyeTrackVRInterface.VRCFTDictionary[__instance.World] = lookup;
                }

                foreach (var transofrmers in EyeTrackVRInterface.FaceTrackParams.Values)
                {
                    foreach (var transformer in transofrmers)
                    {
                        var pair = transformer.Invoke(0);

                        var stream = CreateStream(__instance.World, pair.Key);
                        CreateVariable(dvslot, pair.Key, stream);
                        lookup[pair.Key] = stream;
                    }
                }
            }
        }

        private class EyeTrackVRInterface : IInputDriver
        {
            private Eyes _eyes;
            private const float DefaultPupilSize = 0.0035f;
            public int UpdateOrder => 100;
            public static Dictionary<World, Dictionary<string, ValueStream<float>>> VRCFTDictionary = new();
            private List<KeyValuePair<string, float>> _etvrParameters = new();

            public static readonly Dictionary<string, Func<float, KeyValuePair<string, float>>[]> FaceTrackParams = new()
            {
                    ["EyeLidLeft"] = new[] { MkParam("EyeLidLeft") },
                    ["EyeLidRight"] = new[] { MkParam("EyeLidRight") },
                    ["EyeLid"] = new[] { MkParam("EyeLid") },
                    ["EyeSquint"] = new[] { MkParam("EyeSquint") },
                    ["JawX"] = new[] { MkParam("JawX") },
                    ["JawZ"] = new[] { MkParam("JawZ") },
                    ["BrowDownLeft"] = new[] { MkParam("BrowDownLeft") },
                    ["BrowDownRight"] = new[] { MkParam("BrowDownRight") },
                    ["BrowOuterUp"] = new[] { MkParam("BrowOuterUp") },
                    ["BrowInnerUp"] = new[] { MkParam("BrowInnerUp") },
                    ["BrowUp"] = new[] { MkParam("BrowUp") },
                    ["BrowExpressionLeft"] = new[] { MkParam("BrowExpressionLeft") },
                    ["BrowExpressionRight"] = new[] { MkParam("BrowExpressionRight") },
                    ["BrowExpression"] = new[] { MkParam("BrowExpression") },
                    ["MouthX"] = new[] { MkParam("MouthX") },
                    ["MouthUpperX"] = new[] { MkParam("MouthUpperX") },
                    ["MouthLowerX"] = new[] { MkParam("MouthLowerX") },
                    ["MouthUpperUp"] = new[] { MkParam("MouthUpperUp") },
                    ["MouthLowerDown"] = new[] { MkParam("MouthLowerDown") },
                    ["MouthOpen"] = new[] { MkParam("MouthOpen") },
                    ["MouthSmileLeft"] = new[] { MkParam("MouthSmileLeft") },
                    ["MouthSmileRight"] = new[] { MkParam("MouthSmileRight")},
                    ["MouthSadLeft"] = new[] { MkParam("MouthSadLeft") },
                    ["MouthSadRight"] = new[] { MkParam("MouthSadRight") },
                    ["MouthStretchTightenLeft"] = new[] { MkParam("MouthStretchTightenLeft") },
                    ["MouthStretchTightenRight"] = new[] { MkParam("MouthStretchTightenRight") },
                    ["MouthStretch"] = new[] { MkParam("MouthStretch") },
                    ["MouthTightener"] = new[] { MkParam("MouthTightener") },
                    ["MouthDimple"] = new[] { MkParam("MouthDimple") },
                    ["MouthPress"] = new[] { MkParam("MouthPress") },
                    ["SmileFrownLeft"] = new[] { MkParam("SmileFrownLeft") },
                    ["SmileFrownRight"] = new[] { MkParam("SmileFrownRight")},
                    ["SmileFrown"] = new[] { MkParam("SmileFrown") },
                    ["SmileSadLeft"] = new[] { MkParam("SmileSadLeft") },
                    ["SmileSadRight"] = new[] { MkParam("SmileSadRight") },
                    ["SmileSad"] = new[] { MkParam("SmileSad") },
                    ["LipSuckUpper"] = new[] { MkParam("LipSuckUpper") },
                    ["LipSuckLower"] = new[] { MkParam("LipSuckLower") },
                    ["LipSuck"] = new[] { MkParam("LipSuck") },
                    ["LipFunnelUpper"] = new[] { MkParam("LipFunnelUpper") },
                    ["LipFunnelLower"] = new[] { MkParam("LipFunnelLower") },
                    ["LipFunnel"] = new[] { MkParam("LipFunnel") },
                    ["LipPuckerUpper"] = new[] { MkParam("LipPuckerUpper") },
                    ["LipPuckerLower"] = new[] { MkParam("LipPuckerLower") },
                    ["LipPucker"] = new[] { MkParam("LipPucker") },
                    ["NoseSneer"] = new[] { MkParam("NoseSneer") },
                    ["CheekPuffSuckLeft"] = new[] { MkParam("CheekPuffSuckLeft") },
                    ["CheekPuffSuckRight"] = new[] { MkParam("CheekPuffSuckRight") },
                    ["CheekPuffSuck"] = new[] { MkParam("CheekPuffSuck") },
                    ["CheekSquint"] = new[] { MkParam("CheekSquint") },
                    ["TongueX"] = new[] { MkParam("TongueX") },
                    ["TongueY"] = new[] { MkParam("TongueY") },
                    ["EarLeft"] = new[] { MkParam("EarLeft") },
                    ["EarRight"] = new[] { MkParam("EarRight") },
                    ["Blush"] = new[] { MkParam("Blush") },
                    ["EyeLeftX"] = new[] { MkParam("EyeLeftX") },
                    ["EyeRightX"] = new[] { MkParam("EyeRightX") },
                    ["EyeY"] = new[] { MkParam("EyeY") },
                    ["EyeClosedRight"] = new[] { MkParam("EyeClosedRight") },
                    ["EyeClosedLeft"] = new[] { MkParam("EyeClosedLeft") },
                    ["EyeSquintRight"] = new[] { MkParam("EyeSquintRight") },
                    ["EyeSquintLeft"] = new[] { MkParam("EyeSquintLeft") },
                    ["EyeWideRight"] = new[] { MkParam("EyeWideRight") },
                    ["EyeWideLeft"] = new[] { MkParam("EyeWideLeft") },
                    ["BrowPinchRight"] = new[] { MkParam("BrowPinchRight") },
                    ["BrowPinchLeft"] = new[] { MkParam("BrowPinchLeft") },
                    ["BrowLowererRight"] = new[] { MkParam("BrowLowererRight") },
                    ["BrowLowererLeft"] = new[] { MkParam("BrowLowererLeft")},
                    ["BrowInnerUpRight"] = new[] { MkParam("BrowInnerUpRight") },
                    ["BrowInnerUpLeft"] = new[] { MkParam("BrowInnerUpLeft")},
                    ["BrowOuterUpRight"] = new[] { MkParam("BrowOuterUpRight") },
                    ["BrowOuterUpLeft"] = new[] { MkParam("BrowOuterUpLeft")},
                    ["NasalDilationRight"] = new[] { MkParam("NasalDilationRight") },
                    ["NasalDilationLeft"] = new[] { MkParam("NasalDilationLeft") },
                    ["NasalConstrictRight"] = new[] { MkParam("NasalConstrictRight") },
                    ["NasalConstrictLeft"] = new[] { MkParam("NasalConstrictLeft") },
                    ["CheekSquintRight"] = new[] { MkParam("CheekSquintRight") },
                    ["CheekSquintLeft"] = new[] { MkParam("CheekSquintLeft")},
                    ["CheekPuffRight"] = new[] { MkParam("CheekPuffRight") },
                    ["CheekPuffLeft"] = new[] { MkParam("CheekPuffLeft") },
                    ["CheekSuckRight"] = new[] { MkParam("CheekSuckRight") },
                    ["CheekSuckLeft"] = new[] { MkParam("CheekSuckLeft") },
                    ["JawOpen"] = new[] { MkParam("JawOpen") },
                    ["JawRight"] = new[] { MkParam("JawRight") },
                    ["JawLeft"] = new[] { MkParam("JawLeft") },
                    ["JawForward"] = new[] { MkParam("JawForward") },
                    ["JawBackward"] = new[] { MkParam("JawBackward") },
                    ["JawClench"] = new[] { MkParam("JawClench") },
                    ["JawMandibleRaise"] = new[] { MkParam("JawMandibleRaise") },
                    ["MouthClosed"] = new[] { MkParam("MouthClosed") },
                    ["LipSuckUpperRight"] = new[] { MkParam("LipSuckUpperRight") },
                    ["LipSuckUpperLeft"] = new[] { MkParam("LipSuckUpperLeft") },
                    ["LipSuckLowerRight"] = new[] { MkParam("LipSuckLowerRight") },
                    ["LipSuckLowerLeft"] = new[] { MkParam("LipSuckLowerLeft") },
                    ["LipSuckCornerRight"] = new[] { MkParam("LipSuckCornerRight") },
                    ["LipSuckCornerLeft"] = new[] { MkParam("LipSuckCornerLeft") },
                    ["LipFunnelUpperRight"] = new[] { MkParam("LipFunnelUpperRight") },
                    ["LipFunnelUpperLeft"] = new[] { MkParam("LipFunnelUpperLeft") },
                    ["LipFunnelLowerRight"] = new[] { MkParam("LipFunnelLowerRight") },
                    ["LipFunnelLowerLeft"] = new[] { MkParam("LipFunnelLowerLeft") },
                    ["LipPuckerUpperRight"] = new[] { MkParam("LipPuckerUpperRight") },
                    ["LipPuckerUpperLeft"] = new[] { MkParam("LipPuckerUpperLeft") },
                    ["LipPuckerLowerRight"] = new[] { MkParam("LipPuckerLowerRight") },
                    ["LipPuckerLowerLeft"] = new[] { MkParam("LipPuckerLowerLeft") },
                    ["MouthUpperUpRight"] = new[] { MkParam("MouthUpperUpRight") },
                    ["MouthUpperUpLeft"] = new[] { MkParam("MouthUpperUpLeft") },
                    ["MouthUpperDeepenRight"] = new[] { MkParam("MouthUpperDeepenRight") },
                    ["MouthUpperDeepenLeft"] = new[] { MkParam("MouthUpperDeepenLeft") },
                    ["NoseSneerRight"] = new[] { MkParam("NoseSneerRight") },
                    ["NoseSneerLeft"] = new[] { MkParam("NoseSneerLeft") },
                    ["MouthLowerDownRight"] = new[] { MkParam("MouthLowerDownRight") },
                    ["MouthLowerDownLeft"] = new[] { MkParam("MouthLowerDownLeft") },
                    ["MouthUpperRight"] = new[] { MkParam("MouthUpperRight")},
                    ["MouthUpperLeft"] = new[] { MkParam("MouthUpperLeft") },
                    ["MouthLowerRight"] = new[] { MkParam("MouthLowerRight")},
                    ["MouthLowerLeft"] = new[] { MkParam("MouthLowerLeft") },
                    ["MouthCornerPullRight"] = new[] { MkParam("MouthCornerPullRight") },
                    ["MouthCornerPullLeft"] = new[] { MkParam("MouthCornerPullLeft") },
                    ["MouthCornerSlantRight"] = new[] { MkParam("MouthCornerSlantRight") },
                    ["MouthCornerSlantLeft"] = new[] { MkParam("MouthCornerSlantLeft") },
                    ["MouthFrownRight"] = new[] { MkParam("MouthFrownRight")},
                    ["MouthFrownLeft"] = new[] { MkParam("MouthFrownLeft") },
                    ["MouthStretchRight"] = new[] { MkParam("MouthStretchRight") },
                    ["MouthStretchLeft"] = new[] { MkParam("MouthStretchLeft") },
                    ["MouthDimpleRight"] = new[] { MkParam("MouthDimpleRight") },
                    ["MouthDimpleLeft"] = new[] { MkParam("MouthDimpleLeft")},
                    ["MouthRaiserUpper"] = new[] { MkParam("MouthRaiserUpper") },
                    ["MouthRaiserLower"] = new[] { MkParam("MouthRaiserLower") },
                    ["MouthPressRight"] = new[] { MkParam("MouthPressRight")},
                    ["MouthPressLeft"] = new[] { MkParam("MouthPressLeft") },
                    ["MouthTightenerRight"] = new[] { MkParam("MouthTightenerRight") },
                    ["MouthTightenerLeft"] = new[] { MkParam("MouthTightenerLeft") },
                    ["TongueOut"] = new[] { MkParam("TongueOut") },
                    ["TongueUp"] = new[] { MkParam("TongueUp") },
                    ["TongueDown"] = new[] { MkParam("TongueDown") },
                    ["TongueRight"] = new[] { MkParam("TongueRight") },
                    ["TongueLeft"] = new[] { MkParam("TongueLeft") },
                    ["TongueRoll"] = new[] { MkParam("TongueRoll") },
                    ["TongueBendDown"] = new[] { MkParam("TongueBendDown") },
                    ["TongueCurlUp"] = new[] { MkParam("TongueCurlUp") },
                    ["TongueSquish"] = new[] { MkParam("TongueSquish") },
                    ["TongueFlat"] = new[] { MkParam("TongueFlat") },
                    ["TongueTwistRight"] = new[] { MkParam("TongueTwistRight") },
                    ["TongueTwistLeft"] = new[] { MkParam("TongueTwistLeft")},
            };

            public void CollectDeviceInfos(DataTreeList list)
            {
                var eyeDataTreeDictionary = new DataTreeDictionary();
                eyeDataTreeDictionary.Add("Name", "EyeTrackVR Eye Tracking");
                eyeDataTreeDictionary.Add("Type", "Eye Tracking");
                eyeDataTreeDictionary.Add("Model", "ETVR Module");
                list.Add(eyeDataTreeDictionary);
            }

            public void RegisterInputs(InputInterface inputInterface)
            {
                _eyes = new Eyes(inputInterface, "EyeTrackVR Tracking", true);
            }

            public void UpdateInputs(float deltaTime)
            {

                var focus = Engine.Current.WorldManager?.FocusedWorld;
                // If world is not available
                if (focus != null)
                {
                    // Get or create lookup for world
                    if (!VRCFTDictionary.TryGetValue(focus, out var lookup))
                    {
                        lookup = new();
                        VRCFTDictionary[focus] = lookup;
                    }

                    // user root if null
                    if (focus.LocalUser.Root == null)
                    {
                        Warn("Root not Found");
                        return;
                    }

                    lock (_etvr.Lock)
                    {
                        _etvrParameters.Clear();
                        _etvrParameters.AddRange(_etvr.Parameters);
                    }

                    foreach (var oscParam in _etvrParameters)
                    {
                        if (!FaceTrackParams.TryGetValue(oscParam.Key, out var transformers))
                            continue;

                        foreach (var transformer in transformers)
                        {
                            var param = transformer(oscParam.Value);
                            if (!lookup.TryGetValue(param.Key, out var stream) || (stream != null && stream.IsRemoved))
                            {
                                lookup[param.Key] = null;
                                focus.RunInUpdates(0, () =>
                                {
                                    var s = CreateStream(focus, param.Key);
                                    lookup[param.Key] = s;
                                });
                            }
                            if (stream != null)
                            {
                                stream.Value = param.Value;
                                stream.ForceUpdate();
                            }
                        }
                    }
                }

                _eyes.CombinedEye.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
                _eyes.CombinedEye.IsTracking = _etvr.LastUpdate > DateTime.Now.AddSeconds(-5);
                _eyes.CombinedEye.PupilDiameter = DefaultPupilSize;

                _eyes.LeftEye.RawPosition = float3.Zero;
                _eyes.RightEye.RawPosition = float3.Zero;

                var eyeLidLeft = Parameter("EyeLidLeft");
                var eyeLidRight = Parameter("EyeLidRight");

                _eyes.LeftEye.Openness = MathX.Remap(eyeLidLeft, 0f, 0.75f, 0f, 1f);
                _eyes.RightEye.Openness = MathX.Remap(eyeLidRight, 0f, 0.75f, 0f, 1f);

                _eyes.LeftEye.Widen = MathX.Remap(eyeLidLeft, 0.75f, 1f, 0f, 1f);
                _eyes.RightEye.Widen = MathX.Remap(eyeLidRight, 0.75f, 1f, 0f, 1f);

                var eyeSquintLeft = Parameter("EyeSquintLeft");
                var eyeSquintRight = Parameter("EyeSquintRight");

                _eyes.LeftEye.Squeeze = eyeSquintLeft;
                _eyes.RightEye.Squeeze = eyeSquintRight;
                _eyes.LeftEye.Frown = eyeSquintLeft;
                _eyes.RightEye.Frown = eyeSquintRight;

                var leftEyeRot = floatQ.Euler(
                    _etvr.EyeLeftRightEuler[0].x,
                    _etvr.EyeLeftRightEuler[0].y,
                    0);
                var rightEyeRot = floatQ.Euler(
                    _etvr.EyeLeftRightEuler[1].x,
                    _etvr.EyeLeftRightEuler[1].y,
                    0);

                _eyes.LeftEye.UpdateWithRotation(leftEyeRot);
                _eyes.RightEye.UpdateWithRotation(rightEyeRot);
                _eyes.CombinedEye.UpdateWithRotation(leftEyeRot);

                CombineEyeData();

                _eyes.ConvergenceDistance = 0f;
                _eyes.Timestamp += deltaTime;
                _eyes.FinishUpdate();
            }

            private float Parameter(string key)
            {
                if (_etvr.Parameters.TryGetValue(key, out var val))
                    return val;
                return 0;
            }

            private void CombineEyeData()
            {
                _eyes.IsEyeTrackingActive = _eyes.CombinedEye.IsTracking;
                _eyes.IsDeviceActive = _eyes.CombinedEye.IsDeviceActive;
                _eyes.IsTracking = _eyes.CombinedEye.IsTracking;

                _eyes.LeftEye.IsDeviceActive = _eyes.CombinedEye.IsDeviceActive;
                _eyes.RightEye.IsDeviceActive = _eyes.CombinedEye.IsDeviceActive;
                _eyes.LeftEye.IsTracking = _eyes.CombinedEye.IsTracking;
                _eyes.RightEye.IsTracking = _eyes.CombinedEye.IsTracking;
                _eyes.LeftEye.PupilDiameter = _eyes.CombinedEye.PupilDiameter;
                _eyes.RightEye.PupilDiameter = _eyes.CombinedEye.PupilDiameter;

                _eyes.CombinedEye.IsTracking = false;

                _eyes.CombinedEye.RawPosition = MathX.Average(_eyes.LeftEye.RawPosition, _eyes.RightEye.RawPosition);

                _eyes.CombinedEye.Openness = MathX.Average(_eyes.LeftEye.Openness, _eyes.RightEye.Openness);
                _eyes.CombinedEye.Widen = MathX.Average(_eyes.LeftEye.Widen, _eyes.RightEye.Widen);
                _eyes.CombinedEye.Squeeze = MathX.Average(_eyes.LeftEye.Squeeze, _eyes.RightEye.Squeeze);
                _eyes.CombinedEye.Frown = MathX.Average(_eyes.LeftEye.Frown, _eyes.RightEye.Frown);
            }

            private static Func<float, KeyValuePair<string, float>> MkParam(string key, float min, float max)
            {
                return (float val) => new KeyValuePair<string, float>(key, MathX.Remap(val, min, max, 0f, 1f));
            }

            private static Func<float, KeyValuePair<string, float>> MkParam(string key)
            {
                return (float val) => new KeyValuePair<string, float>(key, val);
            }

            private static float3 Project2DTo3D(float2 v)
            {
                v *= MathX.Deg2Rad;

                var pitch = v.x;
                var yaw = v.y;

                var x = MathX.Cos(yaw) * MathX.Cos(pitch);
                var y = MathX.Sin(yaw) * -MathX.Cos(pitch);
                var z = MathX.Sin(pitch);

                return new float3(x, y, z);
            }
        }
    }
}
