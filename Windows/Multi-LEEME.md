# Goggle 3 LiveView Multi · Windows x64

Visualiza hasta cuatro conexiones USB de DJI Goggles 3 en un mosaico 2×2. Pon nombre a cada piloto, abre un canal a pantalla completa y graba canales individuales o el mosaico completo en MP4.

## Instalación

1. Extrae todo el ZIP en una carpeta.
2. Abre **Goggle 3 LiveView Multi.exe**. Conserva juntos los archivos incluidos.
3. Usa **ES / EN** para cambiar el idioma.

Requiere Windows 11 x64 o Windows 10 22H2 x64. Incluye .NET y el codificador de vídeo. Utiliza cables USB de datos; se recomienda un hub USB con alimentación para varias gafas.

## Conexión

En cada par de gafas:

1. Activa **Configuración → Acerca de → Conexión por cable OTG**.
2. Activa en el menú de acceso directo la compartición de la vista en directo con un dispositivo móvil por Wi-Fi. El ordenador recibe el vídeo por USB y no necesita conectarse a esa red Wi-Fi.
3. Sin aeronave, activa **Grabación de la vista de cámara** en **Configuración → Cámara → Configuración avanzada de la cámara**.

Conecta los cables, selecciona una conexión USB en cada panel y pulsa **Conectar** o **Conectar todas**. Windows puede pedir autorización de administrador para configurar el adaptador de red USB de las gafas. No se modifican el Wi-Fi, Ethernet, DNS, puerta de enlace predeterminada ni firewall.

El USB debe aparecer como adaptador de red operativo en el Administrador de dispositivos de Windows. Si no aparece, comprueba Windows Update y el cable de datos. No instales controladores de páginas de descargas desconocidas.

## Grabación y pantalla completa

- **Grabar** guarda el canal correspondiente.
- **Grabar mosaico** guarda la cuadrícula 2×2 en un MP4 de 1920×1080, con un objetivo de 30 FPS. Los canales sin imagen o con señal detenida aparecen en negro.
- Ambas grabaciones pueden funcionar a la vez. El rendimiento depende del procesador, el USB y el disco.
- Los vídeos se guardan en **Vídeos / Goggle 3 LiveView Multi**.
- Para la grabación y espera a **Guardado** antes de desconectar las gafas o cerrar la aplicación.
- **⛶**, junto a Grabar, muestra el canal a pantalla completa sin controles. El botón de la barra inferior muestra el mosaico completo. **Esc** o **F11** permite salir.

Si no llega imagen, desconecta ese canal, reinicia LiveView en esas gafas y vuelve a conectar. Cierra otros visores que estén usando las mismas gafas.

Utiliza siempre las gafas como pantalla principal para volar.

[Manual oficial DJI Goggles 3](https://dl.djicdn.com/downloads/DJI_Goggles_3/DJI_Goggles_3_User_Manual_EN.pdf) · [Ayuda y avisos de errores](https://github.com/knpuma/goggle-3-liveview/issues)

## Restaurar la configuración USB

La aplicación guarda los cambios de red realizados en cada adaptador DJI. Para revertirlos, cierra el visor y ejecuta este comando desde la carpeta de la aplicación en PowerShell como administrador. Sustituye el GUID por el InterfaceGuid del adaptador DJI que muestra `Get-NetAdapter`:

```powershell
.\Prepare-MultiUsb.ps1 -AdapterId "GUID-DEL-ADAPTADOR" -Restore
```

Licencia MIT. Las licencias de terceros se incluyen en **Licenses**.
