namespace Realm.Maps;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Realm.MapAPI;

public class CustomMap : IWasmModule
{
    private sealed record WaveConfig(
        string[] Composition,
        int TotalCount,
        float SpawnInterval,
        float InterWaveDelay,
        MiniBossInfo? MiniBoss = null,
        bool IsBoss = false);

    private sealed record MiniBossInfo(string BannerKey, Vector3 Color);

    private const int TotalWaves = 15;
    private const float BasePassiveGoldPerSecond = 8.0f;
    private const float InitialGold = 300.0f;
    private const float SpawnHeight = 3.0f;
    private const float SpawnRingMinRadius = 20.0f;
    private const float SpawnRingMaxRadius = 26.0f;

    private const float ShopRotationInterval = 60.0f;
    private const float ShopRerollCost = 100.0f;

    private const float BaseHeroHp = 1200.0f;
    private const float BaseHeroDamage = 45.0f;
    private const float BaseHeroRange = 38.0f;
    private const float BaseHeroArmor = 5.0f;
    private const float MultiShotSplashRatio = 0.70f;

    private enum PerkTier
    {
        Normal = 45,
        Raro = 35,
        Epico = 15,
        Maldito = 5
    }

    private sealed class PerkDefinition
    {
        public string Id { get; }
        public string NameKey { get; }
        public string DescKey { get; }
        public PerkTier Tier { get; }
        public bool IsUnique { get; }
        public int MaxStacks { get; }
        public Vector3 Color { get; }
        public string IconPath { get; }

        public PerkDefinition(string id, string nameKey, string descKey, PerkTier tier, Vector3 color, string iconPath, bool isUnique = false, int maxStacks = 5)
        {
            Id = id;
            NameKey = nameKey;
            DescKey = descKey;
            Tier = tier;
            Color = color;
            IconPath = iconPath;
            IsUnique = isUnique;
            MaxStacks = isUnique ? 1 : maxStacks;
        }
    }

    private static readonly PerkDefinition[] PerkPool =
    {
        new("blood_pact", "PERK_BLOOD_PACT_NAME", "PERK_BLOOD_PACT_DESC", PerkTier.Maldito, new Vector3(0.9f, 0.1f, 0.1f), "res://Assets/icons/abilities/death_aura.rtex", isUnique: true),
        new("crystal_monolith", "PERK_CRYSTAL_MONOLITH_NAME", "PERK_CRYSTAL_MONOLITH_DESC", PerkTier.Maldito, new Vector3(0.8f, 0.1f, 0.9f), "res://Assets/icons/abilities/magic_star.rtex", isUnique: true),
        new("unbound_greed", "PERK_UNBOUND_GREED_NAME", "PERK_UNBOUND_GREED_DESC", PerkTier.Maldito, new Vector3(1.0f, 0.6f, 0.0f), "res://Assets/icons/abilities/golden_fist.rtex", isUnique: true),
        new("unstable_vortex", "PERK_UNSTABLE_VORTEX_NAME", "PERK_UNSTABLE_VORTEX_DESC", PerkTier.Maldito, new Vector3(0.7f, 0.2f, 0.8f), "res://Assets/icons/abilities/purple_energy_ring.rtex", isUnique: true),
        new("demon_deal", "PERK_DEMON_DEAL_NAME", "PERK_DEMON_DEAL_DESC", PerkTier.Maldito, new Vector3(0.95f, 0.15f, 0.15f), "res://Assets/icons/abilities/skull_arrow.rtex", isUnique: true),
        new("blind_fury", "PERK_BLIND_FURY_NAME", "PERK_BLIND_FURY_DESC", PerkTier.Maldito, new Vector3(1.0f, 0.3f, 0.2f), "res://Assets/icons/abilities/powerful_fist.rtex", isUnique: true),

        new("chain_lightning", "PERK_CHAIN_LIGHTNING_NAME", "PERK_CHAIN_LIGHTNING_DESC", PerkTier.Epico, new Vector3(0.3f, 0.7f, 1.0f), "res://Assets/icons/abilities/storm_lightning.rtex", isUnique: true),
        new("explosive_arrows", "PERK_EXPLOSIVE_ARROWS_NAME", "PERK_EXPLOSIVE_ARROWS_DESC", PerkTier.Epico, new Vector3(1.0f, 0.5f, 0.1f), "res://Assets/icons/abilities/volcanic_earthshot.rtex", isUnique: true),
        new("frost_aura", "PERK_FROST_AURA_NAME", "PERK_FROST_AURA_DESC", PerkTier.Epico, new Vector3(0.4f, 0.9f, 1.0f), "res://Assets/icons/abilities/ice_spirit_spell.rtex", isUnique: true),
        new("divine_thorns", "PERK_DIVINE_THORNS_NAME", "PERK_DIVINE_THORNS_DESC", PerkTier.Epico, new Vector3(0.9f, 0.9f, 0.3f), "res://Assets/icons/abilities/holy_cross.rtex", isUnique: true),
        new("sniper_stance", "PERK_SNIPER_STANCE_NAME", "PERK_SNIPER_STANCE_DESC", PerkTier.Epico, new Vector3(0.2f, 0.8f, 0.4f), "res://Assets/icons/abilities/red_target_icon.rtex", isUnique: true),
        new("midas_touch", "PERK_MIDAS_TOUCH_NAME", "PERK_MIDAS_TOUCH_DESC", PerkTier.Epico, new Vector3(1.0f, 0.85f, 0.1f), "res://Assets/icons/abilities/golden_hammers.rtex", isUnique: true),

        new("lifesteal", "PERK_LIFESTEAL_NAME", "PERK_LIFESTEAL_DESC", PerkTier.Raro, new Vector3(0.9f, 0.2f, 0.4f), "res://Assets/icons/abilities/bat_heart_gem.rtex", isUnique: false, maxStacks: 5),
        new("reinforced_multishot", "PERK_REINFORCED_MULTISHOT_NAME", "PERK_REINFORCED_MULTISHOT_DESC", PerkTier.Raro, new Vector3(0.4f, 0.8f, 0.9f), "res://Assets/icons/abilities/star_projectile.rtex", isUnique: false, maxStacks: 5),
        new("royal_tribute", "PERK_ROYAL_TRIBUTE_NAME", "PERK_ROYAL_TRIBUTE_DESC", PerkTier.Raro, new Vector3(1.0f, 0.8f, 0.2f), "res://Assets/icons/abilities/winged_star_shield.rtex", isUnique: false, maxStacks: 5),
        new("piercing_shot", "PERK_PIERCING_SHOT_NAME", "PERK_PIERCING_SHOT_DESC", PerkTier.Raro, new Vector3(0.5f, 0.7f, 1.0f), "res://Assets/icons/abilities/dagger_down.rtex", isUnique: false, maxStacks: 5),
        new("sharp_precision", "PERK_SHARP_PRECISION_NAME", "PERK_SHARP_PRECISION_DESC", PerkTier.Raro, new Vector3(1.0f, 0.4f, 0.6f), "res://Assets/icons/abilities/magic_fist.rtex", isUnique: false, maxStacks: 5),

        new("arrow_sharpening", "PERK_ARROW_SHARPENING_NAME", "PERK_ARROW_SHARPENING_DESC", PerkTier.Normal, new Vector3(0.9f, 0.9f, 0.9f), "res://Assets/icons/abilities/magic_upgrade_arrow.rtex", isUnique: false, maxStacks: 5),
        new("reinforced_plates", "PERK_REINFORCED_PLATES_NAME", "PERK_REINFORCED_PLATES_DESC", PerkTier.Normal, new Vector3(0.8f, 0.8f, 0.85f), "res://Assets/icons/abilities/shield_rank_1.rtex", isUnique: false, maxStacks: 5),
        new("hawk_eye", "PERK_HAWK_EYE_NAME", "PERK_HAWK_EYE_DESC", PerkTier.Normal, new Vector3(0.7f, 1.0f, 0.7f), "res://Assets/icons/abilities/electric_eye.rtex", isUnique: false, maxStacks: 5),
        new("vigor_potion", "PERK_VIGOR_POTION_NAME", "PERK_VIGOR_POTION_DESC", PerkTier.Normal, new Vector3(0.6f, 1.0f, 0.6f), "res://Assets/icons/abilities/magic_healing_light.rtex", isUnique: false, maxStacks: 5)
    };

    private static readonly WaveConfig[] Waves =
    {
        new(new[] { "zombie_soldier" }, TotalCount: 8, SpawnInterval: 0.45f, InterWaveDelay: 22.0f),
        new(new[] { "zombie_soldier" }, TotalCount: 12, SpawnInterval: 0.40f, InterWaveDelay: 20.0f),
        new(new[] { "zombie_soldier", "forest_goblin" }, TotalCount: 12, SpawnInterval: 0.35f, InterWaveDelay: 19.0f),
        new(new[] { "forest_goblin", "zombie_soldier", "forest_goblin" }, TotalCount: 16, SpawnInterval: 0.30f, InterWaveDelay: 18.0f),
        new(new[] { "giant_rock_golem", "zombie_soldier", "forest_goblin" }, TotalCount: 15, SpawnInterval: 0.40f, InterWaveDelay: 18.0f,
            MiniBoss: new MiniBossInfo("MINI_BOSS_1", new Vector3(1f, 0.6f, 0.1f))),
        new(new[] { "cyber_dragon", "zombie_soldier" }, TotalCount: 14, SpawnInterval: 0.40f, InterWaveDelay: 16.0f),
        new(new[] { "zombie_warrior", "cyber_dragon" }, TotalCount: 16, SpawnInterval: 0.35f, InterWaveDelay: 16.0f),
        new(new[] { "flame_drake", "forest_goblin", "zombie_warrior" }, TotalCount: 22, SpawnInterval: 0.35f, InterWaveDelay: 15.0f),
        new(new[] { "dark_iron_ogre", "cyber_dragon", "flame_drake" }, TotalCount: 18, SpawnInterval: 0.35f, InterWaveDelay: 14.0f),
        new(new[] { "mech_spider_monster", "dark_iron_ogre", "forest_goblin" }, TotalCount: 20, SpawnInterval: 0.35f, InterWaveDelay: 14.0f,
            MiniBoss: new MiniBossInfo("MINI_BOSS_2", new Vector3(1f, 0.3f, 0.1f))),
        new(new[] { "zombie_warrior", "flame_drake", "cyber_dragon" }, TotalCount: 26, SpawnInterval: 0.35f, InterWaveDelay: 13.0f),
        new(new[] { "dark_iron_ogre", "forest_goblin", "cyber_dragon" }, TotalCount: 28, SpawnInterval: 0.35f, InterWaveDelay: 13.0f),
        new(new[] { "dark_iron_ogre", "flame_drake", "zombie_warrior" }, TotalCount: 32, SpawnInterval: 0.35f, InterWaveDelay: 12.0f),
        new(new[] { "dark_iron_ogre", "flame_drake", "forest_goblin", "zombie_warrior" }, TotalCount: 42, SpawnInterval: 0.25f, InterWaveDelay: 15.0f),
        new(new[] { "dragon_titan_boss" }, TotalCount: 1, SpawnInterval: 1.00f, InterWaveDelay: 20.0f, IsBoss: true)
    };

    private static readonly Dictionary<string, string[]> EnemyWeaponTable = new()
    {
        { "zombie_soldier", new[] { "rusted_broadsword", "wooden_club", "rusted_dagger" } },
        { "zombie_warrior", new[] { "iron_cleaver", "battle_axe", "spiked_mace" } },
        { "forest_goblin", new[] { "crude_dagger", "hunting_spear", "goblin_cleaver" } },
        { "dark_iron_ogre", new[] { "great_warhammer", "spiked_club", "executioner_axe" } }
    };

    private sealed class PlayerState
    {
        public int PlayerIndex { get; }
        public IUnit? Hero { get; set; }
        public Vector3 QuadrantCenter { get; set; }
        public string HeroWeapon { get; set; } = "green_magic_sword";
        public bool IsDefeated { get; set; }

        public readonly Dictionary<string, int> PerkStacks = new(32);
        public readonly PerkDefinition?[] CurrentDraft = new PerkDefinition?[3];
        public readonly bool[] SlotSold = new bool[3];
        public float ShopRotationTimer = 60.0f;
        public int PerksPicked;
        public int RerollCount;
        public float FrostAuraTimer;
        public float HeroAuraTimer;
        public float PassiveGoldAccumulator;
        public float LifestealTextTimer;
        public float TotalPlayTime;
        public readonly Dictionary<int, float> ThornsCooldowns = new();

        public bool HasBloodPact;
        public bool HasCrystalMonolith;
        public bool HasUnboundGreed;
        public bool HasUnstableVortex;
        public bool HasBlindFury;
        public bool HasChainLightning;
        public bool HasExplosiveArrows;
        public bool HasFrostAura;
        public bool HasDivineThorns;
        public bool HasSniperStance;
        public bool HasMidasTouch;
        public bool HasPiercingShot;
        public int DemonDealExtraWavesRemaining;

        public int CurrentWave;
        public int WaveBeingSpawned;
        public int RemainingToSpawn;
        public int SpawnedInWave;
        public int AliveInWave;
        public float SpawnTimer;
        public float SpawnInterval;
        public float InterWaveTimer;
        public bool Spawning;
        public int TotalKills;
        public float TotalDamageDealt;
        public float TotalGoldEarned;
        public float CurrentIncomePerSecond = BasePassiveGoldPerSecond;

        public IUnit? RaidBossInstance;
        public bool BossEnraged;
        public float BossEscortTimer;
        public float BossIndicatorTimer;
        public bool BossDefeated;

        public PlayerState(int playerIndex)
        {
            PlayerIndex = playerIndex;
            QuadrantCenter = Coordinates.GetQuadrantCenter(playerIndex);
            HeroWeapon = Coordinates.GetHeroWeapon(playerIndex);
        }

        public int GetPerkStackCount(string perkId)
        {
            return PerkStacks.TryGetValue(perkId, out int count) ? count : 0;
        }
    }

    private IGameAPI _api = null!;
    private readonly Dictionary<int, PlayerState> _players = new();
    private readonly Dictionary<int, int> _unitQuadrantMap = new();
    private bool _gameOver;
    private float _economyScanTimer;

    public void Initialize(IGameAPI api)
    {
        _api = api;
        _gameOver = false;
        _economyScanTimer = 0f;
        _players.Clear();
        _unitQuadrantMap.Clear();

        var activePlayers = new List<int>();
        for (int i = 0; i < 12; i++)
        {
            if (_api.IsPlayerActive(i) && !_api.IsPlayerComputer(i) && _api.GetPlayerName(i) != "Enemy_AI")
            {
                activePlayers.Add(i);
            }
        }
        if (activePlayers.Count == 0) activePlayers.Add(0);
        int activeHumanCount = activePlayers.Count;

        for (int slotIdx = 0; slotIdx < activePlayers.Count; slotIdx++)
        {
            int pIdx = activePlayers[slotIdx];
            var pState = new PlayerState(pIdx);
            if (activeHumanCount <= 1)
            {
                pState.QuadrantCenter = Coordinates.Center;
            }
            else
            {
                pState.QuadrantCenter = Coordinates.GetQuadrantCenter(slotIdx);
                pState.HeroWeapon = Coordinates.GetHeroWeapon(slotIdx);
            }
            _players[pIdx] = pState;

            _api.SetPlayerGold(pIdx, InitialGold);
            SpawnSurvivorHero(pState);

            pState.SlotSold[0] = false;
            pState.SlotSold[1] = false;
            pState.SlotSold[2] = false;
            pState.ShopRotationTimer = ShopRotationInterval;
            TriggerNewShopRotation(pState);

            ShowNextWaveCountdown(pState);
        }

        _api.OnUnitDied += OnUnitDied;
        _api.OnUnitDamaged += OnUnitDamaged;
        _api.OnPlayerChatMessage += OnPlayerChatMessage;
        _api.OnSpellCast += OnSpellCast;

        _api.SetLeaderboardVisible("TOWER SURVIVORS", true);
        UpdateLeaderboardDisplay();

        _api.BroadcastMessage(_api.Translate("WELCOME_MESSAGE", 0));
    }

    public void Update(IGameAPI api, float delta)
    {
        if (_gameOver) return;

        bool anyAlive = false;
        foreach (var pState in _players.Values)
        {
            if (pState.IsDefeated) continue;

            CheckDefeatCondition(pState);
            if (pState.IsDefeated) continue;

            anyAlive = true;
            UpdateHeroTickEffects(pState, delta);
            UpdatePassiveIncome(pState, delta);
            UpdateShopRotationTimer(pState, delta);
            UpdateBossEncounterIfActive(pState, delta);
            UpdateWaveStateMachine(pState, delta);
        }

        if (!anyAlive && _players.Count > 0)
        {
            _gameOver = true;
            _api.StopCountdownTimer();
            _api.ShowFeedbackText(_api.Translate("HERO_DEFEATED", 0), new Vector3(1f, 0.2f, 0.2f));
            _api.TriggerDefeat();
            return;
        }

        UpdateEconomyAndUI(delta);
    }

    private void SpawnSurvivorHero(PlayerState pState)
    {
        IUnit? existingHero = null;
        foreach (var unit in _api.GetAllUnits())
        {
            if (unit.UnitId == "survivor_hero" && !unit.IsDead)
            {
                if (Vector3.Distance(unit.Position, pState.QuadrantCenter) < 15f)
                {
                    existingHero = unit;
                    break;
                }
            }
        }

        if (existingHero != null)
        {
            pState.Hero = existingHero;
        }
        else
        {
            pState.Hero = _api.SpawnUnit("survivor_hero", pState.QuadrantCenter, false, bypassPopulation: true);
        }

        if (pState.Hero == null) return;

        pState.Hero.Speed = 0f;
        _api.SetUnitColor(pState.Hero, new Vector3(1f, 1f, 1f));
        _api.SetUnitHandAttachment(pState.Hero, "RightHand", pState.HeroWeapon);
        RecalculateHeroStats(pState);

        if (pState.PlayerIndex == 0)
        {
            _api.SelectUnit(pState.Hero);
        }
    }

    private void CheckDefeatCondition(PlayerState pState)
    {
        if (pState.Hero != null && !pState.Hero.IsDead && pState.Hero.Health > 0f) return;

        pState.IsDefeated = true;
        string defeatMsg = _players.Count <= 1
            ? _api.Translate("HERO_DEFEATED", pState.PlayerIndex)
            : $"P{pState.PlayerIndex + 1}: {_api.Translate("HERO_DEFEATED", pState.PlayerIndex)}";
        _api.ShowFeedbackText(defeatMsg, new Vector3(1f, 0.2f, 0.2f));
    }

    private void UpdateHeroTickEffects(PlayerState pState, float delta)
    {
        var hero = pState.Hero;
        if (hero == null || hero.IsDead) return;

        hero.Speed = 0f;
        pState.TotalPlayTime += delta;
        if (pState.LifestealTextTimer > 0f) pState.LifestealTextTimer -= delta;

        if (pState.HasBloodPact)
        {
            float drain = MathF.Max(1.0f, hero.Health * 0.02f * delta);
            hero.Health = MathF.Max(1.0f, hero.Health - drain);
        }

        if (pState.HasFrostAura)
        {
            pState.FrostAuraTimer -= delta;
            if (pState.FrostAuraTimer <= 0f)
            {
                pState.FrostAuraTimer = 4.0f;
                _api.SpawnVisualEffect("holylight", hero.Position, 2.5f);
                _api.CreateFloatingText("¡ESCARCHA!", hero.Position + new Vector3(0, 2.5f, 0), new Vector3(0.4f, 0.9f, 1.0f), 0.9f);
                foreach (var enemy in _api.GetUnitsInRadius(hero.Position, 20f))
                {
                    if (enemy != null && enemy.IsEnemy && !enemy.IsDead && enemy.Health > 0f)
                    {
                        enemy.Speed = MathF.Max(1.5f, enemy.Speed * 0.60f);
                    }
                }
            }
        }

        if (pState.HasDivineThorns)
        {
            pState.HeroAuraTimer -= delta;
            if (pState.HeroAuraTimer <= 0f)
            {
                pState.HeroAuraTimer = 2.5f;
                _api.SpawnVisualEffect("holylight", hero.Position + new Vector3(0f, 1f, 0f), 1.2f);
            }
        }
    }

    private void UpdatePassiveIncome(PlayerState pState, float delta)
    {
        float incomeDelta = delta * pState.CurrentIncomePerSecond;
        pState.PassiveGoldAccumulator += incomeDelta;
        if (pState.PassiveGoldAccumulator >= 1.0f)
        {
            float toAward = MathF.Floor(pState.PassiveGoldAccumulator);
            pState.PassiveGoldAccumulator -= toAward;
            _api.AdjustPlayerGold(pState.PlayerIndex, toAward);
            pState.TotalGoldEarned += toAward;
        }
    }

    private void UpdateEconomyAndUI(float delta)
    {
        _economyScanTimer -= delta;
        if (_economyScanTimer > 0f) return;

        _economyScanTimer = 1.0f;
        foreach (var pState in _players.Values)
        {
            int royalTributeStacks = pState.GetPerkStackCount("royal_tribute");
            pState.CurrentIncomePerSecond = BasePassiveGoldPerSecond + (royalTributeStacks * 10.0f);
        }
        UpdateLeaderboardDisplay();
    }

    private void UpdateShopRotationTimer(PlayerState pState, float delta)
    {
        pState.ShopRotationTimer -= delta;
        if (pState.ShopRotationTimer <= 0f)
        {
            TriggerNewShopRotation(pState);
        }
    }

    private float GetPerkCost(PerkDefinition perk) => perk.Tier switch
    {
        PerkTier.Normal => 100f,
        PerkTier.Raro => 200f,
        PerkTier.Epico => 350f,
        PerkTier.Maldito => 100f,
        _ => 100f
    };

    private void TriggerNewShopRotation(PlayerState pState)
    {
        pState.ShopRotationTimer = ShopRotationInterval;
        pState.SlotSold[0] = false;
        pState.SlotSold[1] = false;
        pState.SlotSold[2] = false;

        pState.CurrentDraft[0] = null;
        pState.CurrentDraft[1] = null;
        pState.CurrentDraft[2] = null;

        for (int slot = 0; slot < 3; slot++)
        {
            pState.CurrentDraft[slot] = RollSinglePerk(pState);
        }

        if (pState.PlayerIndex == 0)
        {
            for (int slot = 0; slot < 3; slot++)
            {
                var p = pState.CurrentDraft[slot];
                if (p != null)
                {
                    float cost = GetPerkCost(p);
                    string key = slot switch { 0 => "Q", 1 => "W", _ => "E" };
                    string abilityId = $"perk_choose_{slot + 1}";
                    string translatedName = _api.Translate(p.NameKey, pState.PlayerIndex);
                    string translatedDesc = _api.Translate(p.DescKey, pState.PlayerIndex);
                    string name = $"[{key}] {translatedName} ({cost:F0}g)";
                    string tooltip = $"[{key}] {translatedName} — Cost: {cost:F0}g\n{translatedDesc}";
                    _api.RegisterAbility(abilityId, name, tooltip, p.IconPath, isInstant: true);
                }
            }

            float rerollCost = pState.RerollCount == 0 ? 0f : ShopRerollCost;
            string rerollName = pState.RerollCount == 0
                ? _api.Translate("REROLL_FREE", pState.PlayerIndex)
                : string.Format(_api.Translate("REROLL_COST", pState.PlayerIndex), rerollCost);
            string rerollTooltip = string.Format(_api.Translate("REROLL_TOOLTIP", pState.PlayerIndex), rerollCost);
            _api.RegisterAbility("perk_reroll", rerollName, rerollTooltip, "res://Assets/icons/abilities/magic_catalyst.rtex", isInstant: true);

            string meteorName = _api.Translate("METEOR_SPELL_NAME", pState.PlayerIndex);
            string meteorTooltip = _api.Translate("METEOR_SPELL_TOOLTIP", pState.PlayerIndex);
            _api.RegisterAbility("hero_meteor_spell", meteorName, meteorTooltip, "res://Assets/icons/abilities/meteor_strike.rtex", isInstant: false);

            _api.PlayClickSound();
            _api.ShowFeedbackText(_api.Translate("SHOP_READY", pState.PlayerIndex), new Vector3(1f, 0.9f, 0.2f));

            if (pState.Hero != null)
            {
                _api.CreateFloatingText(_api.Translate("SHOP_FLOATING", pState.PlayerIndex), pState.Hero.Position + new Vector3(0, 2.8f, 0), new Vector3(1f, 0.9f, 0.2f), 1.5f);
            }
        }

        UpdateLeaderboardDisplay();
    }

    private PerkDefinition RollSinglePerk(PlayerState pState)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            int roll = _api.RandomInt(0, 99);
            PerkTier targetTier = roll switch
            {
                < 45 => PerkTier.Normal,
                < 80 => PerkTier.Raro,
                < 95 => PerkTier.Epico,
                _ => PerkTier.Maldito
            };

            var candidates = PerkPool.Where(p => p.Tier == targetTier && CanPickPerk(pState, p) && !pState.CurrentDraft.Contains(p)).ToArray();
            if (candidates.Length > 0)
            {
                int index = _api.RandomInt(0, candidates.Length - 1);
                return candidates[index];
            }
        }

        var anyAvailable = PerkPool.Where(p => CanPickPerk(pState, p) && !pState.CurrentDraft.Contains(p)).ToArray();
        if (anyAvailable.Length > 0)
        {
            return anyAvailable[_api.RandomInt(0, anyAvailable.Length - 1)];
        }

        return PerkPool[0];
    }

    private bool CanPickPerk(PlayerState pState, PerkDefinition perk)
    {
        int stacks = pState.GetPerkStackCount(perk.Id);
        return stacks < perk.MaxStacks;
    }

    private void TryBuyPerk(PlayerState pState, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 3) return;
        if (pState.SlotSold[slotIndex])
        {
            _api.ShowFeedbackText(_api.Translate("PERK_SLOT_EMPTY", pState.PlayerIndex), new Vector3(1f, 0.5f, 0.2f));
            return;
        }

        var perk = pState.CurrentDraft[slotIndex];
        if (perk == null) return;

        float cost = GetPerkCost(perk);
        float currentGold = _api.GetPlayerGold(pState.PlayerIndex);
        if (currentGold < cost)
        {
            string notEnough = string.Format(_api.Translate("PERK_NOT_ENOUGH_GOLD", pState.PlayerIndex), cost, currentGold);
            _api.ShowFeedbackText(notEnough, new Vector3(1f, 0.2f, 0.2f));
            if (pState.Hero != null)
            {
                _api.CreateFloatingText($"! -{(cost - currentGold):F0}g", pState.Hero.Position + new Vector3(0, 2f, 0), new Vector3(1f, 0.2f, 0.2f), 0.9f);
            }
            return;
        }

        _api.AdjustPlayerGold(pState.PlayerIndex, -cost);
        pState.SlotSold[slotIndex] = true;

        int current = pState.GetPerkStackCount(perk.Id);
        pState.PerkStacks[perk.Id] = current + 1;
        pState.PerksPicked++;

        if (pState.PlayerIndex == 0)
        {
            string boughtId = $"perk_choose_{slotIndex + 1}";
            string perkName = _api.Translate(perk.NameKey, pState.PlayerIndex);
            _api.RegisterAbility(boughtId, $"[BOUGHT] {perkName}", $"[BOUGHT] {perkName} (x{pState.PerkStacks[perk.Id]})", perk.IconPath, isInstant: true);
        }

        ApplyPerkAcquisition(pState, perk);
        RecalculateHeroStats(pState);

        string localizedPerkName = _api.Translate(perk.NameKey, pState.PlayerIndex);
        string purchaseMsg = string.Format(_api.Translate("PERK_PURCHASED", pState.PlayerIndex), localizedPerkName, pState.PerkStacks[perk.Id]);
        _api.ShowFeedbackText(purchaseMsg, perk.Color);
        _api.PlayClickSound();

        if (pState.Hero != null)
        {
            _api.CreateFloatingText($"+{localizedPerkName} (x{pState.PerkStacks[perk.Id]})", pState.Hero.Position + new Vector3(0, 2.5f, 0), perk.Color, 1.4f);
            _api.SpawnVisualEffect("holylight", pState.Hero.Position + new Vector3(0, 1.2f, 0), 1.3f);
        }

        UpdateLeaderboardDisplay();

        if (pState.SlotSold[0] && pState.SlotSold[1] && pState.SlotSold[2])
        {
            TriggerNewShopRotation(pState);
        }
    }

    private void ApplyPerkAcquisition(PlayerState pState, PerkDefinition perk)
    {
        switch (perk.Id)
        {
            case "blood_pact":
                pState.HasBloodPact = true;
                break;
            case "crystal_monolith":
                pState.HasCrystalMonolith = true;
                break;
            case "unbound_greed":
                pState.HasUnboundGreed = true;
                break;
            case "unstable_vortex":
                pState.HasUnstableVortex = true;
                break;
            case "demon_deal":
                pState.DemonDealExtraWavesRemaining = 2;
                _api.AdjustPlayerGold(pState.PlayerIndex, 1000f);
                pState.TotalGoldEarned += 1000f;
                if (pState.Hero != null) pState.Hero.Health = pState.Hero.MaxHealth;
                _api.ShowFeedbackText(_api.Translate("DEMON_DEAL_TRIGGER", pState.PlayerIndex), new Vector3(0.95f, 0.15f, 0.15f));
                break;
            case "blind_fury":
                pState.HasBlindFury = true;
                break;
            case "chain_lightning":
                pState.HasChainLightning = true;
                break;
            case "explosive_arrows":
                pState.HasExplosiveArrows = true;
                break;
            case "frost_aura":
                pState.HasFrostAura = true;
                break;
            case "divine_thorns":
                pState.HasDivineThorns = true;
                break;
            case "sniper_stance":
                pState.HasSniperStance = true;
                break;
            case "midas_touch":
                pState.HasMidasTouch = true;
                break;
            case "piercing_shot":
                pState.HasPiercingShot = true;
                break;
            case "vigor_potion":
                if (pState.Hero != null) pState.Hero.Health = MathF.Min(pState.Hero.MaxHealth, pState.Hero.Health + 200f);
                break;
        }
    }

    private void TryRerollShop(PlayerState pState)
    {
        float cost = pState.RerollCount == 0 ? 0f : ShopRerollCost;
        if (cost > 0f && _api.GetPlayerGold(pState.PlayerIndex) < cost)
        {
            string notEnough = string.Format(_api.Translate("PERK_NOT_ENOUGH_GOLD", pState.PlayerIndex), cost, _api.GetPlayerGold(pState.PlayerIndex));
            _api.ShowFeedbackText(notEnough, new Vector3(1f, 0.3f, 0.3f));
            return;
        }

        if (cost > 0f)
        {
            _api.AdjustPlayerGold(pState.PlayerIndex, -cost);
        }

        pState.RerollCount++;
        _api.PlayClickSound();
        string rerollMsg = string.Format(_api.Translate("REROLL_SUCCESS", pState.PlayerIndex), ShopRerollCost);
        _api.ShowFeedbackText(rerollMsg, new Vector3(1f, 0.85f, 0.2f));

        TriggerNewShopRotation(pState);
    }

    private string GetHeroProjectileId(PlayerState pState)
    {
        if (pState.HasExplosiveArrows) return "fire_shard";
        if (pState.HasFrostAura) return "frost_shard";
        return "survivor_arrow";
    }

    private void RecalculateHeroStats(PlayerState pState)
    {
        var hero = pState.Hero;
        if (hero == null || hero.IsDead) return;

        int vigorStacks = pState.GetPerkStackCount("vigor_potion");
        float bonusMaxHp = vigorStacks * 350.0f;
        float hpMultiplier = pState.HasCrystalMonolith ? 0.60f : 1.0f;
        float prevMaxHp = hero.MaxHealth;
        float calculatedMaxHp = MathF.Max(150.0f, (BaseHeroHp + bonusMaxHp) * hpMultiplier);

        hero.MaxHealth = calculatedMaxHp;
        if (calculatedMaxHp > prevMaxHp && prevMaxHp > 0f)
        {
            hero.Health += (calculatedMaxHp - prevMaxHp);
        }
        hero.Health = MathF.Min(hero.Health, hero.MaxHealth);

        int arrowSharpenStacks = pState.GetPerkStackCount("arrow_sharpening");
        float flatBonusDamage = arrowSharpenStacks * 12.0f;
        float damageMult = pState.HasBloodPact ? 2.0f : 1.0f;
        hero.Damage = (BaseHeroDamage + flatBonusDamage) * damageMult;

        int hawkEyeStacks = pState.GetPerkStackCount("hawk_eye");
        float flatBonusRange = (hawkEyeStacks * 6.0f) + (pState.HasCrystalMonolith ? 15.0f : 0f) + (pState.HasSniperStance ? 18.0f : 0f);
        float rangeMult = pState.HasBlindFury ? 0.65f : 1.0f;
        hero.Range = MathF.Max(15.0f, (BaseHeroRange + flatBonusRange) * rangeMult);

        if (pState.HasUnstableVortex)
        {
            hero.Armor = 0f;
        }
        else
        {
            int plateStacks = pState.GetPerkStackCount("reinforced_plates");
            hero.Armor = BaseHeroArmor + (plateStacks * 4.0f) + (pState.HasDivineThorns ? 15.0f : 0f);
        }
    }

    private void UpdateWaveStateMachine(PlayerState pState, float delta)
    {
        if (pState.Spawning)
        {
            UpdateActiveWave(pState, delta);
        }
        else
        {
            pState.InterWaveTimer -= delta;
            if (pState.InterWaveTimer <= 0f)
            {
                BeginWave(pState, pState.CurrentWave + 1);
            }
        }
    }

    private void UpdateActiveWave(PlayerState pState, float delta)
    {
        if (pState.RemainingToSpawn > 0)
        {
            UpdateSpawning(pState, delta);
            return;
        }

        if (pState.AliveInWave > 0 || (pState.WaveBeingSpawned == 15 && !pState.BossDefeated)) return;

        pState.Spawning = false;
        pState.CurrentWave++;

        PublishSummaryTableForWave(pState.CurrentWave);

        if (pState.CurrentWave >= TotalWaves)
        {
            EndGameWithVictory();
        }
        else
        {
            ShowNextWaveCountdown(pState);
        }
    }

    private void UpdateSpawning(PlayerState pState, float delta)
    {
        pState.SpawnTimer -= delta;
        if (pState.SpawnTimer > 0f) return;

        pState.SpawnTimer = pState.SpawnInterval;
        SpawnNextEnemyInWave(pState);
    }

    private void EndGameWithVictory()
    {
        _gameOver = true;
        _api.StopCountdownTimer();
        _api.ShowFeedbackText(_api.Translate("VICTORIA_TOTAL", 0), new Vector3(1f, 0.9f, 0.2f));
        _api.TriggerVictory();
    }

    private void ShowNextWaveCountdown(PlayerState pState)
    {
        int next = pState.CurrentWave + 1;
        if (next <= TotalWaves)
        {
            float delay = Waves[next - 1].InterWaveDelay;
            pState.InterWaveTimer = delay;
            if (pState.PlayerIndex == 0)
            {
                string label = string.Format(_api.Translate("WAVE_INTERMISSION", pState.PlayerIndex), next);
                _api.StartCountdownTimer(delay, label);
            }
        }
    }

    private void BeginWave(PlayerState pState, int wave)
    {
        pState.WaveBeingSpawned = wave;
        pState.Spawning = true;
        var config = Waves[wave - 1];

        int extraCount = 0;
        if (pState.DemonDealExtraWavesRemaining > 0)
        {
            extraCount = (int)MathF.Ceiling(config.TotalCount * 0.35f);
            pState.DemonDealExtraWavesRemaining--;
        }

        pState.RemainingToSpawn = config.TotalCount + extraCount;
        pState.SpawnedInWave = 0;
        pState.AliveInWave = pState.RemainingToSpawn;
        pState.SpawnInterval = config.SpawnInterval;
        pState.SpawnTimer = 0f;

        if (pState.PlayerIndex == 0)
        {
            _api.StopCountdownTimer();
            _api.PlayWarningSound();
        }

        if (pState.ShopRotationTimer < 15f)
        {
            TriggerNewShopRotation(pState);
        }

        if (config.IsBoss)
        {
            TriggerRaidBossCinematic(pState);
        }
        else if (config.MiniBoss is { } miniBoss)
        {
            string banner = _api.Translate(miniBoss.BannerKey, pState.PlayerIndex);
            _api.ShowFeedbackText(banner, miniBoss.Color);
        }
        else
        {
            string waveMsg = string.Format(_api.Translate("WAVE_INCOMING", pState.PlayerIndex), wave);
            if (_players.Count <= 1)
            {
                _api.BroadcastMessage(waveMsg);
            }
            else
            {
                _api.BroadcastMessage($"P{pState.PlayerIndex + 1}: {waveMsg}");
            }
        }
    }

    private void SpawnNextEnemyInWave(PlayerState pState)
    {
        var config = Waves[pState.WaveBeingSpawned - 1];
        string unitType = config.Composition[pState.SpawnedInWave % config.Composition.Length];

        IUnit? unit;
        if (unitType == "dragon_titan_boss")
        {
            var bossPos = pState.QuadrantCenter + new Vector3(0f, 0f, -25f);
            unit = _api.SpawnUnit(unitType, bossPos, true);
            if (unit != null)
            {
                pState.RaidBossInstance = unit;
                unit.Scale = 5.0f;
            }
        }
        else
        {
            var spawnPos = Coordinates.GetRandomSpawnPointOnRing(pState.QuadrantCenter, SpawnRingMinRadius, SpawnRingMaxRadius, SpawnHeight);
            unit = _api.SpawnUnit(unitType, spawnPos, true);
        }

        if (unit != null)
        {
            _unitQuadrantMap[unit.UniqueId] = pState.PlayerIndex;

            if (EnemyWeaponTable.TryGetValue(unitType, out var weapons) && weapons.Length > 0)
            {
                int wIndex = _api.RandomInt(0, weapons.Length - 1);
                _api.SetUnitHandAttachment(unit, "RightHand", weapons[wIndex]);
            }

            float hpScale = 1.0f + (pState.PerksPicked * 0.05f) + (pState.WaveBeingSpawned * 0.03f) + (pState.HasUnboundGreed ? 0.15f : 0f);
            unit.MaxHealth *= hpScale;
            unit.Health = unit.MaxHealth;

            if (pState.HasUnboundGreed)
            {
                unit.Speed *= 1.20f;
            }

            unit.AttackMove(pState.QuadrantCenter);
            pState.SpawnedInWave++;
            pState.RemainingToSpawn--;
        }
    }

    private void PublishSummaryTableForWave(int waveNumber)
    {
        string title = string.Format(_api.Translate("WAVE_SUMMARY_TITLE", 0), waveNumber);
        _api.ClearSummaryTable();
        foreach (var p in _players.Values.OrderBy(x => x.PlayerIndex))
        {
            int score = (int)(p.TotalDamageDealt * 0.1f + p.TotalGoldEarned * 0.5f + p.TotalKills * 50);
            string pName = $"Player {p.PlayerIndex + 1}";
            string dmgStr = $"{p.TotalDamageDealt:F0}";
            string incStr = $"{p.TotalGoldEarned:F0}g";
            string scoreStr = $"{score}";
            _api.SetSummaryTableRow(pName, dmgStr, incStr, scoreStr);
        }
        _api.ShowSummaryTable(title, true);
    }

    private void UpdateBossEncounterIfActive(PlayerState pState, float delta)
    {
        if (pState.WaveBeingSpawned != 15) return;
        var boss = pState.RaidBossInstance;
        if (boss == null || boss.IsDead) return;

        UpdateBossTargetIndicator(pState, boss, delta);
        UpdateBossEnragePhase(pState, boss);
        UpdateBossEscortTimer(pState, delta);
    }

    private void UpdateBossTargetIndicator(PlayerState pState, IUnit boss, float delta)
    {
        pState.BossIndicatorTimer -= delta;
        if (pState.BossIndicatorTimer > 0f) return;

        pState.BossIndicatorTimer = 2.0f;
        _api.SpawnTargetIndicator(boss.Position, new Vector3(1f, 0.1f, 0.1f));
    }

    private void UpdateBossEnragePhase(PlayerState pState, IUnit boss)
    {
        if (pState.BossEnraged || boss.Health > boss.MaxHealth * 0.5f) return;

        pState.BossEnraged = true;
        boss.Speed = 8.25f;
        _api.SetUnitColor(boss, new Vector3(1f, 0.2f, 0.2f));
        _api.ShowFeedbackText(_api.Translate("RAID_BOSS_ENRAGE", pState.PlayerIndex), new Vector3(1f, 0.15f, 0.15f));
        _api.PlayWarningSound();
        _api.ShakeCamera(3.0f, 1.5f);
    }

    private void UpdateBossEscortTimer(PlayerState pState, float delta)
    {
        pState.BossEscortTimer -= delta;
        if (pState.BossEscortTimer > 0f) return;

        pState.BossEscortTimer = pState.BossEnraged ? 8.0f : 12.0f;
        SpawnBossEscortWave(pState);
    }

    private void TriggerRaidBossCinematic(PlayerState pState)
    {
        pState.BossEscortTimer = 6.0f;
        pState.BossIndicatorTimer = 0f;

        _api.ShowFeedbackText(_api.Translate("RAID_BOSS_ALERT", pState.PlayerIndex), new Vector3(1f, 0.85f, 0.1f));
        _api.PlayWarningSound();
        _api.ShakeCamera(2.5f, 2.0f);
        var bossPos = pState.QuadrantCenter + new Vector3(0f, 0f, -25f);
        _api.PanCameraTo(bossPos, 1.5f);
        _api.PingMinimap(bossPos);
    }

    private void SpawnBossEscortWave(PlayerState pState)
    {
        for (int s = 0; s < 8; s++) SpawnSingleEscort(pState, "zombie_soldier");
        for (int g = 0; g < 4; g++) SpawnSingleEscort(pState, "forest_goblin");
        for (int o = 0; o < (pState.BossEnraged ? 2 : 0); o++) SpawnSingleEscort(pState, "dark_iron_ogre");

        _api.BroadcastMessage(_api.Translate("RAID_BOSS_ESCORT", pState.PlayerIndex));
    }

    private void SpawnSingleEscort(PlayerState pState, string unitType)
    {
        var spawnPos = Coordinates.GetRandomSpawnPointOnRing(pState.QuadrantCenter, SpawnRingMinRadius, SpawnRingMaxRadius, SpawnHeight);
        var unit = _api.SpawnUnit(unitType, spawnPos, true);
        if (unit != null)
        {
            _unitQuadrantMap[unit.UniqueId] = pState.PlayerIndex;

            if (EnemyWeaponTable.TryGetValue(unitType, out var weapons) && weapons.Length > 0)
            {
                int wIndex = _api.RandomInt(0, weapons.Length - 1);
                _api.SetUnitHandAttachment(unit, "RightHand", weapons[wIndex]);
            }

            unit.AttackMove(pState.QuadrantCenter);
            pState.AliveInWave++;
        }
    }

    private void OnUnitDamaged(IUnit victim, IUnit attacker, float damage)
    {
        if (_gameOver || victim == null || attacker == null || victim.IsDead) return;

        if (victim.IsEnemy && !attacker.IsEnemy && attacker.UnitId == "survivor_hero")
        {
            var pState = _players.Values.FirstOrDefault(p => p.Hero != null && p.Hero.UniqueId == attacker.UniqueId);
            if (pState != null)
            {
                pState.TotalDamageDealt += damage;
                ApplyHeroAttackProcs(pState, victim, damage);
            }
        }

        if (!victim.IsEnemy && victim.UnitId == "survivor_hero" && attacker.IsEnemy)
        {
            var pState = _players.Values.FirstOrDefault(p => p.Hero != null && p.Hero.UniqueId == victim.UniqueId);
            if (pState != null && pState.HasDivineThorns && !attacker.IsDead && attacker.Health > 0f)
            {
                float reflectDamage = damage * 0.50f;
                pState.TotalDamageDealt += reflectDamage;
                DealBonusDamage(attacker, reflectDamage);
                _api.SpawnVisualEffect("lightning", attacker.Position, 0.6f);
                if (!pState.ThornsCooldowns.TryGetValue(attacker.UniqueId, out float nextAllowed) || pState.TotalPlayTime >= nextAllowed)
                {
                    pState.ThornsCooldowns[attacker.UniqueId] = pState.TotalPlayTime + 0.5f;
                    string thornsText = string.Format(_api.Translate("THORNS_FLOATING", pState.PlayerIndex), reflectDamage.ToString("F0"));
                    _api.CreateFloatingText(thornsText, attacker.Position + new Vector3(0, 1.8f, 0), new Vector3(0.9f, 0.9f, 0.2f), 0.7f);
                }
            }
        }
    }

    private void DealBonusDamage(IUnit victim, float amount)
    {
        victim.Health -= amount;
        if (victim.Health <= 0f)
        {
            _api.KillUnit(victim);
        }
    }

    private void ApplyHeroAttackProcs(PlayerState pState, IUnit victim, float damage)
    {
        var hero = pState.Hero;
        if (hero == null) return;

        int sharpStacks = pState.GetPerkStackCount("sharp_precision");
        float critChance = sharpStacks * 0.20f;
        if (critChance > 0f && _api.RandomFloat(0f, 1f) < critChance)
        {
            float critMult = pState.HasCrystalMonolith ? 4.5f : 2.0f;
            float extraCritDamage = damage * (critMult - 1.0f);
            pState.TotalDamageDealt += extraCritDamage;
            DealBonusDamage(victim, extraCritDamage);
            string critText = string.Format(_api.Translate("CRIT_FLOATING", pState.PlayerIndex), critMult.ToString("F1"));
            _api.CreateFloatingText(critText, victim.Position + new Vector3(0, 2.2f, 0), new Vector3(1f, 0.2f, 0.2f), 0.9f);
        }

        if (pState.HasSniperStance)
        {
            float dist = Vector3.Distance(hero.Position, victim.Position);
            if (dist >= 25.0f)
            {
                float bonusSnipe = damage * 0.40f;
                pState.TotalDamageDealt += bonusSnipe;
                DealBonusDamage(victim, bonusSnipe);
                _api.CreateFloatingText(_api.Translate("SNIPER_FLOATING", pState.PlayerIndex), victim.Position + new Vector3(0, 2.0f, 0), new Vector3(0.2f, 0.9f, 0.4f), 0.8f);
            }
        }

        int lifestealStacks = pState.GetPerkStackCount("lifesteal");
        if (lifestealStacks > 0)
        {
            float healAmount = damage * (lifestealStacks * 0.15f);
            hero.Health = MathF.Min(hero.MaxHealth, hero.Health + healAmount);
            if (pState.LifestealTextTimer <= 0f)
            {
                pState.LifestealTextTimer = 0.5f;
                string lsText = string.Format(_api.Translate("LIFESTEAL_FLOATING", pState.PlayerIndex), healAmount.ToString("F0"));
                _api.CreateFloatingText(lsText, hero.Position + new Vector3(0, 1.8f, 0), new Vector3(0.2f, 1f, 0.4f), 0.6f);
            }
        }

        if (pState.HasChainLightning && _api.RandomFloat(0f, 1f) < 0.30f)
        {
            ApplyChainLightning(pState, victim);
        }

        if (pState.HasExplosiveArrows)
        {
            ApplyExplosiveArrows(pState, victim, damage);
        }

        if (pState.HasPiercingShot)
        {
            ApplyPiercingShot(pState, victim, damage);
        }

        if (pState.HasUnstableVortex)
        {
            ApplyUnstableVortex(pState, victim);
        }

        if (pState.HasBlindFury)
        {
            FireFuryArrow(pState, victim);
        }

        int reinforcedStacks = pState.GetPerkStackCount("reinforced_multishot");
        if (reinforcedStacks > 0)
        {
            ApplyMultiShot(pState, victim, damage, reinforcedStacks);
        }
    }

    private void ApplyChainLightning(PlayerState pState, IUnit primary)
    {
        _api.SpawnVisualEffect("lightning", primary.Position, 1.5f);
        _api.CreateFloatingText(_api.Translate("LIGHTNING_FLOATING", pState.PlayerIndex), primary.Position + new Vector3(0, 2f, 0), new Vector3(0.3f, 0.7f, 1.0f), 0.8f);

        int jumps = 0;
        foreach (var nearby in _api.GetUnitsInRadius(primary.Position, 14f))
        {
            if (nearby == null || !nearby.IsEnemy || nearby.UniqueId == primary.UniqueId || nearby.IsDead || nearby.Health <= 0f)
                continue;
            if (jumps >= 4) break;

            pState.TotalDamageDealt += 75f;
            DealBonusDamage(nearby, 75f);
            _api.SpawnVisualEffect("lightning", nearby.Position, 1.0f);
            _api.SpawnProjectile("survivor_arrow", primary.Position, nearby.Position, 50f);
            jumps++;
        }
    }

    private void ApplyExplosiveArrows(PlayerState pState, IUnit primary, float damage)
    {
        _api.SpawnVisualEffect("fireblast", primary.Position, 1.4f);
        if (pState.Hero != null)
        {
            _api.SpawnProjectile("fire_shard", pState.Hero.Position, primary.Position, 40f);
        }
        float splashDamage = damage * 0.50f;

        foreach (var nearby in _api.GetUnitsInRadius(primary.Position, 5f))
        {
            if (nearby == null || !nearby.IsEnemy || nearby.UniqueId == primary.UniqueId || nearby.IsDead || nearby.Health <= 0f)
                continue;

            pState.TotalDamageDealt += splashDamage;
            DealBonusDamage(nearby, splashDamage);
        }
    }

    private void ApplyPiercingShot(PlayerState pState, IUnit primary, float damage)
    {
        float pierceDamage = damage * 0.70f;
        var behind = _api.GetUnitsInRadius(primary.Position, 8f)
            .FirstOrDefault(u => u != null && u.IsEnemy && u.UniqueId != primary.UniqueId && !u.IsDead && u.Health > 0f);

        if (behind != null)
        {
            pState.TotalDamageDealt += pierceDamage;
            DealBonusDamage(behind, pierceDamage);
            _api.SpawnProjectile(GetHeroProjectileId(pState), primary.Position, behind.Position, 55f);
            _api.CreateFloatingText(_api.Translate("PIERCING_FLOATING", pState.PlayerIndex), behind.Position + new Vector3(0, 1.8f, 0), new Vector3(0.5f, 0.7f, 1.0f), 0.7f);
        }
    }

    private void ApplyUnstableVortex(PlayerState pState, IUnit primary)
    {
        if (pState.Hero == null) return;
        int shotCount = 0;
        foreach (var nearby in _api.GetUnitsInRadius(primary.Position, 15f))
        {
            if (nearby == null || !nearby.IsEnemy || nearby.UniqueId == primary.UniqueId || nearby.IsDead || nearby.Health <= 0f)
                continue;
            if (shotCount >= 2) break;

            float bonusDmg = pState.Hero.Damage * 0.60f;
            pState.TotalDamageDealt += bonusDmg;
            DealBonusDamage(nearby, bonusDmg);
            _api.SpawnProjectile(GetHeroProjectileId(pState), pState.Hero.Position, nearby.Position, 36f);
            shotCount++;
        }
    }

    private void FireFuryArrow(PlayerState pState, IUnit victim)
    {
        if (pState.Hero == null) return;
        var secondary = _api.GetUnitsInRadius(victim.Position, 10f)
            .FirstOrDefault(u => u != null && u.IsEnemy && u.UniqueId != victim.UniqueId && !u.IsDead && u.Health > 0f);

        if (secondary == null) return;

        pState.TotalDamageDealt += pState.Hero.Damage;
        DealBonusDamage(secondary, pState.Hero.Damage);
        _api.SpawnProjectile(GetHeroProjectileId(pState), pState.Hero.Position, secondary.Position, 48f);
        _api.CreateFloatingText(_api.Translate("FURY_FLOATING", pState.PlayerIndex), secondary.Position + new Vector3(0f, 2f, 0f), new Vector3(1f, 0.4f, 0.1f), 0.8f);
    }

    private void ApplyMultiShot(PlayerState pState, IUnit victim, float damage, int targetLimit)
    {
        if (pState.Hero == null) return;
        float splash = damage * MultiShotSplashRatio;
        int hits = 0;

        foreach (var nearby in _api.GetUnitsInRadius(victim.Position, 7f))
        {
            if (nearby == null || !nearby.IsEnemy || nearby.UniqueId == victim.UniqueId || nearby.IsDead || nearby.Health <= 0f)
                continue;
            if (hits >= targetLimit) break;

            pState.TotalDamageDealt += splash;
            DealBonusDamage(nearby, splash);
            _api.SpawnProjectile(GetHeroProjectileId(pState), pState.Hero.Position, nearby.Position, 42f);
            hits++;
        }
    }

    private float GetUnitBounty(string unitId) => unitId switch
    {
        "dragon_titan_boss" => 1000.0f,
        "mech_spider_monster" => 250.0f,
        "giant_rock_golem" => 150.0f,
        "dark_iron_ogre" => 20.0f,
        "cyber_dragon" => 20.0f,
        "flame_drake" => 20.0f,
        "zombie_warrior" => 15.0f,
        "forest_goblin" => 10.0f,
        "zombie_soldier" => 10.0f,
        _ => 10.0f
    };

    private void OnUnitDied(IUnit unit, IUnit? killer)
    {
        if (_gameOver || unit == null || !unit.IsEnemy) return;

        foreach (var p in _players.Values)
        {
            p.ThornsCooldowns.Remove(unit.UniqueId);
        }

        int pIdx = 0;
        if (_unitQuadrantMap.TryGetValue(unit.UniqueId, out int mappedIdx))
        {
            pIdx = mappedIdx;
            _unitQuadrantMap.Remove(unit.UniqueId);
        }

        if (!_players.TryGetValue(pIdx, out var pState))
        {
            pState = _players.Values.FirstOrDefault();
            if (pState == null) return;
        }

        pState.AliveInWave = Math.Max(0, pState.AliveInWave - 1);
        pState.TotalKills++;

        float bounty = GetUnitBounty(unit.UnitId);
        if (pState.HasUnboundGreed)
        {
            bounty *= 2.50f;
        }

        if (pState.HasMidasTouch && unit.UnitId != "dragon_titan_boss" && _api.RandomFloat(0f, 1f) < 0.15f)
        {
            bounty += 40.0f;
            _api.CreateFloatingText(_api.Translate("MIDAS_FLOATING", pState.PlayerIndex), unit.Position + new Vector3(0, 2.3f, 0), new Vector3(1f, 0.85f, 0.1f), 1.3f);
        }

        _api.AdjustPlayerGold(pState.PlayerIndex, bounty);
        pState.TotalGoldEarned += bounty;
        _api.CreateFloatingText($"+{bounty:F0}g", unit.Position + new Vector3(0, 1.8f, 0), new Vector3(1f, 0.85f, 0.1f), 1.2f);

        if (unit.UnitId == "dragon_titan_boss")
        {
            pState.BossDefeated = true;
            bool allBossesDead = _players.Values.All(p => p.IsDefeated || p.BossDefeated);
            if (allBossesDead)
            {
                _gameOver = true;
                _api.StopCountdownTimer();
                _api.ShowFeedbackText(_api.Translate("RAID_BOSS_DEFEATED", 0), new Vector3(1f, 0.9f, 0.2f));
                _api.PlayClickSound();
                _api.TriggerVictory();
            }
        }
    }

    private void OnPlayerChatMessage(string message, IUnit? selected)
    {
        if (_gameOver) return;
        if (!_players.TryGetValue(0, out var pState)) return;

        string clean = message.Trim().ToLowerInvariant();
        switch (clean)
        {
            case "-1":
                TryBuyPerk(pState, 0);
                break;
            case "-2":
                TryBuyPerk(pState, 1);
                break;
            case "-3":
                TryBuyPerk(pState, 2);
                break;
            case "-reroll":
                TryRerollShop(pState);
                break;
        }
    }

    private void OnSpellCast(IUnit? caster, string abilityId, Vector3 targetPosition)
    {
        if (_gameOver) return;
        if (!_players.TryGetValue(0, out var pState)) return;

        switch (abilityId)
        {
            case "perk_choose_1":
                TryBuyPerk(pState, 0);
                break;
            case "perk_choose_2":
                TryBuyPerk(pState, 1);
                break;
            case "perk_choose_3":
                TryBuyPerk(pState, 2);
                break;
            case "perk_reroll":
                TryRerollShop(pState);
                break;
            case "hero_meteor_spell":
                if (pState.Hero != null)
                {
                    _api.SpawnProjectile("boss_meteor", pState.Hero.Position, targetPosition, 26f);
                    _api.SpawnVisualEffect("fireblast", targetPosition, 2.5f);
                    _api.CreateFloatingText(_api.Translate("METEOR_FLOATING", pState.PlayerIndex), targetPosition + new Vector3(0, 2f, 0), new Vector3(1f, 0.4f, 0.1f), 1.2f);
                    foreach (var enemy in _api.GetUnitsInRadius(targetPosition, 4.5f))
                    {
                        if (enemy != null && enemy.IsEnemy && !enemy.IsDead && enemy.Health > 0f)
                        {
                            pState.TotalDamageDealt += 120f;
                            DealBonusDamage(enemy, 120f);
                        }
                    }
                }
                break;
        }
    }

    private void UpdateLeaderboardDisplay()
    {
        if (!_players.TryGetValue(0, out var p0)) return;

        _api.SetLeaderboardValue(_api.Translate("LB_WAVE", 0), $"{p0.CurrentWave} / {TotalWaves}");
        _api.SetLeaderboardValue(_api.Translate("LB_GOLD", 0), $"{(int)_api.GetPlayerGold(0)} (+{(int)p0.CurrentIncomePerSecond}/s)");
        _api.SetLeaderboardValue(_api.Translate("LB_SHOP", 0), $"{p0.ShopRotationTimer:F0}s | Perks: {p0.PerksPicked}");
        _api.SetLeaderboardValue(_api.Translate("LB_KILLS", 0), $"{p0.TotalKills}");
        _api.SetLeaderboardValue(_api.Translate("LB_ENEMIES", 0), $"{Math.Max(0, p0.AliveInWave)}");
    }
}
