[![License: LGPL v3](https://img.shields.io/badge/License-LGPL%20v3-blue.svg)](https://www.gnu.org/licenses/lgpl-3.0)

# TeklaStructuralDesigner_Toolkit
BHoM Toolkit to connect with Tekla Structural Designer.

Part of the [BHoM Framework](https://github.com/BHoM).

### Known Versions of Software Supported
Tekla Structural Designer 2024, via `TeklaStructuralDesigner.RemotingAPI` package version 24.0.0.

This toolkit connects through Tekla Structural Designer's Remoting API, which is a gRPC client that
**attaches to an already-running instance of Tekla Structural Designer with a model open**. It has no
means of launching Tekla Structural Designer or opening a file itself - unlike most BHoM adapters, the
software must already be running before the adapter is activated.

This is currently a **read-only, results-focused** adapter: it pulls bar end forces (`BarResultRequest`
/ `BarForce`) and the Bars/Nodes needed to identify them. Push (Create/Update/Delete) is not supported.

### Deployment
Tekla Structural Designer's Remoting API brings its own gRPC dependency chain (`Grpc.Core`,
`Google.Protobuf`, and several BCL shims). Three of those shims collide, at a different version, with
assemblies other BHoM toolkits already place in `C:\ProgramData\BHoM\Assemblies` (for example ETABS'
`Microsoft.Win32.Registry`, and the `System.Runtime.CompilerServices.Unsafe` most toolkits pull in
indirectly). Overwriting those would break other toolkits.

For that reason, `TeklaStructuralDesigner_Adapter.dll` deploys top level as usual, but the Tekla
Structural Designer API and its whole dependency closure deploy into a **private subfolder**,
`C:\ProgramData\BHoM\Assemblies\TeklaStructuralDesigner\`, served at runtime by an `AssemblyResolve`
handler (`Adapter/AssemblyResolver.cs`). **Do not "tidy" this by flattening those files into the shared
folder** - that is what the private subfolder exists to prevent. See the comment on the `CopyToBHoM`
target in `TeklaStructuralDesigner_Adapter.csproj` for the full reasoning, and the version conflict
table it documents.

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
