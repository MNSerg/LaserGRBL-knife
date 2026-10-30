# Сборка LaserGRBL-knife под Windows

Приложение — WinForms на **.NET Framework 4.8**. Сборка нужна на Windows (или через GitHub Actions).

## Быстрый путь: CI

1. Push в ветку или создайте tag `v1.0.0-knife`
2. Workflow **Windows Release Build** соберёт Release и выложит artifact `LaserGRBL-knife-portable.zip`
3. Скачайте ZIP → распакуйте → запустите `LaserGRBL.exe`  
   Требуется установленный [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework) (обычно уже есть на Win10/11).

Ручной запуск workflow: GitHub → Actions → Windows Release Build → Run workflow.

## Локальная сборка (Visual Studio)

1. Установите [Visual Studio 2022](https://visualstudio.microsoft.com/) с рабочей нагрузкой **.NET desktop development**
2. Откройте `LaserGRBL.sln`
3. Configuration: **Release**, Platform: **Any CPU**
4. Build → Build Solution
5. Результат: `LaserGRBL\bin\Release\LaserGRBL.exe`

## Локальная сборка (MSBuild)

```bat
nuget restore LaserGRBL.sln
msbuild LaserGRBL.sln /p:Configuration=Release /p:Platform="Any CPU"
```

## Установщик

- `setup-knife.iss` — установщик форка **LaserGRBL-knife** (отдельный AppId, русский язык, ассоциация `.dxf`)
- Оригинальный `setup.iss` оставлен для совместимости

CI собирает `LaserGRBL-knife-setup.exe` через Inno Setup 6 (Chocolatey). Локально:

```bat
msbuild LaserGRBL.sln /p:Configuration=Release /p:Platform="Any CPU"
"%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" setup-knife.iss
```

## Режим плоттера

После запуска: меню **Grbl → Plotter / Knife settings...** → включите **Enable plotter (knife) mode**.  
Подробности и чеклист вопросов: [PLOTTER.md](PLOTTER.md).
