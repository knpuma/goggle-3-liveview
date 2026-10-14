# Componentes de terceros

## FFmpeg 8.0.3

Copyright (c) 2000–2026 the FFmpeg developers.

Goggle 3 LiveView ejecuta una compilación propia y separada de FFmpeg, sin código GPL ni no libre. Licencia: **GNU Lesser General Public License, versión 2.1 o posterior**. Texto en `third_party/ffmpeg/COPYING.LGPLv2.1`, también en `Goggle 3 LiveView.app/Contents/Resources/Licenses/FFmpeg-LGPL-2.1.txt`.

- Fuente original completa: <https://ffmpeg.org/releases/ffmpeg-8.0.3.tar.xz>
- SHA-256: `6136812ea6d4e68bdba27e33c2a94382711cdf4f8602ffef056ff792bd6f9818`
- Construcción exacta: `scripts/build-decoder.sh`.
- La fuente de FFmpeg no ha sido modificada. Se seleccionaron componentes mediante su configuración de compilación.
- El ejecutable puede sustituirse por otra compilación compatible en `Goggle 3 LiveView.app/Contents/MacOS/g3-decoder`; tras sustituirlo se debe volver a firmar la app localmente. No hay restricciones de licencia que impidan modificar o depurar la aplicación para usar una versión modificada.
- Se proporciona el archivo fuente completo de esa versión junto con los paquetes entregados. No se distribuyen las bibliotecas compartidas de Homebrew ni componentes de la aplicación propietaria de referencia.

## Versión Windows 0.3

El ejecutable Windows de FFmpeg también se compila desde la fuente íntegra de **FFmpeg 8.0.3** indicada arriba. Se construye con `scripts/build-windows-decoder.sh` y llvm-mingw 20261006, sin bibliotecas externas, GPL ni componentes no libres. La versión Windows usa el codificador MPEG-4 nativo de FFmpeg en lugar de VideoToolbox. Las variantes x64 y ARM64 son ejecutables separados de la app, sustituibles, y llevan la licencia LGPL 2.1 o posterior. Los archivos de configuración exacta se incluyen en los scripts de construcción; las rutas del equipo de construcción no son necesarias para ejecutarlos.

La aplicación Windows usa **.NET 10 y WPF**, software de Microsoft y colaboradores bajo sus licencias open source. El runtime redistribuible se incorpora mediante `dotnet publish --self-contained`; sus avisos y licencias se conservan en `Licenses/DotNET-THIRD-PARTY-NOTICES.txt`, `Licenses/DotNET-LICENSE.txt` y `Licenses/WPF-LICENSE.txt` en cada paquete publicado. Fuentes del runtime: <https://github.com/dotnet/runtime>; fuentes de WPF: <https://github.com/dotnet/wpf>. Goggle 3 LiveView no vincula código propietario de DJI ni redistribuye tipografías de Apple.

Se descargaron paquetes BtbN durante la investigación de construcción Windows, pero **no se incluyen ni se usan en los paquetes entregados**. Los decodificadores distribuidos son las compilaciones mínimas propias descritas arriba.

## Referencias de investigación, no incluidas como dependencias

<https://github.com/samuelsadok/dji_protocol/blob/master/udp_protocol.md> describe los campos del protocolo UDP para DJI anteriores. La reutilización en Goggles 3 se comprobó en el dispositivo propio; no se incorporó código de ese repositorio.

<https://github.com/wllm-rbnt/third-eye> documenta Goggles 3 e identifica una ruta de red diferente de su transporte móvil. No se incorporó su código al producto.

<https://github.com/planktonwhc/Goggles3-USB-Livestream> se utilizó como referencia observable para confirmar el vídeo por USB. Su software propietario no se redistribuye.

<https://github.com/morluto/rea> se ejecutó localmente como servidor MCP de análisis. REA no forma parte de la app ni es una dependencia para utilizarla.
