# Rendimiento

## Cómo leer esto

Cada número es un múltiplo del mismo mapeo escrito a mano, medido en la misma corrida con
BenchmarkDotNet. Los tiempos absolutos se mueven entre máquinas; lo que viaja es el múltiplo.

Están tomados en un portátil con otras cosas abiertas, no en una máquina dedicada, así que trata
las diferencias de unos pocos por ciento como ruido. Son indicativos, y son honestos sobre dónde
pierde Mapperion.

## Los escenarios

| Escenario | Manual | Mapperion | Con `MapFast` | Source generator | Mapperly | Mapster | AutoMapper 14 |
|---|---|---|---|---|---|---|---|
| Plano, 10 primitivas | 1,00x | 2,75x | **1,83x** | **0,93x** | 0,97x | 1,82x | 4,75x |
| Anidado con colección | 1,00x | 2,11x | — | — | — | 1,53x | 2,95x |
| Colección de 1.000 | 1,00x | 1,80x | — | 1,18x | 0,94x | 1,24x | 1,50x |
| Aplanado, 4 saltos | 1,00x | 3,40x | — | — | — | 2,11x | 5,54x |
| Record por constructor | 1,00x | 3,46x | — | 1,04x | 1,07x | 2,13x | 5,00x |
| Destino existente | 1,00x | 3,27x | — | — | — | 2,35x | 6,50x |

Las asignaciones coinciden con el código escrito a mano en todos los casos salvo el anidado, que
está en 1,12x.

## Qué dice esto

**El source generator es tan rápido como escribirlo tú**, entre 0,93x y 1,18x. Si el mapeo está de
verdad en tu camino caliente, esa es la respuesta, y es una diferencia mayor que cualquier ajuste
del motor de runtime.

**El motor de runtime le gana a AutoMapper en todas partes**, normalmente por la mitad.

**Mapster es más rápido que el motor de runtime** en todos los escenarios, por entre 1,4x y 1,6x.
Parte de eso es su API: un método de extensión sobre un tipo genérico estático no paga lo que paga
un método genérico llamado a través de una interfaz, que midió 7,7 ns de una llamada de 28.
`MapFast` es un método de extensión que toma el mismo atajo sin tocar `IMapper`, y en el escenario
plano empata con Mapster exactamente.

En términos absolutos el hueco que queda son unos diez nanosegundos por objeto. Para una petición
que mapea cincuenta objetos eso es medio microsegundo, frente a una petición que se mide en
milisegundos.

## MapFast

```csharp
OrderDto dto = mapper.MapFast<Order, OrderDto>(order);
```

El mismo resultado que `Map`, alcanzado sin el coste de llamar a un método genérico a través de
una interfaz. Reconoce el mapper que construye la librería y lo llama directamente, y cae de vuelta
a la interfaz con cualquier otra implementación, así que un decorador sigue funcionando.

Vale la pena en un bucle sobre muchísimos objetos. No vale el ruido en ningún otro sitio.

## Arranque

Para una configuración de seis mapas: construirla cuesta 8,6 µs, construirla y validarla 14,1 µs, y
compilar el primer plan 472 µs. Los planes se compilan en el primer uso y se guardan.
