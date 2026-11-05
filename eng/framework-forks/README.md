# Gradient dithering

AtomicArt uses two local builds:

- `Avalonia.Skia 12.0.6-atomicart.2` enables dithering for linear, radial, and conic gradient brushes. An extra pixel shader removes banding in the alpha channel. Standard Skia dithering does not process this channel.
- `SukiUI 7.0.2.1-atomicart.4` enables dithering in SukiUI background shaders. This includes the window background and transitions between backgrounds.

Prebuilt packages are in `.nuget/local-packages`. The root `NuGet.Config` adds this local package source.

## Source branches

The patches target these exact source commits:

- Avalonia: `fee9c561ce036e8a3e8cee2397c75ca599b4790d` (`12.0.5`);
- SukiUI: `2f4225125894724fb01389e342a5b5f6979fd697`.

To recreate the local branches, run:

```powershell
git clone https://github.com/AvaloniaUI/Avalonia.git Avalonia
git -C Avalonia switch --detach fee9c561ce036e8a3e8cee2397c75ca599b4790d
git -C Avalonia switch -c atomicart/gradient-dithering
git -C Avalonia am "<path-to-AtomicArt>/eng/framework-forks/patches/avalonia-gradient-dithering.patch"

git clone https://github.com/kikipoulet/SukiUI.git SukiUI
git -C SukiUI switch --detach 2f4225125894724fb01389e342a5b5f6979fd697
git -C SukiUI switch -c atomicart/gradient-dithering
git -C SukiUI am "<path-to-AtomicArt>/eng/framework-forks/patches/sukiui-gradient-dithering.patch"
```

Then rebuild the packages:

```powershell
./eng/framework-forks/build.ps1 -ForkRoot "<folder-with-Avalonia-and-SukiUI>"
```

The script builds Avalonia from the AtomicArt root directory. The installed .NET SDK 9 can build this pinned source version. SDK 10 from the fork's `global.json` is not required.
