if "%ENV_ConfigurationName%" == "Thunderstore" goto zip
echo copying from %ENV_TargetPath% to GameFolder\BepInEx\plugins\%ENV_TargetName%\
taskkill /f /im "BoplBattle.exe" /t 2>nul & set errorlevel=0
xcopy "%ENV_TargetPath%" "..\GameFolder\BepInEx\plugins\%ENV_TargetName%\" /y /e /i
rem zipping only on release
if "%ENV_ConfigurationName%" == "Debug" goto :eof
:zip
echo zipping %ENV_TargetName%.zip
xcopy "%ENV_TargetPath%" .\ /y /e /i & xcopy thunderstore\ . /y
tar acvf "%ENV_TargetName%.zip" icon.png manifest.json README.md CHANGELOG.md "%ENV_TargetFileName%"
del "%ENV_TargetFileName%" & del icon.png & del manifest.json
move "%ENV_TargetName%.zip" thunderstore