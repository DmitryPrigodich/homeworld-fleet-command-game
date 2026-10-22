# Local HWRM import module

The importer is separate from the game core. The current cube application does not load or require any game resources. Import UI integration and background conversion are deferred.

The development CLI currently lives in the check runner:

```powershell
dotnet tests/FleetCommand.Checks/bin/Debug/net8.0/FleetCommand.Checks.dll scan 'C:\Games\Homeworld'
dotnet tests/FleetCommand.Checks/bin/Debug/net8.0/FleetCommand.Checks.dll import 'C:\Games\Homeworld' '.local/imported/hwrm' kus_interceptor
```

It reads `_ARCHIVE` version 2 files in `HomeworldRM/Data/HW1Ships.big` and `HW2Ships.big`, resolves the directory tables and decompresses selected entries. It converts supported pooled HOD 2 static vertex/index data and embedded DXT1/3/5 mip chains. Results and provenance receipts stay under the specified private cache. Existing imports are not overwritten. No original game tools, encryption keys or original game files are bundled.

Current output is `model.json`, local DDS files, and `receipt.json` with source/output hashes and source archive entry. Cache creation uses a staging directory; a failed conversion does not replace an existing import. Original installation files are only opened for reading.

Limitations: no guarantee for arbitrary HOD variants; no hierarchy/joint transforms, animations, original shader reconstruction, patch archive selection, backgrounds or terrain resources. Static parts are not a complete render-ready scene. The UI currently draws cubes regardless of imported data.

Format research references:

- Archive layout: [archived HW2 BIG format description](https://gist.github.com/aheadley/aea63fac00e02ce222fd#file-big-hw2-txt), credit to its authors and Relic's published format notes.
- HOD structure cross-check: [HODEditorJS](https://github.com/GuillermoOM/HODEditorJS). No upstream source files or binaries are included in this repository. Its CC BY-NC license is not being represented as an MIT dependency.

Local import and excluding assets from Git are distribution boundaries, not legal guarantees or permission to distribute extracted assets.
