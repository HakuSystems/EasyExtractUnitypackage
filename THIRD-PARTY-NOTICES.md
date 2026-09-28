# Third-party notices

EasyExtract is licensed under the MIT License, see [LICENSE](LICENSE). The desktop app ships the
third-party components below. Each of them keeps its own license, and the full texts are in
[third-party-licenses/](third-party-licenses/). Release builds copy this file and that folder next
to the executable.

The package list comes from the Release build of `EasyExtractCrossPlatform` (packages whose files
end up in the output, direct and transitive). Update it whenever a package reference changes.

## Components with their own license text

| Component | License | Text | Notes |
| --- | --- | --- | --- |
| Fizzler 1.2.1 | LGPL-3.0 | [third-party-licenses/Fizzler-LGPL-3.0.txt](third-party-licenses/Fizzler-LGPL-3.0.txt), [third-party-licenses/GPL-3.0.txt](third-party-licenses/GPL-3.0.txt) | Pulled in by Markdown.Avalonia through Svg.Custom. Ships as a separate `Fizzler.dll` next to the app and can be replaced by a compatible build. Source: https://github.com/atifaziz/Fizzler |
| Svg.Custom 1.0.0 | MS-PL | [third-party-licenses/Svg.Custom-MS-PL.txt](third-party-licenses/Svg.Custom-MS-PL.txt) | Pulled in by Markdown.Avalonia. Source: https://github.com/wieslawsoltes/Svg.Skia |
| StbImageSharp 2.30.15 | Public Domain or MIT | [third-party-licenses/MIT.txt](third-party-licenses/MIT.txt) | License as stated in the README of https://github.com/StbSharp/StbImageSharp |
| ANGLE (native, in Avalonia.Angle.Windows.Natives) | BSD-3-Clause | [third-party-licenses/ANGLE-BSD-3-Clause.txt](third-party-licenses/ANGLE-BSD-3-Clause.txt) | Windows only. |
| PDFium (native, in Docnet.Core 2.6.0) | BSD-3-Clause and bundled notices | [third-party-licenses/PDFium-Windows-Linux.txt](third-party-licenses/PDFium-Windows-Linux.txt), [third-party-licenses/PDFium-macOS.txt](third-party-licenses/PDFium-macOS.txt) | Used for PDF previews. |
| Assimp (native, in SharpAssimp 6.0.12) | BSD-3-Clause | [third-party-licenses/Assimp-BSD-3-Clause.txt](third-party-licenses/Assimp-BSD-3-Clause.txt) | Used for 3D model previews. Source: https://github.com/assimp/assimp |
| GLFW (native, in OpenTK.redist.glfw 3.4.0.44) | Zlib | [third-party-licenses/GLFW-Zlib.txt](third-party-licenses/GLFW-Zlib.txt) |  |
| Skia and HarfBuzz (native, in SkiaSharp and HarfBuzzSharp) | see notices file | [third-party-licenses/SkiaSharp-HarfBuzzSharp-THIRD-PARTY-NOTICES.txt](third-party-licenses/SkiaSharp-HarfBuzzSharp-THIRD-PARTY-NOTICES.txt) | Both packages ship the same notices file. |
| CommunityToolkit.Mvvm 8.4.2 | MIT plus bundled notices | [third-party-licenses/CommunityToolkit.Mvvm-ThirdPartyNotices.txt](third-party-licenses/CommunityToolkit.Mvvm-ThirdPartyNotices.txt) |  |
| Inter typeface (embedded in Avalonia.Fonts.Inter) | OFL-1.1 | [third-party-licenses/Inter-OFL-1.1.txt](third-party-licenses/Inter-OFL-1.1.txt) | Copyright (c) 2016 The Inter Project Authors |
| Everything SDK (`Everything64.dll`) | MIT | [third-party-licenses/Everything-SDK-MIT.txt](third-party-licenses/Everything-SDK-MIT.txt) | Copyright (C) 2022 David Carpenter. Windows builds only, from https://www.voidtools.com/Everything-SDK.zip |
| .NET runtime (self-contained builds) | MIT | https://github.com/dotnet/runtime/blob/main/LICENSE.TXT | Copyright (c) .NET Foundation and Contributors. Its own third-party notices: https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT |

## MIT-licensed packages

The MIT license text is in [third-party-licenses/MIT.txt](third-party-licenses/MIT.txt). Where a package states no copyright
line in its NuGet metadata, the authors field is shown instead and marked as such.

| Package | Version | Copyright |
| --- | --- | --- |
| Avalonia | 12.0.1 | Copyright 2013-2026 © The AvaloniaUI Project |
| Avalonia.AvaloniaEdit | 11.0.0 | AvaloniaEdit (NuGet authors field) |
| Avalonia.Desktop | 12.0.1 | Copyright 2013-2026 © The AvaloniaUI Project |
| Avalonia.Fonts.Inter | 12.0.1 | Copyright 2013-2026 © The AvaloniaUI Project |
| Avalonia.FreeDesktop | 12.0.1 | Copyright 2013-2026 © The AvaloniaUI Project |
| Avalonia.FreeDesktop.AtSpi | 12.0.1 | Copyright 2013-2026 © The AvaloniaUI Project |
| Avalonia.HarfBuzz | 12.0.1 | Copyright 2013-2026 © The AvaloniaUI Project |
| Avalonia.Native | 12.0.1 | Copyright 2013-2026 © The AvaloniaUI Project |
| Avalonia.Remote.Protocol | 12.0.1 | Copyright 2013-2026 © The AvaloniaUI Project |
| Avalonia.Skia | 12.0.1 | Copyright 2013-2026 © The AvaloniaUI Project |
| Avalonia.Svg | 11.0.0 | Copyright © Wiesław Šoltés 2023 |
| Avalonia.Themes.Fluent | 12.0.1 | Copyright 2013-2026 © The AvaloniaUI Project |
| Avalonia.Win32 | 12.0.1 | Copyright 2013-2026 © The AvaloniaUI Project |
| Avalonia.X11 | 12.0.1 | Copyright 2013-2026 © The AvaloniaUI Project |
| ColorDocument.Avalonia | 11.0.3-a1 | Copyright (c) 2024 whistyun |
| ColorTextBlock.Avalonia | 11.0.3-a1 | Copyright (c) 2020 whistyun |
| CommunityToolkit.Mvvm | 8.4.2 | (c) .NET Foundation and Contributors. All rights reserved. |
| DiscordRichPresence | 1.143.0 | Copyright (c) Lachee 2018 |
| Docnet.Core | 2.6.0 | Modestas Petravicius (NuGet authors field) |
| ExCSS | 4.1.4 | Copyright (c) 2024 Tyler Brinks |
| HarfBuzzSharp | 8.3.1.3 | © Microsoft Corporation. All rights reserved. |
| HarfBuzzSharp.NativeAssets.Linux | 8.3.1.3 | © Microsoft Corporation. All rights reserved. |
| HarfBuzzSharp.NativeAssets.macOS | 8.3.1.3 | © Microsoft Corporation. All rights reserved. |
| HarfBuzzSharp.NativeAssets.Win32 | 8.3.1.3 | © Microsoft Corporation. All rights reserved. |
| HtmlAgilityPack | 1.11.42 | Copyright © ZZZ Projects Inc. |
| Markdown.Avalonia | 11.0.3-a1 | Copyright (c) 2010 Bevan Arps, 2020 whistyun |
| Markdown.Avalonia.Html | 11.0.3-a1 | © Simon Baynes 2013; whistyun 2023 |
| Markdown.Avalonia.Svg | 11.0.3-a1 | Copyright (c) 2023 grifsun, whistyun |
| Markdown.Avalonia.SyntaxHigh | 11.0.3-a1 | Copyright (c) 2021 whistyun |
| Markdown.Avalonia.Tight | 11.0.3-a1 | Copyright (c) 2010 Bevan Arps, 2020 whistyun |
| MicroCom.Runtime | 0.11.4 | Copyright 2021 © Nikita Tsukanov |
| NAudio | 2.3.0 | © Mark Heath 2026 |
| NAudio.Asio | 2.3.0 | © Mark Heath 2026 |
| NAudio.Core | 2.3.0 | © Mark Heath 2026 |
| NAudio.Midi | 2.3.0 | © Mark Heath 2026 |
| NAudio.Vorbis | 1.5.0 | Copyright © Andrew Ward 2021 |
| NAudio.Wasapi | 2.3.0 | © Mark Heath 2026 |
| NAudio.WinMM | 2.3.0 | © Mark Heath 2023 |
| Newtonsoft.Json | 13.0.4 | Copyright © James Newton-King 2008 |
| NVorbis | 0.10.4 | Copyright (c) 2020 Andrew Ward |
| OpenTK.Audio.OpenAL | 4.9.4 | Team OpenTK (NuGet authors field) |
| OpenTK.Compute | 4.9.4 | Team OpenTK (NuGet authors field) |
| OpenTK.Core | 4.9.4 | Team OpenTK (NuGet authors field) |
| OpenTK.Graphics | 4.9.4 | Team OpenTK (NuGet authors field) |
| OpenTK.Input | 4.9.4 | Team OpenTK (NuGet authors field) |
| OpenTK.Mathematics | 4.9.4 | Team OpenTK (NuGet authors field) |
| OpenTK.Windowing.Common | 4.9.4 | Team OpenTK (NuGet authors field) |
| OpenTK.Windowing.Desktop | 4.9.4 | Team OpenTK (NuGet authors field) |
| OpenTK.Windowing.GraphicsLibraryFramework | 4.9.4 | Team OpenTK (NuGet authors field) |
| SharpAssimp | 6.0.12 | Copyright (c) 2012-2020 Nicholas Woodfield, (c) 2024 Stefan Koch, (c) 2025 Jérémy Ansel |
| ShimSkiaSharp | 1.0.0 | Copyright © Wiesław Šoltés 2023 |
| SkiaSharp | 3.119.3-preview.1.1 | © Microsoft Corporation. All rights reserved. |
| SkiaSharp.NativeAssets.Linux | 3.119.3-preview.1.1 | © Microsoft Corporation. All rights reserved. |
| SkiaSharp.NativeAssets.macOS | 3.119.3-preview.1.1 | © Microsoft Corporation. All rights reserved. |
| SkiaSharp.NativeAssets.Win32 | 3.119.3-preview.1.1 | © Microsoft Corporation. All rights reserved. |
| Svg.Model | 1.0.0 | Copyright © Wiesław Šoltés 2023 |
| Tmds.DBus.Protocol | 0.92.0 | Tom Deseyn |
| Velopack | 0.0.1535-gb21da2a | Copyright © Velopack Ltd. All rights reserved. |

## Not shipped

`Avalonia.Diagnostics` and its dependencies (`Avalonia.Controls.ColorPicker`, `Avalonia.Themes.Simple`)
are only used in Debug builds. `Avalonia.BuildServices` runs at build time, the WebAssembly native
assets are never copied into the desktop app, and `OpenTK` is a metapackage without files.
