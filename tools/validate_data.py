#!/usr/bin/env python3
"""Validador independente dos dados de jogo (JSON).

Replica, em Python, as regras principais do validador C# (GameDataValidator)
para rodar em qualquer máquina e no CI sem .NET/Unity.

Uso:  python3 tools/validate_data.py [pasta_dos_json]
Saída: código 0 se não houver erros (avisos não falham); 1 se houver erros.

IMPORTANTE: este script valida CONSISTÊNCIA dos dados. Não é mecanismo de
segurança: em jogo online, o servidor valida combate, inventário, economia etc.
"""
import json
import os
import sys

DEFAULT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..",
                           "client", "Assets", "Resources", "GameData")
FILES = ["rules", "attributes", "rarities", "statuses", "combat", "progression", "classes",
         "skills", "items", "sets", "monsters", "loot_tables", "regions"]
SUPPORTED_SCHEMA = 1

errors, warnings = [], []


def err(where, msg):
    errors.append("[ERRO] %s: %s" % (where, msg))


def warn(where, msg):
    warnings.append("[AVISO] %s: %s" % (where, msg))


def load(folder):
    data = {}
    for name in FILES:
        path = os.path.join(folder, name + ".json")
        try:
            with open(path, encoding="utf-8") as f:
                data[name] = json.load(f)
        except FileNotFoundError:
            err(name + ".json", "arquivo não encontrado")
        except json.JSONDecodeError as e:
            err(name + ".json", "JSON inválido: %s" % e)
            continue
        if name in data and data[name].get("schemaVersion") != SUPPORTED_SCHEMA:
            err(name + ".json", "schemaVersion %r não suportado (esperado %d)" % (
                data[name].get("schemaVersion"), SUPPORTED_SCHEMA))
    return data


def unique_ids(items, where, key="id"):
    seen = set()
    for it in items:
        i = it.get(key)
        if not i:
            err(where, "entrada sem '%s'" % key)
        elif i in seen:
            err(where, "id duplicado: %s" % i)
        seen.add(i)
    return seen


def main():
    folder = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_DIR
    d = load(folder)
    if errors:
        return finish(d)

    rules = d["rules"]
    primary = {p["id"] for p in d["attributes"]["primary"]}
    derived = {x["id"] for x in d["attributes"]["derived"]}
    stat_ids = primary | derived
    resources = {r["id"] for r in d["attributes"]["resources"]}
    for r in d["attributes"]["resources"]:
        if r.get("maxStat") and r["maxStat"] not in derived:
            err("attributes", "resource %s: maxStat desconhecido %s" % (r["id"], r["maxStat"]))
        if r.get("regenStat") and r["regenStat"] not in derived:
            err("attributes", "resource %s: regenStat desconhecido %s" % (r["id"], r["regenStat"]))
    for x in d["attributes"]["derived"]:
        for p in x.get("perPrimary", {}):
            if p not in primary:
                err("attributes", "derived %s: atributo primário desconhecido %s" % (x["id"], p))
    for must in ["maxHp", "physAttack", "magicAttack", "physDefense", "magicResist", "critChance",
                 "critMultiplier", "moveSpeed", "hpRegen"]:
        if must not in derived:
            err("attributes", "atributo derivado obrigatório ausente: " + must)

    # raridades
    rar_ids = unique_ids(d["rarities"]["rarities"], "rarities")
    if sorted(rar_ids) != sorted(rules["expectedRarityIds"]):
        err("rarities", "raridades esperadas %s, encontradas %s" % (rules["expectedRarityIds"], sorted(rar_ids)))
    orders = [r["order"] for r in d["rarities"]["rarities"]]
    if len(set(orders)) != len(orders):
        err("rarities", "campo 'order' duplicado")
    for r in d["rarities"]["rarities"]:
        if not r.get("tag"):
            err("rarities", "%s sem 'tag' textual (não depender só de cor)" % r["id"])

    # status
    status = {s["id"]: s for s in d["statuses"]["statuses"]}
    unique_ids(d["statuses"]["statuses"], "statuses")
    for s in status.values():
        if s["kind"] in ("dot", "hot") and s.get("tickInterval", 0) <= 0:
            err("statuses", "%s: dot/hot exige tickInterval > 0" % s["id"])
        if s["kind"] == "slow" and not (0 < s.get("moveSpeedMultiplier", 1) < 1):
            err("statuses", "%s: slow exige moveSpeedMultiplier em (0,1)" % s["id"])
        for k in s.get("statMods", {}):
            if k not in stat_ids:
                err("statuses", "%s: statMod desconhecido %s" % (s["id"], k))

    # combate / progressão
    c = d["combat"]
    if not (0 <= c["mitigation"]["maxMitigation"] < 1):
        err("combat", "maxMitigation deve estar em [0,1)")
    if c["control"]["minMoveSpeedMultiplier"] <= 0:
        err("combat", "minMoveSpeedMultiplier deve ser > 0")
    if c.get("bodyRadius", 0) <= 0:
        err("combat", "bodyRadius deve ser > 0")
    ai = c.get("ai", {})
    if ai.get("evadeSpeedMultiplier", 0) < 1:
        err("combat", "ai.evadeSpeedMultiplier deve ser >= 1")
    if ai.get("evadeHealPercentPerSecond", 0) <= 0:
        err("combat", "ai.evadeHealPercentPerSecond deve ser > 0")
    if not (0 < ai.get("attackRangeFraction", 0) <= 1):
        err("combat", "ai.attackRangeFraction deve estar em (0,1]")
    if ai.get("targetSwitchThreatRatio", 0) < 1:
        err("combat", "ai.targetSwitchThreatRatio deve ser >= 1")
    if d["progression"]["maxLevel"] != rules["maxLevel"]:
        err("progression", "maxLevel difere de rules.maxLevel")

    # classes
    classes = {k["id"]: k for k in d["classes"]["classes"]}
    unique_ids(d["classes"]["classes"], "classes")
    faction_ids = {f["id"] for f in rules["factions"]}
    if len(classes) != rules["expectedClassCount"]:
        err("classes", "esperadas %d classes, encontradas %d" % (rules["expectedClassCount"], len(classes)))
    per_faction = {}
    for k in classes.values():
        if k["faction"] not in faction_ids:
            err("classes", "%s: facção desconhecida %s" % (k["id"], k["faction"]))
        per_faction[k["faction"]] = per_faction.get(k["faction"], 0) + 1
        if k["resource"] not in resources:
            err("classes", "%s: recurso desconhecido %s" % (k["id"], k["resource"]))
        for fld in ("primaryBase", "primaryPerLevel"):
            if set(k[fld]) != primary:
                err("classes", "%s.%s deve definir exatamente os atributos primários" % (k["id"], fld))
        if not k["weaponTypes"]:
            err("classes", "%s: sem weaponTypes" % k["id"])
    for f in faction_ids:
        if per_faction.get(f, 0) != rules["classesPerFaction"]:
            err("classes", "facção %s deve ter %d classes (tem %d)" % (f, rules["classesPerFaction"], per_faction.get(f, 0)))

    # monstros (categorias antes de skills)
    cats = {m["id"]: m for m in d["monsters"]["categories"]}
    unique_ids(d["monsters"]["categories"], "monsters.categories")
    for e in rules["expectedMonsterCategoryIds"] + [rules["bossCategoryId"]]:
        if e not in cats:
            err("monsters.categories", "categoria esperada ausente: " + e)
    for m in cats.values():
        if not m.get("tag"):
            err("monsters.categories", "%s sem tag textual" % m["id"])
    monsters = {m["id"]: m for m in d["monsters"]["monsters"]}
    unique_ids(d["monsters"]["monsters"], "monsters")

    # habilidades
    skills = {s["id"]: s for s in d["skills"]["skills"]}
    unique_ids(d["skills"]["skills"], "skills")
    targetings = {"enemy", "ally", "self", "ground", "selfArea"}
    kinds = {"damage", "heal", "applyStatus", "shield", "dash"}
    sides = {"enemy", "ally", "self"}
    class_slots = {}
    for s in skills.values():
        w = "skill " + s["id"]
        if s["targeting"] not in targetings:
            err(w, "targeting inválido " + s["targeting"])
        for f in ("cooldown", "castTime", "resourceCost", "range", "radius"):
            if s[f] < 0:
                err(w, "%s não pode ser negativo" % f)
        if s["targeting"] in ("ground", "selfArea") and s["radius"] <= 0:
            err(w, "habilidade de área exige radius > 0")
        if s["targeting"] in ("enemy", "ally", "ground") and s["range"] <= 0:
            err(w, "habilidade com alvo exige range > 0")
        if s["maxTargets"] < 1:
            err(w, "maxTargets deve ser >= 1")
        if not s["effects"]:
            err(w, "sem efeitos")
        if not (1 <= s["requiresLevel"] <= rules["maxLevel"]):
            err(w, "requiresLevel fora de [1,%d]" % rules["maxLevel"])
        for e in s["effects"]:
            k = e["kind"]
            side = e.get("side") or {"damage": "enemy", "heal": "ally", "shield": "ally",
                                      "applyStatus": "enemy", "dash": "self"}[k]
            if k not in kinds:
                err(w, "efeito inválido " + k)
            if side not in sides:
                err(w, "side inválido " + side)
            for st in e.get("scaling", {}):
                if st not in stat_ids:
                    err(w, "scaling usa atributo desconhecido " + st)
            if k == "damage" and e["base"] + sum(e["scaling"].values()) <= 0:
                err(w, "dano sem valor")
            if k in ("applyStatus", "shield"):
                sd = status.get(e.get("statusId"))
                if not sd:
                    err(w, "statusId desconhecido %r" % e.get("statusId"))
                else:
                    if k == "shield" and sd["kind"] != "shield":
                        err(w, "efeito shield exige status do tipo shield")
                    if k == "applyStatus" and sd["kind"] in ("dot", "hot", "shield") and \
                            e["base"] + sum(e["scaling"].values()) <= 0:
                        err(w, "dot/hot/shield sem valor (base/scaling)")
            if k == "dash" and (e.get("dashDistance", 0) <= 0 or e.get("dashDirection") not in ("toward", "away")):
                err(w, "dash exige dashDistance > 0 e dashDirection toward/away")
            if s["targeting"] == "enemy" and side == "ally":
                err(w, "habilidade com alvo inimigo não pode ter efeito do lado ally")
            if s["targeting"] == "ally" and side == "enemy":
                err(w, "habilidade com alvo aliado não pode ter efeito do lado enemy")
            if s["targeting"] == "self" and side == "enemy":
                err(w, "habilidade 'self' não pode ter efeito do lado enemy")
        if s["ownerType"] == "class":
            if s["ownerId"] not in classes:
                err(w, "classe dona desconhecida " + str(s["ownerId"]))
            slot = s["slot"]
            if not (1 <= slot <= rules["skillsPerClass"]):
                err(w, "slot %d fora de [1,%d]" % (slot, rules["skillsPerClass"]))
            expected = 0 if slot <= rules["freeSkillSlots"] else rules["paidSkillGoldCost"].get(str(slot))
            if s["goldCost"] != expected:
                err(w, "goldCost %s != esperado %s para o slot %d" % (s["goldCost"], expected, slot))
            key = (s["ownerId"], slot)
            if key in class_slots:
                err(w, "slot %d repetido na classe %s" % (slot, s["ownerId"]))
            class_slots[key] = s["id"]
        elif s["ownerType"] == "monster":
            if s["ownerId"] not in monsters:
                err(w, "monstro dono desconhecido " + str(s["ownerId"]))
            if s["goldCost"] != 0:
                err(w, "habilidade de monstro não pode ter custo")
        else:
            err(w, "ownerType inválido " + s["ownerType"])

    implemented = 0
    for k in classes.values():
        ids = k["skillIds"]
        if len(set(ids)) != len(ids):
            err("class " + k["id"], "skillIds com repetição")
        for sid in ids:
            sk = skills.get(sid)
            if not sk:
                err("class " + k["id"], "skillId inexistente " + sid)
            elif sk["ownerType"] != "class" or sk["ownerId"] != k["id"]:
                err("class " + k["id"], "skill %s pertence a outro dono" % sid)
            else:
                implemented += 1
        if len(ids) > rules["skillsPerClass"]:
            err("class " + k["id"], "mais de %d habilidades" % rules["skillsPerClass"])
    for s in skills.values():
        if s["ownerType"] == "class" and s["ownerId"] in classes and s["id"] not in classes[s["ownerId"]]["skillIds"]:
            err("skill " + s["id"], "não listada em classes[%s].skillIds" % s["ownerId"])

    # itens
    items = {i["id"]: i for i in d["items"]["items"]}
    unique_ids(d["items"]["items"], "items")
    sets = {x["id"]: x for x in d["sets"]["sets"]}
    unique_ids(d["sets"]["sets"], "sets")
    for it in items.values():
        w = "item " + it["id"]
        if it["rarity"] not in rar_ids:
            err(w, "raridade desconhecida " + it["rarity"])
        if it["maxStack"] < 1:
            err(w, "maxStack deve ser >= 1")
        if it["sellValue"] < 0:
            err(w, "sellValue negativo")
        for st in it["stats"]:
            if st not in stat_ids:
                err(w, "stat desconhecido " + st)
        for cid in it["allowedClasses"]:
            if cid not in classes:
                err(w, "classe permitida desconhecida " + cid)
        if it["type"] == "equipment":
            if it["slot"] not in rules["equipmentSlots"]:
                err(w, "slot inválido %r" % it["slot"])
            if it["maxStack"] != 1:
                err(w, "equipamento deve ter maxStack 1")
            if it["slot"] == "weapon" and not it["weaponType"]:
                err(w, "arma sem weaponType")
            if not it["stats"]:
                warn(w, "equipamento sem atributos")
        elif it["type"] == "material":
            if it["slot"] or it["stats"]:
                err(w, "material não deve ter slot/stats")
        else:
            err(w, "type inválido " + it["type"])
        if it.get("setId") and it["setId"] not in sets:
            err(w, "setId desconhecido " + it["setId"])
    for k in classes.values():
        for iid in k["startingItems"]:
            it = items.get(iid)
            if not it:
                err("class " + k["id"], "item inicial inexistente " + iid)
            elif it["type"] != "equipment" or it["requiredLevel"] > 1:
                err("class " + k["id"], "item inicial %s inválido (precisa ser equipamento de nível 1)" % iid)
            elif it["slot"] == "weapon" and it["weaponType"] not in k["weaponTypes"]:
                err("class " + k["id"], "arma inicial %s incompatível com a classe" % iid)
    for st in sets.values():
        for p in st["pieces"]:
            if p not in items:
                err("set " + st["id"], "peça inexistente " + p)
            elif items[p].get("setId") != st["id"]:
                err("set " + st["id"], "peça %s não declara setId" % p)
        last = 1
        for b in st["bonuses"]:
            if not (2 <= b["pieces"] <= len(st["pieces"])) or b["pieces"] <= last:
                err("set " + st["id"], "limiares de peças devem ser crescentes dentro de [2,%d]" % len(st["pieces"]))
            last = b["pieces"]
            for sx in b["stats"]:
                if sx not in stat_ids:
                    err("set " + st["id"], "stat desconhecido " + sx)

    # loot
    tables = {t["id"]: t for t in d["loot_tables"]["tables"]}
    unique_ids(d["loot_tables"]["tables"], "loot_tables")
    for t in tables.values():
        w = "loot " + t["id"]
        if t["goldMin"] < 0 or t["goldMax"] < t["goldMin"]:
            err(w, "faixa de ouro inválida")
        sh = t.get("minContributionShare")
        if sh is not None and not (0 <= sh <= 1):
            err(w, "minContributionShare fora de [0,1]")
        for r in t["rarityCapsPerKill"]:
            if r not in rar_ids:
                err(w, "raridade desconhecida em rarityCapsPerKill: " + r)
        for e in t["entries"]:
            if e["itemId"] not in items:
                err(w, "item inexistente " + e["itemId"])
            if not (0 < e["chance"] <= 1):
                err(w, "chance fora de (0,1] em %s" % e["itemId"])
            if e["min"] < 1 or e["max"] < e["min"]:
                err(w, "quantidade inválida em %s" % e["itemId"])
            it = items.get(e["itemId"])
            if it and e["max"] > it["maxStack"]:
                err(w, "quantidade máxima excede maxStack em %s" % e["itemId"])

    # monstros (detalhes)
    for m in monsters.values():
        w = "monster " + m["id"]
        if m["categoryId"] not in cats:
            err(w, "categoria desconhecida " + m["categoryId"])
        if not (1 <= m["level"] <= rules["maxLevel"]):
            err(w, "nível inválido")
        if m["lootTableId"] not in tables:
            err(w, "tabela de loot inexistente " + m["lootTableId"])
        if m["leashRange"] < m["aggroRange"]:
            err(w, "leashRange menor que aggroRange")
        if m["moveSpeed"] <= 0:
            err(w, "moveSpeed deve ser > 0")
        if not m["skillIds"]:
            err(w, "monstro sem habilidades")
        for sid in m["skillIds"] + [x for p in m["phases"] for x in p["skillIds"]]:
            sk = skills.get(sid)
            if not sk:
                err(w, "habilidade inexistente " + sid)
            elif sk["ownerType"] != "monster" or sk["ownerId"] != m["id"]:
                err(w, "habilidade %s pertence a outro dono" % sid)
        for key in m["ccResist"]:
            if key not in ("stun", "root", "silence", "slow"):
                err(w, "ccResist com tipo desconhecido " + key)
        last = None
        for i, p in enumerate(m["phases"]):
            if i == 0 and p["hpBelowPercent"] != 100:
                err(w, "a primeira fase deve começar em 100%")
            if last is not None and p["hpBelowPercent"] >= last:
                err(w, "fases devem ter limiares decrescentes")
            last = p["hpBelowPercent"]
        if m["categoryId"] == rules["bossCategoryId"] and len(m["phases"]) < 1:
            err(w, "chefe regional deve ter ao menos uma fase")

    # regiões
    regions = {r["id"]: r for r in d["regions"]["regions"]}
    unique_ids(d["regions"]["regions"], "regions")
    if len(regions) != rules["expectedRegionCount"]:
        err("regions", "esperadas %d regiões, encontradas %d" % (rules["expectedRegionCount"], len(regions)))
    orders = sorted(r["order"] for r in regions.values())
    if orders != list(range(1, len(orders) + 1)):
        err("regions", "campo 'order' deve ser 1..N sem repetição")
    impl_regions = 0
    for r in regions.values():
        w = "region " + r["id"]
        if r["minLevel"] > r["maxLevel"] or r["maxLevel"] > rules["maxLevel"]:
            err(w, "faixa de níveis inválida")
        if not r["implemented"]:
            continue
        impl_regions += 1
        b, sp = r["bounds"], r["playerSpawn"]
        if not b or not sp:
            err(w, "região implementada precisa de bounds e playerSpawn")
            continue
        if not (b["minX"] <= sp["x"] <= b["maxX"] and b["minZ"] <= sp["z"] <= b["maxZ"]):
            err(w, "playerSpawn fora dos limites")
        boss_found = False
        for z in r["spawnZones"]:
            m = monsters.get(z["monsterId"])
            if not m:
                err(w, "zona %s com monstro inexistente" % z["id"])
                continue
            if m["regionId"] != r["id"]:
                err(w, "monstro %s pertence a outra região" % m["id"])
            if z["count"] < 1 or z["radius"] <= 0 or z["respawnSeconds"] <= 0:
                err(w, "zona %s com count/radius/respawn inválido" % z["id"])
            if not (b["minX"] <= z["centerX"] <= b["maxX"] and b["minZ"] <= z["centerZ"] <= b["maxZ"]):
                err(w, "zona %s fora dos limites" % z["id"])
            if m["categoryId"] == rules["bossCategoryId"]:
                boss_found = True
                if z["respawnSeconds"] != rules["regionalBossRespawnSeconds"]:
                    err(w, "chefe regional deve reaparecer em %ss" % rules["regionalBossRespawnSeconds"])
                if m["name"] != r["bossName"]:
                    err(w, "bossName difere do nome do monstro")
        if not boss_found:
            err(w, "região implementada sem chefe regional")
        for o in r["obstacles"]:
            if not (b["minX"] <= o["x"] <= b["maxX"] and b["minZ"] <= o["z"] <= b["maxZ"]):
                err(w, "obstáculo fora dos limites: %s" % o)
            if ((o["x"] - sp["x"]) ** 2 + (o["z"] - sp["z"]) ** 2) ** 0.5 < o["radius"] + 1.0:
                err(w, "obstáculo sobre o ponto de nascimento")

    # resumo / pendências
    print("Resumo: %d classes | %d/%d habilidades de classe | %d habilidades de monstros | %d itens | %d monstros | %d/%d regiões implementadas" % (
        len(classes), implemented, rules["expectedClassCount"] * rules["skillsPerClass"],
        sum(1 for s in skills.values() if s["ownerType"] == "monster"), len(items), len(monsters),
        impl_regions, rules["expectedRegionCount"]))
    without = sorted(k for k in classes if not classes[k]["skillIds"])
    if without:
        warn("classes", "%d classes ainda sem habilidades implementadas" % len(without))
    partial = sorted(k for k in classes if 0 < len(classes[k]["skillIds"]) < rules["skillsPerClass"])
    if partial:
        warn("classes", "%d classes com habilidades parciais: %s" % (len(partial), ", ".join(partial)))
    return finish(d)


def finish(_):
    for w in warnings:
        print(w)
    for e in errors:
        print(e)
    print("%d erro(s), %d aviso(s)" % (len(errors), len(warnings)))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
