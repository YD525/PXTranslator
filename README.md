# NIM Translator

**NIM Translator** is a free, open-source tool for Skyrim mod localization.

It supports multiple mod-related file formats, including **PEX, ESM, ESP, and MCM**. The tool focuses on structured string preprocessing, rule-based protection, contextual grouping, and customizable translation workflows to reduce errors during batch translation.

NIM Translator also features **Aggregated Translation**, which groups related strings together before translation. By providing related dialogue and terminology as contextual information, it can improve consistency across repeated terms, character dialogue, and similar sentences while reducing the inconsistencies that can occur when every string is translated independently.

With optional integration of local or online translation engines and user-defined dictionaries, NIM Translator helps translators work more efficiently while maintaining control over the final output.

If you want to give feedback, report issues, or discuss NIM Translator, please feel free to visit the following site:

- [Nexus Mods (for international users)](https://www.nexusmods.com/skyrimspecialedition/mods/143056)

You can download it directly from Nexus Mods or build it yourself here. Both versions are kept up to date.

Your support and feedback are greatly appreciated!

---

## 📦 Features

- ✅ Support for `.pex`, `.esm`, `.esp`, and `.mcm` formats
- 🧠 **Aggregated Translation** — Groups related strings and provides contextual information to improve translation consistency
- 🔁 Batch processing and translation history tracking
- 🌐 Integration with OpenAI, DeepL, and other translation APIs
- 🧠 Heuristic filtering to avoid code-related terms being mistranslated
- 📚 Support for user-defined dictionaries and translation references
- 🔧 Customizable translation workflows
- 📝 Sequential Text Mode for improving consistency in conversational content
- ⚡ Designed for large-scale Skyrim mod localization

### 🧠 Aggregated Translation

Traditional batch translation usually treats each string as an independent translation task. This can cause the same character, item, location, or terminology to receive different translations across a mod.

NIM Translator's **Aggregated Translation** analyzes relationships between strings and groups related content into translation buckets.

Related strings can then be translated with additional contextual information, allowing the translation engine to better understand:

- Repeated terminology
- Character names and dialogue
- Similar sentences
- Related NPC dialogue
- Context-dependent expressions
- Frequently repeated phrases

This helps improve terminology consistency and allows the translation model to consider related content instead of translating every sentence in isolation.

The system can combine text similarity, contextual relationships, and configurable bucket limits to organize large amounts of content into manageable translation groups.

For conversational mods, **Sequential Text Mode** can also be used when preserving the original dialogue order is more important than similarity-based grouping.

---

## Building from source

NIM Translator requires Visual Studio 2022 and the .NET Framework 4.8.1 Developer Pack.

Restore NuGet packages and the pinned project dependencies, then build the x64 Release configuration:

```powershell
nuget restore .\NIMTranslator.sln -PackagesDirectory .\packages -NonInteractive
.\scripts\Restore-Dependencies.ps1
msbuild .\NIMTranslator.sln /m /p:Configuration=Release /p:Platform=x64
```

Dependency versions are recorded in `dependencies.json`. Restored packages and release assets remain untracked.

Push a version tag matching `v*` to create `NIMTranslator-win-x64.zip` and its SHA256 checksum as GitHub
Release assets. The same archive is uploaded to Nexus Mods when the tagged commit is contained in the default branch.

Nexus Mods publishing requires the `NEXUSMODS_API_KEY` and `NEXUSMODS_FILE_ID` Actions secrets. Nexus Mods
currently labels the file identifier as `Group ID` in the `API Info` dialog. Copy that value from the existing
file that should receive the new version.

---

## 🧩 Third-party Components

This project uses the following key open-source libraries and frameworks:

- [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) – WPF text editor component used for code and text display.

---

## 🙏 Special Thanks

I would like to give special thanks to everyone who has contributed to NIM Translator through development assistance, technical advice, testing, localization, and valuable suggestions.

**YD525, [Wuerfelhusten](https://github.com/Wuerfelhusten), and [Cutleast](https://github.com/cutleast)** — Core builders of NIM Translator, contributing to its core architecture, interface, and ESP/PEX file parsing.

**[繁化姬](https://zhconvert.org)** — Chinese character processing.

**[Noggog](https://github.com/Noggog) & [Mutagen](https://github.com/Mutagen-Modding/Mutagen)** — Helped identify and fix issues when reading ESP files, suggested exporting Strings files, and assisted with resolving framework conflicts.

**[Cutleast](https://github.com/cutleast) & [SkyHorizon3](https://github.com/SkyHorizon3)** — Helped resolve issues regarding the generation of specific JSON fields within DSD files.

**[csavasvdb](https://www.nexusmods.com/profile/csavasvdb)** — Recommended "Sequential Text Mode" to improve translation accuracy in conversational mods and proposed ideas for manual bucketing.

**[walkswithwolf](https://next.nexusmods.com/profile/walkswithwolf)** — Provided valuable insights to help me understand the core structure of Skyrim files.

**[Kanie17](https://next.nexusmods.com/profile/Kanie17)** — Suggested multi-color highlighting, improved the keyword search, and actively assisted in testing the program.

**[Neko41](https://www.nexusmods.com/profile/Neko41)** — Helped create the German dictionaries for NIM Translator.

**[Jacky66evil](https://home.gamer.com.tw/profile/index.php?owner=jacky66666)** — Suggested separating the translation cache by model name and helped build the Traditional Chinese dictionaries.

**[50809501](https://www.nexusmods.com/profile/50809501)** — Helped create the Simplified Chinese dictionaries for NIM Translator.

**[is365s](https://www.nexusmods.com/profile/is365s)** — Reported primary key duplication bugs within XML files and continuously helped test the application.

**[撒倫](https://home.gamer.com.tw/profile/index.php?owner=salunt)** — Offered numerous meaningful suggestions regarding Traditional Chinese recognition and translation.

**[zhuabaobao123](https://www.nexusmods.com/profile/zhuabaobao123)** — Created the localized Chinese interface for NIM Translator.

Their contributions, suggestions, testing, and support have provided NIM Translator with a stable foundation and helped us continue improving its translation capabilities.

Special thanks also go to **Nexus Mods, 9DM, 2Game.info, and 泰姆瑞尔MOD组** for their support and encouragement, which continue to inspire my work on NIM Translator.
