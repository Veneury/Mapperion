# Rendimiento

## Cómo leer esto

Cada número es un múltiplo del mismo mapeo escrito a mano, medido en la misma corrida con
BenchmarkDotNet. Los tiempos absolutos se mueven entre máquinas; lo que viaja es el múltiplo.

Están tomados en un portátil, no en una máquina dedicada, así que trata las diferencias de unos
pocos por ciento como ruido. Son indicativos, y son honestos sobre dónde pierde Mapperion.

Cada fila de abajo sale de una sola corrida, con todos los participantes juntos. Eso importa más
de lo que parece: dos corridas en esta máquina han puesto a la misma librería sin tocar un veinte
por ciento aparte, así que una tabla montada con varias puede enseñar un ganador que la medición
nunca encontró.

## Los escenarios

| Escenario | Manual | Mapperion | Con `MapFast` | Source generator | Mapperly | Mapster | AutoMapper 14 |
|---|---|---|---|---|---|---|---|
| Plano, 10 primitivas | 11,3 ns | 2,60x | 1,95x | **1,06x** | 1,02x | 1,80x | 4,93x |
| Anidado con colección | 50,2 ns | 1,74x | — | — | — | **1,43x** | 2,90x |
| Colección de 1.000 | 6,21 µs | 1,75x | — | 1,30x | **1,14x** | 1,38x | 1,74x |
| Aplanado, 4 saltos | 9,47 ns | 3,09x | — | — | — | **1,83x** | 5,38x |
| Record por constructor | 6,86 ns | 2,91x | — | **1,01x** | 1,06x | 2,09x | 7,23x |
| Destino existente | 7,42 ns | 2,81x | — | — | — | **2,01x** | 6,15x |

Las asignaciones coinciden con el código escrito a mano en todas las filas, tanto para Mapperion
como para Mapster. AutoMapper asigna más en dos de ellas.

## Qué dice esto

**El source generator es tan rápido como escribirlo tú**, entre 1,01x y 1,30x, y va por delante de
Mapster en las tres filas en las que aparece. Si el mapeo está de verdad en tu camino caliente,
esa es la respuesta, y es una diferencia mayor que cualquier ajuste del motor de runtime.

**El motor de runtime le gana a AutoMapper en todas partes**, por entre la mitad y dos tercios.

**Mapster es más rápido que el motor de runtime en todos los escenarios**, por entre 1,2x y 1,7x.
La mayor parte de eso no es el mapeo, es cómo llega la llamada. `mapper.Map<A, B>(x)` es un método
genérico alcanzado a través de una interfaz, y el runtime resuelve sus argumentos de tipo en cada
llamada; despiezar una llamada puso eso en unos 7,4 ns de 31. El `source.Adapt<T>()` de Mapster es
un método de extensión sobre un tipo estático y nunca lo paga.

Eso parece un muro, y para una llamada suelta lo es: en el escenario plano, todo lo que Mapster
gasta por encima del código a mano son unos 9 ns, y solo nuestro despacho por interfaz son unos
7,4 de ellos. No hay margen donde ganar sin cambiar `IMapper` — que es justo lo que hace que
migrar desde AutoMapper sea un cambio de namespace, y no está en venta.

Pero nada de ese trabajo depende del objeto que se mapea. **`MapperFor` lo pide una sola vez**, y
pasado el muro la foto se da la vuelta:

| | Corrida 1 | Corrida 2 |
|---|---|---|
| `mapper.MapperFor<Flat, FlatDto>()`, y luego llamado | **1,62x** | **1,20x** |
| Mapster | 2,12x | 1,51x |
| `MapFast` | 2,02x | 1,55x |

Dos corridas, las dos claras, y el margen —de 4 a 5 ns— es varias veces la dispersión. Está
explicado por lo que quita, no encontrado por suerte: el despacho y la búsqueda del plan son por
par, no por objeto, así que un bucle los pagaba una vez por elemento sin motivo.

En términos absolutos el hueco de una llamada suelta es de menos de diez nanosegundos por objeto.
Para una petición que mapea cincuenta objetos eso es medio microsegundo, frente a una petición que
se mide en milisegundos — que es por lo que `Map` sigue siendo lo que hay que escribir hasta que
un profiler diga otra cosa.

## MapperFor, para un bucle

```csharp
Func<Order, OrderDto> toDto = mapper.MapperFor<Order, OrderDto>();

foreach (Order order in orders)
{
    results.Add(toDto(order));
}
```

Todo lo que no depende del objeto —resolver los argumentos de tipo, encontrar el plan— pasa una
vez, y lo que vuelve es un `Func` corriente, así que también entra directo en un `Select`.

Guárdalo mientras dure el bucle, o como campo al lado del mapper. **Pedir uno por llamada cuesta
más de lo que ahorra**, porque el trabajo que evita es el trabajo que hace.

## MapFast, para una llamada suelta

```csharp
OrderDto dto = mapper.MapFast<Order, OrderDto>(order);
```

El mismo resultado que `Map`, alcanzado sin el despacho por interfaz, para los sitios donde no hay
dónde guardar una función. Sigue buscando el plan en cada llamada, así que es el más lento de los
dos; donde hay un bucle, `MapperFor` es la mejor respuesta.

Los dos reconocen el mapper que construye la librería y caen de vuelta a la interfaz con cualquier
otra implementación, así que un decorador sigue funcionando.

## Arranque

Para una configuración de seis mapas: construirla cuesta 8,6 µs, construirla y validarla 14,1 µs, y
compilar el primer plan 472 µs. Los planes se compilan en el primer uso y se guardan.
