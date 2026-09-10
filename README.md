[![License: LGPL v3](https://img.shields.io/badge/License-LGPL%20v3-blue.svg)](https://www.gnu.org/licenses/lgpl-3.0)

# TeklaStructuralDesigner_Toolkit
BHoM Toolkit to connect with Tekla Structural Designer.

Part of the [BHoM Framework](https://github.com/BHoM).

### Known Versions of Software Supported
Tekla Structural Designer 2026, via `TeklaStructuralDesigner.RemotingAPI` package version 26.0.1.

The pin is not arbitrary: Tekla's own Tekla Structural Designer 2026 SP1 release notes state that
26.0.1 is the Remoting API version for that release, and SP1 is version 26.1.0.75. Newer packages
exist on NuGet (26.2.0 at time of writing) but track a later service pack - an API newer than the
application it connects to is the direction that fails, with *"A method has been invoked which is no
longer supported in the connected instance of Tekla Structural Designer"*. Change the version in one
place, the `TsdApiVersion` property in `TeklaStructuralDesigner_Adapter.csproj`.

This toolkit connects through Tekla Structural Designer's Remoting API, which is a gRPC client that
**attaches to an already-running instance of Tekla Structural Designer with a model open**. It has no
means of launching Tekla Structural Designer or opening a file itself - unlike most BHoM adapters, the
software must already be running before the adapter is activated.

This is a **read-only** adapter. Push (Create/Update/Delete) is not supported. What it pulls:

| Pull | Returns |
| --- | --- |
| `Bar`, `Node` | One Bar per span, and the Nodes at span ends |
| `Loadcase`, `LoadCombination`, `ICase` | Loadcases with a `LoadNature`, and combinations whose `LoadCases` reference those Loadcases |
| `ILoad`, or a concrete load type | Bar and nodal loads - see below |
| `BarResultRequest` / `BarForce` | Bar end forces |

### Loads
Tekla Structural Designer hangs loads off loadcases, so pulling loads reads every loadcase. Member
loads map onto `BarUniformlyDistributedLoad`, `BarVaryingDistributedLoad` and `BarPointLoad`; nodal
loads map onto `PointLoad`. Slab, perimeter, snow, wind, temperature and settlement loads are **not**
read: they act on surfaces and areas this adapter does not pull, so there would be nothing to attach
them to.

Four things are worth knowing before trusting a pulled load:

- **Derived loads are excluded by default.** Tekla Structural Designer generates loads of its own -
  decomposed slab and wind loads, loads arriving from an incoming element - alongside the ones a user
  applied. Pulling both double counts the same loading, so only user-applied loads come back unless
  `IncludeDerivedLoads` is set on the pull configuration. Set it when you want what actually acts on
  the members rather than what was drawn.
- **Unit scaling is inferred, not confirmed.** Loads are converted assuming Tekla Structural Designer
  reports them in newtons and millimetres, which is the convention its *results* demonstrably use. If
  pulled loads come back a thousand times too large or too small, that assumption does not hold for
  applied loads; the fix belongs in the scale constants at the top of `Convert/ToBHoM/Load.cs`, and
  the `UdlMagnitudeMatchesTheModel` test exists to settle it.
- **Some loads are deliberately refused**, each with a counted warning saying why: trapezoidal loads
  (the API exposes one magnitude and one distance, which is not enough to reconstruct the shape),
  eccentricity moments (a consequence of how a load is attached, so pulling it would double count),
  loads in the undocumented `U`/`V` directions, and loads whose positions are measured in projection.
- **Combination factors.** Tekla Structural Designer holds strength, service and quasi-permanent
  factors against every loadcase in a combination; BHoM's `LoadCombination` holds one. The strength
  factor is used by default - set `CombinationFactor` on the pull configuration for the others.

### Deployment
Tekla Structural Designer's Remoting API brings its own gRPC dependency chain (`Grpc.Core`,
`Google.Protobuf`, and several BCL shims). Four of those shims collide, at a different version, with
assemblies other BHoM toolkits already place in `C:\ProgramData\BHoM\Assemblies` (for example ETABS'
`Microsoft.Win32.Registry`, and the `System.Runtime.CompilerServices.Unsafe` most toolkits pull in
indirectly). Overwriting those would break other toolkits.

For that reason the dependency closure is deployed in two parts:

- **Those seven BCL shims, and only those,** go into a private subfolder,
  `C:\ProgramData\BHoM\Assemblies\TeklaStructuralDesigner\`, served at runtime by an
  `AssemblyResolve` handler (`Adapter/AssemblyResolver.cs`). **Do not "tidy" these into the shared
  folder** - that is what the private subfolder exists to prevent.
- **Everything else** - `TeklaStructuralDesigner_Adapter.dll`, `TSD.API.Remoting.dll`, the gRPC
  stack and the native `grpc_csharp_ext` binaries - deploys top level, where ordinary probing finds
  it. **Do not move the API into the private folder either.** BHoM enumerates adapter types at
  startup by calling `GetTypes()` on every assembly in the shared folder, which happens before any
  adapter constructor has run and therefore before the `AssemblyResolve` handler is registered. If
  `TSD.API.Remoting.dll` is not reachable by normal probing at that moment, `GetTypes()` throws,
  BHoM discards this assembly whole, and **the adapter silently does not appear in the UI's adapter
  list at all** - no error, just an absence.

See the comment on the `CopyToBHoM` target in `TeklaStructuralDesigner_Adapter.csproj` for the full
reasoning, and the version conflict table it documents.

As resolved for API package 26.0.1, the private copies are *older* than the shared ones in three of
the four cases - the reverse of how it was under 24.0.0, because the 26.x package lowered its
`Microsoft.Bcl.AsyncInterfaces` and `Microsoft.Win32.Registry` pins and stopped pinning the other
shims directly. That does not change the conclusion: the loader does not version check an assembly
returned from an `AssemblyResolve` handler in either direction, and flattening the folder is now a
straight downgrade rather than an upgrade.

The build prunes the private folder of anything no longer in the private set, so upgrading across
API package versions does not leave stale assemblies behind for the resolver to go on serving.

Note that Rhino, Excel and any other host that has loaded the toolkit will hold a file lock on these
assemblies. **Close them before rebuilding**, or the post-build copy fails with `MSB3021`.

### A note on axis convention
`TeklaStructuralDesignerPullConfig.SwapMajorMinorAxes` exists because the mapping from Tekla Structural
Designer's local `y`/`z` axes onto BHoM's major/minor axis convention has not been confirmed against a
live, solved model as of this toolkit's initial port. The default (`false`) is BHoM's own stated
convention (`MY` major axis bending, `MZ` minor axis bending). If a model with a known correct answer
shows the mapping is transposed, set this `true` - and please raise an issue or PR updating the default,
since every user of this toolkit is affected the same way.

# BHoM
A great place to start is reading our Wiki [here](https://github.com/BHoM/documentation/wiki) including pages like the [Structure of the BHoM](https://bhom.xyz/documentation/Basics/Coding%20fundamentals/The-BHoM-code-organisation/) and [Using the BHoM](https://bhom.xyz/documentation/Basics/Using-the-BHoM/).

## Building the BHoM and the Toolkits from Source ##
You will need the following to build BHoM:

- Microsoft Visual Studio 2013 or higher
- Microsoft .NET Framework 4.0 and above (included with Visual Studio 2013)
- Note that there are no software - specific dependencies (only operating system relevant), this is specific: BHoM is a software agnostic object model.


### Clone and build the Core BHoM Repos

In the following build order:
- [BHoM](https://github.com/BHoM/BHoM)
- [BHoM_Engine](https://github.com/BHoM/BHoM_Engine)
- [BHoM_Adapter](https://github.com/BHoM/BHoM_Adapter)
- [BHoM_UI](https://github.com/BHoM/BHoM_UI)

Build as many as you like of your chosen Interop Toolkits:
- [TeklaStructuralDesigner_Toolkit](https://github.com/BHoM/TeklaStructuralDesigner_Toolkit/)
- [Revit_Toolkit](https://github.com/BHoM/Revit_Toolkit)
- [Robot_Toolkit](https://github.com/BHoM/Robot_Toolkit)

Then build as many User Interface Repositories as you like:
- [Rhinoceros_Toolkit](https://github.com/BHoM/Rhinoceros_Toolkit) & [Grasshopper_Toolkit](https://github.com/BHoM/Grasshopper_Toolkit) (you need both for Grasshopper to work)
- [Dynamo_Toolkit](https://github.com/BHoM/Dynamo_Toolkit)
- [Excel_Toolkit](https://github.com/BHoM/Excel_Toolkit)

You are good to go! 

## Development
Every time you change the code in this or other toolkit, you need to make sure that the changes are picked up from the UIs:
- Make your code changes in the toolkit
- Build the toolkit solution
- IMPORTANT: Rebuild [BHoM_UI](https://github.com/BHoM/BHoM_UI) (this will take care of moving the compiled assemblies in the right folders for the UIs to pick!)

## Want to contribute? ##

BHoM is an open-source project and would be nothing without its community. Take a look at our contributing guidelines and tips [here](https://github.com/BHoM/BHoM/blob/main/CONTRIBUTING.md).

## Licence ##

BHoM is free software licenced under GNU Lesser General Public Licence - [https://www.gnu.org/licenses/lgpl-3.0.html](https://www.gnu.org/licenses/lgpl-3.0.html)  
Each contributor holds copyright over their respective contributions.
The project versioning (Git) records all such contribution source information.
See [LICENSE](https://github.com/BHoM/BHoM/blob/main/LICENSE) and [COPYRIGHT_HEADER](https://github.com/BHoM/BHoM/blob/main/COPYRIGHT_HEADER.txt).
