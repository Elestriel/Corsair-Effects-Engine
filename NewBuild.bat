@echo off
cls

set /p newVer="Build: " 
mkdir "CEE-Build-%newVer%"
mkdir "CEE-Build-%newVer%\Corsair Effects Engine"

copy /y "Corsair Effects Engine\bin\Debug\Corsair Effects Engine.exe" "CEE-Build-%newVer%\Corsair Effects Engine\Corsair Effects Engine.exe"
copy /y "Corsair Effects Engine\bin\Debug\Corsair Effects Engine.exe.config" "CEE-Build-%newVer%\Corsair Effects Engine\Corsair Effects Engine.exe.config"
copy /y "Corsair Effects Engine\bin\Debug\NAudio.dll" "CEE-Build-%newVer%\Corsair Effects Engine\NAudio.dll"
copy /y "Corsair Effects Engine\bin\Debug\Xceed.Wpf.Toolkit.dll" "CEE-Build-%newVer%\Corsair Effects Engine\Xceed.Wpf.Toolkit.dll"
copy /y "Corsair Effects Engine\bin\Debug\CUE.NET.dll" "CEE-Build-%newVer%\Corsair Effects Engine\CUE.NET.dll"
copy /y "Corsair Effects Engine\bin\Debug\CUESDK_2013.dll" "CEE-Build-%newVer%\Corsair Effects Engine\CUESDK_2013.dll"
xcopy /s /i /y "Corsair Effects Engine\bin\Debug\CorsairDevices"  "CEE-Build-%newVer%\Corsair Effects Engine\CorsairDevices"

copy /y "SelfUpdater\bin\Debug\SelfUpdater.exe" "CEE-Build-%newVer%\Corsair Effects Engine\SelfUpdater.exe"
copy /y "SelfUpdater\bin\Debug\SelfUpdater.exe.config" "CEE-Build-%newVer%\Corsair Effects Engine\SelfUpdater.exe.config"


mkdir "CEE-Build-%newVer%-Update"

copy /y "Corsair Effects Engine\bin\Debug\Corsair Effects Engine.exe" "CEE-Build-%newVer%-Update\Corsair Effects Engine.exe"
copy /y "Corsair Effects Engine\bin\Debug\Corsair Effects Engine.exe.config" "CEE-Build-%newVer%-Update\Corsair Effects Engine.exe.config"
copy /y "Corsair Effects Engine\bin\Debug\NAudio.dll" "CEE-Build-%newVer%-Update\NAudio.dll"
copy /y "Corsair Effects Engine\bin\Debug\Xceed.Wpf.Toolkit.dll" "CEE-Build-%newVer%-Update\Xceed.Wpf.Toolkit.dll"
copy /y "Corsair Effects Engine\bin\Debug\CUE.NET.dll" "CEE-Build-%newVer%-Update\Corsair Effects Engine\CUE.NET.dll"
copy /y "Corsair Effects Engine\bin\Debug\CUESDK_2013.dll" "CEE-Build-%newVer%-Update\Corsair Effects Engine\CUESDK_2013.dll"
xcopy /s /i /y "Corsair Effects Engine\bin\Debug\CorsairDevices"  "CEE-Build-%newVer%-Update\CorsairDevices"