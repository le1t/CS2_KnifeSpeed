# CS2_KnifeSpeed

## Описание плагина: CS2 KnifeSpeed - плагин для CS2, увеличивающий скорость передвижения игроков при держании ножа и автоматически возвращающий обычную скорость при переключении на другое оружие.

https://www.youtube.com/watch?v=p0WmurPLugo

### Требования
1. CounterStrikeSharp API версии 1.0.362 или выше
2. .NET 8.0 Runtime

### Конфигурационные параметры:

css_knifespeed_enabled (true) - Глобальное включение плагина

css_knifespeed_multiplier (1.3) - Множитель скорости с ножом (1.0-5.0)

css_knifespeed_check_interval (0.1) - Интервал проверки оружия в секундах (0.05-1.0)

css_knifespeed_log_level (1) - Уровень логирования (0=Error, 1=Info, 2=Debug)


### Консольные команды:

css_knifespeed_help - Показать справку

css_knifespeed_settings - Показать настройки

css_knifespeed <значение> - Изменить множитель скорости (1.0-5.0)

css_knifespeed_reload - Перезагрузить конфигурацию

css_plugins reload CS2KnifeSpeed - Перезагрузить плагин

ЭТОТ ПЛАГИН ФОРК ЭТОГО ПЛАГИНА https://github.com/akanora/CS2-WeaponSpeed
