using System;
using System.Collections.Generic;
using System.Linq;

namespace Mmorpg.Core.Data
{
    /// <summary>
    /// Valida consistência dos dados (referências, invariantes do projeto, faixas de valores).
    /// Não é mecanismo de segurança: em jogo online, o servidor valida combate, inventário, economia e recompensas.
    /// As regras principais também existem em tools/validate_data.py (para CI sem .NET).
    /// </summary>
    public static class GameDataValidator
    {
        private static readonly string[] ControlKeys = { "stun", "root", "silence", "slow", "taunt" };

        public static void Validate(GameData d, ValidationReport r)
        {
            ValidateAttributes(d, r);
            ValidateRarities(d, r);
            ValidateStatuses(d, r);
            ValidateCombatAndProgression(d, r);
            ValidateClasses(d, r);
            ValidateSkills(d, r);
            ValidateClassSkillLinks(d, r);
            ValidateItems(d, r);
            ValidateSets(d, r);
            ValidateLoot(d, r);
            ValidateMonsters(d, r);
            ValidateRegions(d, r);
            AddProgressWarnings(d, r);
        }

        // ----------------------------------------------------------- atributos
        private static void ValidateAttributes(GameData d, ValidationReport r)
        {
            foreach (var res in d.Resources.Values)
            {
                if (!string.IsNullOrEmpty(res.MaxStat) && !d.DerivedStats.ContainsKey(res.MaxStat))
                    r.Error("attributes", "resource " + res.Id + ": maxStat desconhecido " + res.MaxStat);
                if (!string.IsNullOrEmpty(res.RegenStat) && !d.DerivedStats.ContainsKey(res.RegenStat))
                    r.Error("attributes", "resource " + res.Id + ": regenStat desconhecido " + res.RegenStat);
                if (string.IsNullOrEmpty(res.MaxStat) && res.Max <= 0)
                    r.Error("attributes", "resource " + res.Id + ": defina maxStat ou max > 0");
            }
            foreach (var s in d.DerivedStats.Values)
            {
                foreach (var p in s.PerPrimary.Keys)
                    if (!d.PrimaryStatIds.Contains(p))
                        r.Error("attributes", "derived " + s.Id + ": atributo primário desconhecido " + p);
            }
            string[] required = { "maxHp", "physAttack", "magicAttack", "physDefense", "magicResist", "critChance", "critMultiplier", "moveSpeed", "hpRegen" };
            foreach (var id in required)
                if (!d.DerivedStats.ContainsKey(id)) r.Error("attributes", "atributo derivado obrigatório ausente: " + id);
        }

        private static void ValidateRarities(GameData d, ValidationReport r)
        {
            var expected = new HashSet<string>(d.Rules.ExpectedRarityIds);
            if (!expected.SetEquals(d.Rarities.Keys))
                r.Error("rarities", "raridades esperadas [" + string.Join(", ", d.Rules.ExpectedRarityIds) + "], encontradas [" + string.Join(", ", d.Rarities.Keys) + "]");
            if (d.Rarities.Values.Select(x => x.Order).Distinct().Count() != d.Rarities.Count)
                r.Error("rarities", "campo 'order' duplicado");
            foreach (var x in d.Rarities.Values)
                if (string.IsNullOrEmpty(x.Tag)) r.Error("rarities", x.Id + " sem 'tag' textual (não depender só de cor)");
        }

        private static void ValidateStatuses(GameData d, ValidationReport r)
        {
            foreach (var s in d.Statuses.Values)
            {
                if ((s.Kind == StatusKind.Dot || s.Kind == StatusKind.Hot) && s.TickInterval <= 0)
                    r.Error("statuses", s.Id + ": dot/hot exige tickInterval > 0");
                if (s.Kind == StatusKind.Slow && !(s.MoveSpeedMultiplier > 0 && s.MoveSpeedMultiplier < 1))
                    r.Error("statuses", s.Id + ": slow exige moveSpeedMultiplier em (0,1)");
                foreach (var k in s.StatMods.Keys)
                    if (!d.IsKnownStat(k)) r.Error("statuses", s.Id + ": statMod desconhecido " + k);
            }
        }

        private static void ValidateCombatAndProgression(GameData d, ValidationReport r)
        {
            var c = d.Combat;
            if (c.Mitigation.MaxMitigation < 0 || c.Mitigation.MaxMitigation >= 1)
                r.Error("combat", "maxMitigation deve estar em [0,1)");
            if (c.Control.MinMoveSpeedMultiplier <= 0)
                r.Error("combat", "minMoveSpeedMultiplier deve ser > 0");
            if (c.GlobalCooldown < 0) r.Error("combat", "globalCooldown negativo");
            if (c.DamageVariance < 0 || c.DamageVariance >= 1) r.Error("combat", "damageVariance deve estar em [0,1)");
            if (c.CorpseSeconds < 0) r.Error("combat", "corpseSeconds negativo");
            if (c.BodyRadius <= 0) r.Error("combat", "bodyRadius deve ser > 0");
            if (c.Ai.EvadeSpeedMultiplier < 1) r.Error("combat", "ai.evadeSpeedMultiplier deve ser >= 1");
            if (c.Ai.EvadeHealPercentPerSecond <= 0) r.Error("combat", "ai.evadeHealPercentPerSecond deve ser > 0");
            if (c.Ai.AttackRangeFraction <= 0 || c.Ai.AttackRangeFraction > 1) r.Error("combat", "ai.attackRangeFraction deve estar em (0,1]");
            if (c.Ai.TargetSwitchThreatRatio < 1) r.Error("combat", "ai.targetSwitchThreatRatio deve ser >= 1");
            foreach (var k in c.Control.MaxDuration.Keys)
                if (!ControlKeys.Contains(k)) r.Error("combat", "control.maxDuration com tipo desconhecido " + k);
            foreach (var k in c.Control.ImmunityAfter.Keys)
                if (!ControlKeys.Contains(k)) r.Error("combat", "control.immunityAfter com tipo desconhecido " + k);
            if (d.Progression.MaxLevel != d.Rules.MaxLevel)
                r.Error("progression", "maxLevel difere de rules.maxLevel");
            if (d.Progression.XpCurve.A <= 0) r.Error("progression", "xpCurve.a deve ser > 0");
        }

        // -------------------------------------------------------------- classes
        private static void ValidateClasses(GameData d, ValidationReport r)
        {
            if (d.Classes.Count != d.Rules.ExpectedClassCount)
                r.Error("classes", "esperadas " + d.Rules.ExpectedClassCount + " classes, encontradas " + d.Classes.Count);
            var factionIds = new HashSet<string>(d.Rules.Factions.Select(f => f.Id));
            foreach (var f in factionIds)
            {
                int n = d.Classes.Values.Count(c => c.Faction == f);
                if (n != d.Rules.ClassesPerFaction)
                    r.Error("classes", "facção " + f + " deve ter " + d.Rules.ClassesPerFaction + " classes (tem " + n + ")");
            }
            var primary = new HashSet<string>(d.PrimaryStatIds);
            foreach (var c in d.Classes.Values)
            {
                string w = "class " + c.Id;
                if (!factionIds.Contains(c.Faction)) r.Error(w, "facção desconhecida " + c.Faction);
                if (!d.Resources.ContainsKey(c.Resource ?? "")) r.Error(w, "recurso desconhecido " + c.Resource);
                if (!primary.SetEquals(c.PrimaryBase.Keys)) r.Error(w, "primaryBase deve definir exatamente os atributos primários");
                if (!primary.SetEquals(c.PrimaryPerLevel.Keys)) r.Error(w, "primaryPerLevel deve definir exatamente os atributos primários");
                if (c.WeaponTypes.Count == 0) r.Error(w, "sem weaponTypes");
            }
        }

        // ------------------------------------------------------------ habilidades
        private static void ValidateSkills(GameData d, ValidationReport r)
        {
            foreach (var s in d.Skills.Values)
            {
                string w = "skill " + s.Id;
                if (s.Cooldown < 0) r.Error(w, "cooldown negativo");
                if (s.CastTime < 0) r.Error(w, "castTime negativo");
                if (s.ResourceCost < 0) r.Error(w, "resourceCost negativo");
                if (s.Range < 0 || s.Radius < 0) r.Error(w, "range/radius negativo");
                if ((s.Targeting == Targeting.Ground || s.Targeting == Targeting.SelfArea) && s.Radius <= 0)
                    r.Error(w, "habilidade de área exige radius > 0");
                if ((s.Targeting == Targeting.Enemy || s.Targeting == Targeting.Ally || s.Targeting == Targeting.Ground) && s.Range <= 0)
                    r.Error(w, "habilidade com alvo exige range > 0");
                if (s.MaxTargets < 1) r.Error(w, "maxTargets deve ser >= 1");
                if (s.Effects.Count == 0) r.Error(w, "sem efeitos");
                if (s.RequiresLevel < 1 || s.RequiresLevel > d.Rules.MaxLevel)
                    r.Error(w, "requiresLevel fora de [1," + d.Rules.MaxLevel + "]");

                foreach (var e in s.Effects)
                {
                    EffectSide side = e.ResolvedSide();
                    foreach (var st in e.Scaling.Keys)
                        if (!d.IsKnownStat(st)) r.Error(w, "scaling usa atributo desconhecido " + st);
                    double value = e.Base + e.Scaling.Values.Sum();
                    switch (e.Kind)
                    {
                        case EffectKind.Damage:
                            if (value <= 0) r.Error(w, "dano sem valor");
                            break;
                        case EffectKind.Heal:
                            if (value <= 0) r.Error(w, "cura sem valor");
                            break;
                        case EffectKind.Shield:
                        case EffectKind.ApplyStatus:
                            StatusDef sd;
                            if (e.StatusId == null || !d.Statuses.TryGetValue(e.StatusId, out sd))
                            {
                                r.Error(w, "statusId desconhecido '" + e.StatusId + "'");
                            }
                            else
                            {
                                if (e.Kind == EffectKind.Shield && sd.Kind != StatusKind.Shield)
                                    r.Error(w, "efeito shield exige status do tipo shield");
                                if (e.Kind == EffectKind.ApplyStatus &&
                                    (sd.Kind == StatusKind.Dot || sd.Kind == StatusKind.Hot || sd.Kind == StatusKind.Shield) && value <= 0)
                                    r.Error(w, "dot/hot/shield sem valor (base/scaling)");
                            }
                            if (e.Chance <= 0 || e.Chance > 1) r.Error(w, "chance fora de (0,1]");
                            break;
                        case EffectKind.Dash:
                            if (e.DashDistance <= 0) r.Error(w, "dash exige dashDistance > 0");
                            break;
                    }
                    if (s.Targeting == Targeting.Enemy && side == EffectSide.Ally)
                        r.Error(w, "habilidade com alvo inimigo não pode ter efeito do lado ally");
                    if (s.Targeting == Targeting.Ally && side == EffectSide.Enemy)
                        r.Error(w, "habilidade com alvo aliado não pode ter efeito do lado enemy");
                    if (s.Targeting == Targeting.Self && side == EffectSide.Enemy)
                        r.Error(w, "habilidade 'self' não pode ter efeito do lado enemy");
                }

                if (s.OwnerType == SkillOwner.Class)
                {
                    if (!d.Classes.ContainsKey(s.OwnerId ?? "")) r.Error(w, "classe dona desconhecida " + s.OwnerId);
                    if (s.Slot < 1 || s.Slot > d.Rules.SkillsPerClass)
                    {
                        r.Error(w, "slot " + s.Slot + " fora de [1," + d.Rules.SkillsPerClass + "]");
                    }
                    else
                    {
                        long expected = 0;
                        if (s.Slot > d.Rules.FreeSkillSlots)
                        {
                            long paid;
                            if (!d.Rules.PaidSkillGoldCost.TryGetValue(s.Slot.ToString(), out paid))
                                r.Error(w, "rules.paidSkillGoldCost sem entrada para o slot " + s.Slot);
                            expected = paid;
                        }
                        if (s.GoldCost != expected)
                            r.Error(w, "goldCost " + s.GoldCost + " != esperado " + expected + " para o slot " + s.Slot);
                    }
                }
                else
                {
                    if (!d.Monsters.ContainsKey(s.OwnerId ?? "")) r.Error(w, "monstro dono desconhecido " + s.OwnerId);
                    if (s.GoldCost != 0) r.Error(w, "habilidade de monstro não pode ter custo");
                }
            }

            // slot repetido dentro de uma classe
            foreach (var group in d.Skills.Values.Where(s => s.OwnerType == SkillOwner.Class).GroupBy(s => s.OwnerId))
            {
                foreach (var dup in group.GroupBy(s => s.Slot).Where(g => g.Count() > 1))
                    r.Error("class " + group.Key, "slot " + dup.Key + " repetido");
            }
        }

        private static void ValidateClassSkillLinks(GameData d, ValidationReport r)
        {
            foreach (var c in d.Classes.Values)
            {
                if (c.SkillIds.Distinct().Count() != c.SkillIds.Count) r.Error("class " + c.Id, "skillIds com repetição");
                if (c.SkillIds.Count > d.Rules.SkillsPerClass)
                    r.Error("class " + c.Id, "mais de " + d.Rules.SkillsPerClass + " habilidades");
                foreach (var sid in c.SkillIds)
                {
                    SkillDef s;
                    if (!d.Skills.TryGetValue(sid, out s)) r.Error("class " + c.Id, "skillId inexistente " + sid);
                    else if (s.OwnerType != SkillOwner.Class || s.OwnerId != c.Id)
                        r.Error("class " + c.Id, "skill " + sid + " pertence a outro dono");
                }
            }
            foreach (var s in d.Skills.Values)
            {
                ClassDef c;
                if (s.OwnerType == SkillOwner.Class && d.Classes.TryGetValue(s.OwnerId ?? "", out c) && !c.SkillIds.Contains(s.Id))
                    r.Error("skill " + s.Id, "não listada em classes[" + s.OwnerId + "].skillIds");
            }
        }

        // ----------------------------------------------------------------- itens
        private static void ValidateItems(GameData d, ValidationReport r)
        {
            foreach (var it in d.Items.Values)
            {
                string w = "item " + it.Id;
                if (!d.Rarities.ContainsKey(it.Rarity ?? "")) r.Error(w, "raridade desconhecida " + it.Rarity);
                if (it.MaxStack < 1) r.Error(w, "maxStack deve ser >= 1");
                if (it.SellValue < 0) r.Error(w, "sellValue negativo");
                foreach (var st in it.Stats.Keys)
                    if (!d.IsKnownStat(st)) r.Error(w, "stat desconhecido " + st);
                foreach (var cid in it.AllowedClasses)
                    if (!d.Classes.ContainsKey(cid)) r.Error(w, "classe permitida desconhecida " + cid);
                if (it.Type == ItemType.Equipment)
                {
                    if (!d.Rules.EquipmentSlots.Contains(it.Slot ?? "")) r.Error(w, "slot inválido '" + it.Slot + "'");
                    if (it.MaxStack != 1) r.Error(w, "equipamento deve ter maxStack 1");
                    if (it.Slot == "weapon" && string.IsNullOrEmpty(it.WeaponType)) r.Error(w, "arma sem weaponType");
                    if (it.Stats.Count == 0) r.Warning(w, "equipamento sem atributos");
                }
                else if (!string.IsNullOrEmpty(it.Slot) || it.Stats.Count > 0)
                {
                    r.Error(w, "material não deve ter slot/stats");
                }
                if (!string.IsNullOrEmpty(it.SetId) && !d.Sets.ContainsKey(it.SetId))
                    r.Error(w, "setId desconhecido " + it.SetId);
            }
            foreach (var c in d.Classes.Values)
            {
                foreach (var iid in c.StartingItems)
                {
                    ItemDef it;
                    if (!d.Items.TryGetValue(iid, out it)) r.Error("class " + c.Id, "item inicial inexistente " + iid);
                    else if (it.Type != ItemType.Equipment || it.RequiredLevel > 1)
                        r.Error("class " + c.Id, "item inicial " + iid + " inválido (precisa ser equipamento de nível 1)");
                    else if (it.Slot == "weapon" && !c.WeaponTypes.Contains(it.WeaponType ?? ""))
                        r.Error("class " + c.Id, "arma inicial " + iid + " incompatível com a classe");
                }
            }
        }

        private static void ValidateSets(GameData d, ValidationReport r)
        {
            foreach (var st in d.Sets.Values)
            {
                string w = "set " + st.Id;
                foreach (var p in st.Pieces)
                {
                    ItemDef it;
                    if (!d.Items.TryGetValue(p, out it)) r.Error(w, "peça inexistente " + p);
                    else if (it.SetId != st.Id) r.Error(w, "peça " + p + " não declara setId");
                }
                int last = 1;
                foreach (var b in st.Bonuses)
                {
                    if (b.Pieces < 2 || b.Pieces > st.Pieces.Count || b.Pieces <= last)
                        r.Error(w, "limiares de peças devem ser crescentes dentro de [2," + st.Pieces.Count + "]");
                    last = b.Pieces;
                    foreach (var k in b.Stats.Keys)
                        if (!d.IsKnownStat(k)) r.Error(w, "stat desconhecido " + k);
                }
            }
        }

        // ------------------------------------------------------------------ loot
        private static void ValidateLoot(GameData d, ValidationReport r)
        {
            foreach (var t in d.LootTables.Values)
            {
                string w = "loot " + t.Id;
                if (t.GoldMin < 0 || t.GoldMax < t.GoldMin) r.Error(w, "faixa de ouro inválida");
                if (t.MinContributionShare.HasValue && (t.MinContributionShare.Value < 0 || t.MinContributionShare.Value > 1))
                    r.Error(w, "minContributionShare fora de [0,1]");
                foreach (var k in t.RarityCapsPerKill.Keys)
                    if (!d.Rarities.ContainsKey(k)) r.Error(w, "raridade desconhecida em rarityCapsPerKill: " + k);
                foreach (var e in t.Entries)
                {
                    ItemDef it;
                    if (!d.Items.TryGetValue(e.ItemId ?? "", out it)) r.Error(w, "item inexistente " + e.ItemId);
                    if (e.Chance <= 0 || e.Chance > 1) r.Error(w, "chance fora de (0,1] em " + e.ItemId);
                    if (e.Min < 1 || e.Max < e.Min) r.Error(w, "quantidade inválida em " + e.ItemId);
                    if (it != null && e.Max > it.MaxStack) r.Error(w, "quantidade máxima excede maxStack em " + e.ItemId);
                }
            }
        }

        // -------------------------------------------------------------- monstros
        private static void ValidateMonsters(GameData d, ValidationReport r)
        {
            var expected = new List<string>(d.Rules.ExpectedMonsterCategoryIds);
            expected.Add(d.Rules.BossCategoryId);
            foreach (var id in expected)
                if (!d.MonsterCategories.ContainsKey(id ?? "")) r.Error("monsters.categories", "categoria esperada ausente: " + id);
            foreach (var c in d.MonsterCategories.Values)
                if (string.IsNullOrEmpty(c.Tag)) r.Error("monsters.categories", c.Id + " sem tag textual");

            foreach (var m in d.Monsters.Values)
            {
                string w = "monster " + m.Id;
                if (!d.MonsterCategories.ContainsKey(m.CategoryId ?? "")) r.Error(w, "categoria desconhecida " + m.CategoryId);
                if (m.Level < 1 || m.Level > d.Rules.MaxLevel) r.Error(w, "nível inválido");
                if (!d.LootTables.ContainsKey(m.LootTableId ?? "")) r.Error(w, "tabela de loot inexistente " + m.LootTableId);
                if (m.LeashRange < m.AggroRange) r.Error(w, "leashRange menor que aggroRange");
                if (m.MoveSpeed <= 0) r.Error(w, "moveSpeed deve ser > 0");
                if (m.SkillIds.Count == 0) r.Error(w, "monstro sem habilidades");
                foreach (var sid in m.SkillIds.Concat(m.Phases.SelectMany(p => p.SkillIds)))
                {
                    SkillDef s;
                    if (!d.Skills.TryGetValue(sid, out s)) r.Error(w, "habilidade inexistente " + sid);
                    else if (s.OwnerType != SkillOwner.Monster || s.OwnerId != m.Id) r.Error(w, "habilidade " + sid + " pertence a outro dono");
                }
                foreach (var k in m.CcResist.Keys)
                    if (!ControlKeys.Contains(k)) r.Error(w, "ccResist com tipo desconhecido " + k);
                double last = double.NaN;
                for (int i = 0; i < m.Phases.Count; i++)
                {
                    var p = m.Phases[i];
                    if (i == 0 && Math.Abs(p.HpBelowPercent - 100) > 1e-9) r.Error(w, "a primeira fase deve começar em 100%");
                    if (!double.IsNaN(last) && p.HpBelowPercent >= last) r.Error(w, "fases devem ter limiares decrescentes");
                    last = p.HpBelowPercent;
                }
                if (m.CategoryId == d.Rules.BossCategoryId && m.Phases.Count < 1)
                    r.Error(w, "chefe regional deve ter ao menos uma fase");
            }
        }

        // --------------------------------------------------------------- regiões
        private static void ValidateRegions(GameData d, ValidationReport r)
        {
            if (d.Regions.Count != d.Rules.ExpectedRegionCount)
                r.Error("regions", "esperadas " + d.Rules.ExpectedRegionCount + " regiões, encontradas " + d.Regions.Count);
            var orders = d.Regions.Values.Select(x => x.Order).OrderBy(x => x).ToList();
            for (int i = 0; i < orders.Count; i++)
                if (orders[i] != i + 1) { r.Error("regions", "campo 'order' deve ser 1..N sem repetição"); break; }

            foreach (var reg in d.Regions.Values)
            {
                string w = "region " + reg.Id;
                if (reg.MinLevel > reg.MaxLevel || reg.MaxLevel > d.Rules.MaxLevel) r.Error(w, "faixa de níveis inválida");
                if (!reg.Implemented) continue;
                var b = reg.Bounds;
                var sp = reg.PlayerSpawn;
                if (b == null || sp == null) { r.Error(w, "região implementada precisa de bounds e playerSpawn"); continue; }
                if (sp.X < b.MinX || sp.X > b.MaxX || sp.Z < b.MinZ || sp.Z > b.MaxZ) r.Error(w, "playerSpawn fora dos limites");
                bool bossFound = false;
                foreach (var z in reg.SpawnZones)
                {
                    MonsterDef m;
                    if (!d.Monsters.TryGetValue(z.MonsterId ?? "", out m)) { r.Error(w, "zona " + z.Id + " com monstro inexistente"); continue; }
                    if (m.RegionId != reg.Id) r.Error(w, "monstro " + m.Id + " pertence a outra região");
                    if (z.Count < 1 || z.Radius <= 0 || z.RespawnSeconds <= 0) r.Error(w, "zona " + z.Id + " com count/radius/respawn inválido");
                    if (z.CenterX < b.MinX || z.CenterX > b.MaxX || z.CenterZ < b.MinZ || z.CenterZ > b.MaxZ)
                        r.Error(w, "zona " + z.Id + " fora dos limites");
                    if (m.CategoryId == d.Rules.BossCategoryId)
                    {
                        bossFound = true;
                        if (Math.Abs(z.RespawnSeconds - d.Rules.RegionalBossRespawnSeconds) > 1e-9)
                            r.Error(w, "chefe regional deve reaparecer em " + d.Rules.RegionalBossRespawnSeconds + "s");
                        if (m.Name != reg.BossName) r.Error(w, "bossName difere do nome do monstro");
                    }
                }
                if (!bossFound) r.Error(w, "região implementada sem chefe regional");
                foreach (var o in reg.Obstacles)
                {
                    if (o.X < b.MinX || o.X > b.MaxX || o.Z < b.MinZ || o.Z > b.MaxZ) r.Error(w, "obstáculo fora dos limites");
                    double dx = o.X - sp.X, dz = o.Z - sp.Z;
                    if (Math.Sqrt(dx * dx + dz * dz) < o.Radius + 1.0) r.Error(w, "obstáculo sobre o ponto de nascimento");
                }
            }
        }

        // ---------------------------------------------------- progresso do escopo
        private static void AddProgressWarnings(GameData d, ValidationReport r)
        {
            int without = d.Classes.Values.Count(c => c.SkillIds.Count == 0);
            if (without > 0) r.Warning("classes", without + " classes ainda sem habilidades implementadas");
            int partial = d.Classes.Values.Count(c => c.SkillIds.Count > 0 && c.SkillIds.Count < d.Rules.SkillsPerClass);
            if (partial > 0) r.Warning("classes", partial + " classes com habilidades parciais");
        }
    }
}
