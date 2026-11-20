# Nimbo

Estudio de escritura de escritorio para Windows y Linux. Esta primera alfa usa C# y .NET 10, Avalonia, SQLite y XeLaTeX.

## Primera sesión

Elige un formato en la biblioteca, escribe un título y pulsa **Crear obra**. A la izquierda se selecciona la sección o el capítulo. Nimbo guarda después de una pausa al escribir. **Ctrl+S** guarda inmediatamente. Desde **Obra** puedes editar título, autor, portada y descripción. Desde **Glosario** puedes crear fichas, asignar una ficha base y añadir campos heredables.

**LaTeX** entrega un ZIP con el archivo fuente y las ilustraciones. **PDF** requiere una instalación local de XeLaTeX con sus paquetes, por ejemplo TeX Live en Linux o MiKTeX en Windows. En **Preferencias** puedes seleccionar el ejecutable de una instalación existente. En un equipo que ya utilizaba LumenWriter puedes elegir el archivo `Tools/MiKTeX/miktex/bin/x64/xelatex.exe` de ese proyecto. El runtime de .NET se incluye en los paquetes de distribución.

## Biblioteca anterior

Usa **Importar biblioteca…** y selecciona la carpeta de LumenWriter que contiene `Data`. Nimbo copia las obras a su propia base local e identifica las obras ya importadas por su ID. Cada obra se confirma por separado y una importación interrumpida se puede repetir. La edición del contenido importado usa texto y medios. La fidelidad de diseños anteriores, estilos de párrafo, tablas WPF y posiciones de figuras requiere cotejo antes de sustituir el editor anterior.

## Compilación

Con el SDK de .NET 10:

```sh
dotnet run --project src/Nimbo.Desktop
dotnet run --project tests/Nimbo.Checks
dotnet run --project tests/Nimbo.Checks -- --pdf
```

La última prueba requiere XeLaTeX. La variable opcional `NIMBO_XELATEX` selecciona su ruta. `NIMBO_TEST_LEGACY` permite verificar una biblioteca anterior contra una base temporal.

## Estructura

- `Nimbo.Core` contiene obras, capítulos, formatos, fichas y herencia de atributos.
- `Nimbo.Storage` guarda en SQLite, comprueba conflictos entre revisiones e importa datos y medios.
- `Nimbo.Publishing` compone PDF y proyectos LaTeX en carpetas temporales y entrega el resultado mediante reemplazo al finalizar.
- `Nimbo.Desktop` contiene las ventanas de Avalonia y el arranque con Velopack.

La compilación en GitHub prepara Windows x64 y Linux x64. Una etiqueta de versión que empiece con `v` genera un borrador de publicación. En Linux el instalador por usuario se ejecuta con `sh instalar-linux.sh ./Nimbo.AppImage`, sustituyendo el nombre por el archivo descargado. Las actualizaciones del lanzador requieren versiones publicadas en GitHub Releases.

El avance y las comprobaciones pendientes se registran en [el plan](docs/PLAN.txt).

