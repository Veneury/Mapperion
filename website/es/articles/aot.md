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

Objetos planos y anidados, colecciones y diccionarios, records y constructores, enums y nullables,
y valores leídos de texto. Los miembros se emparejan como los empareja el motor de runtime: por
nombre, luego sin distinguir mayúsculas, y luego deletreando una ruta — un destino llamado
`CustomerAddressCity` encuentra `Customer.Address.City` él solo, hasta tres miembros de
profundidad.

Cuatro atributos, y los dos primeros son los que vas a usar:

| | |
|---|---|
| `[MapProperty("Customer.Address.City", "CustomerCity")]` | un origen que las convenciones no encontrarían |
| `[MapperIgnore("Note")]` | deja en paz un miembro de destino |
| `[MapperResolve(nameof(Total), "Total")]` | llena un miembro desde un método de la clase, dándole el origen entero |
| `[MapperInclude(typeof(CardPayment), typeof(CardPaymentDto))]` | entrega un origen derivado al método que lo mapea, como hace `Include` |

Los dos últimos son en lo que se convierten un value resolver y `Include` cuando no hay contenedor
del que sacar el resolver ni configuración que leer en ejecución. Todo lo demás —pasos de antes y
después, `ResolutionContext`, leer la configuración de un perfil— es del motor de runtime, porque
nada de eso existe mientras el proyecto compila.

Reporta sus propios problemas como diagnósticos del compilador, de `MPR0001` a `MPR0009`, así que
un mapper que no se puede generar tumba el build con un mensaje en vez de producir algo
sorprendente.

Los prefijos, los sufijos y las convenciones de nombres tampoco se leen. Eso se configura en un
`MapperConfiguration`, así que un origen que escribe sus miembros `first_name` necesita aquí
`[MapProperty]` aunque al motor de runtime se le pueda decir la grafía una vez y olvidarse.

## Está comprobado, no afirmado

`samples/Mapperion.Aot` en el repositorio es una aplicación publicada con `PublishAot=true` que
mapea con código generado y comprueba su propia salida, saliendo con código distinto de cero si un
valor está mal. Se construye con los analizadores de trimming y AOT encendidos, y cada corrida de
CI la publica en nativo y la ejecuta.

Los dos motores se comparan además directamente: una suite de pruebas pasa los mismos casos por
ambos y exige las mismas respuestas.
