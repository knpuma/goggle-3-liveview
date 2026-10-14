# Goggle 3 LiveView — Windows

Descarga la edición x64 para Intel/AMD o ARM64 para Windows con procesador ARM. Extrae el ZIP completo y abre **Goggle 3 LiveView.exe**. El runtime .NET y el decodificador están incluidos.

La [guía visual](https://knpuma.github.io/goggle-3-liveview/) explica la instalación y los ajustes de las gafas. Windows 11 recomendado. Distribución portable: puedes mover la carpeta completa; no se instala un servicio ni un inicio automático.

## Primera conexión USB

Activa OTG y la vista en directo compartida en las gafas. Pulsa Conectar. Windows debe reconocer el adaptador RNDIS de Microsoft; **no lo sustituyas por WinUSB/Zadig**.

La app identifica las Goggles 3 por VID `2CA3` y PID `0020`. Si es necesario, ofrece preparar únicamente ese adaptador y solicita elevación: añade `192.168.60.1/24`, sin modificar otras redes, DNS, puerta de enlace ni cortafuegos. Envía la solicitud de vídeo a `192.168.60.2:9003` por el puerto local `12346`.

La copia del estado DHCP se conserva en `%ProgramData%/Goggles3Mac` (nombre histórico para mantener compatibilidad). Para revertir la configuración añadida, abre PowerShell como administrador dentro de la carpeta de la app y ejecuta `./Prepare-Usb.ps1 -Restore`. El script se detiene si detecta otra IP fija o un conflicto.

Grabar crea MP4 sin audio en `Vídeos/Goggle 3 LiveView`. Detener finaliza el archivo. Usa MPEG-4 con marcas de tiempo reales y frecuencia variable. El diagnóstico de la sesión puede consultarse en el menú contextual de la barra de controles y en `%LocalAppData%/Goggle 3 LiveView/diagnostic.txt`.

## Construcción

SDK oficial .NET 10. Los binarios de FFmpeg correspondientes se incluyen en el árbol de fuentes. Para generar el paquete autocontenido desde Windows:

```powershell
dotnet test Windows/Goggles3.Core.Tests
./Windows/scripts/Publish.ps1 -Runtime win-x64
# O bien: -Runtime win-arm64
```

Desde macOS, se utiliza `EnableWindowsTargeting`:

```sh
GOGGLES_DOTNET="$(command -v dotnet)" zsh scripts/publish-windows.sh x64
GOGGLES_DOTNET="$(command -v dotnet)" zsh scripts/publish-windows.sh arm64
```

Para reconstruir FFmpeg 8.0.3, instala el compilador oficial [llvm-mingw 20261006 para macOS universal](https://github.com/mstorsjo/llvm-mingw/releases/tag/20261006) dentro de `work/windows-dependencies/compiler/` y ejecuta `zsh scripts/build-windows-decoder.sh x64` o `arm64`. Se verifica el SHA-256 de la fuente antes de compilar. Consulta [los avisos de terceros](../THIRD_PARTY_NOTICES.md).

Modo explícito de reproducción local para desarrollo:

```powershell
& '.\Goggle 3 LiveView.exe' --replay C:\pruebas\captura.h264
```

`--software-render` permite usar representación por software cuando el controlador gráfico no inicializa WPF. No cambia el transporte USB. `Goggles3.Check` permite comprobar el decodificador y la grabación sin abrir la interfaz.

## Referencias

- [Microsoft: RNDIS incluido en Windows](https://learn.microsoft.com/en-us/windows-hardware/drivers/network/remote-ndis--rndis-2).
- [Microsoft: compilar WPF desde macOS](https://learn.microsoft.com/en-us/dotnet/core/tools/sdk-errors/netsdk1100).
- [Manual DJI Goggles 3](https://dl.djicdn.com/downloads/DJI_Goggles_3/20240528/DJI_Goggles_3_User_Manual_ES.pdf).
