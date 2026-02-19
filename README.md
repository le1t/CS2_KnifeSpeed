# CS2_KnifeSpeed
Плагин увеличивает скорость передвижения игрока, когда в руках нож. При переключении на другое оружие скорость возвращается к обычной.

# https://www.youtube.com/watch?v=p0WmurPLugo

# Требования
```
CounterStrikeSharp API версии 362 или выше
.NET 8.0 Runtime
```

# Конфигурационные параметры
```
css_knifespeed_enabled <0/1>, def.=1 – Включение/выключение плагина.
css_knifespeed_multiplier <1.0-5.0>, def.=1.3 – Множитель скорости при активном ноже.
css_knifespeed_check_interval <0.05-1.0>, def.=0.1 – Интервал проверки активного оружия (в секундах).
css_knifespeed_log_level <0-5>, def.=4 – Уровень логирования (0-Trace,1-Debug,2-Info,3-Warning,4-Error,5-Critical).
```

# Консольные команды
```
css_knifespeed_help – Показать справку по плагину.
css_knifespeed_settings – Показать текущие настройки и статистику.
css_knifespeed_test – Проверить работу плагина (для игрока: показать текущий VelocityModifier).
css_knifespeed_reload – Перезагрузить конфигурацию и сбросить данные игроков.
css_knifespeed_setenabled <0/1> – Установить значение css_knifespeed_enabled.
css_knifespeed_setmultiplier <1.0-5.0> – Установить множитель скорости.
css_knifespeed_setcheckinterval <0.05-1.0> – Установить интервал проверки (для применения требуется перезагрузка плагина или команда reload).
css_knifespeed_setloglevel <0-5> – Установить уровень логирования.
css_knifespeed – Псевдоним для css_knifespeed_setmultiplier (работает так же).
```

# ЭТОТ ПЛАГИН ФОРК ЭТОГО ПЛАГИНА https://github.com/akanora/CS2-WeaponSpeed
