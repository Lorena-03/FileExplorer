# CONTRIBUTING.md

## Guidelines

Este repositorio requiere comentarios XML (`/// <summary>...</summary>`) en los métodos para facilitar la lectura, el mantenimiento y la generación de documentación. Los colaboradores deben seguir estas reglas al añadir o modificar código.

### Alcance
- Añadir `/// <summary>` sobre cada método (público, internal y privado) en los archivos `.cs` existentes y nuevos.
- Para métodos simples, una línea descriptiva es suficiente. Para métodos con comportamiento complejo, usar varias líneas y describir efectos laterales.
- Documentar `param` y `returns` cuando el método tiene parámetros o devuelve un valor.
- Mantener los comentarios en español, concisos y formales.

### Formato y estilo
- Usar oraciones cortas en imperativo/indicativo: describe qué hace el método, no cómo lo hace internamente.
- Evitar repetir el nombre del método en la descripción.
- Ejemplo mínimo:
  ```csharp
  /// <summary>
  /// Carga la imagen actual en el visor y actualiza la UI.
  /// </summary>
  /// <param name="path">Ruta del archivo de imagen.</param>
  /// <returns>True si la carga fue exitosa.</returns>
  bool LoadCurrentImage(string path) { ... }
  ```
- Para eventos y controladores (handlers) describir el propósito, por ejemplo: "Maneja clic del botón Guardar y abre el diálogo de guardado.".

### Convenciones
- Preferir `/// <summary>` en lugar de comentarios en línea `//` para documentar la intención del método.
- Mantener el texto en español. Si el método implementa un comportamiento estándar de una librería (ej. `Dispose`), permitirse comentario breve o referencia.
- No documentar trivialidades obvias (getters/setters automáticos) salvo que hagan algo adicional.

### Revisión de Pull Requests
- Incluir en la descripción del PR: "Se añadieron/actualizaron comentarios XML en X archivos".
- Los reviewers deben verificar que los comentarios describen el comportamiento y no se contradicen con el código.

### Formato del commit
- Para cambios de documentación usar mensajes tipo: `docs: añadir comentarios XML en <archivo(s)>`.

## Herramientas y generación de documentación
- Se puede usar `dotnet` o herramientas de terceros para generar documentación desde los comentarios XML.
- Mantener la coherencia para que las herramientas automaticas puedan procesar el XML.

## Responsable y mantenimiento
- El equipo debe mantener esta guía. Para cambios en el estilo, actualizar este archivo y notificar al equipo.

---

_Este archivo reemplaza el CONTRIBUTING.md existente (si existe) y establece las reglas para la documentación inline del proyecto._