# Primeros pasos

```
dotnet add package Mapperion
```

## Un mapa

```csharp
using Mapperion;

var configuration = new MapperConfiguration(cfg =>
    cfg.CreateMap<Order, OrderDto>());

IMapper mapper = configuration.CreateMapper();
OrderDto dto = mapper.Map<Order, OrderDto>(order);
```

Los miembros se emparejan por nombre, luego por nombre sin distinguir mayúsculas, y luego
aplanando: un miembro de destino llamado `CustomerAddressCity` encuentra `Customer.Address.City` en
el origen, con una comprobación de nulo en cada paso. Los prefijos y sufijos configurados se quitan
antes de emparejar.

Un par sin `CreateMap` es un error, siempre. Mapperion nunca mapea tipos que no declaraste.

## Configurar un miembro

```csharp
cfg.CreateMap<Order, OrderDto>()
   .ForMember(d => d.Total, o => o.MapFrom(s => s.Lines.Sum(l => l.Price)))
   .ForMember(d => d.Internal, o => o.Ignore())
   .ForMember(d => d.Note, o => o.Condition(s => s.Note.Length > 0));
```

`ForPath` llega dentro del destino, y los objetos del camino se crean según hagan falta:

```csharp
cfg.CreateMap<Delivery, DeliveryDto>()
   .ForPath(d => d.Address.Street, o => o.MapFrom(s => s.Street));
```

`IncludeMembers` construye un destino a partir de varios objetos anidados del origen. El mapa se
consulta primero; solo lo que deja sin resolver se ofrece a los miembros incluidos, en orden:

```csharp
cfg.CreateMap<Application, ApplicationDto>()
   .IncludeMembers(s => s.Applicant, s => s.Employment);
```

## Cuando cada lado escribe los nombres de otra manera

Una fila leída directamente de una base de datos o de un JSON escribe sus miembros `first_name`
mientras el destino los escribe `FirstName`. Ignorar mayúsculas no sirve, porque se diferencian en
un carácter y no en la caja. Dile a cada lado qué grafía usa y las convenciones hacen el resto:

```csharp
cfg.SourceMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
cfg.CreateMap<CustomerRow, CustomerDto>();
```

El aplanado cruza las dos grafías, así que un destino `ShipToCityName` sigue llegando a
`ship_to.city_name`.

Una grafía que la librería no trae es una propiedad. Conviene saber cuál es el margen real: los
nombres de miembro son identificadores CLR, así que el único separador que puede aparecer en uno es
el guion bajo, y una convención propia está ahí para sus variantes, como el separador doble que
emiten algunos generadores de código.

```csharp
public sealed class DoubleUnderscoreNamingConvention : INamingConvention
{
    public string? SeparatorCharacter => "__";
}
```

`ExactMatchNamingConvention` como convención de origen lee los nombres tal cual están escritos y
apaga el aplanado con ellos: `CustomerName` solo empareja entonces con un miembro de origen que se
llame así, nunca con `Customer.Name`.

## Comprobar la configuración

```csharp
configuration.AssertIsValid();
```

Reporta todos los problemas de una vez en lugar del primero: miembros de destino a los que no mapea
nada, pares anidados sin mapa declarado, y bucles sin nada que los detenga. Conviene llamarlo en un
test, para que un error de configuración tumbe el build en vez de una petición.

## Con inyección de dependencias

```
dotnet add package Mapperion.Extensions.DependencyInjection
```

```csharp
services.AddMapperion(typeof(SomeProfile).Assembly);
```

Eso busca clases `Profile`, registra `IMapper` y resuelve converters y resolvers desde el
contenedor, de modo que un resolver puede tener sus propias dependencias.

Registra el resolver además de aquello de lo que depende. Al contenedor se le pide por tipo, y un
tipo que nadie registró no es algo que pueda construir — el mapper cae entonces a un constructor
público sin parámetros, que es justo lo que un resolver con dependencias no tiene:

```csharp
services.AddScoped<ITenantContext, TenantContext>();
services.AddScoped<AuditedByResolver>();
services.AddMapperion(typeof(SomeProfile).Assembly);
```

AutoMapper te pide lo mismo, así que nada de esto cambia al venir de allí.

## Cuando el mapeo es el camino caliente

`mapper.Map<Order, OrderDto>(order)` es un método genérico alcanzado a través de una interfaz, y
el runtime resuelve sus argumentos de tipo en cada llamada. En un mapa pequeño eso es la mayor
parte de lo que cuesta la llamada — más, en el benchmark plano, que todo lo que gasta Mapster.

Dos salidas, en el orden en que vale la pena probarlas.

**`MapperFor`**, cuando el mismo par se mapea más de una vez:

```csharp
Func<Order, OrderDto> toDto = mapper.MapperFor<Order, OrderDto>();

foreach (Order order in orders)
{
    results.Add(toDto(order));
}
```

Nada de ese despacho depende del objeto que se mapea, así que en un bucle es la misma respuesta
encontrada una y otra vez. Esto la pide una sola vez, y lo que vuelve es un `Func` corriente que
también entra en un `Select`. Guárdalo mientras dure el bucle, o como campo al lado del mapper;
pedir uno por llamada cuesta más de lo que ahorra.

Medido en el benchmark plano, esta es la única forma que queda por debajo de Mapster.

**`MapFast`**, para una llamada suelta sin sitio donde guardar una función:

```csharp
OrderDto dto = mapper.MapFast<Order, OrderDto>(order);
```

El mismo resultado, el mismo mapa. Se salta la interfaz pero sigue buscando el plan cada vez, así
que donde hay un bucle, `MapperFor` es la mejor respuesta. Los dos reconocen el mapper que
construye la librería y caen de vuelta a la interfaz con cualquier otra cosa, así que un decorador
o un doble de test siguen funcionando.

**El source generator**, cuando el mapeo es de verdad en lo que tu programa se va el tiempo.
Escribe el mapeo como C# corriente mientras compilas, y corre a la velocidad del código que
habrías escrito a mano — una diferencia mucho mayor de la que `MapFast` puede devolver.

Ninguno vale la pena por defecto. El ahorro son nanosegundos por objeto, que no es nada al lado de
casi cualquier otra cosa que haga una petición; `Map` es lo que hay que escribir hasta que un
profiler diga otra cosa. [Rendimiento](performance.md) tiene los números.

## Cuando un mapeo falla

Un fallo nombra el miembro en el que ocurrió, con la ruta completa a través de mapas anidados y la
posición dentro de una colección:

```
Mapping Batch -> BatchDto failed at 'Readings[2].Ratio'. See the inner exception.
```

`MappingException.MemberPath` lleva esa misma ruta para el código que quiera leerla.

> Los mensajes de error van en inglés, como el código. Traducirlos separaría lo que ves en consola
> de lo que se puede buscar en el repositorio o en un issue.
