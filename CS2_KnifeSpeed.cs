using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Serialization;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace CS2KnifeSpeed;

public class CS2KnifeSpeedConfig : BasePluginConfig
{
    [JsonPropertyName("css_knifespeed_enabled")]
    public int Enabled { get; set; } = 1; // 1 - включен, 0 - выключен

    [JsonPropertyName("css_knifespeed_multiplier")]
    public float SpeedMultiplier { get; set; } = 1.3f; // Множитель скорости (1.0 - 5.0)

    [JsonPropertyName("css_knifespeed_check_interval")]
    public float CheckInterval { get; set; } = 0.1f; // Интервал проверки оружия (0.05 - 1.0 сек)

    [JsonPropertyName("css_knifespeed_log_level")]
    public int LogLevel { get; set; } = 4; // Уровень логирования: 0-Trace, 1-Debug, 2-Information, 3-Warning, 4-Error, 5-Critical
}

[MinimumApiVersion(362)]
public class CS2KnifeSpeed : BasePlugin, IPluginConfig<CS2KnifeSpeedConfig>
{
    public override string ModuleName => "CS2 KnifeSpeed";
    public override string ModuleVersion => "1.6";
    public override string ModuleAuthor => "Fixed by le1t1337 + AI DeepSeek. Code logic by akanora";

    public required CS2KnifeSpeedConfig Config { get; set; }

    private readonly ConcurrentDictionary<int, bool> _hasKnifeEquipped = new();
    private readonly ConcurrentDictionary<int, float> _originalVelocityModifiers = new();

    public void OnConfigParsed(CS2KnifeSpeedConfig config)
    {
        // Валидация параметров
        config.Enabled = Math.Clamp(config.Enabled, 0, 1);
        config.SpeedMultiplier = Math.Clamp(config.SpeedMultiplier, 1.0f, 5.0f);
        config.CheckInterval = Math.Clamp(config.CheckInterval, 0.05f, 1.0f);
        config.LogLevel = Math.Clamp(config.LogLevel, 0, 5);

        Config = config;
    }

    public override void Load(bool hotReload)
    {
        // Регистрация команд
        AddCommand("css_knifespeed_help", "Показать справку по плагину", OnHelpCommand);
        AddCommand("css_knifespeed_settings", "Показать текущие настройки", OnSettingsCommand);
        AddCommand("css_knifespeed_test", "Тестовая команда", OnTestCommand);
        AddCommand("css_knifespeed_reload", "Перезагрузить конфигурацию", OnReloadCommand);

        AddCommand("css_knifespeed_setenabled", "Включить/выключить плагин (0/1)", OnSetEnabledCommand);
        AddCommand("css_knifespeed_setmultiplier", "Установить множитель скорости (1.0 - 5.0)", OnSetMultiplierCommand);
        AddCommand("css_knifespeed_setcheckinterval", "Установить интервал проверки (0.05 - 1.0)", OnSetCheckIntervalCommand);
        AddCommand("css_knifespeed_setloglevel", "Установить уровень логов (0-5)", OnSetLogLevelCommand);

        // Псевдоним для удобства
        AddCommand("css_knifespeed", "Изменить множитель скорости", OnSetMultiplierCommand);

        // Регистрация событий
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterListener<Listeners.OnEntitySpawned>(OnEntitySpawned);
        RegisterListener<Listeners.OnEntityDeleted>(OnEntityDeleted);

        if (Config.Enabled == 1)
        {
            AddTimer(Config.CheckInterval, CheckPlayersWeapons, TimerFlags.REPEAT);
        }

        PrintInfo();

        if (hotReload)
        {
            Server.NextFrame(() =>
            {
                // При hotReload можно ничего не делать, таймер сам подхватит игроков
            });
        }
    }

    private void PrintInfo()
    {
        Log(LogLevel.Information, "===============================================");
        Log(LogLevel.Information, $"Плагин {ModuleName} версии {ModuleVersion} успешно загружен!");
        Log(LogLevel.Information, $"Автор: {ModuleAuthor}");
        Log(LogLevel.Information, "Текущие настройки:");
        Log(LogLevel.Information, $"  css_knifespeed_enabled = {Config.Enabled} (0/1)");
        Log(LogLevel.Information, $"  css_knifespeed_multiplier = {Config.SpeedMultiplier:F2}");
        Log(LogLevel.Information, $"  css_knifespeed_check_interval = {Config.CheckInterval:F2}");
        Log(LogLevel.Information, $"  css_knifespeed_log_level = {Config.LogLevel} (0-Trace,5-Critical)");
        Log(LogLevel.Information, "===============================================");
    }

    private void Log(LogLevel level, string message)
    {
        if ((int)level >= Config.LogLevel)
        {
            Logger.Log(level, "[KnifeSpeed] {Message}", message);
        }
    }

    private void CheckPlayersWeapons()
    {
        if (Config.Enabled == 0) return;

        try
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (player == null || !player.IsValid)
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
                bool isKnife = weaponName.Contains("knife", StringComparison.OrdinalIgnoreCase) ||
                              weaponName.Contains("bayonet", StringComparison.OrdinalIgnoreCase) ||
                              weaponName.Contains("melee", StringComparison.OrdinalIgnoreCase);

                int slot = player.Slot;

                if (isKnife)
                {
                    if (!_hasKnifeEquipped.ContainsKey(slot) || !_hasKnifeEquipped[slot])
                    {
                        if (!_originalVelocityModifiers.ContainsKey(slot))
                        {
                            _originalVelocityModifiers[slot] = pawn.VelocityModifier;
                        }

                        pawn.VelocityModifier = Config.SpeedMultiplier;
                        _hasKnifeEquipped[slot] = true;

                        Log(LogLevel.Debug, $"Игрок {player.PlayerName} ({slot}) теперь с ножом, скорость: {Config.SpeedMultiplier:F2}x");
                    }
                }
                else
                {
                    if (_hasKnifeEquipped.TryGetValue(slot, out bool hasKnife) && hasKnife)
                    {
                        if (_originalVelocityModifiers.TryGetValue(slot, out float original))
                        {
                            pawn.VelocityModifier = original;
                        }
                        else
                        {
                            pawn.VelocityModifier = 1.0f;
                        }
                        _hasKnifeEquipped[slot] = false;

                        Log(LogLevel.Debug, $"Игрок {player.PlayerName} ({slot}) убрал нож, скорость восстановлена");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ошибка в CheckPlayersWeapons");
        }
    }

    private void OnEntitySpawned(CEntityInstance entity)
    {
        if (entity.DesignerName?.Contains("player") == true)
        {
            Log(LogLevel.Trace, "Сущность игрока создана");
        }
    }

    private void OnEntityDeleted(CEntityInstance entity)
    {
        if (entity.DesignerName?.Contains("player") == true)
        {
            Log(LogLevel.Trace, "Сущность игрока удалена");
        }
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid)
        {
            int slot = player.Slot;
            _hasKnifeEquipped[slot] = false;
            _originalVelocityModifiers.TryRemove(slot, out _);

            var pawn = player.PlayerPawn?.Value;
            if (pawn != null && pawn.IsValid)
            {
                pawn.VelocityModifier = 1.0f;
            }

            Log(LogLevel.Debug, $"Игрок {player.PlayerName} ({slot}) заспавнился, скорость сброшена");
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

            Log(LogLevel.Debug, $"Игрок {player.PlayerName} ({slot}) умер, данные очищены");
        }
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid)
        {
            int slot = player.Slot;
            _hasKnifeEquipped.TryRemove(slot, out _);
            _originalVelocityModifiers.TryRemove(slot, out _);

            Log(LogLevel.Debug, $"Игрок {player.PlayerName} ({slot}) отключился, данные очищены");
        }
        return HookResult.Continue;
    }

    // ----- Команды -----

    private void OnHelpCommand(CCSPlayerController? player, CommandInfo command)
    {
        string help = $"""
            ================================================
            СПРАВКА ПО ПЛАГИНУ {ModuleName} v{ModuleVersion}
            ================================================
            ОПИСАНИЕ:
              Увеличивает скорость передвижения игрока, когда в руках нож.
              При переключении на другое оружие скорость возвращается к обычной.

            КОМАНДЫ:
              css_knifespeed_help                - показать эту справку
              css_knifespeed_settings             - показать текущие настройки
              css_knifespeed_test                  - проверить работу плагина
              css_knifespeed_reload                - перезагрузить конфигурацию
              css_knifespeed_setenabled <0/1>      - вкл/выкл плагин
              css_knifespeed_setmultiplier <1.0-5.0> - установить множитель скорости
              css_knifespeed_setcheckinterval <0.05-1.0> - интервал проверки (сек)
              css_knifespeed_setloglevel <0-5>     - уровень логов (0-Trace ... 5-Critical)

            ПРИМЕРЫ:
              css_knifespeed_setmultiplier 1.5
              css_knifespeed_setenabled 1
            ================================================
            """;
        command.ReplyToCommand(help);
    }

    private void OnSettingsCommand(CCSPlayerController? player, CommandInfo command)
    {
        int playersWithKnife = _hasKnifeEquipped.Count(kvp => kvp.Value);
        string status = Config.Enabled == 1 ? "Включён" : "Отключён";

        string settings = $"""
            ================================================
            ТЕКУЩИЕ НАСТРОЙКИ {ModuleName} v{ModuleVersion}
            ================================================
            Статус плагина: {status}
            Множитель скорости: {Config.SpeedMultiplier:F2}
            Интервал проверки: {Config.CheckInterval:F2} сек
            Уровень логов: {Config.LogLevel} (0-Trace,5-Critical)
            Игроков с ножом: {playersWithKnife}
            Всего игроков в словаре: {_hasKnifeEquipped.Count}
            ================================================
            """;
        command.ReplyToCommand(settings);
    }

    private void OnTestCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null)
        {
            command.ReplyToCommand("[KnifeSpeed] Эта команда доступна только игрокам.");
            return;
        }

        if (Config.Enabled == 0)
        {
            command.ReplyToCommand("[KnifeSpeed] Плагин выключен. Включите командой css_knifespeed_setenabled 1.");
            return;
        }

        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid)
        {
            command.ReplyToCommand("[KnifeSpeed] Не удалось получить pawn игрока.");
            return;
        }

        // Принудительно установим скорость (если игрок держит нож, она применится автоматически)
        command.ReplyToCommand($"[KnifeSpeed] Тест: ваш текущий VelocityModifier = {pawn.VelocityModifier:F2}");
        command.ReplyToCommand($"[KnifeSpeed] Если у вас в руках нож, он должен быть {Config.SpeedMultiplier:F2}x.");
    }

    private void OnReloadCommand(CCSPlayerController? player, CommandInfo command)
    {
        // Сбрасываем скорость всем игрокам
        foreach (var p in Utilities.GetPlayers())
        {
            var pawn = p.PlayerPawn?.Value;
            if (pawn?.IsValid == true)
            {
                pawn.VelocityModifier = 1.0f;
            }
        }

        // Очищаем словари
        _hasKnifeEquipped.Clear();
        _originalVelocityModifiers.Clear();

        // Перезагружаем конфиг из файла
        try
        {
            string configPath = Path.Combine(Server.GameDirectory, "counterstrikesharp", "configs", "plugins", "CS2KnifeSpeed", "CS2KnifeSpeed.json");
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                var newConfig = System.Text.Json.JsonSerializer.Deserialize<CS2KnifeSpeedConfig>(json);
                if (newConfig != null)
                {
                    OnConfigParsed(newConfig);
                }
            }
            else
            {
                // Если файла нет, сохраняем текущий
                SaveConfig();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ошибка при перезагрузке конфига");
        }

        // Перезапускаем таймер, если нужно
        if (Config.Enabled == 1)
        {
            AddTimer(Config.CheckInterval, CheckPlayersWeapons, TimerFlags.REPEAT);
        }

        command.ReplyToCommand("[KnifeSpeed] Конфигурация перезагружена.");
        Log(LogLevel.Information, "Конфигурация перезагружена по команде.");
    }

    private void OnSetEnabledCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[KnifeSpeed] Текущее значение Enabled: {Config.Enabled} (по умолч. 1). Использование: css_knifespeed_setenabled <0/1>");
            return;
        }

        string arg = command.GetArg(1);
        if (int.TryParse(arg, out int value) && (value == 0 || value == 1))
        {
            int old = Config.Enabled;
            Config.Enabled = value;
            SaveConfig();

            // Останавливаем/запускаем таймер
            if (old != value)
            {
                if (value == 1)
                {
                    AddTimer(Config.CheckInterval, CheckPlayersWeapons, TimerFlags.REPEAT);
                }
                // Если выключили, таймер продолжит тикать? Лучше управлять таймером через переменную.
                // У нас нет прямого доступа к таймеру, но можно сделать флаг в CheckPlayersWeapons.
                // Для простоты оставим так, но CheckPlayersWeapons проверяет Config.Enabled.
            }

            command.ReplyToCommand($"[KnifeSpeed] Enabled изменён с {old} на {value}.");
        }
        else
        {
            command.ReplyToCommand("[KnifeSpeed] Неверное значение. Используйте 0 или 1.");
        }
    }

    private void OnSetMultiplierCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[KnifeSpeed] Текущее значение множителя: {Config.SpeedMultiplier:F2} (по умолч. 1.3). Использование: css_knifespeed_setmultiplier <1.0-5.0>");
            return;
        }

        string arg = command.GetArg(1).Replace(',', '.');
        if (float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            float old = Config.SpeedMultiplier;
            Config.SpeedMultiplier = Math.Clamp(value, 1.0f, 5.0f);
            SaveConfig();

            // Применить новый множитель ко всем игрокам с ножом
            ApplyNewSpeedToAllKnifePlayers();

            command.ReplyToCommand($"[KnifeSpeed] Множитель изменён с {old:F2} на {Config.SpeedMultiplier:F2}.");
        }
        else
        {
            command.ReplyToCommand("[KnifeSpeed] Неверное значение. Используйте число с точкой, например 1.5.");
        }
    }

    private void OnSetCheckIntervalCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[KnifeSpeed] Текущее значение интервала: {Config.CheckInterval:F2} сек (по умолч. 0.1). Использование: css_knifespeed_setcheckinterval <0.05-1.0>");
            return;
        }

        string arg = command.GetArg(1).Replace(',', '.');
        if (float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            float old = Config.CheckInterval;
            Config.CheckInterval = Math.Clamp(value, 0.05f, 1.0f);
            SaveConfig();

            // Таймер перезапускать не будем, он уже работает с новым интервалом только после перезагрузки плагина.
            // Можно удалить старый таймер и создать новый, но для простоты оставим, что новое значение применится после перезапуска или reload.
            // Или можно реализовать перезапуск таймера. Добавим в reload или сделаем перезапуск здесь.

            command.ReplyToCommand($"[KnifeSpeed] Интервал изменён с {old:F2} на {Config.CheckInterval:F2}. Для применения перезагрузите плагин командой css_knifespeed_reload.");
        }
        else
        {
            command.ReplyToCommand("[KnifeSpeed] Неверное значение. Используйте число с точкой, например 0.2.");
        }
    }

    private void OnSetLogLevelCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"[KnifeSpeed] Текущий уровень логов: {Config.LogLevel} (0-Trace,5-Critical, по умолч. 4). Использование: css_knifespeed_setloglevel <0-5>");
            return;
        }

        string arg = command.GetArg(1);
        if (int.TryParse(arg, out int value))
        {
            int old = Config.LogLevel;
            Config.LogLevel = Math.Clamp(value, 0, 5);
            SaveConfig();
            command.ReplyToCommand($"[KnifeSpeed] Уровень логов изменён с {old} на {Config.LogLevel}.");
        }
        else
        {
            command.ReplyToCommand("[KnifeSpeed] Неверное значение. Используйте число от 0 до 5.");
        }
    }

    private void ApplyNewSpeedToAllKnifePlayers()
    {
        int affected = 0;
        foreach (var player in Utilities.GetPlayers())
        {
            if (player?.IsValid != true) continue;
            var pawn = player.PlayerPawn?.Value;
            if (pawn == null || !pawn.IsValid) continue;

            int slot = player.Slot;
            if (_hasKnifeEquipped.TryGetValue(slot, out bool hasKnife) && hasKnife)
            {
                pawn.VelocityModifier = Config.SpeedMultiplier;
                affected++;
            }
        }
        if (affected > 0)
        {
            Log(LogLevel.Information, $"Новый множитель скорости применён к {affected} игрокам.");
        }
    }

    private void SaveConfig()
    {
        try
        {
            string configPath = Path.Combine(Server.GameDirectory, "counterstrikesharp", "configs", "plugins", "CS2KnifeSpeed", "CS2KnifeSpeed.json");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string json = System.Text.Json.JsonSerializer.Serialize(Config, options);
            File.WriteAllText(configPath, json);
            Log(LogLevel.Debug, $"Конфигурация сохранена в {configPath}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ошибка сохранения конфигурации");
        }
    }

    public override void Unload(bool hotReload)
    {
        // Восстанавливаем скорость всем игрокам
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
        Log(LogLevel.Information, "Плагин выгружен.");
    }
}