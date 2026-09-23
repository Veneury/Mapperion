# Changelog

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/).
Versionado según [SemVer 2.0](https://semver.org/lang/es/).

Antes de la v1.0, las versiones minor pueden introducir cambios de ruptura.

## [Unreleased]

### Added

- Documentación de planeación completa (`docs/`), incluidos 4 ADRs.
- Esqueleto de la solución: multi-targeting `netstandard2.0;net8.0;net9.0`, Central Package
  Management, `.editorconfig` con estilo obligatorio y warnings como errores.
- Contratos base: `IMapper`, jerarquía de excepciones y `TypeMapKey` del modelo de configuración.
- Nombre definitivo fijado: **Mapperion** (D-01 cerrada tras verificar disponibilidad en NuGet,
  GitHub y colisiones de producto).
- Target framework `net10.0` añadido al core y a los tests.
- Modelo de configuración (capa 2): `MapperModel`, `MapperOptions`, `TypeMapDefinition`,
  `MemberDefinition`, `ConstructorParameterDefinition`, `MemberSource` y sus cuatro variantes,
  `MemberPath`, `MemberDescriptor` y las enumeraciones de política.
- `Internal/Guard` centraliza las comprobaciones de nulos y el `#if` que exige `netstandard2.0`.
- ADR-0005: acota qué comparten de verdad el motor runtime y el source generator, corrigiendo
  ADR-0001 y el documento de arquitectura.
- API fluida (capa 1): `MapperConfiguration`, `IMapperConfigurationExpression`,
  `IMappingExpression<,>` e `IMemberConfigurationExpression<,,>`, con `CreateMap`, `ForMember`,
  `MapFrom`, `Ignore`, `Condition`, `NullSubstitute`, `SetMappingOrder`, `UseDestinationValue`,
  `ValidateMemberList`, `MaxDepth` y `PreserveReferences`.
- `MemberExpressionParser` traduce las lambdas a elementos del modelo: una cadena de miembros se
  guarda como `MemberPathSource` y cualquier otra expresión como `CustomSource` opaco.
- Convenciones (capa 3): coincidencia por nombre exacto, sin distinguir mayúsculas, con prefijos y
  sufijos configurables, y aplanado hasta `MaxFlatteningDepth`. La configuración explícita siempre
  gana y un miembro sin origen queda con `Source` nulo para que la validación lo reporte.
- `Profile`, `AddProfile<T>()`, `AddProfile(instancia)` y `AddProfiles(ensamblados)`, adelantados
  desde v0.2 por ser el mayor bloqueo de migración desde AutoMapper.
- Los caminos que usan reflexión están anotados con `[RequiresUnreferencedCode]`, incluido el
  constructor de `MapperConfiguration`. Se retira `IsAotCompatible` del core: era una afirmación
  falsa mientras el motor dependa de reflexión y de `Expression.Compile`.
- `Internal/TrimmingAttributes.cs` aporta el polyfill de `RequiresUnreferencedCodeAttribute` para
  `netstandard2.0`, que PolySharp no cubre.
- La carpeta `docs/` queda fuera del repositorio.
- `AssertIsValid()` y el alias `AssertConfigurationIsValid()`: reportan todos los problemas juntos
  en `MapperConfigurationException.Errors`, nunca solo el primero. Detectan miembros destino sin
  origen, mapas anidados que faltan (desenvolviendo nullables y colecciones) y, con
  `MemberListValidation.Source`, miembros de origen que nadie consume.
- `Internal/TypeClassifier`: distingue tipos simples, nullables y secuencias.
- `ValidateOnBuild` pasa a `false` por defecto, como AutoMapper, para no romper en arranque las
  configuraciones recién migradas.
- `MemberListValidation` global ahora se aplica de verdad a los mapas que no lo sobrescriben.
- Compilador de expresiones (capa 4) y motor de ejecución (capa 5): `MapperConfiguration.CreateMapper()`
  devuelve un `IMapper` con `Map<TDest>(object)`, `Map<TSource,TDest>(source)`,
  `Map(source, destino)` y la sobrecarga no genérica.
- Los planes se compilan la primera vez que se usa cada par y se cachean. Los mapas anidados se
  resuelven en ejecución en vez de insertarse en línea, que es lo que permite que dos mapas se
  referencien mutuamente sin que el compilador recurse.
- Conversiones: nullables en ambos sentidos, numéricas, enums según la política configurada,
  enum con string, `ToString` e `IConvertible` como último recurso.
- Colecciones: array, `List<>`, `HashSet<>`, `ISet<>` y las interfaces de secuencia, con
  `AllowNullCollections` respetado.
- Rutas aplanadas con guardas de nulo que evalúan cada paso una sola vez.
- `Condition`, `NullSubstitute`, `SetMappingOrder` y `UseDestinationValue` llegan al código generado.
- Mapeo por constructor: records posicionales, constructores primarios y cualquier destino sin
  constructor sin parámetros. Se elige la sobrecarga con más argumentos resolubles; los parámetros
  con valor por defecto cubren lo que el origen no aporta.
- `ForCtorParam(nombre, o => o.MapFrom(...))` y `UseValue(...)` para configurar un argumento a mano.
- Los nombres de parámetro se comparan siempre sin distinguir mayúsculas: C# nombra los parámetros
  en camelCase y las propiedades en PascalCase, así que una comparación exacta nunca emparejaría el
  constructor primario de un record con las propiedades del origen.
- Un miembro alimentado por el constructor ya no se asigna otra vez después de construir.
- La validación reporta los parámetros de constructor sin origen y los mapas que les faltan.
- `ReverseMap()`: declara el par inverso y lo devuelve para seguir configurándolo. Invierte los
  miembros renombrados con un `MapFrom` sobre un único miembro escribible; las rutas aplanadas, las
  expresiones arbitrarias y los miembros ignorados no se invierten y el inverso los resuelve por
  convención. Funciona igual dentro de un `Profile`.
- Workflow de CI: build y test en Linux y Windows contra .NET 8, 9 y 10, y `pack` del core.
- `ITypeConverter<TSource,TDest>`, `IValueConverter<TSourceMember,TDestMember>` e
  `IValueResolver<TSource,TDest,TMember>`, con `ResolutionContext` como vía de vuelta al mapper
  para que el código de usuario pueda mapear valores anidados.
- `ConvertUsing<TTypeConverter>()` sustituye el mapa completo de un par de tipos; en ese caso la
  configuración de miembros deja de aplicarse y la validación de miembros se omite.
- `ConvertUsing<TValueConverter, TSourceMember>()` y `MapFrom<TValueResolver>()` por miembro,
  ambos con restricciones de tipo, así que el compilador de C# rechaza un converter que no encaje.
- Converters y resolvers se instancian una sola vez y se reutilizan, cacheados por el motor. De
  momento necesitan un constructor sin parámetros; la resolución por DI llega en v0.4.
- Un resolver que alimenta un parámetro de constructor recibe el destino por defecto, porque
  todavía no existe cuando se calculan los argumentos.
- `BeforeMap` y `AfterMap`, cada uno en tres formas: lambda de dos argumentos, lambda con
  `ResolutionContext`, y `IMappingAction<TSource,TDestination>` como tipo propio.
- Los pasos se insertan en el plan compilado en el orden en que se declararon: los de antes justo
  después de crear el destino, los de después una vez asignados todos los miembros. Los pasos con
  tipo propio se instancian una sola vez, igual que converters y resolvers.
- Un mapa con `ConvertUsing` no ejecuta los pasos: el converter reemplaza el mapa entero, igual que
  reemplaza la configuración de miembros.
- Paquete `Mapperion.Extensions.DependencyInjection` con `AddMapperion(...)`, en tres formas:
  callback de configuración, ensamblados a escanear, o tipos marcadores.
- `MapperConfiguration.CreateMapper(IServiceProvider)`: el mapper pide converters, resolvers y
  acciones al contenedor y cae en la construcción directa para los que este no conoce, así que un
  resolver sin dependencias no necesita registrarse.
- La resolución de instancias sale del motor a `IServiceResolver`. El motor sigue siendo el dueño
  de los planes compilados y se comparte entre todos los mappers de una configuración, de modo que
  registrar `IMapper` como scoped no recompila nada.
- `MappingContext` pasa a llevar también el resolutor y el mapper en curso, para que
  `ResolutionContext.Mapper` devuelva el del scope y no uno global.
- `ProjectTo`: `IQueryable.ProjectTo<TDest>(configuracion)` e `IMapper.ProjectTo<TDest>(consulta)`.
  Un compilador de proyección aparte emite `Expression<Func<TSource,TDest>>` que un proveedor LINQ
  sabe traducir: inicialización de miembros, acceso a miembros, condicionales y `Select`, sin
  bloques, sin variables y sin llamadas a esta librería.
- Vive en el core: solo necesita `IQueryable` y `System.Linq.Expressions`, así que el paquete
  `Mapperion.EntityFrameworkCore` que estaba planeado no hace falta.
- Lo que un proveedor no puede ejecutar se reporta en vez de omitirse: type converters, value
  converters, resolvers y los pasos de `BeforeMap`/`AfterMap`. AutoMapper los omite en silencio; se
  prefirió el error porque una proyección que difiere del mismo mapa por `Map` es un fallo caro de
  encontrar.
- Un mapa que se referencia a sí mismo se reporta también: una proyección se expande por completo
  de antemano, así que un ciclo no tiene fin.
- Las proyecciones se cachean por par de tipos, igual que los planes.
- Tests de integración reales con EF Core y SQLite en memoria: verifican que la consulta se traduce
  a SQL, que solo se piden las columnas del destino y que el filtrado y la paginación siguen
  ocurriendo en la base de datos.
- TFMs `netstandard2.1` y `net472` en los dos paquetes, que pasan a publicar seis: `netstandard2.0`,
  `netstandard2.1`, `net472`, `net8.0`, `net9.0` y `net10.0`.
- `Microsoft.NETFramework.ReferenceAssemblies` aporta los ensamblados de referencia de .NET
  Framework, así que compilar `net472` no exige tener instalado el Developer Pack, ni siquiera en
  Linux.
- `Mapperion.Compatibility.Tests`: una porción representativa de la librería ejecutándose sobre
  .NET Framework real (net472 y net48) además de net8.0 y net10.0. El proyecto reduce sus TFMs
  fuera de Windows, donde .NET Framework no se puede ejecutar.
- Diccionarios: `Dictionary<,>`, `IDictionary<,>` e `IReadOnlyDictionary<,>`, convirtiendo tanto las
  claves como los valores. Se comprueban antes que las colecciones, porque un diccionario también
  es una secuencia de `KeyValuePair<,>` y se mapearía mal.
- La validación también los reconoce ahora: antes reportaba un mapa inexistente de
  `KeyValuePair` a `KeyValuePair` para un diccionario que en realidad se mapeaba bien.
- Una proyección rechaza los diccionarios con un mensaje claro: un proveedor de consultas no tiene
  forma de materializar uno.
- `PreCondition(s => ...)`: descarta el miembro antes incluso de leer su origen.
- `Condition` pasa a evaluarse **después** de resolver el valor, como en AutoMapper. Antes se
  comportaba como una precondición, lo que dejaba a las dos indistinguibles; ahora `Condition`
  paga la lectura y `PreCondition` la evita, que es justo la diferencia entre ambas.
- Un fallo en tiempo de mapeo dice ahora qué miembro lo causó, con la ruta completa a través de
  mapas anidados y colecciones: `Batch.Readings[0].Ratio`. La excepción original queda como
  `InnerException`.
- `MappingException.MemberPath` se rellena de verdad; antes existía y nadie la escribía.
- Un problema de configuración descubierto al compilar un mapa anidado en pleno mapeo sigue
  saliendo como `MapperConfigurationException`, no disfrazado de fallo de mapeo.
- El `catch` del plan no lleva filtro de excepción: un filtro compila a un bloque IL de filtro y
  `DynamicMethod` los rechaza en .NET Framework. Lo detectaron los tests de compatibilidad, que
  pasaban en .NET 8, 9 y 10 y fallaban en net472 y net48.
