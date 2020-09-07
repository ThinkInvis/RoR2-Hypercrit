using RoR2;
using BepInEx;
using MonoMod.Cil;
using R2API.Utils;
using UnityEngine;
using Mono.Cecil.Cil;
using System;
using BepInEx.Configuration;
using System.Runtime.CompilerServices;
using RoR2.Orbs;
using RoR2.Projectile;
using UnityEngine.Networking;

namespace ThinkInvisible.Hypercrit {
    
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin(ModGuid, ModName, ModVer)]
    public class HypercritPlugin:BaseUnityPlugin {
        public const string ModVer = "1.0.2";
        public const string ModName = "Hypercrit";
        public const string ModGuid = "com.ThinkInvisible.Hypercrit";
        
        enum CritStackingMode {Linear, Exponential, Asymptotic}

        public class AdditionalCritInfo : R2API.Networking.Interfaces.ISerializableObject {
            public float totalCritChance = 0f;
            public int numCrits = 0;
            public float damageMult = 1f;

            public void Deserialize(NetworkReader reader) {
                totalCritChance = reader.ReadSingle();
                numCrits = reader.ReadInt32();
                damageMult = reader.ReadSingle();
            }

            public void Serialize(NetworkWriter writer) {
                writer.Write(totalCritChance);
                writer.Write(numCrits);
                writer.Write(damageMult);
            }
        }
        private readonly ConditionalWeakTable<object, AdditionalCritInfo> critInfoAttachments = new ConditionalWeakTable<object, AdditionalCritInfo>();

        int critCap;
        float critBase;
        float critMult;
        float decayParam;
        CritStackingMode stackMode;
        bool multiFlurry;
        int flurryBase;
        int flurryAdd;
        bool nerfFlurry;

        public void Awake() {
            ConfigFile cfgFile = new ConfigFile(Paths.ConfigPath + "\\" + ModGuid + ".cfg", true);

            var cfgCritCap = cfgFile.Bind(new ConfigDefinition("Hypercrit", "CritCap"), 1000000000, new ConfigDescription(
                "Maximum number of extra crit stacks to allow. Reduce SHARPLY (to ~50) if using StackMode: Exponential; attacks may break at high stacks otherwise.",
                new AcceptableValueRange<int>(1,int.MaxValue)));
            critCap = cfgCritCap.Value;
            
            var cfgCritBase = cfgFile.Bind(new ConfigDefinition("Hypercrit", "CritBase"), 2f, new ConfigDescription(
                "Damage multiplier to use for base crit. Replaces vanilla crit multiplier for purposes of dealing damage. Examples used to describe other configs will use default value (2).",
                new AcceptableValueRange<float>(1f,float.MaxValue)));
            critBase = cfgCritBase.Value;

            var cfgCritMult = cfgFile.Bind(new ConfigDefinition("Hypercrit", "CritMult"), 1f, new ConfigDescription(
                "Damage multiplier to use for all crit stacks except the first.",
                new AcceptableValueRange<float>(float.Epsilon,float.MaxValue)));
            critMult = cfgCritMult.Value;

            var cfgDecayParam = cfgFile.Bind(new ConfigDefinition("Hypercrit", "DecayParam"), 1f, new ConfigDescription(
                "Used in Asymptotic stack mode. Higher numbers directly multiply the number of stacks required to reach the same crit multiplier (at DecayParam 1, 1 stack : 3x and 2 stacks : 3.5x; at DecayParam 3, 3 stacks : 3x and 6 stacks : 3.5x).",
                new AcceptableValueRange<float>(float.Epsilon,float.MaxValue)));
            decayParam = cfgDecayParam.Value;

            var cfgStackMode = cfgFile.Bind(new ConfigDefinition("Hypercrit", "StackMode"), CritStackingMode.Linear, new ConfigDescription(
                "How total crit multiplier is calculated based on number of stacked crits. Linear (w/ CritMult 1): x2, x3, x4, x5, x6.... Exponential (w/ CritMult 2): x2, x4, x8, x16, x32.... Asymptotic (w/ CritMult 2): x2, x3, x3.5, x3.75, x3.825...."));
            stackMode = cfgStackMode.Value;

            var cfgMultiFlurry = cfgFile.Bind(new ConfigDefinition("Flurry", "Enabled"), true, new ConfigDescription(
                "If false, no changes will be made to Huntress' alternate primary variant. If true, additional shots will be fired for each additional crit stack."));
            multiFlurry = cfgMultiFlurry.Value;

            var cfgFlurryBase = cfgFile.Bind(new ConfigDefinition("Flurry", "CountBase"), 3, new ConfigDescription(
                "The number of shots of Flurry to fire with 0 crit stacks.",
                new AcceptableValueRange<int>(1, int.MaxValue)));
            flurryBase = cfgFlurryBase.Value;

            var cfgFlurryAdd = cfgFile.Bind(new ConfigDefinition("Flurry", "CountAdd"), 3, new ConfigDescription(
                "The number of extra shots of Flurry to fire per crit stack.",
                new AcceptableValueRange<int>(1, int.MaxValue)));
            flurryAdd = cfgFlurryAdd.Value;

            var cfgNerfFlurry = cfgFile.Bind(new ConfigDefinition("Flurry", "CompensateDamage"), true, new ConfigDescription(
                "If true, only the first crit will count towards extra Flurry damage; every additional stack will adjust total damage to account for increased projectile count."));
            nerfFlurry = cfgNerfFlurry.Value;

            IL.RoR2.HealthComponent.TakeDamage += (il) => {
                ILCursor c = new ILCursor(il);
                int damageInfoIndex = -1;
                bool ILFound = c.TryGotoNext(
                    x => x.MatchLdarg(out damageInfoIndex),
                    x => x.MatchLdfld(typeof(DamageInfo).GetFieldCached("crit")),
                    x => x.MatchBrfalse(out _),
                    x => x.MatchLdloc(out _),
                    x => x.MatchLdcR4(out _));
                if(ILFound) {
                    c.Index += 2;
                    c.EmitDelegate<Func<bool,bool>>((origCrit) => true);
                    c.Index += 3;
                    c.Emit(OpCodes.Ldloc_1);
                    c.Emit(OpCodes.Ldarg, damageInfoIndex);
                    c.EmitDelegate<Func<float, CharacterBody, DamageInfo, float>>((origDmgMult, body, damageInfo)=>{
                        if(!body) {
                            return origDmgMult;
                        }
                        AdditionalCritInfo aci = null;
                        if(critInfoAttachments.TryGetValue(damageInfo, out aci)) {
                            return aci.damageMult;
                        }
                        aci = RollHypercrit(body);
                        critInfoAttachments.Add(damageInfo, aci);
                        return aci.damageMult;
                    });
                } else {
                    Debug.LogError("Hypercrit: failed to apply IL patch (HealthComponent.TakeDamage)! Mod will not work.");
                    return;
                }
            };

            /*On.RoR2.DamageDealtMessage.Serialize += (orig, self, writer) => {
            };*/

            if(multiFlurry) {
                On.EntityStates.Huntress.HuntressWeapon.FireFlurrySeekingArrow.OnEnter += (orig, self) => {
                    orig(self);
                    var newCrit = RollHypercrit(self.characterBody);
                    critInfoAttachments.Add(self, newCrit);

                    self.isCrit = newCrit.numCrits > 0;
                    self.maxArrowCount = flurryBase + newCrit.numCrits * flurryAdd;
                    self.arrowReloadDuration = self.baseArrowReloadDuration * (3f / self.maxArrowCount) / self.attackSpeedStat;
                };

                IL.EntityStates.Huntress.HuntressWeapon.FireSeekingArrow.FireOrbArrow += (il) => {
                    var c = new ILCursor(il);
                    c.GotoNext(x => x.MatchStloc(0));
                    c.Emit(OpCodes.Dup);
                    c.Emit(OpCodes.Ldarg_0);
                    c.EmitDelegate<Action<GenericDamageOrb, EntityStates.Huntress.HuntressWeapon.FireSeekingArrow>>((orb, self) => {
                        if(critInfoAttachments.TryGetValue(self, out AdditionalCritInfo aci)) {
                            critInfoAttachments.Add(orb, aci);
                            if(nerfFlurry && aci.numCrits > 1)
                                aci.damageMult *= (flurryBase+flurryAdd)/(flurryBase+flurryAdd*aci.numCrits);
                        }
                    });
                };

                IL.RoR2.Orbs.GenericDamageOrb.OnArrival += (il) => {
                    var c = new ILCursor(il);
                    c.GotoNext(MoveType.After,
                        x => x.MatchNewobj<DamageInfo>());
                    c.Emit(OpCodes.Dup);
                    c.Emit(OpCodes.Ldarg_0);
                    c.EmitDelegate<Action<DamageInfo, GenericDamageOrb>>((di, orb) => {
                        if(critInfoAttachments.TryGetValue(orb, out AdditionalCritInfo aci))
                            critInfoAttachments.Add(di, aci);
                    });
                };
            }
        }

        //for mod interop
        public bool TryGetHypercrit(object target, ref AdditionalCritInfo aci) {
            return critInfoAttachments.TryGetValue(target, out aci);
        }

        private AdditionalCritInfo RollHypercrit(CharacterBody body) {
            var aci = new AdditionalCritInfo();
            if(body) {
                aci.totalCritChance = body.crit;
                //Base crit chance
                var bCrit = Mathf.Max(body.crit, 0f);
                //Amount of non-guaranteed crit chance (for the final crit in the stack)
                var cCrit = bCrit % 100f;
                aci.numCrits = Mathf.Min(Mathf.FloorToInt(bCrit/100f) + (Util.CheckRoll(cCrit, body.master) ? 1 : 0), critCap);
                if(aci.numCrits == 0) aci.damageMult = 1f;
                else switch(stackMode) {
                    case CritStackingMode.Linear:
                        aci.damageMult = critBase + critMult * (aci.numCrits - 1);
                        break;
                    case CritStackingMode.Asymptotic:
                        aci.damageMult = critBase + critMult * (1f - Mathf.Pow(2, -(aci.numCrits-1)/decayParam));
                        break;
                    default:
                        aci.damageMult = critBase * Mathf.Pow(critMult, aci.numCrits - 1);
                        break;
                }
            }
            return aci;
        }
    }
}