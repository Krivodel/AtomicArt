# Дизеринг градиентов

AtomicArt использует две локальные сборки:

- `Avalonia.Skia 12.0.6-atomicart.2` — включает дизеринг для линейных, радиальных и конических градиентных кистей. Дополнительный пиксельный шейдер устраняет ступени в альфа-канале, который обычный дизеринг Skia не обрабатывает.
- `SukiUI 7.0.2.1-atomicart.3` — включает дизеринг при отрисовке фоновых шейдеров SukiUI, в том числе фона окна и перехода между фонами.

Готовые пакеты находятся в `.nuget/local-packages` и подключаются через корневой `NuGet.Config`.

## Исходные ветки

Патчи рассчитаны на точные исходные коммиты:

- Avalonia: `fee9c561ce036e8a3e8cee2397c75ca599b4790d` (`12.0.5`);
- SukiUI: `2f4225125894724fb01389e342a5b5f6979fd697`.

Для воссоздания локальных веток:

```powershell
git clone https://github.com/AvaloniaUI/Avalonia.git Avalonia
git -C Avalonia switch --detach fee9c561ce036e8a3e8cee2397c75ca599b4790d
git -C Avalonia switch -c atomicart/gradient-dithering
git -C Avalonia am "<путь-к-AtomicArt>/eng/framework-forks/patches/avalonia-gradient-dithering.patch"

git clone https://github.com/kikipoulet/SukiUI.git SukiUI
git -C SukiUI switch --detach 2f4225125894724fb01389e342a5b5f6979fd697
git -C SukiUI switch -c atomicart/gradient-dithering
git -C SukiUI am "<путь-к-AtomicArt>/eng/framework-forks/patches/sukiui-gradient-dithering.patch"
```

После этого пакеты пересобираются командой:

```powershell
./eng/framework-forks/build.ps1 -ForkRoot "<папка-с-Avalonia-и-SukiUI>"
```

Сценарий запускает сборку Avalonia из корня AtomicArt, поэтому для этой закреплённой версии исходников достаточно установленного .NET SDK 9; SDK 10 из `global.json` ответвления не требуется.
