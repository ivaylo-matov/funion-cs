# RougeRevit
The Revit x Rouge interoperability project. This project currently supports [Revit 2021 API][1], [Revit 2020 API][2] and [Revit 2022 API][3].

[1]: https://www.revitapidocs.com/2021.1/
[2]: https://www.revitapidocs.com/2020.1/
[3]: https://www.revitapidocs.com/2022/

## Install
1. [download zip file](Deployment/Install.zip?raw=1)
2. Open the zip file and double-click the install file.
3. Open Revit and click 'Always Load' when add-in is found. The Revit addin will be installed for all supported versions of Revit.

## Build and run
1. Clone the project to your machine and build it through Visual Studio. The requiered `.dll` files will be installed in the correct folders for Revit to automatically load them.
2. The plugin commands will be listed under Revit's Add-ins tab, on the External Tools tab 
3. Select the command to run

## Current functionality
Read a JSON file exported from `rg-designer` in Revit, reading the elements from the data tree into the open document (that should be based in a Modulous template, containing the parameters and family types that are expected).
Rouge elements that are supported: `Wall` `Floor` `Roof` `Ceiling` `Windows` `Doors`

## Current limitations
Levels are inferred from the element's parent `Module` or `Apartment`. The levels should exist in the current file prior to the command execution and are not (yet) informed from the input file.
Instance properties (intended to be defined by the `Code: Spec` parameter) are not implemented yet. Updated template and a defined database, structured and populated, is required to do so.
