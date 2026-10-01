# Inscryption NoBars

Removes the vertical black bars from Inscryption so the game uses the full screen.

## Installation

Install **BepInEx 5.4.1902** for Inscryption, then extract `InscryptionNoBars.zip` into:

```text
BepInEx/plugins/InscryptionNoBars/
```

The folder should contain:

```text
BepInEx/plugins/InscryptionNoBars/
├── manifest.json
├── icon.png
└── InscryptionNoBars.dll
```

## Build

Requires **.NET SDK**.

```bash
dotnet build InscryptionNoBars.sln -c Release
```

Output:

```text
InscryptionNoBars/bin/Release/InscryptionNoBars.zip
```

The ZIP is the installable mod.
