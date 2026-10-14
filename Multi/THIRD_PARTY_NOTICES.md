# Componentes de terceros

## FFmpeg 8.0.3

Copyright (c) 2000–2026 the FFmpeg developers.

Goggle 3 LiveView Multi ejecuta una compilación propia y separada de FFmpeg, sin código GPL ni no libre. Licencia: **GNU Lesser General Public License, versión 2.1 o posterior**. Texto en `third_party/ffmpeg/COPYING.LGPLv2.1`, también en `Goggle 3 LiveView Multi.app/Contents/Resources/Licenses/COPYING.LGPLv2.1`.

- Fuente original completa: <https://ffmpeg.org/releases/ffmpeg-8.0.3.tar.xz>
- SHA-256: `6136812ea6d4e68bdba27e33c2a94382711cdf4f8602ffef056ff792bd6f9818`
- Construcción exacta: `scripts/build-decoder.sh`.
- La fuente de FFmpeg no ha sido modificada. Se seleccionaron componentes mediante su configuración de compilación.
- El ejecutable puede sustituirse por otra compilación compatible en `Goggle 3 LiveView Multi.app/Contents/MacOS/g3-decoder`; tras sustituirlo se debe volver a firmar la app localmente. No hay restricciones de licencia que impidan modificar o depurar la aplicación para usar una versión modificada.
- Se proporciona el archivo fuente completo de esa versión junto con los paquetes entregados. No se distribuyen las bibliotecas compartidas de Homebrew ni componentes de la aplicación propietaria de referencia.

## Edición Multi para macOS

Esta edición no incorpora el runtime .NET ni binarios Windows. La grabación individual y el mosaico usan AVFoundation del sistema. El instalador no contiene vídeos de muestra.

## Referencias de investigación, no incluidas como dependencias

<https://github.com/samuelsadok/dji_protocol/blob/master/udp_protocol.md> describe los campos del protocolo UDP para DJI anteriores. La reutilización en Goggles 3 se comprobó en el dispositivo propio; no se incorporó código de ese repositorio.

<https://github.com/wllm-rbnt/third-eye> documenta Goggles 3 e identifica una ruta de red diferente de su transporte móvil. No se incorporó su código al producto.

<https://github.com/planktonwhc/Goggles3-USB-Livestream> se utilizó como referencia observable para confirmar el vídeo por USB. Su software propietario no se redistribuye.

<https://github.com/morluto/rea> se ejecutó localmente como servidor MCP de análisis. REA no forma parte de la app ni es una dependencia para utilizarla.
