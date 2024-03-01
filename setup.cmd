@echo off
set /p "bopl_path=Bopl battle path (e.g.: C:\Program Files (x86)\Steam\steamapps\common\Bopl Battle): "
mklink /j GameFolder "%bopl_path%"
pause