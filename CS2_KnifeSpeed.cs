using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Commands;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core.Attributes;
using System.Collections.Concurrent;

namespace CS2KnifeSpeed;

public class CS2KnifeSpeedConfig : BasePluginConfig
{
    [JsonPropertyName("css_knifespeed_enabled")]
    public bool Enabled { get; set; } = true;
    
    [JsonPropertyName("css_knifespeed_multiplier")]
    public float SpeedMultiplier { get; set; } = 1.3f;
    
    [JsonPropertyName("css_knifespeed_check_interval")]
    public float CheckInterval { get; set; } = 0.1f;
    
    [JsonPropertyName("css_knifespeed_log_level")]
    public int LogLevel { get; set; } = 1; // 0=Error, 1=Info, 2=Debug
}

[MinimumApiVersion(362)]
public class CS2KnifeSpeed : BasePlugin, IPluginConfig<CS2KnifeSpeedConfig>
{
    public override string ModuleName => "CS2 KnifeSpeed";
    public override string ModuleVersion => "1.5";
    public override string ModuleAuthor => "Fixed by le1t1337 + AI DeepSeek. Code logic by akanora";
    
    public required CS2KnifeSpeedConfig Config { get; set; }
    
    private readonly ConcurrentDictionary<int, bool> _hasKnifeEquipped = new();
    private readonly ConcurrentDictionary<int, float> _originalVelocityModifiers = new();
    
    public void OnConfigParsed(CS2KnifeSpeedConfig config) => Config = config;
    
    public override void Load(bool hotReload)
    {
        // Валидация конфигурации
        Config.SpeedMultiplier = Math.Clamp(Config.SpeedMultiplier, 1.0f, 5.0f);
        Config.CheckInterval = Math.Clamp(Config.CheckInterval, 0.05f, 1.0f);
        Config.LogLevel = Math.Clamp(Config.LogLevel, 0, 2);
        
        // Регистрируем команды
        AddCommand("css_knifespeed", "Change knife speed multiplier (1.0 - 5.0)", OnSpeedMultiplierCommand);
        AddCommand("css_knifespeed_help", "Show KnifeSpeed help", OnHelpCommand);
        AddCommand("css_knifespeed_settings", "Show current KnifeSpeed settings", OnSettingsCommand);
        AddCommand("css_knifespeed_reload", "Reload configuration", OnReloadCommand);
        
        // Выводим информацию о конфигурации
        PrintConVarInfo();
        
        if (!Config.Enabled)
        {
            Log("Plugin disabled in configuration", 1);
            return;
        }
        
        // Запускаем таймер для проверки оружия с использованием нового API
        AddTimer(Config.CheckInterval, CheckPlayersWeapons, TimerFlags.REPEAT);
        
        // Регистрируем обработчики событий с новым API
        RegisterListener<Listeners.OnEntitySpawned>(OnEntitySpawned);
        RegisterListener<Listeners.OnEntityDeleted>(OnEntityDeleted);
        
        // Регистрируем игровые события
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        
        Log($"Plugin v{ModuleVersion} successfully loaded", 1);
    }
    
    private void PrintConVarInfo()
    {
        Log("===============================================", 1);
        Log("Plugin successfully loaded!", 1);
        Log($"Version: {ModuleVersion}", 1);
        Log($"Minimum API Version: 362", 1);
        Log("Current settings:", 1);
        Log($"  css_knifespeed_enabled = {Config.Enabled}", 1);
        Log($"  css_knifespeed_multiplier = {Config.SpeedMultiplier}", 1);
        Log($"  css_knifespeed_check_interval = {Config.CheckInterval}", 1);
        Log($"  css_knifespeed_log_level = {Config.LogLevel}", 1);
        Log("===============================================", 1);
    }
    
    private void CheckPlayersWeapons()
    {
        try
        {
            // Используем новый метод Utilities.GetPlayers() который возвращает только валидных игроков
            foreach (var player in Utilities.GetPlayers())
            {
                if (player == null || !player.IsValid || player.IsBot)
                    continue;
                    
                var pawn = player.PlayerPawn?.Value;
                if (pawn == null || !pawn.IsValid)
                    continue;
                    
                var weaponService = pawn.WeaponServices;
                if (weaponService == null)
                    continue;
                    
                var activeWeapon = weaponService.ActiveWeapon?.Value;
                if (activeWeapon == null || !activeWeapon.IsValid)
                    continue;
                
                string weaponName = activeWeapon.DesignerName ?? string.Empty;
                
                // Проверяем, является ли оружие ножом
                bool isKnife = weaponName.Contains("knife", StringComparison.OrdinalIgnoreCase) || 
                              weaponName.Contains("bayonet", StringComparison.OrdinalIgnoreCase) ||
                              weaponName.Contains("melee", StringComparison.OrdinalIgnoreCase);
                
                int slot = player.Slot;
                
                // Если игрок держит нож
                if (isKnife)
                {
                    if (!_hasKnifeEquipped.ContainsKey(slot) || !_hasKnifeEquipped[slot])
                    {
                        // Сохраняем оригинальный VelocityModifier
                        if (!_originalVelocityModifiers.ContainsKey(slot))
                        {
                            _originalVelocityModifiers[slot] = pawn.VelocityModifier;
                        }
                        
                        // Устанавливаем увеличенную скорость
                        pawn.VelocityModifier = Config.SpeedMultiplier;
                        _hasKnifeEquipped[slot] = true;
                        
                        LogDebug($"Player {player.PlayerName} ({slot}) now has knife speed: {Config.SpeedMultiplier}x");
                    }
                }
                else // Если держит другое оружие
                {
                    if (_hasKnifeEquipped.TryGetValue(slot, out bool hasKnife) && hasKnife)
                    {
                        // Восстанавливаем оригинальный VelocityModifier
                        if (_originalVelocityModifiers.TryGetValue(slot, out float originalModifier))
                        {
                            pawn.VelocityModifier = originalModifier;
                        }
                        else
                        {
                            pawn.VelocityModifier = 1.0f;
                        }
                        _hasKnifeEquipped[slot] = false;
                        
                        LogDebug($"Player {player.PlayerName} ({slot}) restored normal speed");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Exception in CheckPlayersWeapons: {ex.Message}");
        }
    }
    
    private void OnEntitySpawned(CEntityInstance entity)
    {
        if (entity.DesignerName?.Contains("player") == true)
        {
            LogDebug($"Player entity spawned");
        }
    }
    
    private void OnEntityDeleted(CEntityInstance entity)
    {
        if (entity.DesignerName?.Contains("player") == true)
        {
            LogDebug($"Player entity deleted");
        }
    }
    
    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid)
        {
            int slot = player.Slot;
            
            // Сбрасываем состояние при спавне
            _hasKnifeEquipped[slot] = false;
            _originalVelocityModifiers.TryRemove(slot, out _);
            
            // Сбрасываем VelocityModifier
            var pawn = player.PlayerPawn?.Value;
            if (pawn != null && pawn.IsValid)
            {
                pawn.VelocityModifier = 1.0f;
            }
            
            LogDebug($"Player {player.PlayerName} ({slot}) spawned, reset speed");
        }
        return HookResult.Continue;
    }
    
    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid)
        {
            int slot = player.Slot;
            _hasKnifeEquipped.TryRemove(slot, out _);
            _originalVelocityModifiers.TryRemove(slot, out _);
            
            LogDebug($"Player {player.PlayerName} ({slot}) died, cleared state");
        }
        return HookResult.Continue;
    }
    
    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        // Можно добавить логику для событий получения урона
        return HookResult.Continue;
    }
    
    private void OnSpeedMultiplierCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"Current knife speed multiplier: {Config.SpeedMultiplier}");
            command.ReplyToCommand($"Usage: css_knifespeed <value> (1.0 - 5.0)");
            return;
        }
        
        if (float.TryParse(command.GetArg(1), out float newMultiplier))
        {
            newMultiplier = Math.Clamp(newMultiplier, 1.0f, 5.0f);
            
            float oldMultiplier = Config.SpeedMultiplier;
            Config.SpeedMultiplier = newMultiplier;
            
            // Применяем новый множитель ко всем игрокам с ножами
            ApplyNewSpeedToAllKnifePlayers();
            
            string message = $"Speed multiplier changed from {oldMultiplier} to {newMultiplier}";
            command.ReplyToCommand(message);
            
            if (player != null)
            {
                player.PrintToChat($"[KnifeSpeed] {message}");
            }
            
            Log(message, 1);
        }
        else
        {
            command.ReplyToCommand($"Invalid value. Usage: css_knifespeed <value> (1.0 - 5.0)");
        }
    }
    
    private void ApplyNewSpeedToAllKnifePlayers()
    {
        int affectedPlayers = 0;
        
        foreach (var player in Utilities.GetPlayers())
        {
            if (player?.IsValid != true)
                continue;
                
            var pawn = player.PlayerPawn?.Value;
            if (pawn == null || !pawn.IsValid)
                continue;
                
            int slot = player.Slot;
            
            // Если игрок держит нож, обновляем его скорость
            if (_hasKnifeEquipped.TryGetValue(slot, out bool hasKnife) && hasKnife)
            {
                pawn.VelocityModifier = Config.SpeedMultiplier;
                affectedPlayers++;
            }
        }
        
        if (affectedPlayers > 0)
        {
            Log($"Applied new speed multiplier to {affectedPlayers} players", 1);
        }
    }
    
    private void OnHelpCommand(CCSPlayerController? player, CommandInfo command)
    {
        string helpMessage = $"""
            ===============================================
            KNIFESPEED PLUGIN v{ModuleVersion} HELP
            ===============================================
            DESCRIPTION:
              Increases player movement speed when holding a knife.
              Automatically applies speed boost when knife is equipped.
              Restores normal speed when switching to other weapons.

            COMMANDS:
              css_knifespeed_help - Show this help message
              css_knifespeed_settings - Show current plugin settings
              css_knifespeed <value> - Change speed multiplier (1.0 - 5.0)
              css_knifespeed_reload - Reload configuration

            CONSOLE COMMANDS:
              css_plugins reload CS2KnifeSpeed - Reload plugin
              css_plugins unload CS2KnifeSpeed - Unload plugin
            ===============================================
            """;
        
        if (player != null)
        {
            player.PrintToConsole(helpMessage);
            player.PrintToChat($"KnifeSpeed v{ModuleVersion}: Check console for help");
        }
        else
        {
            Console.WriteLine(helpMessage);
        }
    }
    
    private void OnSettingsCommand(CCSPlayerController? player, CommandInfo command)
    {
        int playersWithKnife = _hasKnifeEquipped.Count(kvp => kvp.Value);
        
        string settingsMessage = $"""
            ===============================================
            KNIFESPEED v{ModuleVersion} CURRENT SETTINGS
            ===============================================
            Plugin Enabled: {Config.Enabled}
            Speed Multiplier: {Config.SpeedMultiplier}x
            Check Interval: {Config.CheckInterval}s
            Players With Knife: {playersWithKnife}
            Total Players: {_hasKnifeEquipped.Count}
            ===============================================
            """;
        
        if (player != null)
        {
            player.PrintToConsole(settingsMessage);
            player.PrintToChat($"KnifeSpeed: {Config.SpeedMultiplier}x multiplier, {playersWithKnife} players with knife");
        }
        else
        {
            Console.WriteLine(settingsMessage);
        }
    }
    
    private void OnReloadCommand(CCSPlayerController? player, CommandInfo command)
    {
        // Очищаем состояния
        _hasKnifeEquipped.Clear();
        _originalVelocityModifiers.Clear();
        
        // Восстанавливаем стандартную скорость всем игрокам
        foreach (var p in Utilities.GetPlayers())
        {
            var pawn = p.PlayerPawn?.Value;
            if (pawn?.IsValid == true)
            {
                pawn.VelocityModifier = 1.0f;
            }
        }
        
        // Перезагружаем конфиг
        OnConfigParsed(Config);
        
        string message = "Configuration reloaded successfully";
        command.ReplyToCommand(message);
        
        if (player != null)
        {
            player.PrintToChat($"[KnifeSpeed] {message}");
        }
        
        Log(message, 1);
    }
    
    public override void Unload(bool hotReload)
    {
        // Восстанавливаем стандартную скорость всем игрокам
        foreach (var player in Utilities.GetPlayers())
        {
            var pawn = player.PlayerPawn?.Value;
            if (pawn?.IsValid == true)
            {
                pawn.VelocityModifier = 1.0f;
            }
        }
        
        _hasKnifeEquipped.Clear();
        _originalVelocityModifiers.Clear();
        
        Log("Plugin unloaded", 1);
    }
    
    // Методы логирования
    private void Log(string message, int level = 1)
    {
        if (level <= Config.LogLevel)
        {
            Console.WriteLine($"[KnifeSpeed] {message}");
        }
    }
    
    private void LogError(string message) => Console.WriteLine($"[KnifeSpeed ERROR] {message}");
    private void LogDebug(string message) => Log(message, 2);
}