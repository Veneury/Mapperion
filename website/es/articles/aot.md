# Ahead-of-time y trimming

## La versión corta

El motor de runtime construye árboles de expresión y los compila mientras el programa corre. Eso
no puede funcionar en una aplicación publicada ahead-of-time, y la librería lo dice: los tipos
implicados llevan `[RequiresUnreferencedCode]` y `[RequiresDynamicCode]`, así que el compilador te
avisa en vez de dejar que te enteres en ejecución.

El source generator es la respuesta. Escribe ese mismo mapeo como C# corriente en tiempo de
compilación.

```
dotnet add package Mapperion.SourceGenerator
```

```csharp
[Mapperion.Mapper]
public partial class OrderMapper
{
    [Mapperion.MapProperty("Customer.Address.City", "CustomerCity")]
    public partial OrderDto ToDto(Order source);

    public partial LineDto ToDto(Line source);

    public partial List<LineDto> ToDtos(IList<Line> source);
}
```

El generador rellena los cuerpos. Lo que acaba en tu ensamblado es el código que habrías escrito a
mano: sin reflexión, sin nada compilado en ejecución, sin nada de lo que el trimmer pueda dudar.

## Qué cubre

Objetos planos y anidados, colecciones, rutas explícitas con `[MapProperty]`, `[MapperIgnore]`,
records y constructores, enums y nullables. Reporta sus propios problemas como diagnósticos del
compilador, de `MPR0001` a `MPR0006`, así que un mapper que no se puede generar tumba el build con
un mensaje en vez de producir algo sorprendente.

No hace las cosas que solo tienen sentido en ejecución: value resolvers, pasos de antes y después,
despacho polimórfico. Eso es del motor de runtime.

## Está comprobado, no afirmado

`samples/Mapperion.Aot` en el repositorio es una aplicación publicada con `PublishAot=true` que
mapea con código generado y comprueba su propia salida, saliendo con código distinto de cero si un
valor está mal. Se construye con los analizadores de trimming y AOT encendidos, y cada corrida de
CI la publica en nativo y la ejecuta.

Los dos motores se comparan además directamente: una suite de pruebas pasa los mismos casos por
ambos y exige las mismas respuestas.
