[![License: LGPL v3](https://img.shields.io/badge/License-LGPL%20v3-blue.svg)](https://www.gnu.org/licenses/lgpl-3.0)

# TeklaStructuralDesigner_Toolkit
BHoM Toolkit to connect with Tekla Structural Designer.

Part of the [BHoM Framework](https://github.com/BHoM).

# BHoM
A great place to start is reading our Wiki [here](https://github.com/BHoM/documentation/wiki) including pages like the [Structure of the BHoM](https://github.com/BHoM/documentation/wiki/Structure-of-the-BHoM) and [Using the BHoM](https://github.com/BHoM/documentation/wiki/Using-the-BHoM).

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

BHoM is an open-source project and would be nothing without its community. Take a look at our contributing guidelines and tips [here](https://github.com/BHoM/BHoM/blob/master/CONTRIBUTING.md).

## Licence ##

BHoM is free software licenced under GNU Lesser General Public Licence - [https://www.gnu.org/licenses/lgpl-3.0.html](https://www.gnu.org/licenses/lgpl-3.0.html)  
Each contributor holds copyright over their respective contributions.
The project versioning (Git) records all such contribution source information.
See [LICENSE](https://github.com/BHoM/BHoM/blob/master/LICENSE) and [COPYRIGHT_HEADER](https://github.com/BHoM/BHoM/blob/master/COPYRIGHT_HEADER.txt).
