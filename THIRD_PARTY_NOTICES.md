# Third-party notices

FlickVox application source is MIT licensed. The self-contained preview package includes application dependencies and bundled fonts/icons, but not Piper or voice models. The app downloads those on request during setup.

- NAudio 2.2.1 — MIT License.
- Microsoft .NET 10 self-contained Windows runtime — see `DOTNET-LICENSE.txt` and `DOTNET-ThirdPartyNotices.txt` in the release ZIP for Microsoft's distribution terms and notices. These are distinct from FlickVox's MIT source license.
- Piper 2023.11.14-2 Windows runtime — downloaded from the [official archived rhasspy/piper release](https://github.com/rhasspy/piper/releases/tag/2023.11.14-2); tagged source is MIT licensed (Copyright 2022 Michael Hansen). It is not bundled in the preview ZIP.
- Piper voice models — downloaded from the official `rhasspy/piper-voices` repository. Each model's accompanying metadata identifies its license; review that license before redistribution.
- Inter static font files 4.1 — SIL Open Font License 1.1. Copyright The Inter Project Authors. License: `Assets/Fonts/Inter-LICENSE.txt` in the ZIP (`src/FlickVox/Assets/Fonts/` in source).
- Sora font — SIL Open Font License 1.1. Copyright 2019 The Sora Project Authors. License: `Assets/Fonts/Sora-OFL.txt` in the ZIP.
- Atkinson Hyperlegible font — SIL Open Font License 1.1. License: `Assets/Fonts/Atkinson-OFL.txt` in the ZIP.
- Fluent UI System Icons Regular vector paths — MIT License, Copyright Microsoft Corporation. License: `Assets/Fluent-Icons-LICENSE.txt` in the ZIP.

Voice-model metadata must still be reviewed per model before any future release bundles models. FlickVox downloads selected models directly from their official repository.
