namespace Realm.Maps;

using System;
using System.Numerics;
using Realm.MapAPI;

public class CustomMap : IWasmModule
{
    // ============================================================================
    // 1. CONFIGURACIÓN Y CONSTANTES (Balance, Oleadas, Tablas)
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

    // ---- Balance del Héroe Central (Mejoras apilables con oro) ----
    private const float DamageUpgradeBaseCost = 150.0f;   // Costo = base * (nivel actual + 1)
    private const float DamageUpgradeBonus = 25.0f;        // +Daño por nivel
    private const int MaxDamageLevel = 5;
    private const float RangeUpgradeBaseCost = 300.0f;
    private const float RangeUpgradeBonus = 8.0f;          // +Alcance por nivel
    private const int MaxRangeLevel = 3;
    private const float FuryUpgradeBaseCost = 200.0f;
    private const float FuryChancePerLevel = 0.20f;        // +20% de flecha extra por nivel
    private const int MaxFuryLevel = 3;
    private const float HealthstoneCost = 2000.0f;
    private const float HealthstoneMaxHpBonus = 2500.0f;
    private const float HealthstoneRegenPerSecond = 35.0f;
    private const float MultishotCost = 1500.0f;
    private const float MultiShotSplashRatio = 0.70f;      // % de daño a objetivos secundarios
    private const float HealPotionCost = 200.0f;
    private const float HealPotionAmount = 600.0f;

    // Configuración consolidada de las 15 Oleadas
    private static readonly WaveConfig[] Waves =
    {
        new(new[] { "zombie_soldier" }, TotalCount: 8, SpawnInterval: 0.45f, InterWaveDelay: 22.0f),                                                    // W1
        new(new[] { "zombie_soldier" }, TotalCount: 12, SpawnInterval: 0.40f, InterWaveDelay: 20.0f),                                                   // W2
        new(new[] { "zombie_soldier", "forest_goblin" }, TotalCount: 12, SpawnInterval: 0.35f, InterWaveDelay: 19.0f),                                  // W3
        new(new[] { "forest_goblin", "zombie_soldier", "forest_goblin" }, TotalCount: 16, SpawnInterval: 0.30f, InterWaveDelay: 18.0f),                 // W4
        new(new[] { "giant_rock_golem", "zombie_soldier", "forest_goblin" }, TotalCount: 15, SpawnInterval: 0.40f, InterWaveDelay: 18.0f,
            MiniBoss: new MiniBossInfo("¡MINI-BOSS: Coloso de Roca en camino!", new Vector3(1f, 0.6f, 0.1f))), // W5 (Mini-Boss)
        new(new[] { "cyber_dragon", "zombie_soldier" }, TotalCount: 14, SpawnInterval: 0.40f, InterWaveDelay: 16.0f),                                   // W6 (Air Intro)
        new(new[] { "zombie_warrior", "cyber_dragon" }, TotalCount: 16, SpawnInterval: 0.35f, InterWaveDelay: 16.0f),                                   // W7
        new(new[] { "flame_drake", "forest_goblin", "zombie_warrior" }, TotalCount: 22, SpawnInterval: 0.35f, InterWaveDelay: 15.0f),                   // W8
        new(new[] { "dark_iron_ogre", "cyber_dragon", "flame_drake" }, TotalCount: 18, SpawnInterval: 0.35f, InterWaveDelay: 14.0f),                    // W9
        new(new[] { "mech_spider_monster", "dark_iron_ogre", "forest_goblin" }, TotalCount: 20, SpawnInterval: 0.35f, InterWaveDelay: 14.0f,
            MiniBoss: new MiniBossInfo("¡MINI-BOSS: Araña Mecánica de Asedio!", new Vector3(1f, 0.3f, 0.1f))), // W10 (Mini-Boss)
        new(new[] { "zombie_warrior", "flame_drake", "cyber_dragon" }, TotalCount: 26, SpawnInterval: 0.35f, InterWaveDelay: 13.0f),                    // W11
        new(new[] { "dark_iron_ogre", "forest_goblin", "cyber_dragon" }, TotalCount: 28, SpawnInterval: 0.35f, InterWaveDelay: 13.0f),                 // W12
        new(new[] { "dark_iron_ogre", "flame_drake", "zombie_warrior" }, TotalCount: 32, SpawnInterval: 0.35f, InterWaveDelay: 12.0f),                  // W13
        new(new[] { "dark_iron_ogre", "flame_drake", "forest_goblin", "zombie_warrior" }, TotalCount: 42, SpawnInterval: 0.25f, InterWaveDelay: 15.0f), // W14 (Pre-Boss)
        new(new[] { "dragon_titan_boss" }, TotalCount: 1, SpawnInterval: 1.00f, InterWaveDelay: 20.0f, IsBoss: true)                                     // W15 (Raid Boss)
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
    private int _damageLevel;
    private int _rangeLevel;
    private int _furyLevel;
    private bool _hasHealthstone;
    private bool _hasMultishot;
    private float _heroAuraTimer;

    // Estado de Oleadas
    private int _currentWave;         // Oleadas completadas (0 a 15)
    private int _waveBeingSpawned;    // Índice de oleada en curso (1 a 15)
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
        _currentIncomePerSecond = BasePassiveGoldPerSecond;

        // Oro inicial
        _api.SetPlayerGold(_player, InitialGold);

        // Coordenadas base y spawn (isla central)
        _bossSpawnPos = new Vector3(0f, SpawnHeight, -65f); // Borde norte del plateau caminable

        // El Héroe Central reemplaza al castillo como objetivo y condición de derrota
        SpawnSurvivorHero();

        // Suscribirse a eventos
        _api.OnUnitDied += OnUnitDied;
        _api.OnUnitDamaged += OnUnitDamaged;
        _api.OnPlayerChatMessage += OnPlayerChatMessage;
        _api.OnSpellCast += OnSpellCast;

        // Configuración de UI y Leaderboard inicial
        _api.SetLeaderboardVisible("TOWER SURVIVORS", true);
        UpdateLeaderboardDisplay();

        _api.BroadcastMessage("¡Bienvenidos a TOWER SURVIVORS! Protege a tu Héroe Central en el medio de la isla. Escribe -help para ver las mejoras.");
        ShowNextWaveCountdown();
    }

    public void Update(IGameAPI api, float delta)
    {
        if (_gameOver) return;

        CheckDefeatCondition();
        UpdateHealthstoneRegenAndAura(delta);
        UpdatePassiveIncome(delta);
        UpdateEconomyAndUI(delta);
        UpdateBossEncounterIfActive(delta);
        UpdateWaveStateMachine(delta);
    }

    private void SpawnSurvivorHero()
    {
        // El héroe puede venir precargado desde terrain.json; si no, se genera aquí.
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

        // El héroe es inmóvil (Speed=0) y dispara solo a los enemigos que entran en su rango.
        _api.SetUnitColor(_survivorHero, new Vector3(1f, 1f, 1f));

        // Deja el héroe seleccionado para que la barra de habilidades se vea al iniciar.
        _api.SelectUnit(_survivorHero);
    }

    private void UpdateHealthstoneRegenAndAura(float delta)
    {
        if (_survivorHero == null || _survivorHero.IsDead || !_hasHealthstone) return;

        // Regeneración pasiva de vida mientras se posee la Healthstone
        _survivorHero.Health = MathF.Min(_survivorHero.MaxHealth, _survivorHero.Health + HealthstoneRegenPerSecond * delta);

        // Aura visual permanente en el héroe
        _heroAuraTimer -= delta;
        if (_heroAuraTimer > 0f) return;

        _heroAuraTimer = 2.0f;
        _api.SpawnVisualEffect("holylight", _survivorHero.Position + new Vector3(0f, 1f, 0f), 1.2f);
    }

    private void CheckDefeatCondition()
    {
        // Si el Héroe Central cae, la partida termina (sustituye al castillo)
        if (_survivorHero != null && !_survivorHero.IsDead && _survivorHero.Health > 0f) return;

        _gameOver = true;
        _api.StopCountdownTimer();
        _api.ShowFeedbackText("¡EL HÉROE DEL SURVIVOR HA CAÍDO!", new Vector3(1f, 0.2f, 0.2f));
        _api.TriggerDefeat();
    }

    private void UpdatePassiveIncome(float delta)
    {
        _api.AdjustPlayerGold(_player, delta * _currentIncomePerSecond);
    }

    private void UpdateEconomyAndUI(float delta)
    {
        // Escaneo Económico y Leaderboard a 1 Hz
        _economyScanTimer -= delta;
        if (_economyScanTimer > 0f) return;

        _economyScanTimer = 1.0f;
        _currentIncomePerSecond = BasePassiveGoldPerSecond;
        UpdateLeaderboardDisplay();
    }

    // ============================================================================
    // 4. MÁQUINA DE ESTADO DE OLEADAS (BeginWave, Spawn, Timing)
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
        // Aún quedan enemigos por aparecer: seguir con el temporizador de spawn
        if (_remainingToSpawn > 0)
        {
            UpdateSpawning(delta);
            return;
        }

        // La oleada solo termina cuando todo está muerto
        // (excepto la oleada 15, que espera además a que caiga el Raid Boss)
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
        _remainingToSpawn = config.TotalCount;
        _spawnedInWave = 0;
        _aliveInWave = _remainingToSpawn;
        _spawnInterval = config.SpawnInterval;
        _spawnTimer = 0f;

        _api.StopCountdownTimer();
        _api.PlayWarningSound();

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
            // El Raid Boss aparece majestuosamente en el borde norte
            unit = _api.SpawnUnit(unitType, _bossSpawnPos, true);
            if (unit != null)
            {
                _raidBossInstance = unit;
                unit.Scale = 5.0f;
            }
        }
        else
        {
            // Asedio 360°: cada enemigo nace en un punto aleatorio del anillo perimetral
            unit = _api.SpawnUnit(unitType, RandomSpawnPointOnRing(), true);
        }

        if (unit != null)
        {
            // Todas las unidades (terrestres y aéreas) convergen directo al Héroe Central
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
    // 5. ENCUENTRO CON EL RAID BOSS (Cinemática, Fases, Escoltas)
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
        // Indicador visual de objetivo bajo el jefe cada 2 segundos
        _bossIndicatorTimer -= delta;
        if (_bossIndicatorTimer > 0f) return;

        _bossIndicatorTimer = 2.0f;
        _api.SpawnTargetIndicator(boss.Position, new Vector3(1f, 0.1f, 0.1f));
    }

    private void UpdateBossEnragePhase(IUnit boss)
    {
        // Fase de Furia (Enrage) al caer por debajo del 50% de HP
        if (_bossEnraged || boss.Health > boss.MaxHealth * 0.5f) return;

        _bossEnraged = true;
        boss.Speed = 8.25f; // 5.5 * 1.5
        _api.SetUnitColor(boss, new Vector3(1f, 0.2f, 0.2f));
        _api.ShowFeedbackText("¡EL DRAGÓN TITÁN ESTÁ ENFURECIDO (+50% VELOCIDAD)!", new Vector3(1f, 0.15f, 0.15f));
        _api.PlayWarningSound();
        _api.ShakeCamera(3.0f, 1.5f);
    }

    private void UpdateBossEscortTimer(float delta)
    {
        // Escoltas periódicas mientras el jefe esté vivo
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
        // 8 Soldados + 4 Goblins (+ 2 Ogros si está enfurecido)
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
    // 6. COMBATE DEL HÉROE (Furia de Flechas, Multidisparo)
    // ============================================================================

    private void OnUnitDamaged(IUnit victim, IUnit attacker, float damage)
    {
        if (_gameOver || victim == null || attacker == null || victim.IsDead) return;

        // Solo el Héroe Central aplica efectos de combate propios (Furia / Multidisparo)
        if (victim.IsEnemy && !attacker.IsEnemy && attacker.UnitId == "survivor_hero")
        {
            ApplyHeroAttackEffects(victim, damage);
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

    private void ApplyHeroAttackEffects(IUnit victim, float damage)
    {
        // Furia de Flechas: probabilidad de disparar una flecha extra a otro enemigo
        if (_furyLevel > 0 && _api.RandomFloat(0f, 1f) < FuryChancePerLevel * _furyLevel)
        {
            FireFuryArrow(victim);
        }

        // Multidisparo: los ataques impactan también a los enemigos cercanos
        if (_hasMultishot)
        {
            ApplyMultiShot(victim, damage);
        }
    }

    private void FireFuryArrow(IUnit victim)
    {
        // Furia simula la cadencia del video: el cooldown de ataque no es modificable en runtime,
        // así que un ataque puede disparar una flecha extra inmediata a otro enemigo cercano.
        IUnit? secondary = _api.GetUnitsInRadius(victim.Position, 10f)
            .FirstOrDefault(u => u != null && u.IsEnemy && u.UniqueId != victim.UniqueId && !u.IsDead && u.Health > 0f);

        if (secondary == null) return;

        DealBonusDamage(secondary, _survivorHero!.Damage);
        _api.SpawnProjectile("arrow", _survivorHero.Position, secondary.Position, 40f);
        _api.CreateFloatingText("¡FURIA!", secondary.Position + new Vector3(0f, 2f, 0f), new Vector3(1f, 0.4f, 0.1f), 0.8f);
    }

    private void ApplyMultiShot(IUnit victim, float damage)
    {
        // Flechas dispersas: reparte el 70% del daño a hasta 2 enemigos adicionales
        float splash = damage * MultiShotSplashRatio;
        int hits = 0;

        foreach (var nearby in _api.GetUnitsInRadius(victim.Position, 6f))
        {
            if (nearby == null || !nearby.IsEnemy || nearby.UniqueId == victim.UniqueId || nearby.IsDead || nearby.Health <= 0f)
                continue;
            if (hits >= 2) break;

            DealBonusDamage(nearby, splash);
            _api.SpawnProjectile("arrow", _survivorHero!.Position, nearby.Position, 40f);
            hits++;
        }
    }

    // ============================================================================
    // 7. ECONOMÍA Y RECOMPENSAS (Ingresos, Bounties)
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

        // Recompensas de Oro según Tipo de Unidad
        float bounty = GetUnitBounty(unit.UnitId);
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
    // 8. MEJORAS Y COMPRAS DEL HÉROE (Chat + Botones de Habilidad)
    // ============================================================================

    private void OnPlayerChatMessage(string message, IUnit? selected)
    {
        if (_gameOver) return;

        switch (message.Trim().ToLowerInvariant())
        {
            case "-help":
                ShowHeroUpgradeHelp();
                break;
            case "-healthstone":
                TryBuyUpgrade(HealthstoneCost, () => !_hasHealthstone, ApplyHealthstone, "Healthstone");
                break;
            case "-damage":
                TryBuyUpgrade(DamageUpgradeBaseCost * (_damageLevel + 1), () => _damageLevel < MaxDamageLevel, ApplyDamageUpgrade, "Piedra de Daño");
                break;
            case "-range":
                TryBuyUpgrade(RangeUpgradeBaseCost * (_rangeLevel + 1), () => _rangeLevel < MaxRangeLevel, ApplyRangeUpgrade, "Piedra de Alcance");
                break;
            case "-fury":
                TryBuyUpgrade(FuryUpgradeBaseCost * (_furyLevel + 1), () => _furyLevel < MaxFuryLevel, ApplyFuryUpgrade, "Furia de Flechas");
                break;
            case "-multishot":
                TryBuyUpgrade(MultishotCost, () => !_hasMultishot, ApplyMultishotUpgrade, "Multidisparo");
                break;
            case "-heal":
                TryBuyUpgrade(HealPotionCost, () => true, ApplyHealPotion, "Poción de Restauración");
                break;
            case "-stats":
                PrintHeroStats();
                break;
        }
    }

    private void OnSpellCast(IUnit? caster, string abilityId, Vector3 targetPosition)
    {
        if (_gameOver) return;

        // Los botones de habilidad del héroe comparten la lógica de compra del chat
        switch (abilityId)
        {
            case "pocion_fortuna":
                _api.AdjustPlayerGold(_player, 500);
                _api.ShowFeedbackText("¡Fortuna! +500 de oro.", new Vector3(1f, 0.85f, 0.1f));
                _api.PlayClickSound();
                if (_survivorHero != null)
                {
                    _api.CreateFloatingText("+500g", _survivorHero.Position + new Vector3(0f, 2.5f, 0f), new Vector3(1f, 0.85f, 0.1f), 1.2f);
                }
                break;
            case "survivor_buy_healthstone":
                TryBuyUpgrade(HealthstoneCost, () => !_hasHealthstone, ApplyHealthstone, "Healthstone");
                break;
            case "survivor_buy_damage":
                TryBuyUpgrade(DamageUpgradeBaseCost * (_damageLevel + 1), () => _damageLevel < MaxDamageLevel, ApplyDamageUpgrade, "Piedra de Daño");
                break;
            case "survivor_buy_range":
                TryBuyUpgrade(RangeUpgradeBaseCost * (_rangeLevel + 1), () => _rangeLevel < MaxRangeLevel, ApplyRangeUpgrade, "Piedra de Alcance");
                break;
            case "survivor_buy_fury":
                TryBuyUpgrade(FuryUpgradeBaseCost * (_furyLevel + 1), () => _furyLevel < MaxFuryLevel, ApplyFuryUpgrade, "Furia de Flechas");
                break;
            case "survivor_buy_multishot":
                TryBuyUpgrade(MultishotCost, () => !_hasMultishot, ApplyMultishotUpgrade, "Multidisparo");
                break;
            case "survivor_heal":
                TryBuyUpgrade(HealPotionCost, () => true, ApplyHealPotion, "Poción de Restauración");
                break;
        }
    }

    private void TryBuyUpgrade(float cost, Func<bool> canBuy, Action apply, string name)
    {
        if (_survivorHero == null || _survivorHero.IsDead) return;

        if (!canBuy())
        {
            _api.ShowFeedbackText($"{name}: nivel máximo alcanzado.", new Vector3(1f, 0.7f, 0.2f));
            return;
        }

        if (_api.GetPlayerGold(_player) < cost)
        {
            _api.ShowFeedbackText($"No tienes oro suficiente para {name} ({cost:F0} g).", new Vector3(1f, 0.3f, 0.3f));
            return;
        }

        _api.AdjustPlayerGold(_player, -cost);
        apply();
        _api.PlayClickSound();
        _api.CreateFloatingText($"+{name}", _survivorHero.Position + new Vector3(0f, 2.5f, 0f), new Vector3(0.2f, 1f, 0.3f), 1.2f);
    }

    private void ApplyHealthstone()
    {
        _hasHealthstone = true;
        _survivorHero!.MaxHealth += HealthstoneMaxHpBonus;
        _survivorHero.Health = _survivorHero.MaxHealth; // Curación completa
        _api.ShowFeedbackText("¡Healthstone comprada! +2500 de Vida Máxima y regeneración de +35/s.", new Vector3(0.2f, 1f, 0.3f));
    }

    private void ApplyDamageUpgrade()
    {
        _damageLevel++;
        _survivorHero!.Damage += DamageUpgradeBonus;
        _api.ShowFeedbackText($"¡Piedra de Daño nivel {_damageLevel}! +{DamageUpgradeBonus} de daño.", new Vector3(1f, 0.6f, 0.1f));
    }

    private void ApplyRangeUpgrade()
    {
        _rangeLevel++;
        _survivorHero!.Range += RangeUpgradeBonus;
        _api.ShowFeedbackText($"¡Piedra de Alcance nivel {_rangeLevel}! +{RangeUpgradeBonus} de alcance.", new Vector3(0.3f, 0.7f, 1f));
    }

    private void ApplyFuryUpgrade()
    {
        _furyLevel++;
        _api.ShowFeedbackText($"¡Furia de Flechas nivel {_furyLevel}! +{FuryChancePerLevel * 100:F0}% de flecha extra por ataque.", new Vector3(1f, 0.4f, 0.1f));
    }

    private void ApplyMultishotUpgrade()
    {
        _hasMultishot = true;
        _api.ShowFeedbackText("¡Multidisparo activado! Tus ataques impactan a 3 objetivos cercanos.", new Vector3(0.8f, 0.3f, 1f));
    }

    private void ApplyHealPotion()
    {
        _survivorHero!.Health = MathF.Min(_survivorHero.MaxHealth, _survivorHero.Health + HealPotionAmount);
        _api.ShowFeedbackText($"¡Poción usada! +{HealPotionAmount:F0} de vida.", new Vector3(0.2f, 1f, 0.4f));
    }

    private void ShowHeroUpgradeHelp()
    {
        _api.BroadcastMessage(
            "HABILIDADES ACTIVAS [Q] Bola de fuego | [W] Rayo | [E] Luz sagrada (cura). " +
            "Selecciona el héroe y pulsa la tecla, luego haz clic sobre el objetivo." +
            " MEJORAS -healthstone (2000g): +2500 vida máx y regeneración. " +
            "-damage (150g/nivel, máx 5): +25 daño. " +
            "-range (300g/nivel, máx 3): +8 alcance. " +
            "-fury (200g/nivel, máx 3): flechas extra. " +
            "-multishot (1500g): 3 objetivos. " +
            "-heal (200g): cura 600. -stats: ver estado.");
    }

    private void PrintHeroStats()
    {
        if (_survivorHero == null) return;

        _api.SendMessageToPlayer(_player,
            $"Héroe: {_survivorHero.Damage:F0} daño | {_survivorHero.Range:F0} alcance | " +
            $"Vida {_survivorHero.Health:F0}/{_survivorHero.MaxHealth:F0} | " +
            $"Daño Lv {_damageLevel}/{MaxDamageLevel} | Alcance Lv {_rangeLevel}/{MaxRangeLevel} | " +
            $"Furia Lv {_furyLevel}/{MaxFuryLevel} | Multidisparo: {(_hasMultishot ? "SÍ" : "NO")} | " +
            $"Healthstone: {(_hasHealthstone ? "SÍ" : "NO")}");
    }

    // ============================================================================
    // 9. INTERFAZ Y LEADERBOARD (Actualización de HUD)
    // ============================================================================

    private void UpdateLeaderboardDisplay()
    {
        _api.SetLeaderboardValue("Oleada", $"{_currentWave} / {TotalWaves}");
        _api.SetLeaderboardValue("Oro", $"{(int)_api.GetPlayerGold(_player)} (+{(int)_currentIncomePerSecond}/s)");
        _api.SetLeaderboardValue("Bajas", $"{_totalKills}");
        _api.SetLeaderboardValue("Enemigos Vivos", $"{Math.Max(0, _aliveInWave)}");
    }
}
