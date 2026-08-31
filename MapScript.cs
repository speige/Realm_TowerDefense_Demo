namespace Realm.Maps;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Realm.MapAPI;

public class CustomMap : IWasmModule
{
    // ============================================================================
    // 1. CONFIGURACIÓN Y CONSTANTES (Balance, Oleadas, Tiers de Habilidades)
    // ============================================================================

    private sealed record WaveConfig(
        string[] Composition,
        int TotalCount,
        float SpawnInterval,
        float InterWaveDelay,
        MiniBossInfo? MiniBoss = null,
        bool IsBoss = false);

    private sealed record MiniBossInfo(string Banner, Vector3 Color);

    // ---- Constantes de Configuración & Balance ----
    private const int TotalWaves = 15;
    private const float BasePassiveGoldPerSecond = 8.0f;
    private const float InitialGold = 300.0f;
    private const float SpawnHeight = 3.0f;
    private const float SpawnRingMinRadius = 60.0f;
    private const float SpawnRingMaxRadius = 66.0f;

    // ---- Temporizadores del Sistema de Habilidades (Tienda Rotativa) ----
    private const float ShopRotationInterval = 60.0f;
    private const float ShopRerollCost = 100.0f;

    // ---- Balance del Héroe Central ----
    private const float BaseHeroHp = 1200.0f;
    private const float BaseHeroDamage = 45.0f;
    private const float BaseHeroRange = 38.0f;
    private const float BaseHeroArmor = 5.0f;
    private const float MultiShotSplashRatio = 0.70f;

    // ---- Tiers y Ponderación de Habilidades ----
    private enum PerkTier
    {
        Normal = 45,  // 45% peso
        Raro = 35,    // 35% peso
        Epico = 15,   // 15% peso
        Maldito = 5   // 5% peso (Sacrificio / High Risk)
    }

    private sealed class PerkDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public PerkTier Tier { get; }
        public bool IsUnique { get; }
        public int MaxStacks { get; }
        public Vector3 Color { get; }
        public string IconPath { get; }

        public PerkDefinition(string id, string name, string description, PerkTier tier, Vector3 color, string iconPath = "res://Assets/UI/magic_upgrade_arrow.png", bool isUnique = false, int maxStacks = 5)
        {
            Id = id;
            Name = name;
            Description = description;
            Tier = tier;
            Color = color;
            IconPath = iconPath;
            IsUnique = isUnique;
            MaxStacks = isUnique ? 1 : maxStacks;
        }
    }

    // Catálogo completo de las 21 Habilidades distribuidas en 4 Tiers con Iconos Temáticos
    private static readonly PerkDefinition[] PerkPool =
    {
        // 💀 TIER MALDITO / SACRIFICIO (5% prob)
        new("blood_pact", "Pacto de Sangre", "+100% Daño de Ataque, pero pierdes 2% de vida actual/s", PerkTier.Maldito, new Vector3(0.9f, 0.1f, 0.1f), "res://Assets/UI/cancel_button_2.png", isUnique: true),
        new("crystal_monolith", "Monolito de Cristal", "+250% Daño Crítico y +15 Alcance, pero -40% Vida Máxima", PerkTier.Maldito, new Vector3(0.8f, 0.1f, 0.9f), "res://Assets/UI/magic_upgrade_arrow.png", isUnique: true),
        new("unbound_greed", "Avaricia Desmedida", "+150% Oro por baja, pero enemigos +20% Vel. y +15% Vida", PerkTier.Maldito, new Vector3(1.0f, 0.6f, 0.0f), "res://Assets/UI/gold_coin.png", isUnique: true),
        new("unstable_vortex", "Vórtice Inestable", "Ataques disparan 2 proyectiles caóticos en área, pero Armadura = 0", PerkTier.Maldito, new Vector3(0.7f, 0.2f, 0.8f), "res://Assets/UI/magic_upgrade_arrow.png", isUnique: true),
        new("demon_deal", "Trato con el Demonio", "Cura al 100% y +1000g al instante, pero siguientes 2 oleadas tienen +35% enemigos", PerkTier.Maldito, new Vector3(0.95f, 0.15f, 0.15f), "res://Assets/UI/gold_coin.png", isUnique: true),
        new("blind_fury", "Frenesí Ciego", "+100% Ataque extra continuo (doble disparo), pero -35% Alcance", PerkTier.Maldito, new Vector3(1.0f, 0.3f, 0.2f), "res://Assets/UI/battle_axe.png", isUnique: true),

        // 🟣 TIER BUENO / ÉPICO (15% prob)
        new("chain_lightning", "Rayo Encadenado", "30% prob. de desatar un rayo eléctrico a 5 enemigos (75 daño)", PerkTier.Epico, new Vector3(0.3f, 0.7f, 1.0f), "res://Assets/UI/lightning_spell.png", isUnique: true),
        new("explosive_arrows", "Flechas Explosivas", "Ataques detonan en 5m infligiendo 50% de daño en área", PerkTier.Epico, new Vector3(1.0f, 0.5f, 0.1f), "res://Assets/UI/fire_spell.png", isUnique: true),
        new("frost_aura", "Aura de Escarcha", "Cada 4s emite un pulso que ralentiza 40% a enemigos en 20m por 3s", PerkTier.Epico, new Vector3(0.4f, 0.9f, 1.0f), "res://Assets/UI/magic_upgrade_arrow.png", isUnique: true),
        new("divine_thorns", "Armadura de Espinas", "+15 Armadura y refleja 50% del daño cuerpo a cuerpo al atacante", PerkTier.Epico, new Vector3(0.9f, 0.9f, 0.3f), "res://Assets/UI/battle_shield.png", isUnique: true),
        new("sniper_stance", "Disparo de Asedio", "+18 Alcance y +40% de daño a enemigos a más de 25m", PerkTier.Epico, new Vector3(0.2f, 0.8f, 0.4f), "res://Assets/UI/elf_warrior.png", isUnique: true),
        new("midas_touch", "Toque de Midas", "15% prob. al matar un enemigo de ganar +40g adicional inmediato", PerkTier.Epico, new Vector3(1.0f, 0.85f, 0.1f), "res://Assets/UI/gold_coin.png", isUnique: true),

        // 🔵 TIER MEDIO / RARO (35% prob - Stackeable hasta 5x)
        new("lifesteal", "Sed de Sangre", "Recupera 15% del daño infligido como vida por nivel", PerkTier.Raro, new Vector3(0.9f, 0.2f, 0.4f), "res://Assets/UI/golden_hammers.png", isUnique: false, maxStacks: 5),
        new("reinforced_multishot", "Multidisparo Reforzado", "+1 objetivo adicional alcanzado en multidisparo", PerkTier.Raro, new Vector3(0.4f, 0.8f, 0.9f), "res://Assets/UI/scroll_icon.png", isUnique: false, maxStacks: 5),
        new("royal_tribute", "Tributo Real", "+10 de oro pasivo por segundo por nivel", PerkTier.Raro, new Vector3(1.0f, 0.8f, 0.2f), "res://Assets/UI/gold_coin.png", isUnique: false, maxStacks: 5),
        new("piercing_shot", "Disparo Perforante", "Ataques atraviesan y dañan a un objetivo detrás (70% daño)", PerkTier.Raro, new Vector3(0.5f, 0.7f, 1.0f), "res://Assets/UI/battle_axe.png", isUnique: false, maxStacks: 5),
        new("sharp_precision", "Puntería Certera", "+20% Probabilidad de Crítico x2.0 por nivel", PerkTier.Raro, new Vector3(1.0f, 0.4f, 0.6f), "res://Assets/UI/battle_axe.png", isUnique: false, maxStacks: 5),

        // ⚪ TIER NORMAL / COMÚN (45% prob - Stackeable hasta 5x)
        new("arrow_sharpening", "Afilado de Flechas", "+12 de daño de ataque base por nivel", PerkTier.Normal, new Vector3(0.9f, 0.9f, 0.9f), "res://Assets/UI/battle_axe.png", isUnique: false, maxStacks: 5),
        new("reinforced_plates", "Placas Reforzadas", "+4 de armadura base por nivel", PerkTier.Normal, new Vector3(0.8f, 0.8f, 0.85f), "res://Assets/UI/battle_shield.png", isUnique: false, maxStacks: 5),
        new("hawk_eye", "Ojo de Halcón", "+6 de alcance de disparo por nivel", PerkTier.Normal, new Vector3(0.7f, 1.0f, 0.7f), "res://Assets/UI/elf_warrior.png", isUnique: false, maxStacks: 5),
        new("vigor_potion", "Poción de Vigor", "+350 de vida máxima y cura 200 de vida por nivel", PerkTier.Normal, new Vector3(0.6f, 1.0f, 0.6f), "res://Assets/UI/magic_upgrade_arrow.png", isUnique: false, maxStacks: 5)
    };

    // Configuración consolidada de las 15 Oleadas
    private static readonly WaveConfig[] Waves =
    {
        new(new[] { "zombie_soldier" }, TotalCount: 8, SpawnInterval: 0.45f, InterWaveDelay: 22.0f),
        new(new[] { "zombie_soldier" }, TotalCount: 12, SpawnInterval: 0.40f, InterWaveDelay: 20.0f),
        new(new[] { "zombie_soldier", "forest_goblin" }, TotalCount: 12, SpawnInterval: 0.35f, InterWaveDelay: 19.0f),
        new(new[] { "forest_goblin", "zombie_soldier", "forest_goblin" }, TotalCount: 16, SpawnInterval: 0.30f, InterWaveDelay: 18.0f),
        new(new[] { "giant_rock_golem", "zombie_soldier", "forest_goblin" }, TotalCount: 15, SpawnInterval: 0.40f, InterWaveDelay: 18.0f,
            MiniBoss: new MiniBossInfo("¡MINI-BOSS: Coloso de Roca en camino!", new Vector3(1f, 0.6f, 0.1f))),
        new(new[] { "cyber_dragon", "zombie_soldier" }, TotalCount: 14, SpawnInterval: 0.40f, InterWaveDelay: 16.0f),
        new(new[] { "zombie_warrior", "cyber_dragon" }, TotalCount: 16, SpawnInterval: 0.35f, InterWaveDelay: 16.0f),
        new(new[] { "flame_drake", "forest_goblin", "zombie_warrior" }, TotalCount: 22, SpawnInterval: 0.35f, InterWaveDelay: 15.0f),
        new(new[] { "dark_iron_ogre", "cyber_dragon", "flame_drake" }, TotalCount: 18, SpawnInterval: 0.35f, InterWaveDelay: 14.0f),
        new(new[] { "mech_spider_monster", "dark_iron_ogre", "forest_goblin" }, TotalCount: 20, SpawnInterval: 0.35f, InterWaveDelay: 14.0f,
            MiniBoss: new MiniBossInfo("¡MINI-BOSS: Araña Mecánica de Asedio!", new Vector3(1f, 0.3f, 0.1f))),
        new(new[] { "zombie_warrior", "flame_drake", "cyber_dragon" }, TotalCount: 26, SpawnInterval: 0.35f, InterWaveDelay: 13.0f),
        new(new[] { "dark_iron_ogre", "forest_goblin", "cyber_dragon" }, TotalCount: 28, SpawnInterval: 0.35f, InterWaveDelay: 13.0f),
        new(new[] { "dark_iron_ogre", "flame_drake", "zombie_warrior" }, TotalCount: 32, SpawnInterval: 0.35f, InterWaveDelay: 12.0f),
        new(new[] { "dark_iron_ogre", "flame_drake", "forest_goblin", "zombie_warrior" }, TotalCount: 42, SpawnInterval: 0.25f, InterWaveDelay: 15.0f),
        new(new[] { "dragon_titan_boss" }, TotalCount: 1, SpawnInterval: 1.00f, InterWaveDelay: 20.0f, IsBoss: true)
    };

    // ============================================================================
    // 2. ESTADO DEL JUEGO (Variables agrupadas por contexto)
    // ============================================================================

    // Estado del Motor y Jugador
    private IGameAPI _api = null!;
    private int _player;
    private bool _gameOver;
    private Vector3 _bossSpawnPos;

    // Estado del Héroe Central (Survivor)
    private IUnit? _survivorHero;
    private float _heroAuraTimer;

    // Estado de Habilidades y Tienda Roguelite (Zero-Alloc)
    private readonly Dictionary<string, int> _perkStacks = new(32);
    private readonly PerkDefinition?[] _currentDraft = new PerkDefinition?[3];
    private readonly bool[] _slotSold = new bool[3];
    private float _shopRotationTimer;
    private int _perksPicked;
    private int _rerollCount;
    private float _frostAuraTimer;

    // Flags y acumuladores de Perks
    private bool _hasBloodPact;
    private bool _hasCrystalMonolith;
    private bool _hasUnboundGreed;
    private bool _hasUnstableVortex;
    private bool _hasBlindFury;
    private bool _hasChainLightning;
    private bool _hasExplosiveArrows;
    private bool _hasFrostAura;
    private bool _hasDivineThorns;
    private bool _hasSniperStance;
    private bool _hasMidasTouch;
    private bool _hasPiercingShot;
    private int _demonDealExtraWavesRemaining;

    // Estado de Oleadas
    private int _currentWave;
    private int _waveBeingSpawned;
    private int _remainingToSpawn;
    private int _spawnedInWave;
    private int _aliveInWave;
    private float _spawnTimer;
    private float _spawnInterval;
    private float _interWaveTimer;
    private bool _spawning;
    private int _totalKills;
    private float _currentIncomePerSecond;

    // Estado del Raid Boss (Oleada 15)
    private IUnit? _raidBossInstance;
    private bool _bossEnraged;
    private float _bossEscortTimer;
    private float _bossIndicatorTimer;
    private bool _bossDefeated;

    // Temporizadores de Muestreo (Zero-Alloc a 30Hz)
    private float _economyScanTimer;

    // ============================================================================
    // 3. CICLO DE VIDA PRINCIPAL (Initialize, Update)
    // ============================================================================

    public void Initialize(IGameAPI api)
    {
        _api = api;
        _player = 0;
        _currentWave = 0;
        _waveBeingSpawned = 0;
        _gameOver = false;
        _spawning = false;
        _totalKills = 0;
        _bossEnraged = false;
        _bossDefeated = false;
        _raidBossInstance = null;
        _economyScanTimer = 0f;
        _bossEscortTimer = 0f;
        _bossIndicatorTimer = 0f;
        _heroAuraTimer = 0f;
        _frostAuraTimer = 0f;
        _currentIncomePerSecond = BasePassiveGoldPerSecond;

        // Inicializar Estado de Habilidades y Tienda
        _perkStacks.Clear();
        _perksPicked = 0;
        _rerollCount = 0;
        _currentDraft[0] = null;
        _currentDraft[1] = null;
        _currentDraft[2] = null;

        _hasBloodPact = false;
        _hasCrystalMonolith = false;
        _hasUnboundGreed = false;
        _hasUnstableVortex = false;
        _hasBlindFury = false;
        _hasChainLightning = false;
        _hasExplosiveArrows = false;
        _hasFrostAura = false;
        _hasDivineThorns = false;
        _hasSniperStance = false;
        _hasMidasTouch = false;
        _hasPiercingShot = false;
        _demonDealExtraWavesRemaining = 0;

        // Oro inicial
        _api.SetPlayerGold(_player, InitialGold);

        // Coordenadas base y spawn
        _bossSpawnPos = new Vector3(0f, SpawnHeight, -65f);

        // Generar Héroe Central
        SpawnSurvivorHero();

        // Suscribirse a eventos
        _api.OnUnitDied += OnUnitDied;
        _api.OnUnitDamaged += OnUnitDamaged;
        _api.OnPlayerChatMessage += OnPlayerChatMessage;
        _api.OnSpellCast += OnSpellCast;

        // Inicializar tienda de habilidades
        _slotSold[0] = false;
        _slotSold[1] = false;
        _slotSold[2] = false;
        _shopRotationTimer = ShopRotationInterval;
        TriggerNewShopRotation();

        // Configuración de UI y Leaderboard inicial
        _api.SetLeaderboardVisible("TOWER SURVIVORS", true);
        UpdateLeaderboardDisplay();

        _api.BroadcastMessage("¡Bienvenidos a TOWER SURVIVORS! Tienda rotativa de habilidades por oro. Compra con [Q, W, E] (-1, -2, -3), Reroll con [R] o Hechizo con [D].");
        ShowNextWaveCountdown();
    }

    public void Update(IGameAPI api, float delta)
    {
        if (_gameOver) return;

        CheckDefeatCondition();
        UpdateHeroTickEffects(delta);
        UpdatePassiveIncome(delta);
        UpdateEconomyAndUI(delta);
        UpdateShopRotationTimer(delta);
        UpdateBossEncounterIfActive(delta);
        UpdateWaveStateMachine(delta);
    }

    private void SpawnSurvivorHero()
    {
        _survivorHero = null;
        foreach (var unit in _api.GetAllUnits())
        {
            if (unit.UnitId == "survivor_hero")
            {
                _survivorHero = unit;
                break;
            }
        }
        if (_survivorHero == null)
        {
            _survivorHero = _api.SpawnUnit("survivor_hero", Coordinates.HeroCenter, false, bypassPopulation: true);
        }
        if (_survivorHero == null) return;

        _survivorHero.Speed = 0f;
        _api.SetUnitColor(_survivorHero, new Vector3(1f, 1f, 1f));
        RecalculateHeroStats();
        _api.SelectUnit(_survivorHero);
    }

    private void CheckDefeatCondition()
    {
        if (_survivorHero != null && !_survivorHero.IsDead && _survivorHero.Health > 0f) return;

        _gameOver = true;
        _api.StopCountdownTimer();
        _api.ShowFeedbackText("¡EL HÉROE DEL SURVIVOR HA CAÍDO!", new Vector3(1f, 0.2f, 0.2f));
        _api.TriggerDefeat();
    }

    private void UpdateHeroTickEffects(float delta)
    {
        if (_survivorHero == null || _survivorHero.IsDead) return;

        // Inmovilidad absoluta (Speed = 0)
        _survivorHero.Speed = 0f;

        // 1. Drenaje de Vida de Pacto de Sangre (-2% de vida actual por segundo)
        if (_hasBloodPact)
        {
            float drain = MathF.Max(1.0f, _survivorHero.Health * 0.02f * delta);
            _survivorHero.Health = MathF.Max(1.0f, _survivorHero.Health - drain);
        }

        // 2. Aura de Escarcha periódica (cada 4s en radio 20m)
        if (_hasFrostAura)
        {
            _frostAuraTimer -= delta;
            if (_frostAuraTimer <= 0f)
            {
                _frostAuraTimer = 4.0f;
                _api.SpawnVisualEffect("holylight", _survivorHero.Position, 2.5f);
                foreach (var enemy in _api.GetUnitsInRadius(_survivorHero.Position, 20f))
                {
                    if (enemy != null && enemy.IsEnemy && !enemy.IsDead && enemy.Health > 0f)
                    {
                        enemy.Speed = MathF.Max(1.5f, enemy.Speed * 0.60f);
                        _api.CreateFloatingText("¡ESCARCHA!", enemy.Position + new Vector3(0, 1.5f, 0), new Vector3(0.4f, 0.9f, 1.0f), 0.7f);
                    }
                }
            }
        }

        // 3. Aura visual permanente (Espinas Divinas)
        if (_hasDivineThorns)
        {
            _heroAuraTimer -= delta;
            if (_heroAuraTimer <= 0f)
            {
                _heroAuraTimer = 2.5f;
                _api.SpawnVisualEffect("holylight", _survivorHero.Position + new Vector3(0f, 1f, 0f), 1.2f);
            }
        }
    }

    private void UpdatePassiveIncome(float delta)
    {
        _api.AdjustPlayerGold(_player, delta * _currentIncomePerSecond);
    }

    private void UpdateEconomyAndUI(float delta)
    {
        _economyScanTimer -= delta;
        if (_economyScanTimer > 0f) return;

        _economyScanTimer = 1.0f;
        int royalTributeStacks = GetPerkStackCount("royal_tribute");
        _currentIncomePerSecond = BasePassiveGoldPerSecond + (royalTributeStacks * 10.0f);
        UpdateLeaderboardDisplay();
    }

    // ============================================================================
    // 4. SISTEMA DE TIENDA ROGUELITE DE HABILIDADES POR ORO (Zero-Alloc)
    // ============================================================================

    private void UpdateShopRotationTimer(float delta)
    {
        _shopRotationTimer -= delta;
        if (_shopRotationTimer <= 0f)
        {
            TriggerNewShopRotation();
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

    private void TriggerNewShopRotation()
    {
        _shopRotationTimer = ShopRotationInterval;
        _slotSold[0] = false;
        _slotSold[1] = false;
        _slotSold[2] = false;

        _currentDraft[0] = null;
        _currentDraft[1] = null;
        _currentDraft[2] = null;

        for (int slot = 0; slot < 3; slot++)
        {
            _currentDraft[slot] = RollSinglePerk();
        }

        // Registrar dinámicamente nombres, tooltips e iconos en los 5 botones del HUD
        for (int slot = 0; slot < 3; slot++)
        {
            var p = _currentDraft[slot];
            if (p != null)
            {
                float cost = GetPerkCost(p);
                string key = slot switch { 0 => "Q", 1 => "W", _ => "E" };
                string abilityId = $"perk_choose_{slot + 1}";
                string name = $"[{key}] {p.Name} ({cost:F0}g)";
                string tooltip = $"[{key}] {p.Name} ({p.Tier}) — Coste: {cost:F0} Oro\n{p.Description}";
                _api.RegisterAbility(abilityId, name, tooltip, p.IconPath, isInstant: true);
            }
        }

        // Registrar Reroll y Meteorito
        float rerollCost = _rerollCount == 0 ? 0f : ShopRerollCost;
        string rerollName = _rerollCount == 0 ? "[R] Reroll (Gratis)" : $"[R] Reroll ({rerollCost:F0}g)";
        _api.RegisterAbility("perk_reroll", rerollName, $"[R] Barajar Ofertas de Tienda\nCoste: {rerollCost:F0} Oro", "res://Assets/UI/gold_coin.png", isInstant: true);
        _api.RegisterAbility("hero_meteor_spell", "[D] Meteorito Ígneo", "[D] Meteorito Ígneo (Cooldown: 15s)\nLanza un meteorito colosal sobre el área objetivo infligiendo 120 de daño.", "res://Assets/UI/fire_spell.png", isInstant: false);

        _api.PlayClickSound();
        _api.ShowFeedbackText("¡TIENDA RENOVADA! Compra con Q/W/E (-1, -2, -3) o Reroll [R]", new Vector3(1f, 0.9f, 0.2f));

        if (_survivorHero != null)
        {
            _api.CreateFloatingText("¡TIENDA LISTA!", _survivorHero.Position + new Vector3(0, 2.8f, 0), new Vector3(1f, 0.9f, 0.2f), 1.5f);
        }

        PrintCurrentDraft();
        UpdateLeaderboardDisplay();
    }

    private PerkDefinition RollSinglePerk()
    {
        // Ponderación por Tier: Normal 45, Raro 35, Épico 15, Maldito 5
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

            // Filtrar candidatos elegibles del Tier
            var candidates = PerkPool.Where(p => p.Tier == targetTier && CanPickPerk(p) && !_currentDraft.Contains(p)).ToArray();
            if (candidates.Length > 0)
            {
                int index = _api.RandomInt(0, candidates.Length - 1);
                return candidates[index];
            }
        }

        // Fallback garantizado a cualquier perk disponible
        var anyAvailable = PerkPool.Where(p => CanPickPerk(p) && !_currentDraft.Contains(p)).ToArray();
        if (anyAvailable.Length > 0)
        {
            return anyAvailable[_api.RandomInt(0, anyAvailable.Length - 1)];
        }

        return PerkPool[0];
    }

    private bool CanPickPerk(PerkDefinition perk)
    {
        int stacks = GetPerkStackCount(perk.Id);
        return stacks < perk.MaxStacks;
    }

    private int GetPerkStackCount(string perkId)
    {
        return _perkStacks.TryGetValue(perkId, out int count) ? count : 0;
    }

    private void TryBuyPerk(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 3) return;
        if (_slotSold[slotIndex])
        {
            _api.ShowFeedbackText("¡Esta oferta ya fue comprada!", new Vector3(1f, 0.5f, 0.2f));
            return;
        }

        var perk = _currentDraft[slotIndex];
        if (perk == null) return;

        float cost = GetPerkCost(perk);
        float currentGold = _api.GetPlayerGold(_player);
        if (currentGold < cost)
        {
            _api.ShowFeedbackText($"¡Oro insuficiente! ({currentGold:F0}/{cost:F0}g)", new Vector3(1f, 0.2f, 0.2f));
            if (_survivorHero != null)
            {
                _api.CreateFloatingText($"¡Falta {(cost - currentGold):F0}g!", _survivorHero.Position + new Vector3(0, 2f, 0), new Vector3(1f, 0.2f, 0.2f), 0.9f);
            }
            return;
        }

        _api.AdjustPlayerGold(_player, -cost);
        _slotSold[slotIndex] = true;

        int current = GetPerkStackCount(perk.Id);
        _perkStacks[perk.Id] = current + 1;
        _perksPicked++;

        // Actualizar botón a [COMPRADO]
        string keyChar = slotIndex switch { 0 => "Q", 1 => "W", _ => "E" };
        string boughtId = $"perk_choose_{slotIndex + 1}";
        _api.RegisterAbility(boughtId, $"[COMPRADO] {perk.Name}", $"[COMPRADO] {perk.Name} (x{_perkStacks[perk.Id]})\nYa adquirido en esta rotación.", perk.IconPath, isInstant: true);

        ApplyPerkAcquisition(perk);
        RecalculateHeroStats();

        _api.BroadcastMessage($"[Comprado {cost:F0}g] {perk.Name} ({perk.Tier}) (x{_perkStacks[perk.Id]}): {perk.Description}");
        _api.ShowFeedbackText($"¡Comprado: {perk.Name} (x{_perkStacks[perk.Id]})!", perk.Color);
        _api.PlayClickSound();

        if (_survivorHero != null)
        {
            _api.CreateFloatingText($"+{perk.Name} (x{_perkStacks[perk.Id]})", _survivorHero.Position + new Vector3(0, 2.5f, 0), perk.Color, 1.4f);
            _api.SpawnVisualEffect("holylight", _survivorHero.Position + new Vector3(0, 1.2f, 0), 1.3f);
        }

        UpdateLeaderboardDisplay();

        // Si compró los 3 slots de la tienda, rota inmediatamente
        if (_slotSold[0] && _slotSold[1] && _slotSold[2])
        {
            TriggerNewShopRotation();
        }
    }

    private void ApplyPerkAcquisition(PerkDefinition perk)
    {
        switch (perk.Id)
        {
            case "blood_pact":
                _hasBloodPact = true;
                break;
            case "crystal_monolith":
                _hasCrystalMonolith = true;
                break;
            case "unbound_greed":
                _hasUnboundGreed = true;
                break;
            case "unstable_vortex":
                _hasUnstableVortex = true;
                break;
            case "demon_deal":
                _demonDealExtraWavesRemaining = 2;
                _api.AdjustPlayerGold(_player, 1000f);
                if (_survivorHero != null) _survivorHero.Health = _survivorHero.MaxHealth;
                _api.ShowFeedbackText("¡Trato con el Demonio! +1000g y Curación 100%. Las próximas 2 oleadas serán más densas.", new Vector3(0.95f, 0.15f, 0.15f));
                break;
            case "blind_fury":
                _hasBlindFury = true;
                break;
            case "chain_lightning":
                _hasChainLightning = true;
                break;
            case "explosive_arrows":
                _hasExplosiveArrows = true;
                break;
            case "frost_aura":
                _hasFrostAura = true;
                break;
            case "divine_thorns":
                _hasDivineThorns = true;
                break;
            case "sniper_stance":
                _hasSniperStance = true;
                break;
            case "midas_touch":
                _hasMidasTouch = true;
                break;
            case "piercing_shot":
                _hasPiercingShot = true;
                break;
            case "vigor_potion":
                if (_survivorHero != null) _survivorHero.Health = MathF.Min(_survivorHero.MaxHealth, _survivorHero.Health + 200f);
                break;
        }
    }

    private void TryRerollShop()
    {
        float cost = _rerollCount == 0 ? 0f : ShopRerollCost;
        if (cost > 0f && _api.GetPlayerGold(_player) < cost)
        {
            _api.ShowFeedbackText($"Oro insuficiente para Reroll ({cost:F0}g necesario).", new Vector3(1f, 0.3f, 0.3f));
            return;
        }

        if (cost > 0f)
        {
            _api.AdjustPlayerGold(_player, -cost);
        }

        _rerollCount++;
        _api.PlayClickSound();
        _api.ShowFeedbackText(_rerollCount == 1 ? "¡Reroll gratuito utilizado!" : $"¡Reroll por {cost:F0}g aplicado!", new Vector3(1f, 0.85f, 0.2f));

        TriggerNewShopRotation();
    }

    private void PrintCurrentDraft()
    {
        for (int i = 0; i < 3; i++)
        {
            var p = _currentDraft[i];
            if (p != null)
            {
                string status = _slotSold[i] ? "[COMPRADO]" : $"{GetPerkCost(p):F0}g";
                _api.BroadcastMessage($"[{i + 1}] {p.Name} ({p.Tier}) - {status}: {p.Description}");
            }
        }
    }

    private string GetHeroProjectileId()
    {
        if (_hasExplosiveArrows) return "fire_shard";
        if (_hasFrostAura) return "frost_shard";
        return "survivor_arrow";
    }

    // ============================================================================
    // 5. RECALCULACIÓN DE ESTADÍSTICAS DEL HÉROE (Idempotente)
    // ============================================================================

    private void RecalculateHeroStats()
    {
        if (_survivorHero == null || _survivorHero.IsDead) return;

        // ---- 1. VIDA MÁXIMA ----
        int vigorStacks = GetPerkStackCount("vigor_potion");
        float bonusMaxHp = vigorStacks * 350.0f;
        float hpMultiplier = _hasCrystalMonolith ? 0.60f : 1.0f;
        float prevMaxHp = _survivorHero.MaxHealth;
        float calculatedMaxHp = MathF.Max(150.0f, (BaseHeroHp + bonusMaxHp) * hpMultiplier);

        _survivorHero.MaxHealth = calculatedMaxHp;
        if (calculatedMaxHp > prevMaxHp && prevMaxHp > 0f)
        {
            _survivorHero.Health += (calculatedMaxHp - prevMaxHp);
        }
        _survivorHero.Health = MathF.Min(_survivorHero.Health, _survivorHero.MaxHealth);

        // ---- 2. DAÑO ----
        int arrowSharpenStacks = GetPerkStackCount("arrow_sharpening");
        float flatBonusDamage = arrowSharpenStacks * 12.0f;
        float damageMult = _hasBloodPact ? 2.0f : 1.0f;
        _survivorHero.Damage = (BaseHeroDamage + flatBonusDamage) * damageMult;

        // ---- 3. ALCANCE ----
        int hawkEyeStacks = GetPerkStackCount("hawk_eye");
        float flatBonusRange = (hawkEyeStacks * 6.0f) + (_hasCrystalMonolith ? 15.0f : 0f) + (_hasSniperStance ? 18.0f : 0f);
        float rangeMult = _hasBlindFury ? 0.65f : 1.0f;
        _survivorHero.Range = MathF.Max(15.0f, (BaseHeroRange + flatBonusRange) * rangeMult);

        // ---- 4. ARMADURA ----
        if (_hasUnstableVortex)
        {
            _survivorHero.Armor = 0f;
        }
        else
        {
            int plateStacks = GetPerkStackCount("reinforced_plates");
            _survivorHero.Armor = BaseHeroArmor + (plateStacks * 4.0f) + (_hasDivineThorns ? 15.0f : 0f);
        }
    }

    // ============================================================================
    // 6. MÁQUINA DE ESTADO DE OLEADAS (BeginWave, Spawn, Timing)
    // ============================================================================

    private void UpdateWaveStateMachine(float delta)
    {
        if (_spawning)
        {
            UpdateActiveWave(delta);
        }
        else
        {
            _interWaveTimer -= delta;
            if (_interWaveTimer <= 0f)
            {
                BeginWave(_currentWave + 1);
            }
        }
    }

    private void UpdateActiveWave(float delta)
    {
        if (_remainingToSpawn > 0)
        {
            UpdateSpawning(delta);
            return;
        }

        if (_aliveInWave > 0 || (_waveBeingSpawned == 15 && !_bossDefeated)) return;

        _spawning = false;
        _currentWave++;

        if (_currentWave >= TotalWaves)
        {
            EndGameWithVictory();
        }
        else
        {
            ShowNextWaveCountdown();
        }
    }

    private void UpdateSpawning(float delta)
    {
        _spawnTimer -= delta;
        if (_spawnTimer > 0f) return;

        _spawnTimer = _spawnInterval;
        SpawnNextEnemyInWave();
    }

    private void EndGameWithVictory()
    {
        _gameOver = true;
        _api.StopCountdownTimer();
        _api.ShowFeedbackText("¡VICTORIA TOTAL! ¡HAS SUPERVIVIDO A TODAS LAS OLEADAS!", new Vector3(1f, 0.9f, 0.2f));
        _api.TriggerVictory();
    }

    private void ShowNextWaveCountdown()
    {
        int next = _currentWave + 1;
        if (next <= TotalWaves)
        {
            float delay = Waves[next - 1].InterWaveDelay;
            _interWaveTimer = delay;
            _api.StartCountdownTimer(delay, $"Oleada {next}");
            _api.BroadcastMessage($"Siguiente oleada {next} en {delay:F0}s. ¡Prepárate!");
        }
    }

    private void BeginWave(int wave)
    {
        _waveBeingSpawned = wave;
        _spawning = true;
        var config = Waves[wave - 1];

        // Trato con el Demonio añade +35% de enemigos si está activo
        int extraCount = 0;
        if (_demonDealExtraWavesRemaining > 0)
        {
            extraCount = (int)MathF.Ceiling(config.TotalCount * 0.35f);
            _demonDealExtraWavesRemaining--;
        }

        _remainingToSpawn = config.TotalCount + extraCount;
        _spawnedInWave = 0;
        _aliveInWave = _remainingToSpawn;
        _spawnInterval = config.SpawnInterval;
        _spawnTimer = 0f;

        _api.StopCountdownTimer();
        _api.PlayWarningSound();

        // Renovar ofertas de la tienda al iniciar nueva oleada si queda poco tiempo
        if (_shopRotationTimer < 15f)
        {
            TriggerNewShopRotation();
        }

        if (config.IsBoss)
        {
            TriggerRaidBossCinematic();
        }
        else if (config.MiniBoss is { } miniBoss)
        {
            _api.ShowFeedbackText(miniBoss.Banner, miniBoss.Color);
        }
        else
        {
            _api.BroadcastMessage($"¡Oleada {wave} entrante!");
        }
    }

    private void SpawnNextEnemyInWave()
    {
        var config = Waves[_waveBeingSpawned - 1];
        string unitType = config.Composition[_spawnedInWave % config.Composition.Length];

        IUnit? unit;
        if (unitType == "dragon_titan_boss")
        {
            unit = _api.SpawnUnit(unitType, _bossSpawnPos, true);
            if (unit != null)
            {
                _raidBossInstance = unit;
                unit.Scale = 5.0f;
            }
        }
        else
        {
            unit = _api.SpawnUnit(unitType, RandomSpawnPointOnRing(), true);
        }

        if (unit != null)
        {
            // Escalado adaptativo de enemigos vs mejoras del jugador
            float hpScale = 1.0f + (_perksPicked * 0.05f) + (_waveBeingSpawned * 0.03f) + (_hasUnboundGreed ? 0.15f : 0f);
            unit.MaxHealth *= hpScale;
            unit.Health = unit.MaxHealth;

            if (_hasUnboundGreed)
            {
                unit.Speed *= 1.20f;
            }

            unit.AttackMove(Coordinates.HeroCenter);
            _spawnedInWave++;
            _remainingToSpawn--;
        }
    }

    private Vector3 RandomSpawnPointOnRing()
    {
        float angle = _api.RandomFloat(0f, MathF.Tau);
        float radius = _api.RandomFloat(SpawnRingMinRadius, SpawnRingMaxRadius);
        return new Vector3(MathF.Cos(angle) * radius, SpawnHeight, MathF.Sin(angle) * radius);
    }

    // ============================================================================
    // 7. ENCUENTRO CON EL RAID BOSS (Cinemática, Fases, Escoltas)
    // ============================================================================

    private void UpdateBossEncounterIfActive(float delta)
    {
        if (_waveBeingSpawned != 15) return;
        var boss = _raidBossInstance;
        if (boss == null || boss.IsDead) return;

        UpdateBossTargetIndicator(boss, delta);
        UpdateBossEnragePhase(boss);
        UpdateBossEscortTimer(delta);
    }

    private void UpdateBossTargetIndicator(IUnit boss, float delta)
    {
        _bossIndicatorTimer -= delta;
        if (_bossIndicatorTimer > 0f) return;

        _bossIndicatorTimer = 2.0f;
        _api.SpawnTargetIndicator(boss.Position, new Vector3(1f, 0.1f, 0.1f));
    }

    private void UpdateBossEnragePhase(IUnit boss)
    {
        if (_bossEnraged || boss.Health > boss.MaxHealth * 0.5f) return;

        _bossEnraged = true;
        boss.Speed = 8.25f;
        _api.SetUnitColor(boss, new Vector3(1f, 0.2f, 0.2f));
        _api.ShowFeedbackText("¡EL DRAGÓN TITÁN ESTÁ ENFURECIDO (+50% VELOCIDAD)!", new Vector3(1f, 0.15f, 0.15f));
        _api.PlayWarningSound();
        _api.ShakeCamera(3.0f, 1.5f);
    }

    private void UpdateBossEscortTimer(float delta)
    {
        _bossEscortTimer -= delta;
        if (_bossEscortTimer > 0f) return;

        _bossEscortTimer = _bossEnraged ? 8.0f : 12.0f;
        SpawnBossEscortWave();
    }

    private void TriggerRaidBossCinematic()
    {
        _bossEscortTimer = 6.0f;
        _bossIndicatorTimer = 0f;

        _api.ShowFeedbackText("¡ALERTA DE RAID: EL DRAGÓN TITÁN HA ENTRADO EN EL CAMPO DE BATALLA!", new Vector3(1f, 0.85f, 0.1f));
        _api.PlayWarningSound();
        _api.ShakeCamera(2.5f, 2.0f);
        _api.PanCameraTo(_bossSpawnPos, 1.5f);
        _api.PingMinimap(_bossSpawnPos);
    }

    private void SpawnBossEscortWave()
    {
        for (int s = 0; s < 8; s++) SpawnSingleEscort("zombie_soldier");
        for (int g = 0; g < 4; g++) SpawnSingleEscort("forest_goblin");
        for (int o = 0; o < (_bossEnraged ? 2 : 0); o++) SpawnSingleEscort("dark_iron_ogre");

        _api.BroadcastMessage("¡El Dragón Titán invoca una horda de escoltas!");
    }

    private void SpawnSingleEscort(string unitType)
    {
        var unit = _api.SpawnUnit(unitType, RandomSpawnPointOnRing(), true);
        if (unit != null)
        {
            unit.AttackMove(Coordinates.HeroCenter);
            _aliveInWave++;
        }
    }

    // ============================================================================
    // 8. COMBATE DEL HÉROE (Procs de Perks, Furia, Crítico, Lifesteal, Rayos)
    // ============================================================================

    private void OnUnitDamaged(IUnit victim, IUnit attacker, float damage)
    {
        if (_gameOver || victim == null || attacker == null || victim.IsDead) return;

        // A. Daño infligido POR el Héroe a un enemigo
        if (victim.IsEnemy && !attacker.IsEnemy && attacker.UnitId == "survivor_hero")
        {
            ApplyHeroAttackProcs(victim, damage);
        }

        // B. Daño recibido POR el Héroe de un enemigo (Espinas Divinas)
        if (!victim.IsEnemy && victim.UnitId == "survivor_hero" && attacker.IsEnemy)
        {
            if (_hasDivineThorns && !attacker.IsDead && attacker.Health > 0f)
            {
                float reflectDamage = damage * 0.50f;
                DealBonusDamage(attacker, reflectDamage);
                _api.SpawnVisualEffect("lightning", attacker.Position, 0.6f);
                _api.CreateFloatingText($"ESPINAS -{reflectDamage:F0}", attacker.Position + new Vector3(0, 1.8f, 0), new Vector3(0.9f, 0.9f, 0.2f), 0.7f);
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

    private void ApplyHeroAttackProcs(IUnit victim, float damage)
    {
        if (_survivorHero == null) return;

        // 1. GOLPE CRÍTICO (Puntería Certera + Monolito de Cristal)
        int sharpStacks = GetPerkStackCount("sharp_precision");
        float critChance = sharpStacks * 0.20f;
        if (critChance > 0f && _api.RandomFloat(0f, 1f) < critChance)
        {
            float critMult = _hasCrystalMonolith ? 4.5f : 2.0f;
            float extraCritDamage = damage * (critMult - 1.0f);
            DealBonusDamage(victim, extraCritDamage);
            _api.CreateFloatingText($"¡CRÍTICO x{critMult:F1}!", victim.Position + new Vector3(0, 2.2f, 0), new Vector3(1f, 0.2f, 0.2f), 0.9f);
        }

        // 2. DISPARO DE ASEDIO (Sniper Stance a más de 25m)
        if (_hasSniperStance)
        {
            float dist = Vector3.Distance(_survivorHero.Position, victim.Position);
            if (dist >= 25.0f)
            {
                float bonusSnipe = damage * 0.40f;
                DealBonusDamage(victim, bonusSnipe);
                _api.CreateFloatingText("¡ASIEDO +40%!", victim.Position + new Vector3(0, 2.0f, 0), new Vector3(0.2f, 0.9f, 0.4f), 0.8f);
            }
        }

        // 3. ROBO DE VIDA (Lifesteal / Sed de Sangre)
        int lifestealStacks = GetPerkStackCount("lifesteal");
        if (lifestealStacks > 0)
        {
            float healAmount = damage * (lifestealStacks * 0.15f);
            _survivorHero.Health = MathF.Min(_survivorHero.MaxHealth, _survivorHero.Health + healAmount);
            _api.CreateFloatingText($"+{healAmount:F0} HP", _survivorHero.Position + new Vector3(0, 1.8f, 0), new Vector3(0.2f, 1f, 0.4f), 0.6f);
        }

        // 4. RAYO ENCADENADO (Chain Lightning 30% prob -> 5 objetivos)
        if (_hasChainLightning && _api.RandomFloat(0f, 1f) < 0.30f)
        {
            ApplyChainLightning(victim);
        }

        // 5. FLECHAS EXPLOSIVAS (AoE 5m)
        if (_hasExplosiveArrows)
        {
            ApplyExplosiveArrows(victim, damage);
        }

        // 6. DISPARO PERFORANTE (Piercing Shot a objetivo detrás)
        if (_hasPiercingShot)
        {
            ApplyPiercingShot(victim, damage);
        }

        // 7. VÓRTICE INESTABLE (Disparos caóticos)
        if (_hasUnstableVortex)
        {
            ApplyUnstableVortex(victim);
        }

        // 8. FURIA DE FLECHAS (Frenesí Ciego)
        if (_hasBlindFury)
        {
            FireFuryArrow(victim);
        }

        // 9. MULTIDISPARO REFORZADO
        int reinforcedStacks = GetPerkStackCount("reinforced_multishot");
        if (reinforcedStacks > 0)
        {
            ApplyMultiShot(victim, damage, reinforcedStacks);
        }
    }

    private void ApplyChainLightning(IUnit primary)
    {
        _api.SpawnVisualEffect("lightning", primary.Position, 1.5f);
        _api.CreateFloatingText("¡RAYO!", primary.Position + new Vector3(0, 2f, 0), new Vector3(0.3f, 0.7f, 1.0f), 0.8f);

        int jumps = 0;
        foreach (var nearby in _api.GetUnitsInRadius(primary.Position, 14f))
        {
            if (nearby == null || !nearby.IsEnemy || nearby.UniqueId == primary.UniqueId || nearby.IsDead || nearby.Health <= 0f)
                continue;
            if (jumps >= 4) break;

            DealBonusDamage(nearby, 75f);
            _api.SpawnVisualEffect("lightning", nearby.Position, 1.0f);
            _api.SpawnProjectile("survivor_arrow", primary.Position, nearby.Position, 50f);
            jumps++;
        }
    }

    private void ApplyExplosiveArrows(IUnit primary, float damage)
    {
        _api.SpawnVisualEffect("fireblast", primary.Position, 1.4f);
        if (_survivorHero != null)
        {
            _api.SpawnProjectile("fire_shard", _survivorHero.Position, primary.Position, 40f);
        }
        float splashDamage = damage * 0.50f;

        foreach (var nearby in _api.GetUnitsInRadius(primary.Position, 5f))
        {
            if (nearby == null || !nearby.IsEnemy || nearby.UniqueId == primary.UniqueId || nearby.IsDead || nearby.Health <= 0f)
                continue;

            DealBonusDamage(nearby, splashDamage);
        }
    }

    private void ApplyPiercingShot(IUnit primary, float damage)
    {
        float pierceDamage = damage * 0.70f;
        var behind = _api.GetUnitsInRadius(primary.Position, 8f)
            .FirstOrDefault(u => u != null && u.IsEnemy && u.UniqueId != primary.UniqueId && !u.IsDead && u.Health > 0f);

        if (behind != null)
        {
            DealBonusDamage(behind, pierceDamage);
            _api.SpawnProjectile(GetHeroProjectileId(), primary.Position, behind.Position, 55f);
            _api.CreateFloatingText("¡PERFORACIÓN!", behind.Position + new Vector3(0, 1.8f, 0), new Vector3(0.5f, 0.7f, 1.0f), 0.7f);
        }
    }

    private void ApplyUnstableVortex(IUnit primary)
    {
        int shotCount = 0;
        foreach (var nearby in _api.GetUnitsInRadius(primary.Position, 15f))
        {
            if (nearby == null || !nearby.IsEnemy || nearby.UniqueId == primary.UniqueId || nearby.IsDead || nearby.Health <= 0f)
                continue;
            if (shotCount >= 2) break;

            DealBonusDamage(nearby, _survivorHero!.Damage * 0.60f);
            _api.SpawnProjectile(GetHeroProjectileId(), _survivorHero.Position, nearby.Position, 36f);
            shotCount++;
        }
    }

    private void FireFuryArrow(IUnit victim)
    {
        IUnit? secondary = _api.GetUnitsInRadius(victim.Position, 10f)
            .FirstOrDefault(u => u != null && u.IsEnemy && u.UniqueId != victim.UniqueId && !u.IsDead && u.Health > 0f);

        if (secondary == null) return;

        DealBonusDamage(secondary, _survivorHero!.Damage);
        _api.SpawnProjectile(GetHeroProjectileId(), _survivorHero.Position, secondary.Position, 48f);
        _api.CreateFloatingText("¡FURIA!", secondary.Position + new Vector3(0f, 2f, 0f), new Vector3(1f, 0.4f, 0.1f), 0.8f);
    }

    private void ApplyMultiShot(IUnit victim, float damage, int targetLimit)
    {
        float splash = damage * MultiShotSplashRatio;
        int hits = 0;

        foreach (var nearby in _api.GetUnitsInRadius(victim.Position, 7f))
        {
            if (nearby == null || !nearby.IsEnemy || nearby.UniqueId == victim.UniqueId || nearby.IsDead || nearby.Health <= 0f)
                continue;
            if (hits >= targetLimit) break;

            DealBonusDamage(nearby, splash);
            _api.SpawnProjectile(GetHeroProjectileId(), _survivorHero!.Position, nearby.Position, 42f);
            hits++;
        }
    }

    // ============================================================================
    // 9. ECONOMÍA Y RECOMPENSAS (Bounties, Toque de Midas, Avaricia)
    // ============================================================================

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

        _aliveInWave = Math.Max(0, _aliveInWave - 1);
        _totalKills++;

        // Recompensa base y multiplicador de Avaricia Desmedida (+150%)
        float bounty = GetUnitBounty(unit.UnitId);
        if (_hasUnboundGreed)
        {
            bounty *= 2.50f;
        }

        // Toque de Midas (15% de probabilidad al matar no-boss de ganar +40g)
        if (_hasMidasTouch && unit.UnitId != "dragon_titan_boss" && _api.RandomFloat(0f, 1f) < 0.15f)
        {
            bounty += 40.0f;
            _api.CreateFloatingText("¡MIDAS +40g!", unit.Position + new Vector3(0, 2.3f, 0), new Vector3(1f, 0.85f, 0.1f), 1.3f);
        }

        _api.AdjustPlayerGold(_player, bounty);
        _api.CreateFloatingText($"+{bounty:F0}g", unit.Position + new Vector3(0, 1.8f, 0), new Vector3(1f, 0.85f, 0.1f), 1.2f);

        // Muerte del Gran Raid Boss Final
        if (unit.UnitId == "dragon_titan_boss")
        {
            _bossDefeated = true;
            _gameOver = true;
            _api.StopCountdownTimer();
            _api.ShowFeedbackText("¡EL DRAGÓN TITÁN HA SIDO DERROTADO! ¡VICTORIA TOTAL!", new Vector3(1f, 0.9f, 0.2f));
            _api.PlayClickSound();
            _api.TriggerVictory();
        }
    }

    // ============================================================================
    // 10. MEJORAS, DRAFT Y COMANDOS (Chat + Botones de Habilidad)
    // ============================================================================

    private void OnPlayerChatMessage(string message, IUnit? selected)
    {
        if (_gameOver) return;

        string clean = message.Trim().ToLowerInvariant();
        switch (clean)
        {
            case "-1":
                TryBuyPerk(0);
                break;
            case "-2":
                TryBuyPerk(1);
                break;
            case "-3":
                TryBuyPerk(2);
                break;
            case "-reroll":
                TryRerollShop();
                break;
            case "-perks":
                PrintActivePerks();
                break;
            case "-stats":
                PrintHeroStats();
                break;
            case "-help":
                ShowHeroUpgradeHelp();
                break;
        }
    }

    private void OnSpellCast(IUnit? caster, string abilityId, Vector3 targetPosition)
    {
        if (_gameOver) return;

        switch (abilityId)
        {
            case "perk_choose_1":
                TryBuyPerk(0);
                break;
            case "perk_choose_2":
                TryBuyPerk(1);
                break;
            case "perk_choose_3":
                TryBuyPerk(2);
                break;
            case "perk_reroll":
                TryRerollShop();
                break;
            case "hero_meteor_spell":
                if (_survivorHero != null)
                {
                    _api.SpawnProjectile("boss_meteor", _survivorHero.Position, targetPosition, 26f);
                    _api.SpawnVisualEffect("fireblast", targetPosition, 2.5f);
                    _api.CreateFloatingText("¡METEORITO!", targetPosition + new Vector3(0, 2f, 0), new Vector3(1f, 0.4f, 0.1f), 1.2f);
                    foreach (var enemy in _api.GetUnitsInRadius(targetPosition, 4.5f))
                    {
                        if (enemy != null && enemy.IsEnemy && !enemy.IsDead && enemy.Health > 0f)
                        {
                            DealBonusDamage(enemy, 120f);
                        }
                    }
                }
                break;
        }
    }

    private void ShowHeroUpgradeHelp()
    {
        _api.BroadcastMessage(
            "TIENDA ROTATIVA CADA 30s: Compra con [Q, W, E] (-1, -2, -3) o Reroll [R] (-reroll). " +
            "COMANDOS: -perks (ver talentos acumulados) | -stats (estadísticas del héroe).");
    }

    private void PrintActivePerks()
    {
        if (_perkStacks.Count == 0)
        {
            _api.SendMessageToPlayer(_player, "Aún no has adquirido ningún talento. ¡Compra ofertas en la tienda!");
            return;
        }

        string summary = string.Join(", ", _perkStacks.Select(kv => $"{kv.Key} (x{kv.Value})"));
        _api.SendMessageToPlayer(_player, $"Talentos Activos ({_perksPicked}): {summary}");
    }

    private void PrintHeroStats()
    {
        if (_survivorHero == null) return;

        _api.SendMessageToPlayer(_player,
            $"Héroe: {_survivorHero.Damage:F0} daño | {_survivorHero.Range:F0} alcance | {_survivorHero.Armor:F0} armadura | " +
            $"Vida {_survivorHero.Health:F0}/{_survivorHero.MaxHealth:F0} | " +
            $"Perks Totales: {_perksPicked} | Rerolls Usados: {_rerollCount}");
    }

    // ============================================================================
    // 11. INTERFAZ Y LEADERBOARD (Actualización de HUD en Tiempo Real)
    // ============================================================================

    private void UpdateLeaderboardDisplay()
    {
        _api.SetLeaderboardValue("Oleada", $"{_currentWave} / {TotalWaves}");
        _api.SetLeaderboardValue("Oro", $"{(int)_api.GetPlayerGold(_player)} (+{(int)_currentIncomePerSecond}/s)");
        _api.SetLeaderboardValue("Tienda", $"{_shopRotationTimer:F0}s | Perks: {_perksPicked}");
        _api.SetLeaderboardValue("Bajas", $"{_totalKills}");
        _api.SetLeaderboardValue("Enemigos Vivos", $"{Math.Max(0, _aliveInWave)}");
    }
}
