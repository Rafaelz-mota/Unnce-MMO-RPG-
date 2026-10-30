#!/usr/bin/env python3
"""Gera o catálogo da loja (itens, aparência e shops.json) a partir de tabelas abaixo.
Idempotente: remove os itens 'loja_*' gerados antes e escreve de novo. Uso: python3 tools/generate_shop_catalog.py"""
import json, os, sys
G = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "client", "Assets", "Resources", "GameData")

TIERS = [  # nível, raridade, adjetivo (masc., fem.)
    (1, "common", "do Aprendiz", "de Aprendiz"), (10, "common", "do Soldado", "de Soldado"),
    (20, "rare", "do Veterano", "de Veterano"), (30, "rare", "do Mestre", "de Mestre"),
    (40, "epic", "do Campeão", "de Campeão"), (50, "epic", "do Herói", "de Herói")]
RMULT = {"common": 1.0, "rare": 1.15, "epic": 1.3}
RPRICE = {"common": 1, "rare": 2, "epic": 4}
TIER_COLOR = ["#8A7A6A", "#A8B0BC", "#6FA8FF", "#5BD6A0", "#BA68C8", "#FFB74D"]
TIER_GLOW = [None, None, "#6FA8FF", "#7DFF9A", "#D79BFF", "#FFD27A"]

# tipo: (nome, gênero, magia?, multiplicador, atributo secundário, shape de aparência, mão)
WEAPONS = {
    "sword": ("Espada", "f", False, 1.0, "strength", "sword", "hand_r"),
    "axe": ("Machado", "m", False, 1.1, "strength", "axe", "hand_r"),
    "mace": ("Maça", "f", False, 1.0, "vitality", "mace", "hand_r"),
    "spear": ("Lança", "f", False, 1.0, "agility", "spear", "hand_r"),
    "dagger": ("Adaga", "f", False, 0.85, "critChance", "dagger", "hand_r"),
    "bow": ("Arco", "m", False, 1.0, "agility", "bow", "hand_l"),
    "crossbow": ("Besta", "f", False, 1.05, "agility", "bow", "hand_l"),
    "wrench": ("Chave Inglesa", "f", False, 0.95, "intellect", "mace", "hand_r"),
    "rifle": ("Rifle", "m", False, 1.1, "agility", "stick", "hand_r"),
    "pistol": ("Pistola", "f", False, 0.9, "critChance", "stick", "hand_r"),
    "whip": ("Chicote", "m", False, 0.9, "agility", "stick", "hand_r"),
    "fist": ("Manopla", "f", False, 0.9, "agility", "stick", "hand_r"),
    "staff": ("Cajado", "m", True, 1.0, "intellect", "staff", "hand_r"),
    "wand": ("Varinha", "f", True, 0.9, "critChance", "wand", "hand_r"),
    "scepter": ("Cetro", "m", True, 0.9, "spirit", "scepter", "hand_r"),
    "scythe": ("Foice", "f", True, 1.1, "vitality", "spear", "hand_r"),
    "lute": ("Alaúde", "m", True, 0.9, "spirit", "stick", "hand_r"),
}
ARMOR_CLASSES = {
    "placa": ["guerreiro", "guardiao", "paladino", "cavaleiro_da_morte", "berserker", "lanceiro"],
    "couro": ["monge", "arqueiro", "assassino", "lamina_sombria", "atirador", "domador_de_feras", "engenheiro", "bardo"],
    "pano": ["mago", "mago_runico", "sacerdote", "druida", "bruxo", "necromante", "ilusionista", "invocador", "clerigo", "alquimista"],
}
ARMOR_NAME = {"placa": {"head": ("Elmo", "m"), "chest": ("Peitoral", "m"), "legs": ("Grevas", "f"), "feet": ("Botas de Placa", "f")},
              "couro": {"head": ("Capuz de Couro", "m"), "chest": ("Casaco de Couro", "m"), "legs": ("Calças de Couro", "f"), "feet": ("Botas de Couro", "f")},
              "pano": {"head": ("Capuz de Tecido", "m"), "chest": ("Túnica", "f"), "legs": ("Calções de Tecido", "m"), "feet": ("Sapatilhas", "f")}}
ARMOR_SHAPE = {"placa": {"head": "helm", "chest": "plate", "legs": "pants", "feet": "boots"},
               "couro": {"head": "hood", "chest": "tunic", "legs": "pants", "feet": "boots"},
               "pano": {"head": "hood", "chest": "tunic", "legs": "pants", "feet": "boots"}}
SLOT_W = {"head": 0.8, "chest": 1.2, "legs": 1.0, "feet": 0.6}
SLOT_PRICE = {"head": 1.0, "chest": 1.4, "legs": 1.1, "feet": 0.8}
NECKLACES = [("Colar de Vitalidade", {"maxHp": 12, "hpRegen": 0.15}), ("Colar do Estudioso", {"magicAttack": 1.0, "maxMana": 8}),
             ("Colar do Guerreiro", {"physAttack": 1.0, "physDefense": 0.5})]
RINGS = [("Anel de Força", {"strength": 1}), ("Anel de Intelecto", {"intellect": 1}), ("Anel de Agilidade", {"agility": 1, "critChance": 0.002}),
         ("Anel de Espírito", {"spirit": 1, "manaRegen": 0.1})]


def r5(x):
    return max(5, int(round(x / 5.0)) * 5)


def price(level, rar, w=1.0):
    return r5(8 * (level + 2) ** 1.6 * RPRICE[rar] * w)


def main():
    items = json.load(open(os.path.join(G, "items.json"), encoding="utf-8"))
    pres = json.load(open(os.path.join(G, "presentation.json"), encoding="utf-8"))
    items["items"] = [i for i in items["items"] if not i["id"].startswith("loja_")]
    pres["items"] = [i for i in pres["items"] if not i["id"].startswith("loja_")]
    stock = []

    def add(it, p, look=None):
        items["items"].append(it)
        stock.append({"itemId": it["id"], "price": p})
        if look:
            pres["items"].append(look)

    for ti, (lvl, rar, adj_m, adj_f) in enumerate(TIERS):
        rm = RMULT[rar]
        sec = 1 + lvl // 10
        for wt, (nm, g, magic, wm, sstat, shape, hand) in WEAPONS.items():
            atk = round((3 + 0.75 * lvl) * rm * wm)
            stats = {("magicAttack" if magic else "physAttack"): atk}
            if sstat == "critChance":
                stats["critChance"] = round(0.01 + 0.002 * ti, 3)
            else:
                stats[sstat] = sec
            if magic:
                stats["maxMana"] = 8 + 3 * lvl // 2
            name = nm + " " + (adj_m if g == "m" else adj_f)
            wid = "loja_%s_t%d" % (wt, ti + 1)
            p = price(lvl, rar, 1.3)
            it = {"id": wid, "name": name, "type": "equipment", "slot": "weapon", "rarity": rar, "requiredLevel": lvl,
                  "weaponType": wt, "allowedClasses": [], "stats": stats, "setId": None, "maxStack": 1,
                  "sellValue": p // 4, "tradable": True, "origin": "loja", "description": ""}
            look = {"id": wid, "shape": shape, "color": TIER_COLOR[ti], "socket": hand}
            if TIER_GLOW[ti]:
                look["glow"] = TIER_GLOW[ti]
            add(it, p, look)
        for at, classes in ARMOR_CLASSES.items():
            for slot in ("head", "chest", "legs", "feet"):
                d = round((1 + 0.45 * lvl) * rm * SLOT_W[slot] * (1.5 if at == "placa" else 1.0 if at == "couro" else 0.5)) or 1
                if at == "placa":
                    stats = {"physDefense": d, "maxHp": round(6 + 0.9 * lvl * SLOT_W[slot]), "strength": sec if slot == "chest" else 0}
                elif at == "couro":
                    stats = {"physDefense": d, "agility": sec if slot != "feet" else 0, "maxHp": round(4 + 0.5 * lvl * SLOT_W[slot])}
                    if slot == "feet":
                        stats["moveSpeed"] = round(0.1 + 0.02 * ti, 2)
                else:
                    stats = {"physDefense": d, "magicResist": round((1 + 0.45 * lvl) * rm * SLOT_W[slot] * 1.2) or 1,
                             "maxMana": round(4 + 0.7 * lvl * SLOT_W[slot]), "intellect": sec if slot == "chest" else 0}
                stats = {k: v for k, v in stats.items() if v}
                an, ag = ARMOR_NAME[at][slot]
                name = an + " " + (adj_m if ag == "m" else adj_f)
                aid = "loja_%s_%s_t%d" % (at, slot, ti + 1)
                p = price(lvl, rar, SLOT_PRICE[slot])
                it = {"id": aid, "name": name, "type": "equipment", "slot": slot, "rarity": rar, "requiredLevel": lvl,
                      "weaponType": None, "allowedClasses": classes, "stats": stats, "setId": None, "maxStack": 1,
                      "sellValue": p // 4, "tradable": True, "origin": "loja", "description": ""}
                look = {"id": aid, "shape": ARMOR_SHAPE[at][slot], "color": TIER_COLOR[ti]}
                if TIER_GLOW[ti]:
                    look["glow"] = TIER_GLOW[ti]
                add(it, p, look)
        for slot, defs, key in (("necklace", NECKLACES, "colar"), ("ring", RINGS, "anel")):
            for k, (nm, base) in enumerate(defs):
                stats = {s: (round(v * (1 + lvl // 5 * 0.6) * rm, 2) if s in ("maxHp", "maxMana", "physAttack", "magicAttack", "physDefense") else
                             round(v * (1 + lvl / 12.0) * rm, 3 if s in ("critChance",) else 2)) for s, v in base.items()}
                for s in list(stats):
                    if s in ("strength", "intellect", "agility", "spirit"):
                        stats[s] = max(1, round(stats[s]))
                    elif s in ("maxHp", "maxMana", "physAttack", "magicAttack"):
                        stats[s] = max(1, round(stats[s]))
                jid = "loja_%s_%d_t%d" % (key, k + 1, ti + 1)
                p = price(lvl, rar, 1.6)
                it = {"id": jid, "name": nm + " " + adj_m,
                      "type": "equipment", "slot": slot, "rarity": rar, "requiredLevel": lvl, "weaponType": None, "allowedClasses": [],
                      "stats": stats, "setId": None, "maxStack": 1, "sellValue": p // 4, "tradable": True, "origin": "loja", "description": ""}
                add(it, p)

    json.dump(items, open(os.path.join(G, "items.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    json.dump(pres, open(os.path.join(G, "presentation.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    for fn in ("items.json", "presentation.json"):
        open(os.path.join(G, fn), "a").write("\n")
    shops = {"schemaVersion": 1, "shops": [{
        "id": "mercador_do_vale", "name": "Mercador do Vale",
        "description": "Armas, armaduras, colares e anéis de todas as faixas de nível.", "stock": stock}]}
    json.dump(shops, open(os.path.join(G, "shops.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    open(os.path.join(G, "shops.json"), "a").write("\n")
    print("itens gerados:", len(stock))


if __name__ == "__main__":
    main()
