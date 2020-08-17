using RoR2;
using BepInEx;
using MonoMod.Cil;
using R2API.Utils;
using UnityEngine;
using Mono.Cecil.Cil;
using System;
using BepInEx.Configuration;

namespace ThinkInvisible.Hypercrit {
    
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin(ModGuid, ModName, ModVer)]
    public class HypercritPlugin:BaseUnityPlugin {
        public const string ModVer = "1.0.0";
        public const string ModName = "Hypercrit";
        public const string ModGuid = "com.ThinkInvisible.Hypercrit";
        
        enum CritStackingMode {Linear, Exponential, Asymptotic}

        public void Awake() {
            ConfigFile cfgFile = new ConfigFile(Paths.ConfigPath + "\\" + ModGuid + ".cfg", true);

            var cfgCritCap = cfgFile.Bind(new ConfigDefinition("Hypercrit", "CritCap"), 50, new ConfigDescription(
                "Maximum number of extra crit stacks to allow.",
                new AcceptableValueRange<int>(1,int.MaxValue)));
            
            var cfgCritBase = cfgFile.Bind(new ConfigDefinition("Hypercrit", "CritBase"), 2f, new ConfigDescription(
                "Damage multiplier to use for base crit. Replaces vanilla crit multiplier for purposes of dealing damage. Examples used to describe other configs will use default value (2).",
                new AcceptableValueRange<float>(1f,float.MaxValue)));

            var cfgCritMult = cfgFile.Bind(new ConfigDefinition("Hypercrit", "CritMult"), 2f, new ConfigDescription(
                "Damage multiplier to use for all crit stacks except the first. Examples used to describe other configs will use default value (2).",
                new AcceptableValueRange<float>(float.Epsilon,float.MaxValue)));

            var cfgDecayParam = cfgFile.Bind(new ConfigDefinition("Hypercrit", "DecayParam"), 1f, new ConfigDescription(
                "Used in Asymptotic stack mode. Higher numbers directly multiply the number of stacks required to reach the same crit multiplier (at DecayParam 1, 1 stack : 3x and 2 stacks : 3.5x; at DecayParam 3, 3 stacks : 3x and 6 stacks : 3.5x).",
                new AcceptableValueRange<float>(float.Epsilon,float.MaxValue)));

            var cfgStackMode = cfgFile.Bind(new ConfigDefinition("Hypercrit", "StackMode"), CritStackingMode.Exponential, new ConfigDescription(
                "How total crit multiplier is calculated based on number of stacked crits. Linear: x2, x4, x6, x8, x10.... Exponential: x2, x4, x8, x16, x32.... Asymptotic: x2, x3, x3.5, x3.75, x3.825...."));

            IL.RoR2.HealthComponent.TakeDamage += (il) => {
                ILCursor c = new ILCursor(il);
                bool ILFound = c.TryGotoNext(MoveType.After,
                    x => x.MatchLdfld(typeof(DamageInfo).GetFieldCached("crit")),
                    x => x.OpCode == OpCodes.Brfalse_S,
                    x => x.MatchLdloc(out _));
                if(ILFound) {
                    c.Remove();
                    c.Emit(OpCodes.Ldloc_1);
                    c.EmitDelegate<Func<CharacterBody,float>>(x=>{
                        if(x) {
                            //Amount of crit chance past 100%
                            var bCrit = Mathf.Max(x.crit - 100f, 0f);
                            //Amount of non-guaranteed crit chance (for the final crit in the stack)
                            var cCrit = bCrit % 100f;
                            float mult;
                            int extraCrits = Mathf.FloorToInt(bCrit/100f) + (Util.CheckRoll(cCrit, x.master) ? 1 : 0);
                            switch(cfgStackMode.Value) {
                                case CritStackingMode.Linear:
                                    mult = cfgCritBase.Value + cfgCritMult.Value * extraCrits;
                                    break;
                                case CritStackingMode.Asymptotic:
                                    mult = cfgCritBase.Value + cfgCritMult.Value * (1f - Mathf.Pow(2, -extraCrits/cfgDecayParam.Value));
                                    break;
                                default:
                                    mult = cfgCritBase.Value * Mathf.Pow(cfgCritMult.Value, extraCrits);
                                    break;
                            }
                            
                            //PlayScaledSound doesn't seem to work properly (sound plays but is never pitched up/down more than a few semitones, seemingly at random)
                            //As-is, this will just play a sound identical to the existing one
                            //if(extraCrits>0)Util.PlayScaledSound("Play_UI_crit",RoR2Application.instance.gameObject,Mathf.Pow(2f,-Mathf.Min(extraCrits,36)/12f*4f));
                            return mult;
                        } else return 2f;
                    });
                } else {
                    Debug.LogError("Hypercrit: failed to apply IL patch! Mod not loaded.");
                }
            };
        }
    }
}