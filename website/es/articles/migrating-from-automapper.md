# Migrar desde AutoMapper

El objetivo es que cambiar `using AutoMapper;` por `using Mapperion;` resuelva la mayoría de los
archivos, y que lo que quede sea una lista corta que puedas despachar en una tarde.

## Cuánto vale eso por ahora

Hay un ejemplo en el repositorio que configura una misma capa de facturación dos veces —una en
AutoMapper 14 y otra en Mapperion— sobre un dominio compartido, mapea las mismas facturas por las
dos y falla cuando discrepan en cualquier campo. La CI lo corre.

La configuración que migra usa perfiles, value resolvers, un type converter, `ForCtorParam` sobre
un record posicional, `NullSubstitute`, `Condition`, `Ignore`, `AfterMap`,
`ResolutionContext.Items` rellenados por llamada, aplanado de tres saltos y un enum que solo cruza
por nombre.

El diff entero entre las dos versiones es **un `using` por archivo y un `!`**: Mapperion tipa la
bolsa de items como `IDictionary<string, object?>` en vez de `IDictionary<string, object>`, así
que sacar un valor con un cast necesita el operador de perdón de nulos en un contexto nullable.

Conviene ser claro sobre lo que eso *no* es. Dice que la API acepta las mismas llamadas y da las
mismas respuestas. No dice nada de tu build, del cableado de tu contenedor ni de tus treinta
perfiles, y lo escribió alguien que ya conocía las dos librerías. Las formas coinciden; eso es una
afirmación más pequeña que «tu migración va a ser fácil».

## El procedimiento

1. Asegúrate primero de que tus tests pasan sobre AutoMapper. Quieres un punto de partida bueno
   conocido.
2. Instala `Mapperion` y `Mapperion.Extensions.DependencyInjection` al lado. Las dos librerías
   pueden convivir en un proyecto —los namespaces y los nombres de tipo son distintos—, así que
   puedes migrar módulo a módulo.
3. Sustituye `using AutoMapper;` por `using Mapperion;`.
4. Ajusta el puñado de renombrados de la tabla de abajo.
5. Compila. Lo que siga fallando es una diferencia real; la tabla dice cuál.
6. Ejecuta `AssertIsValid()`. Suele sacar a la luz pares que AutoMapper resolvía implícitamente.
7. Pasa tus tests y luego quita AutoMapper.

## Lo que es igual

`MapperConfiguration`, `Profile`, `CreateMap`, `ForMember`, `ForPath`, `ForCtorParam`, `MapFrom`,
`Ignore`, `Condition`, `PreCondition`, `NullSubstitute`, `ConvertUsing`, `ConstructUsing`,
`ReverseMap`, `Include`, `IncludeBase`, `IncludeMembers`, `BeforeMap`, `AfterMap`, `MaxDepth`,
`PreserveReferences`, `AllowNullCollections`, `ProjectTo`, `ITypeConverter`, `IValueConverter`,
`IValueResolver`, `IMappingAction`, `ResolutionContext.Items`, `SourceMemberNamingConvention`,
`DestinationMemberNamingConvention`.

## Lo que cambia de nombre

| AutoMapper | Mapperion |
|---|---|
| `AssertConfigurationIsValid()` | `AssertIsValid()`, y el nombre largo funciona como alias |
| `AddAutoMapper(...)` | `AddMapperion(...)` |
| `RecognizePrefixes` / `RecognizePostfixes` | `RecognizeSourcePrefixes` / `RecognizeSourcePostfixes`, porque también hay de destino |
| `AutoMapperConfigurationException` | `MapperConfigurationException` |
| `AutoMapperMappingException` | `MappingException` |
| `Mapper.Map(...)` estático de la v4 | `MapperHost.Instance.Map(...)`, tras `MapperHost.Initialize` |

## Lo que cambia a propósito

**Un par sin mapa es siempre un error.** AutoMapper se puede configurar para mapear tipos que
nunca declaraste. Mapperion no lo hace: un par sin declarar es una `MapperConfigurationException`,
al arrancar si llamas a `AssertIsValid()` y en el primer uso si no. Las sorpresas en producción son
peores que unas cuantas líneas de `CreateMap` de más.

**Un grafo de objetos que se cierra falla en vez de tumbar el proceso.** Si dos mapas se referencian
y los datos tienen un ciclo, AutoMapper recurre hasta agotar la pila, y una `StackOverflowException`
no se puede capturar — eso es
[CVE-2026-32933](https://github.com/advisories/GHSA-rvv3-g6hj-g44x), sin arreglar en su línea MIT.
Mapperion cuenta la profundidad de cualquier mapa que pueda alcanzarse a sí mismo y lanza
`RecursionLimitException`, que deriva de `MappingException`, al pasar de `RecursionLimit`. Por
defecto son 64, igual que `System.Text.Json`. Solo pagan el contador los mapas que cierran un bucle
sin protección.

**`ProjectTo` avisa de lo que no puede traducir.** AutoMapper se salta en silencio converters,
resolvers y pasos de before/after dentro de una proyección, así que el mismo mapa da respuestas
distintas por `Map` y por `ProjectTo`. Mapperion lanza en su lugar, y dice qué miembro y por qué.

**`AfterMap` no puede reemplazar el destino.** Usa `ConstructUsing`, que existe con las dos
sobrecargas de AutoMapper. Declarar `ConstructUsing` y `ForCtorParam` en el mismo mapa se rechaza
al construir la configuración, en vez de dejar que gane la fábrica en silencio.

**Leer a través de un miembro cuenta como usarlo**, al validar contra la lista de origen. Con
`ForMember(d => d.Anything, o => o.MapFrom(s => s.Depot.Code))` se lee `Depot`, así que
`MemberListValidation.Source` lo da por usado. AutoMapper cuenta una ruta que encontró su
convención de nombres pero no una que escribiste tú, y reporta `Depot` como no mapeado — se
comprobó corriendo las dos bibliotecas lado a lado, y parece más un artefacto de cómo registra lo
que emparejó la convención que una regla que alguien eligiera.

La lectura laxa es la de por defecto porque ser más laxo no puede romper una migración: una
configuración que AutoMapper aceptaba se acepta aquí. Pon `cfg.ReadingThroughAMemberUsesIt = false`
para su comportamiento exacto.

**La validación está apagada por defecto**, como en AutoMapper. `cfg.ValidateOnBuild = true` la
enciende.

## Lo que no viene

Nada, en lo que respecta a AutoMapper: todo lo que hace tiene equivalente aquí, `ProjectTo` para
EF6 incluido desde la 0.10.0, con su propia suite de pruebas corriendo sobre net472.

Lo que una proyección no puede hacer en ninguna de las dos librerías es construir un diccionario o
despachar a un mapa derivado: su forma queda fijada antes de leer una fila. Mapperion lo dice en
vez de saltárselo.
