ECHO OFF
set "addin_filename=RougeRevitAddIn.addin"

set "version=2019"
set "addins_folder=%ProgramData%\Autodesk\Revit\Addins\%version%"
set "source_folder=Revit %version%"
ECHO Copying %version% files:

if EXIST "%addins_folder%" (
  xcopy "%source_folder%\*.*" "%addins_folder%" /Y /E /I 
) else (
  ECHO no Revit %version% installed
)

set "version=2020"
set "addins_folder=%ProgramData%\Autodesk\Revit\Addins\%version%"
set "source_folder=Revit %version%"
ECHO Copying %version% files:

IF EXIST "%addins_folder%" (
  xcopy "%source_folder%\*.*" "%addins_folder%" /Y /E /I 
) else (
  ECHO no Revit %version% installed
)

set "version=2021"
set "addins_folder=%ProgramData%\Autodesk\Revit\Addins\%version%"
set "source_folder=Revit %version%"
ECHO Copying %version% files:

IF EXIST "%addins_folder%" (
  xcopy "%source_folder%\*.*" "%addins_folder%" /Y /E /I 
) else (
  ECHO no Revit %version% installed
)

set "version=2022"
set "addins_folder=%ProgramData%\Autodesk\Revit\Addins\%version%"
set "source_folder=Revit %version%"
ECHO Copying %version% files:

IF EXIST "%addins_folder%" (
  xcopy "%source_folder%\*.*" "%addins_folder%" /Y /E /I 
) else (
  ECHO no Revit %version% installed
)

pause