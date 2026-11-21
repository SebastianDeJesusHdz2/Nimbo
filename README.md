# Nimbo

Estudio de escritura de escritorio para Windows y Linux. Nimbo 0.5.1 usa C# y .NET 10, Avalonia, SQLite y XeLaTeX.

## Primera sesión

Elige un formato en la biblioteca, escribe un título y pulsa **Crear obra**. A la izquierda se selecciona la sección o el capítulo. Nimbo guarda después de una pausa al escribir. **Ctrl+S** guarda inmediatamente. Desde **Obra** puedes editar título, autor, portada y descripción. Desde **Glosario** puedes crear fichas, asignar una ficha base y añadir campos heredables.

**LaTeX** entrega un ZIP con el archivo fuente y las ilustraciones. **PDF** requiere una instalación local de XeLaTeX con sus paquetes, por ejemplo TeX Live en Linux o MiKTeX en Windows. En **Preferencias** puedes seleccionar el ejecutable de una instalación existente. En un equipo que ya utilizaba LumenWriter puedes elegir el archivo `Tools/MiKTeX/miktex/bin/x64/xelatex.exe` de ese proyecto. En Ubuntu, los paquetes de la prueba de impresión son `texlive-xetex`, `texlive-latex-extra` y `texlive-fonts-recommended`. Este último aporta las métricas de letra que utiliza Beamer. El runtime de .NET se incluye en los paquetes de distribución.

## Biblioteca anterior

Las novelas aparecen como libros giratorios, los documentos como hojas y las presentaciones como diapositivas. En el editor, sitúa el cursor donde quieres una ilustración y abre **Imágenes**. Puedes escoger una imagen o varias, escribir un pie y elegir la composición de cada una. **Publicar → Vista previa de impresión** permite comprobar el ajuste del texto alrededor de una imagen antes de imprimir.

En **Glosario → Cuadernillo** puedes elegir el tamaño de papel y exportar todas las fichas o una selección. Cada ficha tiene también su propio botón de PDF. **Publicar → Edición completa y fichas** prepara el manuscrito, el proyecto LaTeX, el glosario y los retratos de personaje con sus datos en PDF.

### Importación

Usa **Importar biblioteca…** y selecciona la carpeta de LumenWriter que contiene `Data`. Nimbo copia las obras a su propia base local e identifica las obras ya importadas por su ID. Cada obra se confirma por separado y una importación interrumpida se puede repetir. Cuando la biblioteca contiene una edición de lectura en su taller, **Obra → Edición anterior** permite recuperar los PDF y el ZIP originales. El PDF generado desde el manuscrito usa la maquetación de Nimbo. El importador convierte el énfasis, las listas, las tablas sencillas y las figuras que puede cotejar con el texto original. La fidelidad de diseños anteriores, estilos de párrafo, tablas WPF y posiciones de figuras requiere cotejo antes de sustituir el editor anterior.

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
- `Nimbo.Application` coordina la sesión de escritura, las revisiones y la recuperación por copia.
- `Nimbo.Storage` guarda en SQLite, comprueba conflictos entre revisiones e importa datos y medios.
- `Nimbo.Publishing` compone PDF y proyectos LaTeX en carpetas temporales y entrega el resultado mediante reemplazo al finalizar.
- `Nimbo.Desktop` contiene las ventanas de Avalonia y el arranque con Velopack.

La compilación en GitHub prepara Windows x64 y Linux x64. Una etiqueta de versión que empiece con `v` genera un borrador de publicación. En Linux el instalador por usuario se ejecuta con `sh instalar-linux.sh ./Nimbo.AppImage`, sustituyendo el nombre por el archivo descargado. Las actualizaciones del lanzador requieren versiones publicadas en GitHub Releases.

El avance y las comprobaciones pendientes se registran en [el plan](docs/PLAN.txt).





## Licencia

El código de Nimbo se distribuye bajo la [GNU General Public License, versión 3](LICENSE) (GPL-3.0-only). Los derechos sobre los manuscritos, ilustraciones y presentaciones que creas siguen correspondiendo a sus respectivos autores. Las bibliotecas de terceros conservan sus propias licencias.

En el manuscrito, **Ctrl+F** busca en la sección abierta. **F3** avanza y **Mayús+F3** retrocede. **Esc** devuelve el foco al texto. Al reabrir una obra, Nimbo recupera la última sección visitada.

### Plantillas y lectura

En **Nueva obra** elige el formato y la plantilla. En un CV, **Perfil y diseño** reúne los datos de contacto y el estilo impreso. Cada sección admite varias entradas con puesto, institución, fechas y resultados. Cambiar el estilo desde la ficha conserva los apartados que ya escribiste.

**Leer** abre la sección sin herramientas de edición. **Vista previa** compone el PDF con XeLaTeX y lo abre en el lector del sistema. En **Ficha y diseño** puedes elegir papel, tinta, interlineado y sangría. Las preferencias de pantalla se guardan aparte, en **Preferencias → Apariencia y escritura**.
