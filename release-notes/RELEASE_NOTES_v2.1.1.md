# RAGE 2 Modding Toolkit v2.1.1

**Fecha**: 2026-09-30
**SHA256**: 7264FB8900752ACA8D0EF10B1A147266F31D6BC2CF67747EEB97527F3E1FF24F
**Tamaño**: 47.14 MB

## Highlights

Esta version cierra un bug critico que impedia usar **EXTRACT EVERYTHING** en el release publico, y anade soporte multi-file al **World Settings Editor**.

## Bug critico arreglado

El boton **EXTRACT EVERYTHING** crasheaba con "Index was out of range" al final del proceso, cuando ya habia extraido y clasificado los 39,673 archivos. Un falso positivo: el extract funcionaba, solo fallaba el popup de resumen.

Ahora el wizard avanza al Step 4 con el resumen completo y cierra sin problemas.

## World Settings Editor: edita multiples ficheros en una sesion

Antes: al cambiar de nodo o de fichero, el valor editado se perdia. SAVE solo escribia la pagina visible.

Ahora:
- Editas N nodos en M ficheros distintos.
- Cambias de fichero, vuelves, todo sigue ahi.
- SAVE abre un confirm dialog que lista los ficheros afectados.
- Un solo ZIP con todos los .rtpc modificados.

## GUI

- Home: botones inferiores en grid 2x2 simetrico.
- Wizard Extract: fuera el boton redundante "Extract all" y el label huerfano.
- EXTRACT EVERYTHING con misma anchura que el boton principal.
- Help del editor de settings actualizado.

## Internal

- 6 sitios de Process.Start ahora en using (handles liberados inmediatamente).
- Diagnostico extendido en StartExtract (dumps a _outputs si hay excepcion).

## Instalacion

1. Descarga RAGE2TOOLKIT-v2.1.1.7z
2. Verifica SHA256: 7264FB8900752ACA8D0EF10B1A147266F31D6BC2CF67747EEB97527F3E1FF24F
3. Extrae en cualquier carpeta
4. Ejecuta RAGE2Toolkit.exe
5. En la home, selecciona RAGE2.exe y output folder

## Notas

- No incluye oo2core_7_win64.dll (se autocopia del juego al seleccionar RAGE2.exe).
- Requiere RAGE 2 instalado localmente.
- Compatible con mods existentes de v2.1.0.
