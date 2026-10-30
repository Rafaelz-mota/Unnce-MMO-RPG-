using System;
using System.Collections.Generic;
using System.IO;
using Mmorpg.Core.Aiming;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Combat;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.Events;
using Mmorpg.Core.Hotbar;
using Mmorpg.Core.Items;
using Mmorpg.Core.Shop;
using Mmorpg.Core.Presentation;
using Mmorpg.Core.World;

namespace Mmorpg.Core.Session
{
    /// <summary>
    /// Uma partida local (protótipo offline): região + personagem + estado de interface (alvo selecionado, log).
    /// É a ÚNICA porta de entrada dos comandos do jogador: a camada Unity só traduz toque/teclado em chamadas daqui,
    /// e tudo é validado por <see cref="CombatWorld"/> e <see cref="PlayerCharacter"/>. No jogo online, estes mesmos
    /// comandos viram mensagens para o servidor, que mantém a simulação autoritativa.
    /// </summary>
    public sealed class GameSession
    {
        /// <summary>Maior passo de simulação aceito por Tick. Evita "espiral da morte" após travadas ou pausas do app.</summary>
        public const double MaxTickSeconds = 0.25;

        private const int MaxLogLines = 60;

        private readonly ICharacterStore _store;
        private readonly List<string> _log = new List<string>();
        private readonly List<GameEvent> _events = new List<GameEvent>();
        private double _lastAutosave;

        public GameData Data { get; private set; }
        public WorldSimulation Sim { get; private set; }
        public PlayerCharacter Character { get; private set; }
        public Combatant Player { get; private set; }

        public int SelectedTargetId { get; private set; }

        /// <summary>Se verdadeiro, habilidades ofensivas sem alvo escolhem o inimigo mais próximo ao alcance.</summary>
        public bool AutoTarget { get; set; }

        /// <summary>Alcance máximo do Tab / "alvo mais próximo" (conveniência de interface, não é regra de combate).</summary>
        public double TabTargetRange { get; set; }

        /// <summary>Segundos entre salvamentos automáticos (0 desliga). Também salva ao subir de nível e ao sair.</summary>
        public double AutosaveSeconds { get; set; }

        /// <summary>Mensagem do último erro de salvamento (null se o último salvamento deu certo).</summary>
        public string LastSaveError { get; private set; }

        public IReadOnlyList<string> Log { get { return _log; } }

        /// <summary>Todo evento da simulação passa por aqui (a Unity usa para números de dano, anéis de área, sons).</summary>
        public event Action<GameEvent> EventRaised;

        private GameSession(GameData data, WorldSimulation sim, PlayerCharacter pc, Combatant player, ICharacterStore store)
        {
            Data = data;
            Sim = sim;
            Character = pc;
            Player = player;
            _store = store;
            AutoTarget = true;
            TabTargetRange = 30.0;
            AutosaveSeconds = 60.0;
        }

        // ------------------------------------------------------------------ criação
        public static GameSession Start(GameData data, PlayerCharacter pc, string regionId, IRng rng, ICharacterStore store)
        {
            if (data == null) throw new ArgumentNullException("data");
            if (pc == null) throw new ArgumentNullException("pc");
            var sim = new WorldSimulation(data, regionId, rng);
            Combatant player = sim.AddPlayer(pc);
            return new GameSession(data, sim, pc, player, store);
        }

        // ------------------------------------------------------------------ passo de simulação
        /// <summary>Avança o jogo. Chame uma vez por frame com o tempo real decorrido.</summary>
        public void Tick(double dt)
        {
            if (double.IsNaN(dt) || double.IsInfinity(dt) || dt <= 0) return;
            if (dt > MaxTickSeconds) dt = MaxTickSeconds;

            Sim.Update(dt);
            bool leveled = false;

            List<GameEvent> fresh = Sim.World.DrainEvents();
            for (int i = 0; i < fresh.Count; i++)
            {
                GameEvent e = fresh[i];
                _events.Add(e);
                if (e.Type == GameEventType.LevelUp && e.TargetId == Player.Id) leveled = true;
                string line = EventLog.Format(Data, e, Player.Id);
                if (line != null) AddLog(line);
                if (EventRaised != null) EventRaised(e);
            }
            if (_events.Count > 512) _events.RemoveRange(0, _events.Count - 512);

            if (SelectedTargetId != 0 && !TargetSelector.IsValid(Sim.World, Player, SelectedTargetId)) SelectedTargetId = 0;

            if (leveled) Save();
            else if (AutosaveSeconds > 0 && Sim.World.Time - _lastAutosave >= AutosaveSeconds) Save();
        }

        private void AddLog(string text)
        {
            _log.Add(text);
            if (_log.Count > MaxLogLines) _log.RemoveAt(0);
        }

        /// <summary>Mensagem de interface (aviso do jogo que não vem da simulação).</summary>
        public void Notify(string text)
        {
            if (!string.IsNullOrEmpty(text)) AddLog(text);
        }

        // ------------------------------------------------------------------ movimento e alvo
        /// <summary>Define a direção de movimento no mundo (X leste, Z norte), comprimento 0..1. O mundo limita e valida.</summary>
        public void SetMove(Vec2 worldDirection)
        {
            Sim.World.SetMoveIntent(Player, worldDirection.X, worldDirection.Z);
        }

        public void SelectTarget(int id)
        {
            SelectedTargetId = TargetSelector.IsValid(Sim.World, Player, id) ? id : 0;
        }

        public void ClearTarget() { SelectedTargetId = 0; }

        /// <summary>Tab: próximo inimigo válido, do mais perto ao mais longe. Devolve o id escolhido (0 se não há).</summary>
        public int CycleTarget()
        {
            SelectedTargetId = TargetSelector.CycleNext(Sim.World, Player, SelectedTargetId, TabTargetRange);
            return SelectedTargetId;
        }

        public Combatant SelectedTarget { get { return Sim.World.Get(SelectedTargetId); } }

        // ------------------------------------------------------------------ habilidades
        public IList<HotbarSlot> Hotbar() { return HotbarModel.Build(Character); }

        /// <summary>
        /// Usa a habilidade do slot 1..9. Se o slot é uma habilidade paga ainda não comprada, NÃO compra
        /// (compra é uma ação explícita: <see cref="LearnSkill"/>), apenas avisa.
        /// </summary>
        public UseSkillResult UseHotbarSlot(int number)
        {
            if (number < 1 || number > Data.Rules.SkillsPerClass) return UseSkillResult.UnknownSkill;
            SkillDef skill = null;
            foreach (SkillDef s in Character.ClassSkills())
                if (s.Slot == number) { skill = s; break; }
            if (skill == null)
            {
                Notify("Esta habilidade ainda não existe nesta versão.");
                return UseSkillResult.UnknownSkill;
            }
            return UseSkill(skill.Id);
        }

        public UseSkillResult UseSkill(string skillId)
        {
            SkillDef skill;
            if (skillId == null || !Data.Skills.TryGetValue(skillId, out skill)) return UseSkillResult.UnknownSkill;

            // Antes de mirar: "bloqueada" é uma resposta mais útil do que "sem alvo".
            if (!Character.IsSkillLearned(skillId))
            {
                AddLog(EventLog.Describe(UseSkillResult.NotLearned));
                return UseSkillResult.NotLearned;
            }

            SkillAim aim = SkillTargeting.Aim(Sim.World, Player, skill, SelectedTargetId, AutoTarget);
            UseSkillResult result = aim.Ok
                ? Sim.World.UseSkill(Player, skillId, aim.TargetId, aim.Ground)
                : aim.Failure;

            if (result == UseSkillResult.Ok && aim.TargetId != 0 && aim.TargetId != Player.Id && SelectedTargetId == 0)
                SelectedTargetId = aim.TargetId; // o alvo escolhido automaticamente fica selecionado
            string message = EventLog.Describe(result);
            if (message != null) AddLog(message);
            return result;
        }

        // ------------------------------------------------------------------ modo de teste (offline)
        /// <summary>
        /// Liga as ferramentas de teste. Só existe no protótipo offline e começa DESLIGADO: quem monta a sessão decide.
        /// Um servidor online nunca deve ligar isto; progressão no online é decidida pelo servidor.
        /// </summary>
        public bool DevToolsEnabled { get; set; }

        /// <summary>Modo de teste: nível máximo, todas as habilidades da classe e ouro extra. Devolve false se desligado.</summary>
        public bool DevUnlockAll()
        {
            if (!DevToolsEnabled) return false;
            int guard = 0;
            while (!Character.IsMaxLevel && guard++ < 1000) Character.AddXp(Character.XpToNext);
            Character.SyncFreeSkills();

            long need = 0;
            foreach (SkillDef s in Character.ClassSkills())
                if (!Character.IsSkillLearned(s.Id)) need += s.GoldCost;
            if (need > Character.Gold) Character.AddGold(need - Character.Gold);
            foreach (SkillDef s in Character.ClassSkills())
                Character.TryLearnSkill(s.Id);
            Character.AddGold(100000);

            Sim.World.RefreshPlayerStats(Player, true);
            AddLog("MODO DE TESTE: nível " + Character.Level + " e todas as habilidades da classe liberadas.");
            return true;
        }

        /// <summary>Compra (com Gold) a habilidade paga do slot 6..9. Os custos vêm de rules.json.</summary>
        public LearnSkillResult LearnSkill(int number)
        {
            SkillDef skill = null;
            foreach (SkillDef s in Character.ClassSkills())
                if (s.Slot == number) { skill = s; break; }
            if (skill == null) return LearnSkillResult.UnknownSkill;

            LearnSkillResult r = Character.TryLearnSkill(skill.Id);
            switch (r)
            {
                case LearnSkillResult.Ok:
                    AddLog("Você aprendeu " + skill.Name + ".");
                    Save();
                    break;
                case LearnSkillResult.NotEnoughGold:
                    AddLog("Ouro insuficiente para " + skill.Name + " (" + skill.GoldCost + ").");
                    break;
                case LearnSkillResult.LevelTooLow:
                    AddLog(skill.Name + " exige nível " + skill.RequiresLevel + ".");
                    break;
            }
            return r;
        }

        // ------------------------------------------------------------------ equipamento
        public EquipResult Equip(int inventorySlot)
        {
            EquipResult r = Character.Equip(inventorySlot);
            if (r == EquipResult.Ok) Sim.World.RefreshPlayerStats(Player, false);
            else AddLog(DescribeEquip(r));
            return r;
        }

        public EquipResult Unequip(string slot)
        {
            EquipResult r = Character.Unequip(slot);
            if (r == EquipResult.Ok) Sim.World.RefreshPlayerStats(Player, false);
            else AddLog(DescribeEquip(r));
            return r;
        }

        // ------------------------------------------------------------------ loja
        /// <summary>Mercador mais próximo dentro do alcance de interação, ou null.</summary>
        public NpcDef NearbyMerchant()
        {
            NpcDef best = null;
            double bestD = double.MaxValue;
            foreach (NpcDef n in Sim.Region.Npcs)
            {
                if (n.Role != "merchant") continue;
                double dist = Vec2.Distance(Player.Position, new Vec2(n.X, n.Z));
                if (dist <= n.InteractRange && dist < bestD) { best = n; bestD = dist; }
            }
            return best;
        }

        private bool CanTrade(string shopId)
        {
            if (DevToolsEnabled) return true;
            NpcDef n = NearbyMerchant();
            return n != null && (shopId == null || n.ShopId == shopId) && !Player.Dead;
        }

        public ShopResult BuyItem(string shopId, string itemId, int count)
        {
            if (!CanTrade(shopId)) { AddLog(ShopService.Describe(ShopResult.TooFar)); return ShopResult.TooFar; }
            long spent;
            ShopResult r = ShopService.Buy(Data, Character, shopId, itemId, count, out spent);
            AddLog(r == ShopResult.Ok
                ? "Comprou " + Data.Items[itemId].Name + (count > 1 ? " x" + count : "") + " por " + spent + " de ouro."
                : ShopService.Describe(r));
            return r;
        }

        public ShopResult SellItem(int inventorySlot, int count)
        {
            if (!CanTrade(null)) { AddLog(ShopService.Describe(ShopResult.TooFar)); return ShopResult.TooFar; }
            var stack = Character.Inventory.Get(inventorySlot);
            string name = stack != null ? Data.Items[stack.ItemId].Name : null;
            long earned;
            ShopResult r = ShopService.Sell(Data, Character, inventorySlot, count, out earned);
            AddLog(r == ShopResult.Ok ? "Vendeu " + name + (count > 1 ? " x" + count : "") + " por " + earned + " de ouro." : ShopService.Describe(r));
            return r;
        }

        public static string DescribeEquip(EquipResult r)
        {
            switch (r)
            {
                case EquipResult.Ok: return null;
                case EquipResult.EmptySlot: return "Nada para equipar aqui.";
                case EquipResult.NotEquipment: return "Este item não é um equipamento.";
                case EquipResult.UnknownSlot: return "Espaço de equipamento desconhecido.";
                case EquipResult.LevelTooLow: return "Nível insuficiente para este equipamento.";
                case EquipResult.ClassNotAllowed: return "Sua classe não pode usar este equipamento.";
                case EquipResult.WeaponTypeNotAllowed: return "Sua classe não pode usar este tipo de arma.";
                case EquipResult.InventoryFull: return "Inventário cheio.";
            }
            return null;
        }

        // ------------------------------------------------------------------ persistência
        /// <summary>Salva o personagem. Nunca lança: falha de disco vira aviso no log e em <see cref="LastSaveError"/>.</summary>
        public bool Save()
        {
            _lastAutosave = Sim.World.Time;
            if (_store == null) return false;
            try
            {
                _store.Save(Character.ToSave());
                LastSaveError = null;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                LastSaveError = ex.Message;
                AddLog("Não foi possível salvar o personagem: " + ex.Message);
                return false;
            }
        }

        /// <summary>Lê e valida um personagem salvo. Devolve null e preenche 'error' se o arquivo não existe ou é inválido.</summary>
        public static PlayerCharacter TryLoadCharacter(GameData data, ICharacterStore store, string name, out string error)
        {
            error = null;
            if (store == null || string.IsNullOrWhiteSpace(name) || !store.Exists(name)) return null;
            try
            {
                return PlayerCharacter.Restore(data, store.Load(name));
            }
            catch (SaveValidationException ex)
            {
                error = ex.Message;
                return null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = "Não foi possível ler o arquivo de save: " + ex.Message;
                return null;
            }
        }
    }
}
