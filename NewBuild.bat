@echo off
cls

set /p newVer="Build: " 
mkdir "CEE-Build-%newVer%"

copy /y "Corsair Effects Engine\bin\Debug\Corsair Effects Engine.exe" "CEE-Build-%newVer%\Corsair Effects Engine.exe"
copy /y "Corsair Effects Engine\bin\Debug\Corsair Effects Engine.exe.config" "CEE-Build-%newVer%\Corsair Effects Engine.exe.config"
copy /y "Corsair Effects Engine\bin\Debug\NAudio.dll" "CEE-Build-%newVer%\NAudio.dll"
copy /y "Corsair Effects Engine\bin\Debug\Xceed.Wpf.Toolkit.dll" "CEE-Build-%newVer%\Xceed.Wpf.Toolkit.dll"
xcopy /s /i /y "Corsair Effects Engine\bin\Debug\CorsairDevices"  "CEE-Build-%newVer%\CorsairDevices"

copy /y "SelfUpdater\bin\Debug\SelfUpdater.exe" "CEE-Build-%newVer%\SelfUpdater.exe"
copy /y "SelfUpdater\bin\Debug\SelfUpdater.exe.config" "CEE-Build-%newVer%\SelfUpdater.exe.config"


mkdir "CEE-Build-%newVer%-Update"

copy /y "Corsair Effects Engine\bin\Debug\Corsair Effects Engine.exe" "CEE-Build-%newVer%-Update\Corsair Effects Engine.exe"
copy /y "Corsair Effects Engine\bin\Debug\Corsair Effects Engine.exe.config" "CEE-Build-%newVer%-Update\Corsair Effects Engine.exe.config"
copy /y "Corsair Effects Engine\bin\Debug\NAudio.dll" "CEE-Build-%newVer%-Update\NAudio.dll"
copy /y "Corsair Effects Engine\bin\Debug\Xceed.Wpf.Toolkit.dll" "CEE-Build-%newVer%-Update\Xceed.Wpf.Toolkit.dll"
xcopy /s /i /y "Corsair Effects Engine\bin\Debug\CorsairDevices"  "CEE-Build-%newVer%-Update\CorsairDevices"