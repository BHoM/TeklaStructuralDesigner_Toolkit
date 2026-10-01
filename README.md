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
| `Bar` | One Bar per span, with its section and material - see below |
| `Node` | The Nodes at span ends and at every support, with the support as a `Constraint6DOF` |
| `Panel` | Slab items, structural wall panels, roofs and wind walls - see below |
| `Loadcase`, `LoadCombination`, `ICase` | Loadcases with a `LoadNature`, and combinations whose `LoadCases` reference those Loadcases |
| `ILoad`, or a concrete load type | Bar, nodal, area, contour and line loads - see below |
| `BarResultRequest` / `BarForce` | Bar end forces |
| `NodeResultRequest` / `NodeReaction`, `NodeDisplacement` | Support reactions and node displacements - see below |

### Sections, materials and supports
- **Steel sections** are looked up in the BHoM steel section library (`Structure\SectionProperties`,
  EU/UK/US) by name, with `BH.Engine.Library.Query.Match`. Tekla Structural Designer's catalogue
  names ("UB 305x165x40", "IPE 300", "HE 300 B", "W14X90") mostly match directly. A section not in
  the library - including supplier variants such as "Euro SHS" or "Hybox 355 SHS" - is built from
  its Tekla Structural Designer dimensions instead, as a BHoM shape profile passed to
  `BH.Engine.Structure.Create.SectionPropertyFromProfile`. Library sections are given the model's
  own steel grade, not the library's default S355.
  Plated I sections ("PB 1000x500/35x457.3") become a `FabricatedISectionProfile`.
- **Concrete sections** have no library, so they are always built from their shape: rectangular,
  circular, T, L, I and C sections map onto the equivalent BHoM profile and become a
  `ConcreteSection`. Other shapes (trapezium, polygon, lozenge and so on) fall back to an
  `ExplicitSection` holding the constants Tekla Structural Designer reports.
- **Materials** are matched to the BHoM material library (`Structure\Materials`) by grade name
  first ("S355", "C30/37"), provided it is the same kind of material and its Young's modulus agrees
  with the model's within 5%. Otherwise they are built as BHoM `Steel` and `Concrete` from the
  model's own values; timber and general materials stay `GenericIsotropicMaterial`.
- **Releases** become the Bar's `Release`. Tekla Structural Designer reports each span end's fixity
  as six DegreeOfFreedom flags in the span's local axes (a set flag is a connected direction), which
  map one to one onto `Constraint6DOF`: Fx..Mz onto TranslationX..RotationZ. Pinned ends come back
  as `xxxxoo`, fixed and cantilever ends as `Fix`, and axial or torsional releases drop X or RX.
  Linear springs are carried as springs; nominally pinned, nominally fixed and partially fixed ends
  are approximated, with a warning.
- **Supports** become the Node's `Support`: fixed/free per direction, named `Fix`, `Pin` or by
  their fixity code. A support with its own axis system gives the Node that `Orientation`, which
  BHoM reads the support in. Support springs are reported as a warning rather than converted,
  because their axis mapping has not been verified against a live model.
  A support is attached to every Node at its position, not only to the one on its own construction
  point: Tekla Structural Designer routinely puts a support and the member end it holds up on two
  different, coincident construction points (half the supports on the test model, including the raker
  bases). Matching by construction point alone left the Bar's end Node free, and the support was then
  lost the moment the model reached a package that merges coincident nodes, such as Robot.
- **Fragments.** Every pulled object carries a `TeklaStructuralDesignerId`, whose `Id` is the
  readable identifier used in requests and results, and whose `PersistentId` is Tekla Structural
  Designer's own Guid for the object. Bars also carry `TeklaStructuralDesignerMemberProperties`.

Every Bar pull ends with a note saying how many sections were matched to the library, built from
their dimensions, or pulled as explicit constants, naming any that were not matched.

The adapter's `Convert` class is `internal`, unlike the public one in most toolkits: its methods
take Tekla Structural Designer API types, which would be unusable as components in the BHoM UIs.

### Panels
Pulling `Panel` returns every 2D element in the model. Each carries a `TeklaStructuralDesignerId`
(the element name, or `WallName:PanelIndex` for a wall panel) and a
`TeklaStructuralDesignerPanelProperties` fragment saying what it was pulled from.

- **Span direction is the Panel's local x** for every kind of panel: it is taken from the element
  plane Tekla Structural Designer reports, which already includes the element's rotation angle, and
  set through the Panel's `OrientationAngle`. Directional properties (`SlabOnDeck`, `HollowCore`) run
  along local x.
- **Slab items** become one Panel each, outlined by their complete contour: slab openings are
  Openings and column drops are cut out. Column drops are Panels of their own with
  `PanelType.DropPanel`. Every slab is a `ConstantThickness` of its overall depth by default, because
  that is the only slab property the Robot and ETABS toolkits can push: Robot's surface property
  converter throws a null reference on any other type, and ETABS creates nothing for it. Composite
  slabs are named after their deck and precast slabs after their plank and topping (a 150mm plank
  with a 75mm structural topping is a 225mm slab), and the slab type is on the fragment's
  `ElementSubType`. Set `DetailedSurfaceProperties` on the pull configuration to get the nearest
  BHoM type instead: `SlabOnDeck` built from the deck profile for composite slabs, and a `HollowCore`
  or `ConstantThickness` plank wrapped in a `ToppedSlab` where the topping is structural for precast
  slabs. Ribbed and waffle slabs are always solid slabs of their overall depth, because the API does
  not expose the rib geometry, with a warning. One way or two way spanning is recorded on the
  fragment's `SpanType`.
- **Structural walls** become one Panel per wall panel, between its bottom and top edges, with a
  `ConstantThickness` wall property and local x running horizontally along the wall. Wall openings
  become Openings; one that straddles two wall panels is left out, with a warning.
- **Roofs and wind walls** are loading panels: a `LoadingPanelProperty` with no material or
  thickness, spanning one way (`LoadApplication = TwoSides`), with `ReferenceEdge` set to the edge
  running most nearly along the span. The Panel's normal matches the element's in Tekla Structural
  Designer, which for a wind wall is the direction the wind acts in. Tekla Structural Designer
  exposes no span direction for wind walls, so their local x is simply horizontal along the wall.

### Loads
Tekla Structural Designer hangs loads off loadcases, so pulling loads reads every loadcase. Member
loads map onto `BarUniformlyDistributedLoad`, `BarVaryingDistributedLoad` and `BarPointLoad`; nodal
loads map onto `PointLoad`.

Area loads map onto the Panels (see [Panels](#panels)):

| Tekla Structural Designer | BHoM |
| --- | --- |
| Uniform load on a slab item, slab, wall, roof or wind wall | `AreaUniformlyDistributedLoad` on the Panels pulled for that element |
| Uniform rectangular or polygonal patch load | `ContourLoad` over its outline, placed in 3D on its construction plane |
| Uniform line load on a plane | `GeometricalLineLoad` |

**Push the Panels and the area loads in the same Push.** An `AreaUniformlyDistributedLoad` refers to
its Panels, and the receiving package has to recognise those Panels as ones in its model. For bar
loads the Robot and ETABS toolkits do that themselves: they declare `Bar` a dependency of bar loads
and match Bars to the model by their end nodes, which is why a bar load pushed on its own works.
Neither toolkit does the same for Panels, so an area load pushed on its own is refused as a load
without object ids. Pulled Panels and the Panels inside pulled loads share a `BHoM_Guid` derived
from the Tekla Structural Designer element, and when both are in one Push, BHoM's push pre-process
swaps the load's Panels for the ones being pushed, which then carry the package's ids. Push into a
model that does not already hold those Panels: Robot has no Panel comparer, so pushing the same
Panels again creates duplicates.

Area loads are N/mm² in Tekla Structural Designer and Pa in BHoM, with the same direction convention
as member loads (global Z positive downwards in Tekla Structural Designer, so gravity is -Z). Only
global directions are converted; a load measured in projection is `Projected`. Point loads on a
plane, varying area and polygonal loads, perimeter loads, and loads on vertical construction planes
are skipped with a counted warning. Snow, diaphragm, temperature and settlement loads are not read.

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

### Load combinations, case numbers and results
Tekla Structural Designer holds a strength, a service and a quasi-permanent factor against every
loadcase of a combination, and solves the strength and service sets separately. BHoM's
`LoadCombination` holds one factor per case, so each Tekla Structural Designer combination is pulled
as **two** LoadCombinations, one per limit state (quasi-permanent is not pulled), for the limit states
the combination is switched on for:

| Tekla Structural Designer | BHoM `Number` | BHoM `Name` | Factors | Results read from |
| --- | --- | --- | --- | --- |
| Loadcase 30 "30 LL CONCOURSE" | `30` | `30 LL CONCOURSE` | - | the loadcase |
| Combination 48, strength | `1048` | `Strength 48 1.35Gk + ...` | strength | the combination |
| Combination 48, service | `2048` | `Service 48 1.35Gk + ...` | service | its SLS combination (`SlsId`) |

- **Numbers** are the ones Tekla Structural Designer shows - the number each name starts with - not
  its internal index, which differs. Strength is 1000 + the combination number and Service 2000 + it,
  so a number is unique across loadcases and both limit states and reads straight back to both the
  combination and the limit state. The convention lives in the Engine: `Query.CaseNumber`,
  `Query.CombinationNumber`, `Query.LimitState`, `Query.FilterByLimitState` and `Query.CombinationName`.
  Combination numbers of 1000 or more have no room in the convention and are left out, with an error.
- Each LoadCombination carries a `TeklaStructuralDesignerCombinationProperties` fragment with its
  `LimitState` and the Tekla Structural Designer combination number, name and Guid.
- **Results** carry the case `Number` as their `ResultCase` - 1048 for the strength results of
  combination 48, 2048 for its service results, 30 for loadcase 30. On a `BarResultRequest` or a
  `NodeResultRequest`, name cases by a pulled Loadcase or LoadCombination, by number (`1048`, or `"1048"`), or by name. With no
  cases named, the pull configuration decides: `IncludeStrengthCombinations` (default true),
  `IncludeServiceCombinations` (default false) and `IncludeLoadcases` (default false).
- **Strength results include notional horizontal loads.** Checked on a live model: service results
  equal the loadcase results times the service factors exactly, but strength results do not equal
  them times the strength factors, because Tekla Structural Designer adds the combination's notional
  horizontal loads, which are not a loadcase. A pulled Strength LoadCombination therefore does not
  reproduce Tekla Structural Designer's strength results if re-analysed elsewhere; the pull notes how
  many combinations this affects.

### Node results
A `NodeResultRequest` reads `NodeReaction` or `NodeDisplacement` from the solver model, in one call
per case, with the same cases and pull configuration as bar forces. Other node result types are not
read.

- **Results are reported against the pulled Nodes.** Their `ObjectId` is the Node's
  `TeklaStructuralDesignerId` (the Guid of its construction point, as text). Name Nodes on the
  request by that id or by passing pulled Nodes; with none named, every Node is read.
- **A Node is matched to the solver node at its position**, within a millimetre: the solver has
  nodes of its own, identified only by an index and coordinates. Solver nodes that no pulled Node
  sits on - along a span, or in the mesh of a slab or wall - are left out.
- **Reactions are read at supported Nodes only, once per position.** Where several Nodes share a
  support's position, the reaction is reported against the first, so that reactions sum correctly.
  Supports the solver places where there is no pulled Node, such as along the base of a wall, are
  counted in a warning and not read, so the reactions pulled can be less than the total on the
  structure.
- **Units and signs.** Forces are N, moments N.m, displacements m and rotations rad, all in global
  axes with +Z upwards: an upward reaction and an upward displacement are positive. Checked on a
  live model under self weight.

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

### Member orientation
A Bar's `OrientationAngle` comes from Tekla Structural Designer's `GlobalRotationAngle`, which is
measured from global axes and matches the solver's own gamma angle for the member. Its other angle,
`RotationAngle`, is measured from the member's own construction plane and is **not** interchangeable:
on a sloped plane the two differ by the slope (25 degrees on the test model).

Vertical members are then given a quarter turn, because the two packages measure a vertical member's
zero rotation from different horizontal axes: Tekla Structural Designer's local y runs along global X,
BHoM's along global Y. This was confirmed on a live model by comparing a column's local end forces
with the global support reactions at the same end. The turn reverses for a member modelled top down.

# BHoM
A great place to start is reading our Wiki [here](https://github.com/BHoM/documentation/wiki) including pages like the [Structure of the BHoM](https://bhom.xyz/documentation/Basics/Coding%20fundamentals/The-BHoM-code-organisation/) and [Using the BHoM](https://bhom.xyz/documentation/Basics/Using-the-BHoM/).

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
