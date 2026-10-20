# Goggle 3 LiveView · Linux

Visor USB directo para **DJI Goggles 3**, con grabación MP4 local y pantalla completa. Sin RC Pro, Raspberry Pi ni dispositivos intermedios. Incluye inglés y español; cambia con **ES / EN**.

**Linux 0.3.3 · Solo se ha probado en Ubuntu 24.04 LTS x64 (Intel/AMD); no se han probado otras distribuciones Linux.** Requiere X11 o XWayland. Linux ARM64 y Wayland nativo no son compatibles.

## Instalación

Descarga el `.deb` de la [publicación principal del visor](https://github.com/knpuma/goggle-3-liveview/releases/tag/v0.3.2) y ejecuta en su carpeta:

```sh
sudo apt install ./Goggle-3-LiveView-Linux-x64-0.3.3.deb
```

Abre **Goggle 3 LiveView** desde el menú de aplicaciones. Incluye .NET; apt instala FFmpeg y las dependencias. **No ejecutes la app con sudo.** En Wayland necesitas XWayland. La configuración USB inicial requiere una sesión gráfica con agente de autorización polkit.

## Conexión

1. Conecta las Goggles 3 encendidas con un **cable USB de datos**.
2. Activa **Settings → About → OTG Wired Connection** en las gafas.
3. Mueve el botón 5D hacia atrás y activa la **compartición LiveView con un dispositivo móvil por Wi-Fi**. Aunque se llame así, esta app recibe la imagen por **USB**; no conectes el ordenador a la Wi-Fi de las gafas.
4. Pulsa **Conectar** y autoriza el diálogo del sistema si aparece. Solo se configura el adaptador USB verificado como DJI, con `192.168.60.1/24`. No se cambian Wi-Fi, DNS, puerta de enlace ni cortafuegos. La configuración es temporal y puede solicitarse de nuevo al reconectar o reiniciar.

Sin aeronave enlazada, mantén también **Camera View Recording** activado en **Settings → Camera → Advanced Camera Settings**. Para mostrar la cámara de vuelo, las gafas deben recibir señal de una aeronave o unidad aérea compatible.

Los menús siguen el [manual oficial de DJI](https://dl.djicdn.com/downloads/DJI_Goggles_3/DJI_Goggles_3_User_Manual_EN.pdf). Este visor USB independiente y el funcionamiento sin aeronave no son funciones Linux respaldadas por DJI.

## Controles

- **Grabar / Detener:** MP4 sin audio en Vídeos → Goggle 3 LiveView. Espera al mensaje de guardado. Desconectar o cerrar finaliza la grabación.
- **Pantalla completa**, junto a Grabar, o **F11:** oculta controles y estadísticas. **Esc** vuelve a la ventana. Se conserva la proporción de imagen.
- FPS mide fotogramas decodificados; 1920 × 1080 es el tamaño de vista/grabación tras escalar, no necesariamente la resolución de origen. Mb/s mide vídeo recibido, no el enlace de radio.

## Si no conecta

Cierra otros visores, desconecta en la app, desactiva y activa LiveView en las gafas y conecta de nuevo. Comprueba OTG, cable y Camera View Recording. Linux necesita su controlador estándar **rndis_host / usbnet**. El USB esperado es `2ca3:0020`; puedes revisar `lsusb` e `ip link` sin modificar dispositivos.

Si otra red usa `192.168.60.x`, desconéctala o resuelve el conflicto. Si NetworkManager cambia la dirección, configura **solo la conexión USB DJI** desde su editor: IPv4 Manual, `192.168.60.1`, prefijo `24`, sin puerta de enlace ni DNS, y marca usar esta conexión únicamente para los recursos de su red. No modifiques la conexión Wi-Fi/Ethernet. La app no sobrescribe otras direcciones automáticamente.

Si el cortafuegos bloquea las respuestas, permite únicamente el tráfico desde `192.168.60.2:9003` hacia UDP local `12346` por la interfaz DJI. No desactives el cortafuegos globalmente.

## Archivo portátil

El `.tar.gz` incluye .NET, pero requiere FFmpeg y bibliotecas del sistema. Consulta los comandos de dependencias en la [guía completa](README.md#portable-archive), extrae el archivo y ejecuta `./goggle-3-liveview`. Configura previamente **solo el adaptador DJI** mediante NetworkManager como se indica arriba. El portátil no instala el asistente privilegiado; el `.deb` ofrece la configuración integrada. Mantén juntos todos los archivos.

## Verificación y ayuda

La automatización comprueba compilación en Ubuntu 24.04, protocolo UDP con interfaz fijada, salvaguardas de configuración, decodificación H.264, MP4 reproducible y apertura gráfica de ambos paquetes, idiomas y pantalla completa. **No equivale a verificar unas Goggles 3 físicas conectadas a Linux**; esta primera edición es preliminar.

[Notifica un fallo](https://github.com/knpuma/goggle-3-liveview/issues) indicando distribución, escritorio, firmware, mensaje y pasos. No publiques números de serie ni información privada. Usa siempre las gafas como pantalla principal durante el vuelo.

Código MIT. Licencias de componentes en `Licenses/`. No se incluye FFmpeg: se utiliza el de tu distribución. Los vídeos se guardan localmente, sin subidas a Internet. [Compilación y detalles técnicos](README.md#build).
