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

## Cuando un mapeo falla

Un fallo nombra el miembro en el que ocurrió, con la ruta completa a través de mapas anidados y la
posición dentro de una colección:

```
Mapping Batch -> BatchDto failed at 'Readings[2].Ratio'. See the inner exception.
```

`MappingException.MemberPath` lleva esa misma ruta para el código que quiera leerla.

> Los mensajes de error van en inglés, como el código. Traducirlos separaría lo que ves en consola
> de lo que se puede buscar en el repositorio o en un issue.
